using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Providers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core
{
    /// <summary>
    /// 上下文压缩器 — 全面对齐 Reasonix compact.go + prune.go 三级压缩机制
    ///
    /// 三级策略：
    ///   Level 1 (SoftNotice, 50%): 通知上下文增长，不修改前缀（cache-first）
    ///   Level 2 (Snip, 60%): 截断过期工具结果（保留首尾行），无需 LLM
    ///   Level 3 (Prune + Summary, 80%): 清除过期工具结果 → 若仍超阈值则 LLM 摘要折叠
    ///   Force (90%): 强制压缩，跳过经济性检查
    ///
    /// 核心设计：
    ///   - Pinned Prefix: system + 首条用户消息 + 已有摘要，永不折叠
    ///   - Partition Fold: 小用户消息原样保留，仅折叠 assistant/tool 工作
    ///   - Token Budget Tail: 按 token 预算保留尾部（非消息数）
    ///   - Archive: 被折叠消息归档到 JSONL 文件
    ///   - Stuck Guard: 连续压缩检测，防止死循环
    ///   - Fold Economics: 可折叠区域 < 400 token 时跳过 LLM 调用
    ///   - KeepErrors: error:/blocked: 工具结果保留不折叠
    /// </summary>
    public class ContextCompactor
    {
        // ── 阈值常量（对齐 Reasonix compact.go） ──
        private const double DefaultSoftCompactRatio = 0.5;
        private const double DefaultToolResultSnipRatio = 0.6;
        private const double DefaultCompactRatio = 0.8;
        private const double DefaultCompactForceRatio = 0.9;
        private const double DefaultCompactTarget = 0.5;
        private const int DefaultTailTokens = 16384;
        private const int MinRecentKeep = 2;
        private const int MinCompactMessages = 2;
        private const double FallbackTokPerChar = 0.25; // ~4 chars/token
        private const int MaxPinnedFirstUserTokens = 1500;
        private const double PinnedFirstUserWindowFrac = 0.15;
        private const int MinFoldTokens = 400;
        private const int MinPruneBytes = 1024;
        private const int SummaryTimeoutMs = 90000;

        // ── Snip/Prune 标记 ──
        private const string SnippedMarker = "[snipped tool result — ";
        private const string PrunedMarker = "[elided tool result — ";

        // ── 摘要标签 ──
        private const string SummaryTagOpen = "<compaction-summary>";
        private const string SummaryTagClose = "</compaction-summary>";

        // ── Snip 策略（对齐 Reasonix snipStrategy） ──
        private static readonly SnipStrategy ReadOnlySnip = new SnipStrategy { Head = 80, Tail = 12, HeadChars = 10000, TailChars = 2000 };
        private static readonly SnipStrategy SideEffectSnip = new SnipStrategy { Head = 40, Tail = 40, HeadChars = 8000, TailChars = 8000 };

        private readonly ICopilotProvider _provider;
        private readonly IEventSink _sink;
        private readonly CopilotConfig _config;
        private readonly string _archiveDir;

        // ── 状态 ──
        private bool _softCompactNoticed;
        private int _consecutiveCompacts;
        private bool _compactStuck;
        private UsageData _lastUsage;

        public ContextCompactor(ICopilotProvider provider, IEventSink sink, CopilotConfig config, string archiveDir = null)
        {
            _provider = provider;
            _sink = sink;
            _config = config;
            _archiveDir = archiveDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "E3DCopilot", "archive");
        }

        /// <summary>更新最近一次 usage（每步 LLM 调用后调用）</summary>
        public void UpdateUsage(UsageData usage)
        {
            if (usage != null) _lastUsage = usage;
        }

        /// <summary>重置 turn 级状态</summary>
        public void ResetTurnState()
        {
            // 不重置 _consecutiveCompacts / _compactStuck（跨 turn 持久）
        }

        // ═══════════════════════════════════════════════════════════
        //  主入口 — MaybeCompact（对齐 Reasonix maybeCompact）
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 主入口：根据当前 prompt token 占比决定执行哪级压缩
        /// </summary>
        public async Task MaybeCompactAsync(CopilotSession session, CancellationToken ct)
        {
            int contextWindow = GetContextWindow();
            if (contextWindow <= 0) return; // 未配置窗口，禁用压缩

            int promptTokens = EstimatePromptTokens(session);
            if (promptTokens <= 0) return;

            double softRatio = GetConfigRatio("SoftCompactRatio", DefaultSoftCompactRatio);
            double snipRatio = GetConfigRatio("ToolResultSnipRatio", DefaultToolResultSnipRatio);
            double compactRatio = GetConfigRatio("CompactRatio", DefaultCompactRatio);
            double forceRatio = GetConfigRatio("CompactForceRatio", DefaultCompactForceRatio);

            int softThreshold = (int)(contextWindow * softRatio);
            int snipThreshold = (int)(contextWindow * snipRatio);
            int compactThreshold = (int)(contextWindow * compactRatio);
            int forceThreshold = (int)(contextWindow * forceRatio);

            // ── Level 1: Soft Notice（50%~60%）── 仅通知，不修改
            if (promptTokens >= softThreshold && promptTokens < snipThreshold && !_softCompactNoticed)
            {
                _softCompactNoticed = true;
                _sink?.Emit(CopilotEvent.Notice(
                    $"上下文已达窗口的 {Percent(promptTokens, contextWindow)}%，将在 {Percent(compactRatio, 1.0)}% 时自动清理"));
                return;
            }

            // ── Level 2: Snip（60%~80%）── 截断过期工具结果
            if (promptTokens >= snipThreshold && promptTokens < compactThreshold)
            {
                var stats = SnipStaleToolResults(session);
                if (stats.Results > 0)
                {
                    int savedTokens = (int)(stats.SavedChars * TokPerChar(session));
                    _sink?.Emit(CopilotEvent.Notice(
                        $"已截断 {stats.Results} 个过期工具结果（估计节省 ~{savedTokens} tokens）"));
                }
                return;
            }

            // ── 低于阈值：重置卡住状态 ──
            if (promptTokens < compactThreshold)
            {
                _consecutiveCompacts = 0;
                _compactStuck = false;
                return;
            }

            // ── 卡住保护 ──
            if (_compactStuck) return;

            bool force = promptTokens >= forceThreshold;

            // ── Level 3a: Prune（清除过期工具结果）──
            var pruneStats = PruneStaleToolResults(session);
            if (pruneStats.Results > 0)
            {
                int savedTokens = (int)(pruneStats.SavedChars * TokPerChar(session));
                _sink?.Emit(CopilotEvent.Notice(
                    $"已清除 {pruneStats.Results} 个过期工具结果（估计节省 ~{savedTokens} tokens）"));

                // Prune 后重新估算，若已降到阈值以下则跳过 Summary
                if (!force)
                {
                    int newEstimate = promptTokens - savedTokens;
                    if (newEstimate < compactThreshold) return;
                }
            }

            // ── Level 3b: Summary Compaction（LLM 摘要折叠）──
            bool ok = await CompactAsync(session, "auto", force, ct);
            if (!ok)
            {
                _sink?.Emit(CopilotEvent.Notice("上下文清理暂时跳过"));
                return;
            }

            // ── Stuck Guard: 连续压缩检测 ──
            _consecutiveCompacts++;
            if (_consecutiveCompacts >= 2)
            {
                _compactStuck = true;
                _sink?.Emit(CopilotEvent.Notice(
                    $"自动上下文清理已暂停：context_window={contextWindow} 过小，" +
                    "系统提示 + 单轮对话已超阈值。建议增大 context_window 或减少工具输出。"));
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Snip — 截断过期工具结果（保留首尾行）
        // ═══════════════════════════════════════════════════════════

        public PruneStats SnipStaleToolResults(CopilotSession session)
        {
            return MaintainStaleToolResults(session, isSnip: true);
        }

        // ═══════════════════════════════════════════════════════════
        //  Prune — 清除过期工具结果（替换为短占位符）
        // ═══════════════════════════════════════════════════════════

        public PruneStats PruneStaleToolResults(CopilotSession session)
        {
            return MaintainStaleToolResults(session, isSnip: false);
        }

        private PruneStats MaintainStaleToolResults(CopilotSession session, bool isSnip)
        {
            var stats = new PruneStats();
            int contextWindow = GetContextWindow();
            if (contextWindow <= 0) return stats;

            var msgs = session.Messages;
            int head, start;
            if (!PlanCompaction(msgs, out head, out start, 1))
            {
                if (isSnip) return stats;
                // Prune 模式回退：保护最近 N 条
                head = 1;
                start = msgs.Count - MinRecentKeep;
                if (start < head) return stats;
            }

            // 收集需要维护的工具结果索引
            var indices = new List<int>();
            for (int i = head; i < start; i++)
            {
                var m = msgs[i];
                if (!ShouldMaintainToolResult(m, isSnip)) continue;
                // KeepErrors: 保留 error/blocked 结果
                if (IsErrorMessage(m)) continue;
                indices.Add(i);
            }
            if (indices.Count == 0) return stats;

            // 归档原始内容
            string archivePath = null;
            if (!string.IsNullOrEmpty(_archiveDir))
            {
                var originals = new List<ChatMessage>();
                foreach (var i in indices)
                {
                    if (!isSnip && (msgs[i].Content ?? "").StartsWith(SnippedMarker))
                        continue; // 已 snip 的不重复归档
                    originals.Add(msgs[i]);
                }
                if (originals.Count > 0)
                {
                    archivePath = ArchiveMessages(originals);
                    stats.Archive = archivePath;
                }
            }

            // 执行替换
            foreach (var i in indices)
            {
                var m = msgs[i];
                string replacement = isSnip
                    ? SnipToolResult(m, archivePath)
                    : PruneToolResult(m, archivePath);
                if (replacement == m.Content) continue;

                stats.SavedChars += (m.Content?.Length ?? 0) - replacement.Length;
                m.Content = replacement;
                stats.Results++;
            }

            return stats;
        }

        private bool ShouldMaintainToolResult(ChatMessage m, bool isSnip)
        {
            if (m.Role != MessageRole.Tool) return false;
            string content = m.Content ?? "";
            if (content.StartsWith(PrunedMarker)) return false; // 已 prune 的不再处理

            if (isSnip)
            {
                return content.Length >= MinPruneBytes && !content.StartsWith(SnippedMarker);
            }
            // Prune: 已 snip 的可以升级为 prune，或者未处理的大结果
            if (content.StartsWith(SnippedMarker)) return true;
            return content.Length >= MinPruneBytes;
        }

        private string SnipToolResult(ChatMessage m, string archive)
        {
            string content = m.Content ?? "";
            string toolName = m.ToolCallId ?? "tool";
            string archiveRef = string.IsNullOrEmpty(archive) ? "not archived" : archive;
            var strategy = GetSnipStrategy(toolName);

            var lines = content.Split('\n');
            if (lines.Length <= strategy.Head + strategy.Tail)
            {
                // 单大行：按字符截断
                int headChars = Math.Min(strategy.HeadChars, content.Length / 2);
                int tailChars = Math.Min(strategy.TailChars, content.Length / 4);
                string headText = content.Substring(0, headChars);
                string tailText = content.Substring(content.Length - tailChars);
                int omitted = content.Length - headChars - tailChars;
                return $"{SnippedMarker}{toolName}, {content.Length} bytes archived to {archiveRef}; single large line truncated]\n" +
                       $"{headText}\n[... {omitted} bytes omitted ...]\n{tailText}";
            }

            string headLines = string.Join("\n", lines.Take(strategy.Head));
            string tailLines = string.Join("\n", lines.Skip(lines.Length - strategy.Tail));
            int omittedLines = lines.Length - strategy.Head - strategy.Tail;
            return $"{SnippedMarker}{toolName}, {content.Length} bytes archived to {archiveRef}; showing first {strategy.Head} lines and last {strategy.Tail} lines]\n" +
                   $"{headLines}\n[... {omittedLines} lines omitted ...]\n{tailLines}";
        }

        private string PruneToolResult(ChatMessage m, string archive)
        {
            string content = m.Content ?? "";
            string toolName = m.ToolCallId ?? "tool";
            // 如果之前已 snip，提取原始大小和归档路径
            string archiveRef = ExtractOriginalArchive(content);
            if (string.IsNullOrEmpty(archiveRef))
                archiveRef = string.IsNullOrEmpty(archive) ? "not archived" : archive;
            int originalBytes = ExtractOriginalBytes(content);

            return $"{PrunedMarker}{toolName}, {originalBytes} bytes archived to {archiveRef}; re-run the tool if the data is needed again]";
        }

        // ═══════════════════════════════════════════════════════════
        //  Summary Compaction — LLM 摘要折叠（对齐 Reasonix compact）
        // ═══════════════════════════════════════════════════════════

        private async Task<bool> CompactAsync(CopilotSession session, string trigger, bool force, CancellationToken ct)
        {
            var msgs = session.Messages;
            int head, start;
            if (!PlanCompaction(msgs, out head, out start, MinCompactMessages))
            {
                // 尝试单消息折叠
                if (!PlanCompaction(msgs, out head, out start, 1))
                    return false;
            }

            var region = msgs.GetRange(head, start - head);

            // Partition: 保留小用户消息 + 已有摘要，折叠其余
            List<ChatMessage> kept, fold;
            PartitionFold(session, region, out kept, out fold);
            if (fold.Count == 0) return false;

            // 经济性检查
            if (!force && !FoldEconomics(fold)) return false;

            // 归档
            string archived = null;
            if (!string.IsNullOrEmpty(_archiveDir))
            {
                archived = ArchiveMessages(fold);
            }

            // LLM 摘要（含重试）
            string summary;
            try
            {
                summary = await SummarizeWithRetryAsync(fold, ct);
            }
            catch
            {
                // 机械折叠回退
                summary = MechanicalFoldDigest(fold.Count, archived);
                _sink?.Emit(CopilotEvent.Notice("摘要生成失败，已执行机械折叠"));
            }

            // 重组会话：pinned prefix + kept + summary + tail
            var compacted = new List<ChatMessage>();
            // head 部分（pinned prefix）
            for (int i = 0; i < head; i++)
                compacted.Add(msgs[i]);
            // kept 部分（小用户消息 + 已有摘要）
            compacted.AddRange(kept);
            // 摘要消息
            compacted.Add(new ChatMessage(MessageRole.User,
                $"{SummaryTagOpen}\nSummary of earlier conversation (older messages were compacted to save context):\n{summary}\n{SummaryTagClose}"));
            // tail 部分
            for (int i = start; i < msgs.Count; i++)
                compacted.Add(msgs[i]);

            // 替换会话
            msgs.Clear();
            msgs.AddRange(compacted);

            _sink?.Emit(CopilotEvent.Notice(
                $"上下文已压缩: {fold.Count} 条消息 → 摘要, {msgs.Count - head - kept.Count - 1} 条原样保留"));
            return true;
        }

        // ═══════════════════════════════════════════════════════════
        //  Plan Compaction — 定位压缩区域（对齐 Reasonix planCompaction）
        // ═══════════════════════════════════════════════════════════

        private bool PlanCompaction(List<ChatMessage> msgs, out int head, out int start, int min)
        {
            head = PinnedPrefixLen(msgs);
            int contextWindow = GetContextWindow();

            if (contextWindow > 0)
            {
                int budget = DefaultTailTokens;
                int maxByWin = (int)(contextWindow * DefaultCompactTarget);
                if (maxByWin < budget) budget = maxByWin;
                start = TailStart(msgs, head, budget, MinRecentKeep);
            }
            else
            {
                start = msgs.Count - MinRecentKeep;
                // 对齐：不在 tool 消息中间切割
                while (start > head && start < msgs.Count && msgs[start].Role == MessageRole.Tool)
                    start--;
            }

            if (start < head) start = head;
            if (start - head < min)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Pinned Prefix: system + 首条用户消息（如果够小）+ 已有摘要
        /// 这些永不被折叠
        /// </summary>
        private int PinnedPrefixLen(List<ChatMessage> msgs)
        {
            int i = 0;
            // System 消息
            if (i < msgs.Count && msgs[i].Role == MessageRole.System)
                i++;
            // 首条用户消息（如果够小且不是摘要）
            if (i < msgs.Count && msgs[i].Role == MessageRole.User
                && !IsCompactionSummary(msgs[i]) && IsPinnableUserTurn(msgs[i]))
                i++;
            // 已有的压缩摘要
            while (i < msgs.Count && IsCompactionSummary(msgs[i]))
                i++;
            return i;
        }

        private bool IsPinnableUserTurn(ChatMessage m)
        {
            int contextWindow = GetContextWindow();
            int budget = MaxPinnedFirstUserTokens;
            if (contextWindow > 0)
            {
                int fracBudget = (int)(contextWindow * PinnedFirstUserWindowFrac);
                if (fracBudget < budget) budget = fracBudget;
            }
            int chars = MsgChars(m);
            return (int)(chars * FallbackTokPerChar) <= budget;
        }

        /// <summary>
        /// Tail Start: 从尾部向前扫描，按 token 预算确定保留起点
        /// 对齐 Reasonix tailStart — 不在 tool 消息中间切割
        /// </summary>
        private int TailStart(List<ChatMessage> msgs, int head, int budgetTokens, int minKeep)
        {
            int start = msgs.Count;
            int acc = 0;
            double ratio = FallbackTokPerChar;

            for (int i = msgs.Count - 1; i > head; i--)
            {
                int c = (int)(MsgChars(msgs[i]) * ratio);
                if (msgs.Count - i > minKeep && acc + c > budgetTokens)
                    break;
                acc += c;
                start = i;
            }

            // 对齐：不在 tool 消息中间切割
            while (start > head && start < msgs.Count && msgs[start].Role == MessageRole.Tool)
                start--;

            return start;
        }

        // ═══════════════════════════════════════════════════════════
        //  Partition Fold — 分割保留/折叠区域
        // ═══════════════════════════════════════════════════════════

        private void PartitionFold(CopilotSession session, List<ChatMessage> region,
            out List<ChatMessage> kept, out List<ChatMessage> fold)
        {
            kept = new List<ChatMessage>();
            fold = new List<ChatMessage>();

            foreach (var m in region)
            {
                if (IsCompactionSummary(m) || (m.Role == MessageRole.User && IsPinnableUserTurn(m)))
                {
                    kept.Add(m);
                }
                else if (IsErrorMessage(m))
                {
                    // KeepErrors: 保留错误工具结果
                    kept.Add(m);
                }
                else
                {
                    fold.Add(m);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Summarize — LLM 摘要生成（对齐 Reasonix summarize）
        // ═══════════════════════════════════════════════════════════

        private const string SummarySystemPrompt =
            "You are compacting the earlier part of a coding agent's conversation to save context.\n" +
            "The agent keeps your summary alongside the user's own turns (kept verbatim) and the recent tail; " +
            "your job is to fold the assistant/tool work into a briefing it can resume from.\n" +
            "Write under these exact headings, omitting a heading only if it has no content:\n\n" +
            "## Standing facts & constraints\n" +
            "Everything the user stated that still governs the work — names, paths, IDs, versions, preferences, and hard rules.\n\n" +
            "## Goal\nThe user's request and intent.\n\n" +
            "## Decisions & rationale\nKey choices made so far and why.\n\n" +
            "## Files & code\nFiles read or modified, with specific facts that matter.\n\n" +
            "## Commands & outcomes\nCommands run and their relevant results.\n\n" +
            "## Errors & fixes\nProblems hit and how they were resolved.\n\n" +
            "## Pending & next step\nWhat is still in progress and the single most concrete next action.\n\n" +
            "Rules: be terse — bullet points and fragments. Preserve identifiers, paths, and numbers exactly. " +
            "Do NOT invent anything not present in the messages.";

        private async Task<string> SummarizeWithRetryAsync(List<ChatMessage> fold, CancellationToken ct)
        {
            try
            {
                return await SummarizeAsync(fold, ct);
            }
            catch (OperationCanceledException)
            {
                throw; // 取消不重试
            }
            catch
            {
                // 重试一次
                return await SummarizeAsync(fold, ct);
            }
        }

        private async Task<string> SummarizeAsync(List<ChatMessage> fold, CancellationToken ct)
        {
            string transcript = RenderTranscript(fold);

            var modelRef = _config?.DefaultModel ?? "";
            var (providerConfig, modelName) = _config.ResolveModel(modelRef);

            var request = new CopilotRequest
            {
                Model = modelName,
                Messages = new List<ChatMessage>
                {
                    new ChatMessage(MessageRole.System, SummarySystemPrompt),
                    new ChatMessage(MessageRole.User, transcript)
                },
                Temperature = 0.1,
                MaxTokens = 2048
            };

            var sb = new StringBuilder();
            using (var timeoutCts = new CancellationTokenSource(SummaryTimeoutMs))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token))
            {
                await _provider.StreamAsync(request, chunk =>
                {
                    if (chunk.Type == ChunkType.Text && !string.IsNullOrEmpty(chunk.Content))
                        sb.Append(chunk.Content);
                }, linked.Token);
            }

            string result = sb.ToString().Trim();
            if (string.IsNullOrEmpty(result))
                throw new InvalidOperationException("Summarizer returned empty output");
            return result;
        }

        // ═══════════════════════════════════════════════════════════
        //  辅助方法
        // ═══════════════════════════════════════════════════════════

        private bool FoldEconomics(List<ChatMessage> fold)
        {
            return EstimateMessagesTokens(fold) >= MinFoldTokens;
        }

        private int EstimateMessagesTokens(List<ChatMessage> msgs)
        {
            int total = 0;
            foreach (var m in msgs)
            {
                total += 4; // framing overhead
                total += EstimateTextTokens(m.Content);
                total += EstimateTextTokens(m.ReasoningContent);
                if (m.ToolCalls != null)
                {
                    foreach (var tc in m.ToolCalls)
                    {
                        total += 8;
                        total += EstimateTextTokens(tc.Name);
                        total += EstimateTextTokens(tc.Arguments);
                    }
                }
            }
            return total;
        }

        private int EstimateTextTokens(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int bytes = Encoding.UTF8.GetByteCount(s);
            int byBytes = (bytes + 3) / 4;
            // CJK 文本接近 1 rune/token
            int runes = s.Length;
            return runes > byBytes ? runes : byBytes;
        }

        private int EstimatePromptTokens(CopilotSession session)
        {
            // 优先使用真实 usage
            if (_lastUsage != null && _lastUsage.PromptTokens > 0)
                return _lastUsage.PromptTokens;
            // 回退：字符估算
            int totalChars = 0;
            foreach (var m in session.Messages)
                totalChars += MsgChars(m);
            return (int)(totalChars * FallbackTokPerChar);
        }

        private double TokPerChar(CopilotSession session)
        {
            if (_lastUsage != null && _lastUsage.PromptTokens > 0)
            {
                int chars = 0;
                foreach (var m in session.Messages)
                    chars += MsgChars(m);
                if (chars > 0)
                {
                    double r = (double)_lastUsage.PromptTokens / chars;
                    if (r > 0.05 && r < 2.0) return r;
                }
            }
            return FallbackTokPerChar;
        }

        private int MsgChars(ChatMessage m)
        {
            int n = (m.Content ?? "").Length;
            if (m.ToolCalls != null)
            {
                foreach (var tc in m.ToolCalls)
                    n += (tc.Name ?? "").Length + (tc.Arguments ?? "").Length;
            }
            return n;
        }

        private int GetContextWindow()
        {
            // 从 Provider 配置读取
            if (_config?.Providers != null && _config.Providers.Count > 0)
            {
                var p = _config.Providers[0];
                if (p.ContextWindow > 0) return p.ContextWindow;
            }
            // 回退到 Ui 配置（兼容旧配置）
            return 0;
        }

        private double GetConfigRatio(string name, double defaultVal)
        {
            var ui = _config?.Ui;
            if (ui == null) return defaultVal;
            switch (name)
            {
                case "CompactRatio":
                    return ui.CompactRatio > 0 ? ui.CompactRatio : defaultVal;
                case "SoftCompactRatio":
                    return ui.SoftCompactRatio > 0 ? ui.SoftCompactRatio : defaultVal;
                case "ToolResultSnipRatio":
                    return ui.ToolResultSnipRatio > 0 ? ui.ToolResultSnipRatio : defaultVal;
                case "CompactForceRatio":
                    return ui.CompactForceRatio > 0 ? ui.CompactForceRatio : defaultVal;
                default:
                    return defaultVal;
            }
        }

        private static bool IsCompactionSummary(ChatMessage m)
        {
            if (m.Role != MessageRole.User) return false;
            string content = (m.Content ?? "").TrimStart('\n', ' ');
            return content.StartsWith(SummaryTagOpen);
        }

        private static bool IsErrorMessage(ChatMessage m)
        {
            if (m.Role != MessageRole.Tool) return false;
            string s = (m.Content ?? "").TrimStart().ToLowerInvariant();
            return s.StartsWith("error:") || s.StartsWith("blocked:");
        }

        private SnipStrategy GetSnipStrategy(string toolName)
        {
            // 写入类工具用 SideEffect 策略，只读类用 ReadOnly 策略
            if (IsWriteToolName(toolName))
                return SideEffectSnip;
            return ReadOnlySnip;
        }

        private static bool IsWriteToolName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            switch (name)
            {
                case "modify":
                case "design":
                case "piping":
                case "execute_pml":
                case "batch":
                case "write_file":
                case "cad_import":
                case "autocad":
                case "export":
                    return true;
                default:
                    return false;
            }
        }

        private string RenderTranscript(List<ChatMessage> msgs)
        {
            var sb = new StringBuilder();
            foreach (var m in msgs)
            {
                switch (m.Role)
                {
                    case MessageRole.User:
                        sb.AppendLine($"[user]\n{m.Content}\n");
                        break;
                    case MessageRole.Assistant:
                        if (!string.IsNullOrEmpty(m.Content))
                            sb.AppendLine($"[assistant]\n{m.Content}");
                        if (m.ToolCalls != null)
                        {
                            foreach (var tc in m.ToolCalls)
                                sb.AppendLine($"[assistant calls {tc.Name}] {SummarizeToolArgs(tc.Arguments)}");
                        }
                        sb.AppendLine();
                        break;
                    case MessageRole.Tool:
                        string content = m.Content ?? "";
                        if (content.Length > 500)
                            content = content.Substring(0, 500) + "...";
                        sb.AppendLine($"[tool result]\n{content}\n");
                        break;
                    case MessageRole.System:
                        sb.AppendLine($"[system]\n{m.Content}\n");
                        break;
                }
            }
            return sb.ToString();
        }

        private static string SummarizeToolArgs(string args)
        {
            if (string.IsNullOrEmpty(args)) return "(no arguments)";
            try
            {
                var parsed = JObject.Parse(args);
                var keys = parsed.Properties().Select(p => p.Name).ToList();
                return $"{{{string.Join(", ", keys)}}} ({keys.Count} keys)";
            }
            catch
            {
                return $"({args.Length} bytes)";
            }
        }

        private static string MechanicalFoldDigest(int count, string archive)
        {
            string where = string.IsNullOrEmpty(archive) ? "." : $" (archived to {archive}).";
            return $"{count} earlier message(s) were folded here to free context, " +
                   $"but the automatic summary was unavailable{where} " +
                   "Ask the user if you need details from before this point.";
        }

        private string ArchiveMessages(List<ChatMessage> msgs)
        {
            try
            {
                if (!Directory.Exists(_archiveDir))
                    Directory.CreateDirectory(_archiveDir);

                string path = Path.Combine(_archiveDir,
                    DateTime.Now.ToString("yyyyMMdd-HHmmss.fff") + ".jsonl");
                using (var writer = new StreamWriter(path, false, Encoding.UTF8))
                {
                    foreach (var m in msgs)
                    {
                        var obj = new
                        {
                            role = m.Role.ToString().ToLowerInvariant(),
                            content = m.Content,
                            tool_call_id = m.ToolCallId,
                            tool_calls = m.ToolCalls
                        };
                        writer.WriteLine(JsonConvert.SerializeObject(obj, Formatting.None));
                    }
                }
                return path;
            }
            catch
            {
                return null;
            }
        }

        private static int ExtractOriginalBytes(string content)
        {
            if (content.StartsWith(SnippedMarker))
            {
                // 格式: [snipped tool result — name, NNNN bytes archived to ...
                int start = content.IndexOf(",", SnippedMarker.Length);
                if (start > 0)
                {
                    int end = content.IndexOf(" bytes", start);
                    if (end > start)
                    {
                        string numStr = content.Substring(start + 1, end - start - 1).Trim();
                        int n;
                        if (int.TryParse(numStr, out n) && n > 0) return n;
                    }
                }
            }
            return content.Length;
        }

        private static string ExtractOriginalArchive(string content)
        {
            if (!content.StartsWith(SnippedMarker)) return null;
            const string marker = " bytes archived to ";
            int start = content.IndexOf(marker);
            if (start < 0) return null;
            start += marker.Length;
            int end = content.IndexOf(";", start);
            if (end < 0) return null;
            string archive = content.Substring(start, end - start).Trim();
            return archive == "not archived" ? null : archive;
        }

        private static double Percent(double value, double total)
        {
            return total > 0 ? Math.Round(value / total * 100) : 0;
        }

        // ═══════════════════════════════════════════════════════════
        //  数据结构
        // ═══════════════════════════════════════════════════════════

        public class PruneStats
        {
            public int Results { get; set; }
            public int SavedChars { get; set; }
            public string Archive { get; set; }
        }

        private class SnipStrategy
        {
            public int Head { get; set; }
            public int Tail { get; set; }
            public int HeadChars { get; set; }
            public int TailChars { get; set; }
        }
    }
}
