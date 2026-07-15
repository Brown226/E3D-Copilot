using System;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Agents;
using E3DCopilot.Core.Logging;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// dispatch_subagent — 派发只读子代理并行执行
    ///
    /// 元能力工具：
    /// 主 Agent 在遇到可并行的只读查询任务时，通过此工具派发多个只读子代理。
    /// 每个子代理拥有独立的 AgentLoop + CopilotSession，独立执行后回传结果。
    ///
    /// 对齐 Reasonix task → SubagentSpec + SubagentRun
    /// </summary>
    public class SubagentDispatchHandler : IToolHandler
    {
        private readonly SubagentRunner _runner;

        public SubagentDispatchHandler(SubagentRunner runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public string Name => "dispatch_subagent";

        public string Description =>
            "Dispatch a read-only sub-agent to perform a specific task in isolation. " +
            "The sub-agent runs with its own context and can only use read-only tools. " +
            "Use this for parallel read-only queries that are independent of each other. " +
            "Example: inspecting multiple element types simultaneously. " +
            "The result is returned as text for you to summarize.";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""name"": {
      ""type"": ""string"",
      ""description"": ""Unique name for this sub-agent, e.g. inspector-pipe. 子代理唯一名称""
    },
    ""system_prompt"": {
      ""type"": ""string"",
      ""description"": ""Optional system prompt to specialize the sub-agent's behavior. 专长提示词（可选）""
    },
    ""task"": {
      ""type"": ""string"",
      ""description"": ""The task description to give to the sub-agent. 子代理要执行的任务""
    },
    ""mode"": {
      ""type"": ""string"",
      ""enum"": [""executor"", ""planner""],
      ""description"": ""Sub-agent mode: executor (run task) or planner (read-only plan). C2 双模型""
    },
    ""readonly"": {
      ""type"": ""boolean"",
      ""description"": ""Whether the sub-agent is read-only (default: true). Set false to allow write operations — writes still go through the main Agent's approval flow. 安全受限：缺省只读"",
      ""default"": true
    },
    ""provider"": {
      ""type"": ""string"",
      ""description"": ""Optional provider/model ref (e.g. 'local/Qwen3.5-32B') for handoff specialty switching. C2""
    }
  },
  ""required"": [""name"", ""task""]
}";

        /// <summary>派发本身是只读动作（不直接修改 E3D 数据）</summary>
        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var json = JObject.Parse(args);
                string name = json.Value<string>("name");
                string task = json.Value<string>("task");
                string systemPrompt = json.Value<string>("system_prompt");
                string mode = json.Value<string>("mode") ?? "executor";
                string providerRef = json.Value<string>("provider");
                bool isReadOnly = json.Value<bool?>("readonly") ?? true; // 缺省只读（安全优先）

                if (string.IsNullOrWhiteSpace(name))
                    return ToolResult.Fail("'name' is required");
                if (string.IsNullOrWhiteSpace(task))
                    return ToolResult.Fail("'task' is required");

                // 检查嵌套深度
                if (_runner.CurrentDepth >= SubagentRunner.MaxSubagentDepth)
                {
                    return ToolResult.Fail(
                        $"Sub-agent nesting depth limit ({SubagentRunner.MaxSubagentDepth}) exceeded. " +
                        "Cannot dispatch further sub-agents.");
                }

                // 创建子代理上下文（C2：双模型 / handoff）
                var ctx = new SubagentContext
                {
                    Name = name,
                    SystemPrompt = systemPrompt,
                    IsReadOnly = isReadOnly, // 由 readonly 参数控制，缺省只读
                    Session = new CopilotSession(),
                    Mode = mode == "planner" ? SubagentMode.Planner : SubagentMode.Executor,
                    PreferredProvider = providerRef
                };

                CopilotLogger.Info("SubagentDispatch: name='{0}', task='{1}', depth={2}",
                    name, task?.Substring(0, Math.Min(50, task?.Length ?? 0)), _runner.CurrentDepth);

                // 运行子代理
                var result = await _runner.RunAsync(ctx, task, ct);

                if (result.Success)
                {
                    CopilotLogger.Info("Subagent '{0}' completed successfully, output length={1}",
                        name, result.Output?.Length ?? 0);
                    return ToolResult.Ok(result.Output, new { agentName = name });
                }
                else
                {
                    return ToolResult.Fail($"Sub-agent '{name}' failed");
                }
            }
            catch (OperationCanceledException)
            {
                return ToolResult.Fail("Sub-agent execution was cancelled");
            }
            catch (Exception ex)
            {
                CopilotLogger.Warn("SubagentDispatch failed: {0}", ex.Message);
                return ToolResult.Fail($"SubagentDispatch failed: {ex.Message}");
            }
        }
    }
}
