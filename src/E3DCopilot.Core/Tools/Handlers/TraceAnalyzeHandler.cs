using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// Trace 分析工具 — 把 ConversationTracer 落盘的执行轨迹读出来并做定量诊断。
    ///
    /// 为什么需要它：
    /// 大王现场反馈「执行不丝滑、总是卡顿」，但界面只能看到"正在回复"，
    /// 看不到时间到底花在等模型、等审批、还是等工具。凭感觉优化必然改错地方。
    ///
    /// 本工具把一次对话拆成可归因的耗时结构：
    ///   LLM 耗时 / TTFT / 审批等待 / 工具执行 / 上下文规模
    /// 并直接给出结论（比如"LLM 占 96%，瓶颈在模型端点"）。
    ///
    /// 操作：
    /// - list:   列出最近 N 个 trace 文件
    /// - analyze: 分析指定（或最新）trace，输出耗时归因与问题清单
    /// - raw:    读取原始 trace JSON
    /// </summary>
    public class TraceAnalyzeHandler : IToolHandler
    {
        private readonly Logging.ConversationTracer _tracer;

        public TraceAnalyzeHandler(Logging.ConversationTracer tracer)
        {
            _tracer = tracer;
        }

        public string Name => "trace_analyze";

        public string Description =>
            @"读取并诊断本机 E小智 的历史执行轨迹（trace），定位执行卡顿的真实原因。
每次对话结束会自动落盘一份 trace，包含：每步 LLM 思考内容、工具调用与参数、工具结果、
LLM 耗时、首 token 延迟(TTFT)、审批等待时长、上下文规模(tokens/工具数)。

操作：
- list:    列出最近的 trace 文件（时间、大小、结果）
- analyze: 分析并给出耗时归因结论（默认分析最新一条）
- raw:     读取原始 JSON

何时使用：用户抱怨卡顿/慢/没反应，或需要复盘某次执行为什么失败时。";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""action"": {
      ""type"": ""string"",
      ""enum"": [""list"", ""analyze"", ""raw""],
      ""description"": ""list=列出文件, analyze=分析诊断, raw=读取原始 JSON""
    },
    ""file"": {
      ""type"": ""string"",
      ""description"": ""[analyze/raw 可选] trace 文件名或完整路径，留空则用最新一条""
    },
    ""count"": {
      ""type"": ""integer"",
      ""description"": ""[list 可选] 列出条数，默认 10""
    }
  },
  ""required"": [""action""]
}";

        public bool IsReadOnly => true;

        public Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            JObject a;
            try { a = JObject.Parse(string.IsNullOrWhiteSpace(args) ? "{}" : args); }
            catch (Exception ex) { return Task.FromResult(ToolResult.Fail($"参数解析失败: {ex.Message}")); }

            string action = (string)a["action"] ?? "analyze";
            string file = (string)a["file"];
            int count = a["count"] != null ? (int)a["count"] : 10;

            if (_tracer == null || !_tracer.IsEnabled)
                return Task.FromResult(ToolResult.Fail("trace 记录器未启用（检查配置 Logging.TraceEnabled）"));

            try
            {
                switch (action.ToLowerInvariant())
                {
                    case "list": return Task.FromResult(List(count));
                    case "raw": return Task.FromResult(Raw(file));
                    default: return Task.FromResult(Analyze(file));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResult.Fail($"trace 分析失败: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  list
        // ═══════════════════════════════════════════════════════════

        private ToolResult List(int count)
        {
            var items = _tracer.ListTraces(count);
            if (items.Count == 0)
                return ToolResult.Ok($"trace 目录为空：{_tracer.TraceDir}\n先进行一次对话后即可生成。");

            var sb = new StringBuilder();
            sb.AppendLine($"trace 目录：{_tracer.TraceDir}");
            sb.AppendLine($"最近 {items.Count} 条：");
            sb.AppendLine();
            foreach (var it in items)
            {
                sb.AppendLine($"{it.FileName}  {it.SizeBytes / 1024.0:F1}KB  {it.LastModified:yyyy-MM-dd HH:mm:ss}");
            }
            return ToolResult.Ok(sb.ToString());
        }

        // ═══════════════════════════════════════════════════════════
        //  raw
        // ═══════════════════════════════════════════════════════════

        private ToolResult Raw(string file)
        {
            string path = ResolvePath(file);
            if (path == null) return ToolResult.Fail("未找到 trace 文件");
            string json = _tracer.ReadTrace(path);
            if (json == null) return ToolResult.Fail($"读取失败: {path}");
            return ToolResult.Ok(json);
        }

        // ═══════════════════════════════════════════════════════════
        //  analyze — 核心：耗时归因
        // ═══════════════════════════════════════════════════════════

        private ToolResult Analyze(string file)
        {
            string path = ResolvePath(file);
            if (path == null) return ToolResult.Fail("未找到 trace 文件");

            string json = _tracer.ReadTrace(path);
            if (json == null) return ToolResult.Fail($"读取失败: {path}");

            var root = JObject.Parse(json);
            var steps = root["steps"] as JArray ?? new JArray();

            long totalMs = 0, llmMs = 0, approvalMs = 0, toolMs = 0;
            int toolCount = 0, failedToolCount = 0;
            long ttftSum = 0; int ttftCount = 0;
            var findings = new List<string>();
            var perStep = new List<string>();

            foreach (var s in steps)
            {
                long dur = (long?)s["durationMs"] ?? 0;
                long sLlm = (long?)s["llmMs"] ?? 0;
                long sAppr = (long?)s["approvalMs"] ?? 0;
                long ttft = (long?)s["ttftMs"] ?? -1;

                var tools = s["toolCalls"] as JArray ?? new JArray();
                long sTool = 0;
                foreach (var t in tools)
                {
                    toolCount++;
                    sTool += (long?)t["durationMs"] ?? 0;
                    if (t["success"] != null && !(bool)t["success"]) failedToolCount++;
                }

                // 若旧版 trace 没有 llmMs 字段，用 duration - 工具耗时 - 审批耗时推算
                long derivedLlm = sLlm > 0 ? sLlm : Math.Max(0, dur - sTool - sAppr);

                totalMs += dur;
                llmMs += derivedLlm;
                approvalMs += sAppr;
                toolMs += sTool;
                if (ttft >= 0) { ttftSum += ttft; ttftCount++; }

                var ctx = s["context"];
                string ctxInfo = ctx != null
                    ? $" | 上下文 {ctx["messages"]}条消息 / {ctx["tools"]}工具 / 工具schema {ctx["toolSchemaChars"]}字符 / 消息 {ctx["messageChars"]}字符"
                    : "";

                var st = s["stream"];
                string streamInfo = "";
                if (st != null && ((int?)st["chunks"] ?? 0) > 0)
                {
                    streamInfo = $" | 流式 {st["chunks"]}块 中位间隔 {st["medianGapMs"]}ms"
                        + $" 最长 {st["maxGapMs"]}ms 卡顿 {st["stalls"]}次"
                        + $" 速率 {st["charsPerSec"]}字符/秒";
                }

                perStep.Add($"  步骤 {s["step"]}: 总 {dur}ms = LLM {derivedLlm}ms"
                    + (sAppr > 0 ? $" + 审批 {sAppr}ms" : "")
                    + $" + 工具 {sTool}ms | {tools.Count} 个工具"
                    + (ttft >= 0 ? $" | 首token {ttft}ms" : "")
                    + streamInfo
                    + ctxInfo);
            }

            bool hasNewFields = steps.Count > 0 && ((JObject)steps[0])["llmMs"] != null;

            // ── 诊断结论 ──
            if (totalMs > 0)
            {
                double llmPct = llmMs * 100.0 / totalMs;
                double toolPct = toolMs * 100.0 / totalMs;
                double apprPct = approvalMs * 100.0 / totalMs;

                if (llmPct >= 70)
                    findings.Add($"LLM 调用占 {llmPct:F0}% 总耗时 —— 瓶颈在模型端点（生成速度或排队），不在工具执行");
                else if (toolPct >= 50)
                    findings.Add($"工具执行占 {toolPct:F0}% 总耗时 —— 瓶颈在具体工具实现");
                else if (apprPct >= 30)
                    findings.Add($"审批等待占 {apprPct:F0}% 总耗时 —— 审批环节拖累了流程");
            }

            if (ttftCount > 0)
            {
                double avgTtft = (double)ttftSum / ttftCount;
                if (avgTtft > 10000)
                    findings.Add($"平均首 token 延迟 {avgTtft / 1000:F1}s —— 模型排队严重或上下文过大");
                else if (avgTtft > 3000)
                    findings.Add($"平均首 token 延迟 {avgTtft / 1000:F1}s —— 偏高，建议精简上下文");
            }

            // 上下文膨胀检测（看第一步 vs 最后一步）
            if (steps.Count >= 2)
            {
                var c0 = steps[0]["context"];
                var cl = steps[steps.Count - 1]["context"];
                if (c0 != null && cl != null)
                {
                    int m0 = (int?)c0["messages"] ?? 0;
                    int ml = (int?)cl["messages"] ?? 0;
                    int sch = (int?)cl["toolSchemaChars"] ?? 0;
                    if (ml > m0 * 2 && ml > 10)
                        findings.Add($"上下文从 {m0} 条膨胀到 {ml} 条 —— 每轮重发全部历史，越跑越慢");
                    if (sch > 20000)
                        findings.Add($"工具 schema 单次占用 {sch} 字符（约 {sch / 2.5:F0} tokens）—— 工具描述过重，每轮都在重复付费");
                }
            }

            if (failedToolCount > 0)
                findings.Add($"{failedToolCount} 次工具失败（共 {toolCount} 次调用）—— 检查失败工具的参数与可用性");

            // 单次工具调用耗时过长（往往是全盘扫描/无边界遍历）
            {
                long worstMs = 0; string worstName = null;
                foreach (var s in steps)
                {
                    var tools = s["toolCalls"] as JArray;
                    if (tools == null) continue;
                    foreach (var t in tools)
                    {
                        long d = (long?)t["durationMs"] ?? 0;
                        if (d > worstMs) { worstMs = d; worstName = (string)t["name"]; }
                    }
                }
                if (worstMs >= 10000)
                    findings.Add($"单次工具 {worstName} 耗时 {worstMs / 1000.0:F1}s —— 存在无边界遍历或缺少超时，检查该工具的搜索范围");
            }

            // ── 流式节奏：区分「模型慢」和「界面卡」 ──
            // 总耗时高 + 输出速率也高 + 但卡顿多 => 模型不慢，是转发/渲染被阻塞
            {
                var streamSteps = new List<JToken>();
                foreach (var s in steps)
                {
                    var st = s["stream"];
                    if (st != null && ((int?)st["chunks"] ?? 0) > 0) streamSteps.Add(st);
                }

                if (streamSteps.Count > 0)
                {
                    int totalStalls = 0;
                    long worstGap = 0;
                    double totalCps = 0;
                    int cpsCount = 0;

                    foreach (var st in streamSteps)
                    {
                        totalStalls += (int?)st["stalls"] ?? 0;
                        long g = (long?)st["maxGapMs"] ?? 0;
                        if (g > worstGap) worstGap = g;
                        double cps = (double?)st["charsPerSec"] ?? 0;
                        if (cps > 0) { totalCps += cps; cpsCount++; }
                    }

                    double avgCps = cpsCount > 0 ? totalCps / cpsCount : 0;

                    if (worstGap >= 2000)
                        findings.Add($"流式输出最长中断 {worstGap / 1000.0:F1}s —— 输出中途卡住，不是模型产不出，而是转发/渲染被阻塞");
                    else if (totalStalls >= 5)
                        findings.Add($"流式输出卡顿 {totalStalls} 次（间隔>300ms），最长 {worstGap}ms —— 界面存在逐块阻塞，检查事件封送与渲染路径");

                    if (avgCps >= 20 && totalStalls >= 5)
                        findings.Add($"平均输出速率 {avgCps:F0} 字符/秒并不慢，但伴随 {totalStalls} 次卡顿 —— 端点是健康的，问题在 UI 线程封送");
                    else if (avgCps > 0 && avgCps < 5 && totalStalls < 5)
                        findings.Add($"平均输出速率仅 {avgCps:F1} 字符/秒且无明显卡顿 —— 瓶颈确实是模型端点生成速度");

                    // 假流式检测：所有增量集中在极短时间内到达，而前面有很长的静默期。
                    // 这是"点了发送后界面长时间没反应、然后整段文字瞬间出现"的典型特征。
                    // 此时优化前端或 UI 封送都无济于事——首个字什么时候出现完全由端点决定。
                    var firstStream = streamSteps[0];
                    long spanMs0 = (long?)firstStream["spanMs"] ?? 0;
                    int chunks0 = (int?)firstStream["chunks"] ?? 0;
                    long firstTtft = 0;
                    foreach (var s in steps)
                    {
                        long tt = (long?)s["ttftMs"] ?? -1;
                        if (tt > 0) { firstTtft = tt; break; }
                    }

                    if (chunks0 >= 50 && spanMs0 <= 100 && firstTtft >= 3000)
                    {
                        findings.Add(
                            $"假流式：{chunks0} 个增量全部在 {spanMs0}ms 内到达，但首块延迟 {firstTtft}ms —— " +
                            "服务端先整体生成再一次性推流。界面会先静默数秒、然后整段文字瞬间出现；" +
                            "此现象与前端/UI 封送无关，只能换真正逐字输出的端点");
                    }
                }
            }

            var outcome = (string)root["outcome"];
            if (outcome == "cancelled")
                findings.Add("本轮被取消（用户中断或审批超时未响应）");
            else if (outcome == "max_steps")
                findings.Add("达到最大步数上限 —— 任务未完成，可能需要拆解");

            var errors = root["errors"] as JArray;
            if (errors != null && errors.Count > 0)
            {
                foreach (var e in errors.Take(3))
                    findings.Add($"错误: {e}");
            }

            // ── 输出 ──
            var sb = new StringBuilder();
            sb.AppendLine($"=== Trace 分析：{Path.GetFileName(path)} ===");
            sb.AppendLine($"时间: {root["startTime"]}  →  {root["endTime"]}");
            sb.AppendLine($"模型: {root["model"]}");
            sb.AppendLine($"用户输入: {root["userInput"]}");
            sb.AppendLine($"结果: {outcome} | 步骤 {steps.Count} | 工具调用 {toolCount}");
            sb.AppendLine($"总 Token: prompt {(int?)root.SelectToken("totalTokens.prompt") ?? 0}"
                + $" + completion {(int?)root.SelectToken("totalTokens.completion") ?? 0}");
            sb.AppendLine();

            sb.AppendLine("--- 耗时归因 ---");
            sb.AppendLine($"  总耗时      {totalMs,8} ms");
            sb.AppendLine($"  LLM 调用    {llmMs,8} ms  ({(totalMs > 0 ? llmMs * 100.0 / totalMs : 0):F0}%)");
            if (approvalMs > 0)
                sb.AppendLine($"  审批等待    {approvalMs,8} ms  ({(totalMs > 0 ? approvalMs * 100.0 / totalMs : 0):F0}%)");
            sb.AppendLine($"  工具执行    {toolMs,8} ms  ({(totalMs > 0 ? toolMs * 100.0 / totalMs : 0):F0}%)");
            sb.AppendLine();

            sb.AppendLine("--- 分步明细 ---");
            foreach (var line in perStep) sb.AppendLine(line);
            sb.AppendLine();

            sb.AppendLine("--- 诊断结论 ---");
            if (findings.Count == 0)
                sb.AppendLine("  未发现明显问题。");
            else
                foreach (var f in findings) sb.AppendLine($"  • {f}");

            if (!hasNewFields)
            {
                sb.AppendLine();
                sb.AppendLine("注：这条 trace 由旧版本生成，缺少 llmMs/ttftMs/context 字段，");
                sb.AppendLine("    耗时按「总时长 - 工具耗时」推算，精度有限。新对话会记录完整数据。");
            }

            sb.AppendLine();
            sb.AppendLine($"原始文件: {path}");

            return ToolResult.Ok(sb.ToString());
        }

        private string ResolvePath(string file)
        {
            if (!string.IsNullOrWhiteSpace(file))
            {
                if (File.Exists(file)) return file;
                string candidate = Path.Combine(_tracer.TraceDir, file);
                if (File.Exists(candidate)) return candidate;
                return null;
            }
            return _tracer.GetLatestTracePath();
        }
    }
}
