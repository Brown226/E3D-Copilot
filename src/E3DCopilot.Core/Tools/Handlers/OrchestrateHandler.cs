using System;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Agents;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// orchestrate_task — 高层编排入口
    ///
    /// LLM 通过此工具派发 Planner-Executor 编排流程：
    /// Planner 子代理分析任务 → 产出结构化计划 → 按依赖/并行执行 → 汇总结果
    ///
    /// E3：Planner-Executor 编排引擎入口
    /// </summary>
    public class OrchestrateHandler : IToolHandler
    {
        private readonly Orchestrator _orchestrator;

        public OrchestrateHandler(Orchestrator orchestrator)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        }

        public string Name => "orchestrate_task";

        public string Description =>
            "Break a complex task into a structured plan with parallel steps. " +
            "Use this when the task has multiple independent sub-tasks that can run concurrently, " +
            "or when some steps depend on others. " +
            "The system will: 1) create a plan, 2) execute steps in dependency order, 3) summarize results.";

        public string ParameterSchema =>
            @"{
  ""type"": ""object"",
  ""properties"": {
    ""task"": {
      ""type"": ""string"",
      ""description"": ""The complex task to break down and execute""
    }
  },
  ""required"": [""task""]
}";

        public bool IsReadOnly => false; // 编排可能包含写操作

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(args))
                return ToolResult.Fail("Missing task parameter");

            try
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(args);
                var task = json["task"]?.ToString();
                if (string.IsNullOrWhiteSpace(task))
                    return ToolResult.Fail("Missing 'task' in parameters");

                var result = await _orchestrator.OrchestrateAsync(task, ct);
                return new ToolResult { Success = true, Text = result };
            }
            catch (OperationCanceledException)
            {
                return ToolResult.Fail("编排已取消");
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"编排失败: {ex.Message}");
            }
        }
    }
}
