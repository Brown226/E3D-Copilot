using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Logging;
using E3DCopilot.Core.Providers;
using E3DCopilot.Core.Security;
using E3DCopilot.Core.Tools;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Agents
{
    /// <summary>
    /// Planner-Executor 编排引擎
    /// 借鉴 Reasonix coordinator.go 的编排模式
    /// </summary>
    public class Orchestrator
    {
        private readonly ICopilotProvider _provider;
        private readonly IEventSink _sink;
        private readonly ToolExecutor _executor;
        private readonly CopilotConfig _config;
        private readonly CopilotController _controller;
        private readonly CommandPermissionController _permission;

        /// <summary>子代理运行器（复用 AgentLoop）</summary>
        private readonly SubagentRunner _subagentRunner;

        public Orchestrator(
            ICopilotProvider provider,
            IEventSink sink,
            ToolExecutor executor,
            CopilotConfig config,
            CopilotController controller,
            CommandPermissionController permission,
            SubagentRunner subagentRunner)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _sink = sink;
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _config = config ?? CopilotConfig.Load();
            _controller = controller;
            _permission = permission ?? CommandPermissionController.CreateDefault();
            _subagentRunner = subagentRunner ?? throw new ArgumentNullException(nameof(subagentRunner));
        }

        /// <summary>
        /// 执行编排流程：Plan → Execute → Summarize
        /// </summary>
        public async Task<string> OrchestrateAsync(string task, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            // Phase 1: Plan — Planner 子代理产出计划
            _sink?.Emit(CopilotEvent.Notice($"编排: 开始规划任务..."));
            var plan = await PlanAsync(task, ct);
            if (plan == null)
                return "编排失败: Planner 未能产出有效计划";

            // 验证计划
            var validationError = plan.Validate();
            if (validationError != null)
                return $"编排失败: 计划无效 — {validationError}";

            _sink?.Emit(CopilotEvent.Notice(
                $"编排: 计划 '{plan.Title}' 包含 {plan.Steps.Count} 个步骤"));

            // Phase 2: Execute — 按依赖和并行组执行
            var results = await ExecuteAsync(plan, ct);

            // Phase 3: Summarize — 汇总结果
            return SummarizeResults(plan, results);
        }

        /// <summary>
        /// Phase 1: 派发 Planner 子代理产出结构化计划
        /// </summary>
        private async Task<ExecutionPlan> PlanAsync(string task, CancellationToken ct)
        {
            var plannerCtx = new SubagentContext
            {
                Name = "planner",
                SystemPrompt = BuildPlannerPrompt(),
                IsReadOnly = true,
                Mode = SubagentMode.Planner,
                Session = new CopilotSession()
            };

            var result = await _subagentRunner.RunAsync(plannerCtx, task, ct);
            if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
            {
                CopilotLogger.Warn("Orchestrator.Plan: Planner 子代理失败");
                return null;
            }

            // 从 Planner 输出中提取 JSON
            try
            {
                string json = ExtractJson(result.Output);
                var plan = ExecutionPlan.FromJson(json);
                if (plan == null)
                {
                    CopilotLogger.Warn("Orchestrator.Plan: 无法解析计划 JSON");
                    return null;
                }
                return plan;
            }
            catch (Exception ex)
            {
                CopilotLogger.Warn("Orchestrator.Plan: 解析失败 — {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Phase 2: 按依赖图 + 并行组执行步骤
        /// </summary>
        private async Task<Dictionary<string, AgentResult>> ExecuteAsync(
            ExecutionPlan plan, CancellationToken ct)
        {
            var results = new Dictionary<string, AgentResult>();
            var completed = new HashSet<string>();

            // 按并行组分组
            var groups = plan.Steps.GroupBy(s => s.ParallelGroup)
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var group in groups)
            {
                ct.ThrowIfCancellationRequested();

                // 过滤：本组中依赖已满足的步骤
                var ready = group.Where(s =>
                    s.DependsOn == null || s.DependsOn.All(d => completed.Contains(d))
                ).ToList();

                if (ready.Count == 0) continue;

                // 同组并行执行
                var tasks = ready.Select(step => ExecuteStepAsync(step, ct));
                var stepResults = await Task.WhenAll(tasks);

                for (int i = 0; i < ready.Count; i++)
                {
                    results[ready[i].Id] = stepResults[i];
                    completed.Add(ready[i].Id);
                }
            }

            return results;
        }

        private async Task<AgentResult> ExecuteStepAsync(PlanStep step, CancellationToken ct)
        {
            _sink?.Emit(CopilotEvent.Notice($"编排: 执行步骤 '{step.Name}'..."));

            var ctx = new SubagentContext
            {
                Name = $"executor-{step.Id}",
                SystemPrompt = BuildExecutorPrompt(step),
                IsReadOnly = step.ReadOnly,
                Mode = SubagentMode.Executor,
                Session = new CopilotSession(),
                PreferredProvider = step.Provider
            };

            var result = await _subagentRunner.RunAsync(ctx, step.Task, ct);
            step.Result = result;
            return result;
        }

        /// <summary>
        /// Phase 3: 汇总所有步骤结果
        /// </summary>
        private static string SummarizeResults(ExecutionPlan plan, Dictionary<string, AgentResult> results)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"## 执行结果: {plan.Title}");
            sb.AppendLine();

            int successCount = 0;
            int failCount = 0;

            foreach (var step in plan.Steps)
            {
                results.TryGetValue(step.Id, out var result);
                if (result != null && result.Success)
                {
                    successCount++;
                    sb.AppendLine($"✅ **{step.Name}**: 完成");
                    if (!string.IsNullOrWhiteSpace(result.Output) && result.Output.Length <= 500)
                        sb.AppendLine($"   {result.Output}");
                }
                else
                {
                    failCount++;
                    sb.AppendLine($"❌ **{step.Name}**: 失败");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"**总计**: {successCount} 成功, {failCount} 失败");
            return sb.ToString();
        }

        /// <summary>
        /// 从 Planner 输出中提取 JSON 块
        /// </summary>
        private static string ExtractJson(string text)
        {
            // 尝试提取 ```json ... ``` 代码块
            int start = text.IndexOf("```json");
            if (start >= 0)
            {
                start = text.IndexOf('\n', start) + 1;
                int end = text.IndexOf("```", start);
                if (end > start)
                    return text.Substring(start, end - start).Trim();
            }
            // 回退：尝试找第一个 { 到最后一个 }
            int braceStart = text.IndexOf('{');
            int braceEnd = text.LastIndexOf('}');
            if (braceStart >= 0 && braceEnd > braceStart)
                return text.Substring(braceStart, braceEnd - braceStart + 1);
            return text;
        }

        private static string BuildPlannerPrompt()
        {
            return "You are a task planning specialist. " +
                "Analyze the user's request and produce a structured execution plan in JSON format. " +
                "Output ONLY valid JSON (no markdown, no extra text). " +
                "Format:\n" +
                "{\n" +
                "  \"title\": \"Plan title\",\n" +
                "  \"steps\": [\n" +
                "    {\n" +
                "      \"id\": \"step-1\",\n" +
                "      \"name\": \"Brief step name\",\n" +
                "      \"task\": \"Detailed task description for the executor\",\n" +
                "      \"depends_on\": [],\n" +
                "      \"parallel_group\": 0,\n" +
                "      \"readonly\": true\n" +
                "    }\n" +
                "  ]\n" +
                "}\n\n" +
                "Rules:\n" +
                "- Steps in the same parallel_group run concurrently.\n" +
                "- Use depends_on for sequential dependencies.\n" +
                "- readonly=true for steps that only query data.\n" +
                "- Keep each step focused and independently executable.";
        }

        private static string BuildExecutorPrompt(PlanStep step)
        {
            return $"You are executing a specific step of a larger plan. " +
                $"Your task is: {step.Task}\n\n" +
                "Complete ONLY this task. Use available tools to get results. " +
                "Report your findings concisely. Do not attempt other steps.";
        }

        /// <summary>
        /// 测试辅助方法 — 暴露 ExtractJson 给单元测试
        /// </summary>
        public static string ExtractJsonForTest(string text) => ExtractJson(text);
    }
}
