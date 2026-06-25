using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Logging;
using E3DCopilot.Core.Memory;
using E3DCopilot.Core.Providers;
using E3DCopilot.Core.Security;
using E3DCopilot.Core.Skills;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core
{
    /// <summary>
    /// Core Agent loop: LLM call → ToolExecutor dispatch → result injection → loop
    /// net48 compatible: callback mode streaming, no IAsyncEnumerable
    /// 
    /// Phase 1 对齐 Reasonix:
    ///   - 只读工具并行执行（partitionToolCalls + Task.WhenAll）
    ///   - executeOne 提取为独立方法
    ///   - Storm Breaker 循环检测
    ///   - Grace Round 最大步数优雅收尾
    /// </summary>
    public class AgentLoop
    {
        private readonly ICopilotProvider _provider;
        private readonly IEventSink _sink;
        private readonly ToolExecutor _executor;
        private readonly CommandPermissionController _permission;
        private readonly ToolPolicy _toolPolicy;
        private readonly CopilotConfig _config;
        private readonly CopilotController _controller;
        private readonly SkillManager _skillManager;

        private int MaxSteps => _config?.Ui?.MaxSteps > 0 ? _config.Ui.MaxSteps : 20;

        // ── Storm Breaker 状态（由 CopilotController 跨 turn 持久化注入） ──
        public string StormSig { get; set; } = "";
        public int StormCount { get; set; } = 0;
        private const int StormBreakThreshold = 3;

        public AgentLoop(ICopilotProvider provider, IEventSink sink,
            ToolExecutor executor, CommandPermissionController permission,
            CopilotConfig config, CopilotController controller = null,
            ToolPolicy toolPolicy = null, SkillManager skillManager = null,
            string stormSig = "", int stormCount = 0)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _sink = sink;
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _permission = permission ?? CommandPermissionController.CreateDefault();
            _toolPolicy = toolPolicy ?? CreateDefaultToolPolicy();
            _config = config ?? CopilotConfig.Load();
            _controller = controller;
            _skillManager = skillManager;
            StormSig = stormSig;
            StormCount = stormCount;
        }

        private ToolPolicy CreateDefaultToolPolicy()
        {
            var policy = new ToolPolicy();
            string mode = _controller?.ToolApprovalMode ?? "auto";

            if (mode == "yolo")
            {
                policy.ApplyPreset(ToolPreset.Auto);
                policy.Set("ask", ApprovalMode.Auto);
                policy.Set("todo_write", ApprovalMode.Auto);
                policy.Set("complete_step", ApprovalMode.Auto);
                policy.Set("read_file", ApprovalMode.Auto);
                policy.Set("write_file", ApprovalMode.Auto);
                policy.Set("run_skill", ApprovalMode.Auto);
                policy.Set("cad_import", ApprovalMode.Auto);
                policy.Set("autocad", ApprovalMode.Auto);
                policy.Set("todo_write", ApprovalMode.Auto);
                policy.Set("memory", ApprovalMode.Auto);
                policy.Set("grep", ApprovalMode.Auto);
                policy.Set("glob", ApprovalMode.Auto);
            }
            else if (mode == "ask")
            {
                policy.ApplyPreset(ToolPreset.Confirm);
                policy.Set("query", ApprovalMode.Ask);
                policy.Set("get_attributes", ApprovalMode.Ask);
                policy.Set("check", ApprovalMode.Ask);
                policy.Set("calculate", ApprovalMode.Ask);
                policy.Set("export", ApprovalMode.Ask);
                policy.Set("geometry", ApprovalMode.Ask);
                policy.Set("ask", ApprovalMode.Auto);
                policy.Set("todo_write", ApprovalMode.Auto);
                policy.Set("complete_step", ApprovalMode.Auto);
                policy.Set("read_file", ApprovalMode.Ask);
                policy.Set("write_file", ApprovalMode.Ask);
                policy.Set("run_skill", ApprovalMode.Ask);
                policy.Set("modify", ApprovalMode.Ask);
                policy.Set("execute_pml", ApprovalMode.Ask);
                policy.Set("design", ApprovalMode.Ask);
                policy.Set("piping", ApprovalMode.Ask);
                policy.Set("undo_redo", ApprovalMode.Auto);
                policy.Set("report", ApprovalMode.Auto);
                policy.Set("compare", ApprovalMode.Auto);
                policy.Set("hierarchy", ApprovalMode.Auto);
                policy.Set("batch", ApprovalMode.Ask);
                policy.Set("cad_import", ApprovalMode.Ask);
                policy.Set("autocad", ApprovalMode.Ask);
                policy.Set("todo_write", ApprovalMode.Auto);
                policy.Set("memory", ApprovalMode.Auto);
                policy.Set("grep", ApprovalMode.Auto);
                policy.Set("glob", ApprovalMode.Auto);
            }
            else
            {
                // "auto"：只读工具自动执行，写工具需确认
                policy.ApplyPreset(ToolPreset.Confirm);
                policy.Set("query", ApprovalMode.Auto);
                policy.Set("get_attributes", ApprovalMode.Auto);
                policy.Set("check", ApprovalMode.Auto);
                policy.Set("calculate", ApprovalMode.Auto);
                policy.Set("export", ApprovalMode.Auto);
                policy.Set("geometry", ApprovalMode.Auto);
                policy.Set("ask", ApprovalMode.Auto);
                policy.Set("todo_write", ApprovalMode.Auto);
                policy.Set("complete_step", ApprovalMode.Auto);
                policy.Set("read_file", ApprovalMode.Auto);
                policy.Set("run_skill", ApprovalMode.Auto);
                policy.Set("undo_redo", ApprovalMode.Auto);
                policy.Set("report", ApprovalMode.Auto);
                policy.Set("compare", ApprovalMode.Auto);
                policy.Set("hierarchy", ApprovalMode.Auto);
                policy.Set("write_file", ApprovalMode.Ask);
                policy.Set("modify", ApprovalMode.Ask);
                policy.Set("execute_pml", ApprovalMode.Ask);
                policy.Set("design", ApprovalMode.Ask);
                policy.Set("piping", ApprovalMode.Ask);
                policy.Set("batch", ApprovalMode.Ask);
                policy.Set("cad_import", ApprovalMode.Auto);
                policy.Set("autocad", ApprovalMode.Auto);
                policy.Set("todo_write", ApprovalMode.Auto);
                policy.Set("memory", ApprovalMode.Auto);
                policy.Set("grep", ApprovalMode.Auto);
                policy.Set("glob", ApprovalMode.Auto);
            }

            return policy;
        }

        /// <summary>
        /// Run Agent loop
        /// </summary>
        public async Task RunAsync(CopilotSession session, string input, string[] images = null,
            CancellationToken ct = default)
        {
            session.AddUserMessage(input, images);
            CopilotLogger.Info("AgentLoop.RunAsync 开始: input='{0}', images={1}, session messages={2}", 
                input?.Substring(0, Math.Min(50, input?.Length ?? 0)), 
                images != null ? images.Length : 0, 
                session.Messages.Count);

            // ── 记忆注入：每 turn 只计算一次，后续步骤复用 ──
            string memoryContext = ComputeMemoryContext(session);

            for (int step = 0; step < MaxSteps; step++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    // ── Grace Round: 最大步数前注入收尾 nudge（对齐 Reasonix） ──
                    if (step == MaxSteps - 1)
                    {
                        session.AddSystemMessage(
                            "You are about to reach the maximum step limit. " +
                            "Stop calling tools and give a final summary of what has been done, " +
                            "what still needs to be done, and any recommendations.");
                    }

                    // ── Steer Queue: 用户中途干预，注入引导消息（对齐 Reasonix steerQueue） ──
                    if (_controller?.SteerQueue != null)
                    {
                        string steer;
                        while (_controller.SteerQueue.TryDequeue(out steer))
                        {
                            session.AddSystemMessage(
                                $"[user steer] The user has provided mid-run guidance: \"{steer}\"\n" +
                                "Please adjust your approach accordingly.");
                            _sink?.Emit(CopilotEvent.Notice($"已注入用户引导: {steer}"));
                        }
                    }

                    // 1. Build request (including available tool definitions)
                    var request = BuildRequest(session, memoryContext);

                    // 1.5 每轮 LLM 调用前发射 TurnStarted（前端 startStreaming 幂等，不会重复创建 assistant 消息）
                    _sink.Emit(new CopilotEvent
                    {
                        Kind = EventKind.TurnStarted,
                        Text = $"Step {step + 1}"
                    });

                    // 2. Call LLM (streaming)
                    var result = await StreamLlmAsync(request, ct);

                    // 3. Save assistant message
                    session.AddAssistantMessage(result.Text, result.ToolCalls);

                    // 4. No tool calls → done
                    if (result.ToolCalls == null || result.ToolCalls.Count == 0)
                    {
                        CopilotLogger.Info("AgentLoop step {0}: 无工具调用，对话结束", step);
                        return;  // TurnDone 由 Controller 统一发射
                    }

                    // 5. Execute tools — read-only parallel, writer serial（对齐 Reasonix executeBatch）
                    await ExecuteBatchAsync(session, result.ToolCalls, ct);

                    // 6. Context compression
                    MaybeCompact(session);
                }
                catch (OperationCanceledException)
                {
                    _sink.Emit(CopilotEvent.Notice("已取消"));
                    return;  // TurnDone 由 Controller 统一发射
                }
                catch (Exception ex)
                {
                    try { CopilotLogger.Error(ex, "AgentLoop step {0} failed", step); } catch { }

                    string msg = "遇到错误";
                    try { msg = $"错误: {ex.GetType().Name}: {ex.Message}"; } catch { }

                    _sink?.Emit(CopilotEvent.Error(msg));
                    return;  // TurnDone 由 Controller 统一发射
                }
            }

            _sink.Emit(CopilotEvent.Notice($"已达到最大步数限制 {MaxSteps}"));
        }

        // ═══════════════════════════════════════════════════════════
        //  executeBatch — 分区并行执行（对齐 Reasonix executeBatch）
        //  只读工具段：Task.WhenAll 并行
        //  写入工具段：串行保序
        // ═══════════════════════════════════════════════════════════

        private async Task ExecuteBatchAsync(CopilotSession session, List<ToolCall> calls, CancellationToken ct)
        {
            var results = new string[calls.Count];
            var errors = new string[calls.Count];

            // ── 去重：同一轮中 Name+Arguments 完全相同的工具调用，只执行一次 ──
            // LLM 有时会返回重复的 tool_calls（尤其是 ask），去重避免重复弹窗
            var dedupeMap = new Dictionary<string, int>(); // signature → first index
            var isDupe = new bool[calls.Count];
            for (int j = 0; j < calls.Count; j++)
            {
                string sig = $"{calls[j].Name}\0{calls[j].Arguments}";
                if (dedupeMap.ContainsKey(sig))
                {
                    isDupe[j] = true;
                    CopilotLogger.Info("ExecuteBatchAsync: dedup tool call {0} (idx {1} → idx {2})",
                        calls[j].Name, j, dedupeMap[sig]);
                }
                else
                {
                    dedupeMap[sig] = j;
                }
            }

            // 分区：连续只读 → parallel，写/未知 → serial
            int i = 0;
            while (i < calls.Count)
            {
                if (ct.IsCancellationRequested) break;

                // 跳过重复调用（结果会在下面从首个实例复制）
                if (isDupe[i]) { i++; continue; }

                if (IsParallelisable(calls[i].Name))
                {
                    // 收集连续只读段（跳过重复）
                    int start = i;
                    i++;
                    while (i < calls.Count && (IsParallelisable(calls[i].Name) || isDupe[i]))
                        i++;

                    // 并行执行只读段（对齐 Reasonix runParallel）
                    var tasks = new List<Task>();
                    for (int j = start; j < i; j++)
                    {
                        if (ct.IsCancellationRequested || isDupe[j]) continue;
                        int idx = j; // capture
                        tasks.Add(Task.Run(async () =>
                        {
                            var (output, err) = await ExecuteOneAsync(session, calls[idx], ct);
                            results[idx] = output;
                            errors[idx] = err;
                        }, ct));
                    }
                    await Task.WhenAll(tasks);
                }
                else
                {
                    // 串行执行写入/未知工具
                    var (output, err) = await ExecuteOneAsync(session, calls[i], ct);
                    results[i] = output;
                    errors[i] = err;
                    i++;
                }
            }

            // 将去重调用的结果从首个实例复制过来
            for (int j = 0; j < calls.Count; j++)
            {
                if (isDupe[j])
                {
                    string sig = $"{calls[j].Name}\0{calls[j].Arguments}";
                    int firstIdx = dedupeMap[sig];
                    results[j] = results[firstIdx];
                    errors[j] = errors[firstIdx];
                }
            }

            // ── Storm Breaker（对齐 Reasonix applyStormBreaker） ──
            ApplyStormBreaker(calls, errors, results);

            // 注入结果到 session
            for (int j = 0; j < calls.Count; j++)
            {
                // 空结果也要告知 LLM 工具已执行，避免 LLM 困惑
                string result = string.IsNullOrEmpty(results[j])
                    ? $"(tool {calls[j].Name} executed, no output)"
                    : results[j];
                session.AddToolResult(calls[j].Id, result);
            }
        }

        /// <summary>
        /// 判断工具是否可并行执行（只读 + 非 todo/complete_step）
        /// 对齐 Reasonix parallelisable()
        /// </summary>
        private bool IsParallelisable(string toolName)
        {
            var handler = _executor.GetHandler(toolName);
            return handler != null && handler.IsReadOnly;
        }

        /// <summary>
        /// 元能力/只读工具不受危险模式检测限制（todo_write、complete_step 等）
        /// 它们的 JSON 参数可能被误判（如包含 >、; 等字符），但实际不会执行 Shell/PML
        /// </summary>
        private static readonly HashSet<string> _dangerousExemptTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "todo_write", "complete_step", "memory", "run_skill",
            "read_file", "write_file", "grep", "glob", "ask",
            "report", "compare", "hierarchy"
        };

        private bool IsMetaToolDangerousExempt(string toolName)
        {
            return _dangerousExemptTools.Contains(toolName);
        }

        // ═══════════════════════════════════════════════════════════
        //  executeOne — 单工具执行（对齐 Reasonix executeOne）
        //  包含：权限检查 + 审批 + plan mode 门控 + 实际执行
        // ═══════════════════════════════════════════════════════════

        private async Task<(string output, string errMsg)> ExecuteOneAsync(
            CopilotSession session, ToolCall call, CancellationToken ct)
        {
            // 跳过空工具调用
            if (string.IsNullOrWhiteSpace(call.Name))
            {
                _sink?.Emit(CopilotEvent.ToolStart(call.Id, call.Name ?? "unknown", call.Arguments));
                _sink?.Emit(CopilotEvent.Notice($"已跳过空工具名称的调用"));
                _sink?.Emit(CopilotEvent.ToolFail(call.Id, "empty name"));
                return ("已跳过: 工具名称为空", "empty name");
            }

            // ── 1. 危险模式检测（仅对写/执行类工具检查，元能力只读工具始终安全）──
            if (!IsMetaToolDangerousExempt(call.Name) && _permission.HasDangerousPattern(call.Arguments))
            {
                string msg = $"Tool {call.Name} 参数包含危险模式，已阻止";
                _sink?.Emit(CopilotEvent.ToolStart(call.Id, call.Name, call.Arguments));
                _sink?.Emit(CopilotEvent.Error(msg));
                _sink?.Emit(CopilotEvent.ToolFail(call.Id, msg));
                return (msg, "危险模式已阻止");
            }

            // ── 2. Plan Mode 门控（对齐 Reasonix planModeBlocked） ──
            bool isPlanMode = session?.IsPlanMode ?? false;
            if (!_toolPolicy.IsAllowed(call.Name, isPlanMode))
            {
                string msg = isPlanMode
                    ? $"blocked: \"{call.Name}\" is a writer tool and plan mode is read-only. Keep exploring with read-only tools."
                    : $"Tool {call.Name} blocked by tool policy";
                _sink?.Emit(CopilotEvent.ToolStart(call.Id, call.Name, call.Arguments));
                _sink?.Emit(CopilotEvent.Error(msg));
                _sink?.Emit(CopilotEvent.ToolFail(call.Id, msg));
                return (msg, "blocked by plan mode");
            }

            // ── 3. 批量操作检测 ──
            bool isBatch = _permission.IsBatchOperation(call.Arguments);

            // ── 4. 审批检查 ──
            bool needsApproval = _toolPolicy.GetMode(call.Name) == ApprovalMode.Ask || isBatch;

            // ── 4.1 通知前端工具开始（审批前就发射，让 ToolCard 先显示）──
            _sink?.Emit(CopilotEvent.ToolStart(call.Id, call.Name, call.Arguments));

            if (needsApproval)
            {
                var approval = new PendingApproval
                {
                    ToolName = call.Name,
                    Args = call.Arguments,
                    Description = $"{call.Name}({call.Arguments})"
                        + (isBatch ? " [batch]" : "")
                };

                if (_controller != null)
                {
                    _controller.RegisterApproval(approval);
                }
                else
                {
                    _sink.Emit(new CopilotEvent
                    {
                        Kind = EventKind.ApprovalRequest,
                        Data = approval
                    });
                }

                var approvalResult = await approval.WaitAsync(ct);
                if (!approvalResult.Allow)
                {
                    string msg = $"User rejected {call.Name}";
                    _sink?.Emit(CopilotEvent.ToolFail(call.Id, msg));
                    return (msg, "user rejected");
                }
            }

            // ── 5. 实际执行（ToolStart 已在第 4 步发射）──
            CopilotLogger.Info("工具执行开始: {0}, args={1}", call.Name, call.Arguments?.Substring(0, Math.Min(200, call.Arguments?.Length ?? 0)));
            try
            {
                var toolResult = await _executor.ExecuteAsync(call.Name, call.Arguments, ct);
                CopilotLogger.Info("工具执行完成: {0}, success={1}, duration={2}ms, resultLen={3}", call.Name, toolResult.Success, toolResult.DurationMs, toolResult.Text?.Length ?? 0);
                if (toolResult.Success)
                {
                    string output = TruncateToolResult(toolResult.Text, call.Name);
                    _sink?.Emit(CopilotEvent.ToolComplete(call.Id, output));
                    return (output, null);
                }
                else
                {
                    string errOutput = TruncateToolResult(toolResult.Error ?? toolResult.Text, call.Name);
                    _sink?.Emit(CopilotEvent.ToolFail(call.Id, toolResult.Error ?? "execution failed"));
                    return (errOutput, toolResult.Error ?? "execution failed");
                }
            }
            catch (Exception ex)
            {
                string err = $"{ex.GetType().Name}: {ex.Message}";
                CopilotLogger.Error(ex, "工具执行异常: {0}", call.Name);
                _sink?.Emit(CopilotEvent.ToolFail(call.Id, err));
                return ($"Error: {err}", err);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Storm Breaker（对齐 Reasonix applyStormBreaker）
        //  连续 N 次同样错误 → 注入 nudge 让模型换策略
        // ═══════════════════════════════════════════════════════════

        private void ApplyStormBreaker(List<ToolCall> calls, string[] errors, string[] results)
        {
            // 构建签名：所有调用都是 error 才算 storm
            if (calls.Count == 0) { StormSig = ""; StormCount = 0; return; }

            bool allErrored = true;
            var sb = new StringBuilder();
            for (int i = 0; i < calls.Count; i++)
            {
                if (string.IsNullOrEmpty(errors[i]))
                {
                    allErrored = false;
                    break;
                }
                sb.Append(calls[i].Name);
                sb.Append('\0');
                sb.Append(errors[i]);
                sb.Append('\0');
            }

            if (!allErrored)
            {
                // 有成功的调用 → 重置计数器
                StormSig = "";
                StormCount = 0;
                return;
            }

            string sig = sb.ToString();
            if (sig != StormSig)
            {
                StormSig = sig;
                StormCount = 1;
                return;
            }

            StormCount++;
            if (StormCount < StormBreakThreshold)
                return;

            // 注入 nudge（对齐 Reasonix：在第一个 tool result 后追加 loop guard 消息）
            string subject = calls.Count == 1
                ? $"\"{calls[0].Name}\""
                : $"this batch of {calls.Count} tool calls";
            string shortMsg = calls.Count == 1
                ? calls[0].Name
                : $"a batch of {calls.Count} calls";

            results[0] = results[0] + $"\n\n[循环守卫] {subject} 已连续 {StormCount} 次因相同错误失败。重新发送此调用（即使修改措辞）也不会有帮助。请更换策略：如果参数被截断，请在单次调用中写入更少内容；否则修正参数、使用其他工具，或在最终回复中解释阻塞原因。";

            _sink.Emit(CopilotEvent.Notice(
                $"循环守卫: {shortMsg} 已连续 {StormCount} 次因相同方式失败 — 提示模型更换策略"));
        }

        // ═══════════════════════════════════════════════════════════
        //  StreamLlmAsync — LLM 流式调用
        // ═══════════════════════════════════════════════════════════

        private async Task<(string Text, List<ToolCall> ToolCalls)> StreamLlmAsync(
            CopilotRequest request, CancellationToken ct)
        {
            string text = "";
            string reasoningText = "";  // 累积 reasoning 内容，用于后续工具调用提取
            var toolCalls = new List<ToolCall>();

            await _provider.StreamAsync(request, chunk =>
            {
                switch (chunk.Type)
                {
                    case ChunkType.Reasoning:
                        reasoningText += chunk.Content;
                        // 只发射给前端，不累积到 text（text 用于 assistant 回复和 XML fallback）
                        _sink.Emit(CopilotEvent.Reasoning(chunk.Content));
                        break;
                    case ChunkType.Text:
                        text += chunk.Content;
                        _sink.Emit(CopilotEvent.TextEvent(chunk.Content));
                        break;
                    case ChunkType.ToolCall:
                        MergeToolCall(toolCalls, chunk.ToolCall);
                        break;
                    case ChunkType.Usage:
                        _sink.Emit(new CopilotEvent
                        {
                            Kind = EventKind.Usage,
                            Text = $"Token: {chunk.UsageData.TotalTokens}"
                        });
                        break;
                }
            }, ct);

            // ── Reasoning 工具调用提取（适配 MiMo v2.5 等推理模型）──
            // 这些模型不返回结构化 delta.tool_calls，而是在 reasoning 中描述工具调用
            if (toolCalls.Count == 0 && !string.IsNullOrEmpty(reasoningText))
            {
                var reasoningCalls = Providers.ReasoningToolCallExtractor.Extract(reasoningText);
                if (reasoningCalls.Count > 0)
                {
                    toolCalls.AddRange(reasoningCalls);
                    _sink.Emit(CopilotEvent.Notice(
                        $"已从推理内容中提取 {reasoningCalls.Count} 个工具调用（推理回退模式）"));
                }
            }

            // ── XML fallback ──
            if (toolCalls.Count == 0 && !string.IsNullOrEmpty(text)
                && Providers.ToolInvocationParser.ContainsToolInvocation(text))
            {
                var xmlCalls = Providers.ToolInvocationParser.ExtractToolCalls(text);
                if (xmlCalls.Count > 0)
                {
                    toolCalls.AddRange(xmlCalls);
                    text = Providers.ToolInvocationParser.StripToolInvocationTags(text);
                    _sink.Emit(CopilotEvent.Notice(
                        $"已从文本中解析 {xmlCalls.Count} 个 XML 格式工具调用（回退模式）"));
                }
            }

            // ─ 检查空回复并传递错误信息 ──
            string errorMessage = null;
            if (string.IsNullOrWhiteSpace(text) && toolCalls.Count == 0)
            {
                errorMessage = "LLM 返回了空响应（无内容且无工具调用）";
                _sink.Emit(CopilotEvent.Notice("警告: LLM 返回了空响应"));
            }

            _sink.Emit(CopilotEvent.StreamEnd(errorMessage: errorMessage));
            return (text, toolCalls);
        }

        private void MergeToolCall(List<ToolCall> existing, ToolCall incoming)
        {
            var match = existing.FirstOrDefault(t => t.Id == incoming.Id);
            if (match != null)
            {
                if (!string.IsNullOrEmpty(incoming.Name))
                    match.Name += incoming.Name;
                if (!string.IsNullOrEmpty(incoming.Arguments))
                    match.Arguments += incoming.Arguments;
            }
            else
            {
                existing.Add(incoming);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  BuildRequest — 构建 LLM 请求（对齐 Reasonix boot.go 装配）
        // ═══════════════════════════════════════════════════════════

        private CopilotRequest BuildRequest(CopilotSession session, string memoryContext = null)
        {
            string modelRef = !string.IsNullOrEmpty(_controller?.CurrentModelName)
                ? _controller.CurrentModelName
                : _config.DefaultModel;
            var (providerConfig, modelName) = _config.ResolveModel(modelRef);

            var request = new CopilotRequest
            {
                Model = modelName,
                Temperature = providerConfig.Temperature,
                MaxTokens = providerConfig.MaxTokens,
                Messages = new List<ChatMessage>()
            };

            // System Prompt（静态基础 + Skill 索引 + 动态上下文）
            // 静态基础享受 vLLM 前缀缓存
            string currentElement = _executor.GetCurrentElementName();
            var selectedElements = _executor.GetSelectedElementNames();
            string currentZone = DeriveZoneFromElement(currentElement);
            request.Messages.Add(new ChatMessage(MessageRole.System,
                SystemPrompt.Build(currentElement, currentZone, selectedElements, _skillManager)));

            // Tool definitions — 暴露给 LLM 的工具
            var toolSchemas = new List<ToolSchema>();
            var exposedToolNames = new HashSet<string>
            {
                "query", "modify", "check", "calculate", "export", "execute_pml",
                "get_attributes",
                "design", "piping", "geometry",
                "undo_redo", "report", "compare", "hierarchy", "batch",
                "ask", "todo_write", "complete_step", "read_file", "write_file",
                "run_skill", "grep", "glob", "memory",
                "cad_import", "autocad"
            };
            foreach (var handler in _executor.GetAllHandlers())
            {
                if (exposedToolNames.Contains(handler.Name))
                {
                    toolSchemas.Add(new ToolSchema
                    {
                        Name = handler.Name,
                        Description = handler.Description,
                        ParametersJson = handler.ParameterSchema
                    });
                }
            }
            request.Tools = toolSchemas;

            // History messages (last 20)
            var history = session.GetRecentMessages(20);

            // 安全守卫：删除头部孤立的 tool 消息
            while (history.Count > 0 && history[0].Role == MessageRole.Tool)
            {
                history.RemoveAt(0);
            }

            request.Messages.AddRange(history);

            // ── 多模态 Vision 门控：非视觉模型时剥离图片数据，避免 API 报错 ──
            if (providerConfig.VisionModels != null && providerConfig.VisionModels.Count > 0)
            {
                bool isVisionModel = providerConfig.VisionModels.Exists(
                    vm => !string.IsNullOrEmpty(vm) &&
                          vm.Equals(modelName, StringComparison.OrdinalIgnoreCase));
                if (!isVisionModel)
                {
                    foreach (var msg in request.Messages)
                    {
                        if (msg.Images != null && msg.Images.Length > 0)
                            msg.Images = null;
                    }
                }
            }

            // ── 记忆注入：放在历史之后、更靠近当前对话位置（每 turn 只计算一次）──
            if (!string.IsNullOrEmpty(memoryContext))
            {
                request.Messages.Add(new ChatMessage(MessageRole.System, memoryContext));
            }

            return request;
        }

        // ═══════════════════════════════════════════════════════════
        //  MaybeCompact — 上下文压缩（对齐 Reasonix maybeCompact）
        //  轻量实现：消息数 > 15 时，保留最近 6 条 + 摘要前缀
        // ═══════════════════════════════════════════════════════════

        private void MaybeCompact(CopilotSession session)
        {
            const int compactThreshold = 15;  // assistant 消息超过此数量触发
            const int keepTail = 6;           // 保留最近 N 条消息

            var msgs = session.Messages;
            int assistantCount = msgs.Count(m => m.Role == MessageRole.Assistant);
            if (assistantCount <= compactThreshold) return;

            // 找到保留分割点：保留最后 keepTail 条消息
            // 实际保留多一点，确保 tool 消息不孤悬
            int keepFrom = Math.Max(0, msgs.Count - keepTail);
            // 确保分割点后的首条消息不是孤立的 tool 消息
            while (keepFrom < msgs.Count && msgs[keepFrom].Role == MessageRole.Tool)
                keepFrom++;
            if (keepFrom >= msgs.Count) return; // 保护：无有效消息可保留

            // 构建摘要（简单方案：提取用户消息和助手消息首句）
            var oldMsgs = msgs.Take(keepFrom).ToList();
            var summary = BuildCompactSummary(oldMsgs);

            // 替换：摘要 system 消息 + 尾部原样保留
            var tail = msgs.Skip(keepFrom).ToList();
            msgs.Clear();
            msgs.Add(new ChatMessage(MessageRole.System,
                $"<compaction-summary>\n{summary}\n</compaction-summary>"));
            msgs.AddRange(tail);

            _sink.Emit(CopilotEvent.Notice(
                $"上下文已压缩: {oldMsgs.Count} 条消息 → 摘要, {tail.Count} 条原样保留"));
        }

        private string BuildCompactSummary(List<ChatMessage> messages)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Earlier conversation summary");

            string currentGoal = "";
            foreach (var msg in messages)
            {
                if (msg.Role == MessageRole.User && !string.IsNullOrEmpty(msg.Content))
                {
                    // 提取用户目标
                    string trimmed = msg.Content.Length > 200
                        ? msg.Content.Substring(0, 200) + "..."
                        : msg.Content;
                    if (currentGoal != trimmed)
                    {
                        sb.AppendLine($"- User asked: {trimmed}");
                        currentGoal = trimmed;
                    }
                }
                else if (msg.Role == MessageRole.Assistant && !string.IsNullOrEmpty(msg.Content))
                {
                    // 提取助手首句作为响应摘要
                    string firstLine = msg.Content.Split(new[] { '\n' }, 2)[0];
                    if (firstLine.Length > 150)
                        firstLine = firstLine.Substring(0, 150) + "...";
                    sb.AppendLine($"  → {firstLine}");
                }
            }

            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════════
        //  记忆注入 — 每 turn 计算一次，返回注入文本（空字符串表示无匹配）
        // ═══════════════════════════════════════════════════════════

        private string ComputeMemoryContext(CopilotSession session)
        {
            if (_controller?.Memory == null) return null;

            // 提取最后一条用户消息作为搜索源
            string lastUserMsg = null;
            for (int i = session.Messages.Count - 1; i >= 0; i--)
            {
                if (session.Messages[i].Role == MessageRole.User
                    && !string.IsNullOrEmpty(session.Messages[i].Content))
                {
                    lastUserMsg = session.Messages[i].Content;
                    break;
                }
            }
            if (string.IsNullOrWhiteSpace(lastUserMsg)) return null;

            // 提取关键词（简单策略：按空格分割，取 > 2 字符的词，去重，最多 5 个）
            var words = lastUserMsg.Split(new[] { ' ', ',', '，', ';', '。', '？', '！', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            var keywords = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var w in words)
            {
                string trimmed = w.Trim();
                if (trimmed.Length > 2 && seen.Add(trimmed))
                    keywords.Add(trimmed);
                if (keywords.Count >= 5) break;
            }
            if (keywords.Count == 0) return null;

            // 搜索记忆
            try
            {
                var allMemories = _controller.Memory.List();
                if (allMemories.Count == 0) return null;

                var matched = new List<Memory.MemoryEntry>();
                foreach (var m in allMemories)
                {
                    int score = 0;
                    string titleLower = (m.Title ?? "").ToLowerInvariant();
                    string contentLower = (m.Content ?? "").ToLowerInvariant();
                    foreach (var kw in keywords)
                    {
                        string kwLower = kw.ToLowerInvariant();
                        if (titleLower.Contains(kwLower)) score += 3;
                        if (contentLower.Contains(kwLower)) score += 2;
                        if (m.Tags != null && Array.Exists(m.Tags, t => t.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0))
                            score += 1;
                    }
                    if (score > 0)
                        matched.Add(m);
                }

                if (matched.Count == 0) return null;

                // 取分数最高的前 3 条，构建注入文本
                var top = matched.OrderByDescending(m =>
                {
                    int s = 0;
                    string tl = (m.Title ?? "").ToLowerInvariant();
                    string cl = (m.Content ?? "").ToLowerInvariant();
                    foreach (var kw in keywords)
                    {
                        string kl = kw.ToLowerInvariant();
                        if (tl.Contains(kl)) s += 3;
                        if (cl.Contains(kl)) s += 2;
                    }
                    return s;
                }).Take(3).ToList();

                var sb = new StringBuilder();
                sb.AppendLine("<relevant-memories>");
                sb.AppendLine("以下是与当前对话相关的历史记忆，请参考：");
                foreach (var m in top)
                {
                    string excerpt = m.Content;
                    if (excerpt.Length > 200)
                        excerpt = excerpt.Substring(0, 197) + "...";
                    sb.AppendLine($"- [{m.Kind}] {m.Title}: {excerpt}");
                }
                sb.AppendLine("</relevant-memories>");

                _sink?.Emit(CopilotEvent.Notice($"记忆上下文: {top.Count} 条相关记忆已注入"));
                return sb.ToString();
            }
            catch
            {
                // 记忆检索失败不影响主流程
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Token 预算 — 工具结果超长截断
        // ═══════════════════════════════════════════════════════════

        private const int MaxToolResultChars = 4000;

        private string TruncateToolResult(string text, string toolName)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= MaxToolResultChars)
                return text;

            string truncated = text.Substring(0, MaxToolResultChars);
            string hint = $"\n\n[truncated: {toolName} result was {text.Length} chars, showing first {MaxToolResultChars}. " +
                          "Use a more specific query or add filters to reduce results.]";
            _sink?.Emit(CopilotEvent.Notice($"Token 预算: {toolName} 结果已截断 {text.Length}→{MaxToolResultChars} 字符"));
            return truncated + hint;
        }

        // ═══════════════════════════════════════════════════════════
        //  Zone 推导 — 从元素路径提取 zone 名称
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 从元素路径推导 zone 名称。
        /// E3D 路径格式: /ZONE/SUBZONE/.../ELEMENT
        /// 返回第一级 zone，如 /MDS/FRAMES/VT18 → /MDS
        /// </summary>
        private static string DeriveZoneFromElement(string elementName)
        {
            if (string.IsNullOrWhiteSpace(elementName)) return null;
            // /MDS/FRAMES/VT18 → segments = ["", "MDS", "FRAMES", "VT18"]
            var parts = elementName.Split('/');
            if (parts.Length >= 3 && !string.IsNullOrEmpty(parts[1]))
            {
                return "/" + parts[1]; // /MDS
            }
            return elementName; // 太短就原样返回
        }
    }
}
