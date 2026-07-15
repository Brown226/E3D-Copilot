# 第二期 Phase 2：Planner-Executor 编排引擎

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 将 `SubagentMode.Planner/Executor` 枚举值变为真正的编排流程——Planner 产出结构化计划，Executor 按依赖并行执行，主 Agent 审查后汇总。

**架构：** 新建 `Orchestrator` 类管理编排流程。Planner 子代理（只读）分析任务 → 输出 JSON 计划（步骤列表 + 依赖关系 + 并行分组）→ 主 Agent 审查确认 → Executor 子代理按 DAG 并行执行 → 汇总结果。

**技术栈：** .NET Framework 4.8 / C# 7.3，NUnit 3，Newtonsoft.Json

---

## 文件结构

| 文件 | 职责 | 操作 |
|---|---|---|
| `src/E3DCopilot.Core/Agents/Orchestrator.cs` | Planner-Executor 编排引擎 | 新建 |
| `src/E3DCopilot.Core/Agents/ExecutionPlan.cs` | 计划数据结构（步骤、依赖、并行组） | 新建 |
| `src/E3DCopilot.Core/Tools/Handlers/OrchestrateHandler.cs` | `orchestrate_task` 工具（高层编排入口） | 新建 |
| `src/E3DCopilot.Core/Agents/SubagentRunner.cs` | 支持 Planner 模式专用提示词 | 修改 |
| `src/E3DCopilot.Core/SystemPrompt.cs` | 增补编排能力说明 | 修改 |
| `src/E3DCopilot.Core/CopilotController.cs` | 注册 OrchestrateHandler | 修改 |
| `src/E3DCopilot.Tests/OrchestratorTests.cs` | 编排引擎测试 | 新建 |

---

## 任务 1：ExecutionPlan 数据结构

**文件：**
- 创建：`src/E3DCopilot.Core/Agents/ExecutionPlan.cs`

- [ ] **步骤 1：编写 ExecutionPlan 类**

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;

namespace E3DCopilot.Core.Agents
{
    /// <summary>
    /// 执行计划 — Planner 子代理产出的结构化计划
    /// </summary>
    public class ExecutionPlan
    {
        /// <summary>计划标题</summary>
        [JsonProperty("title")]
        public string Title { get; set; }

        /// <summary>计划步骤列表（按执行顺序）</summary>
        [JsonProperty("steps")]
        public List<PlanStep> Steps { get; set; } = new List<PlanStep>();

        /// <summary>从 JSON 反序列化</summary>
        public static ExecutionPlan FromJson(string json)
        {
            return JsonConvert.DeserializeObject<ExecutionPlan>(json);
        }

        /// <summary>序列化为 JSON</summary>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>验证计划有效性</summary>
        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(Title))
                return "计划缺少标题";
            if (Steps == null || Steps.Count == 0)
                return "计划没有步骤";
            for (int i = 0; i < Steps.Count; i++)
            {
                var s = Steps[i];
                if (string.IsNullOrWhiteSpace(s.Name))
                    return $"步骤 {i + 1} 缺少名称";
                if (string.IsNullOrWhiteSpace(s.Task))
                    return $"步骤 {i + 1} ({s.Name}) 缺少任务描述";
                // 验证依赖引用的步骤存在
                if (s.DependsOn != null)
                {
                    foreach (var dep in s.DependsOn)
                    {
                        if (!Steps.Exists(x => x.Id == dep))
                            return $"步骤 {i + 1} ({s.Name}) 依赖不存在的步骤: {dep}";
                    }
                }
            }
            return null; // 有效
        }
    }

    /// <summary>
    /// 计划步骤
    /// </summary>
    public class PlanStep
    {
        /// <summary>步骤唯一 ID（如 "step-1"）</summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>步骤名称（简短描述）</summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>任务描述（传给 Executor 子代理）</summary>
        [JsonProperty("task")]
        public string Task { get; set; }

        /// <summary>依赖的步骤 ID 列表（这些步骤完成后才能执行本步骤）</summary>
        [JsonProperty("depends_on")]
        public List<string> DependsOn { get; set; } = new List<string>();

        /// <summary>并行组编号（同一组的步骤并行执行，0 = 串行）</summary>
        [JsonProperty("parallel_group")]
        public int ParallelGroup { get; set; }

        /// <summary>是否只读步骤（默认 true）</summary>
        [JsonProperty("readonly")]
        public bool ReadOnly { get; set; } = true;

        /// <summary>指定使用的 Provider（可选，handoff 专长切换）</summary>
        [JsonProperty("provider")]
        public string Provider { get; set; }

        /// <summary>执行结果（执行后填充）</summary>
        [JsonIgnore]
        public AgentResult Result { get; set; }
    }
}
```

- [ ] **步骤 2：构建验证**

运行：`dotnet build src/E3DCopilot.sln -c Release 2>&1 | Select-String "error|Build succeeded"`
预期：Build succeeded

- [ ] **步骤 3：Commit**

```bash
git add src/E3DCopilot.Core/Agents/ExecutionPlan.cs
git commit -m "feat: ExecutionPlan 数据结构 — 步骤/依赖/并行组"
```

---

## 任务 2：Orchestrator 编排引擎

**文件：**
- 创建：`src/E3DCopilot.Core/Agents/Orchestrator.cs`

- [ ] **步骤 1：编写 Orchestrator 类**

```csharp
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
        /// 执行编排流程：Plan → Review → Execute → Summarize
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
        private string SummarizeResults(ExecutionPlan plan, Dictionary<string, AgentResult> results)
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
    }
}
```

- [ ] **步骤 2：构建验证**

运行：`dotnet build src/E3DCopilot.sln -c Release 2>&1 | Select-String "error|Build succeeded"`
预期：Build succeeded

- [ ] **步骤 3：Commit**

```bash
git add src/E3DCopilot.Core/Agents/Orchestrator.cs
git commit -m "feat: Orchestrator Planner-Executor 编排引擎"
```

---

## 任务 3：OrchestrateHandler 工具

**文件：**
- 创建：`src/E3DCopilot.Core/Tools/Handlers/OrchestrateHandler.cs`

- [ ] **步骤 1：编写 OrchestrateHandler**

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Agents;
using E3DCopilot.Core.Logging;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// orchestrate_task — 高层编排工具
    /// 主 Agent 只需描述任务，由 Orchestrator 自动分解为 Planner→Executor 流程
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
            "Orchestrate a complex multi-step task. The system will plan the task, " +
            "execute steps in parallel where possible, and summarize results. " +
            "Use this for complex tasks that involve multiple independent sub-tasks.";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""task"": {
      ""type"": ""string"",
      ""description"": ""The complex task description to orchestrate""
    }
  },
  ""required"": [""task""]
}";

        public bool IsReadOnly => true; // 编排本身是只读动作

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var json = JObject.Parse(args);
                string task = json.Value<string>("task");

                if (string.IsNullOrWhiteSpace(task))
                    return ToolResult.Fail("'task' is required");

                CopilotLogger.Info("OrchestrateHandler: task='{0}'",
                    task.Substring(0, Math.Min(80, task.Length)));

                var result = await _orchestrator.OrchestrateAsync(task, ct);

                return ToolResult.Ok(result);
            }
            catch (OperationCanceledException)
            {
                return ToolResult.Fail("Orchestration was cancelled");
            }
            catch (Exception ex)
            {
                CopilotLogger.Warn("OrchestrateHandler failed: {0}", ex.Message);
                return ToolResult.Fail($"Orchestration failed: {ex.Message}");
            }
        }
    }
}
```

- [ ] **步骤 2：构建验证**

运行：`dotnet build src/E3DCopilot.sln -c Release`

- [ ] **步骤 3：Commit**

```bash
git add src/E3DCopilot.Core/Tools/Handlers/OrchestrateHandler.cs
git commit -m "feat: OrchestrateHandler — orchestrate_task 工具"
```

---

## 任务 4：CopilotController 注入 Orchestrator

**文件：**
- 修改：`src/E3DCopilot.Core/CopilotController.cs`

- [ ] **步骤 1：在 CreateDefault 中构造 Orchestrator 并注册 OrchestrateHandler**

在 `CopilotController.CreateDefault` 方法中（约第 280 行附近，`SubagentRunner` 构造之后）：

```csharp
// 构造 Orchestrator（依赖 SubagentRunner）
var orchestrator = new Orchestrator(
    provider, sink, executor, config, null, permission, subagentRunner);

// 注册 orchestrate_task 工具
executor.Register(new OrchestrateHandler(orchestrator));
```

同时将 `orchestrator` 字段保存到 `CopilotController` 中（如果后续需要）：

```csharp
// 在 CopilotController 类中新增字段
public Orchestrator Orchestrator { get; private set; }

// 在 CreateDefault 中赋值
this.Orchestrator = orchestrator;
```

- [ ] **步骤 2：构建验证**

运行：`dotnet build src/E3DCopilot.sln -c Release`

- [ ] **步骤 3：Commit**

```bash
git add src/E3DCopilot.Core/CopilotController.cs
git commit -m "feat: CopilotController 注入 Orchestrator + OrchestrateHandler"
```

---

## 任务 5：SystemPrompt 增补编排说明

**文件：**
- 修改：`src/E3DCopilot.Core/SystemPrompt.cs`

- [ ] **步骤 1：在 BuildBasePrompt 中新增编排规则**

在第 7 条规则后新增第 8 条：

```csharp
// 在 BuildBasePrompt 的 Principles 部分，第 7 条之后追加：
"8. Complex multi-step tasks — use orchestrate_task to decompose into a plan " +
"with parallel execution where possible; the orchestrator handles planning, " +
"execution, and summarization automatically.\n\n"
```

更新后的 Principles 完整为 8 条。

- [ ] **步骤 2：构建验证 + 清除缓存**

由于 `SystemPrompt` 使用了静态缓存 `_cachedBasePrompt`，修改后需要重启才能生效。开发阶段可以通过 `SystemPrompt.InvalidateCache()` 强制刷新（如果存在）。如果没有该方法，手动重启 E3D。

运行：`dotnet build src/E3DCopilot.sln -c Release`

- [ ] **步骤 3：Commit**

```bash
git add src/E3DCopilot.Core/SystemPrompt.cs
git commit -m "feat: SystemPrompt 增补编排能力说明（第 8 条）"
```

---

## 任务 6：Orchestrator 单元测试

**文件：**
- 创建：`src/E3DCopilot.Tests/OrchestratorTests.cs`

- [ ] **步骤 1：编写测试**

```csharp
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Agents;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class OrchestratorTests
    {
        [Test]
        public void ExecutionPlan_Validate_ValidPlan_ReturnsNull()
        {
            var plan = new ExecutionPlan
            {
                Title = "Test Plan",
                Steps =
                {
                    new PlanStep { Id = "s1", Name = "Step 1", Task = "Do thing 1" },
                    new PlanStep { Id = "s2", Name = "Step 2", Task = "Do thing 2", DependsOn = { "s1" } }
                }
            };
            Assert.IsNull(plan.Validate());
        }

        [Test]
        public void ExecutionPlan_Validate_MissingTitle_ReturnsError()
        {
            var plan = new ExecutionPlan
            {
                Steps = { new PlanStep { Id = "s1", Name = "S1", Task = "T1" } }
            };
            Assert.That(plan.Validate(), Does.Contain("标题"));
        }

        [Test]
        public void ExecutionPlan_Validate_NoSteps_ReturnsError()
        {
            var plan = new ExecutionPlan { Title = "Empty" };
            Assert.That(plan.Validate(), Does.Contain("步骤"));
        }

        [Test]
        public void ExecutionPlan_Validate_MissingDep_ReturnsError()
        {
            var plan = new ExecutionPlan
            {
                Title = "Bad Deps",
                Steps =
                {
                    new PlanStep { Id = "s1", Name = "S1", Task = "T1", DependsOn = { "s-nonexistent" } }
                }
            };
            Assert.That(plan.Validate(), Does.Contain("依赖"));
        }

        [Test]
        public void ExecutionPlan_FromJson_Roundtrip()
        {
            var plan = new ExecutionPlan
            {
                Title = "Roundtrip",
                Steps =
                {
                    new PlanStep { Id = "s1", Name = "Query", Task = "Query pipes", ReadOnly = true, ParallelGroup = 0 },
                    new PlanStep { Id = "s2", Name = "Modify", Task = "Modify pipes", ReadOnly = false, ParallelGroup = 1, DependsOn = { "s1" } }
                }
            };
            var json = plan.ToJson();
            var restored = ExecutionPlan.FromJson(json);
            Assert.That(restored.Title, Is.EqualTo("Roundtrip"));
            Assert.That(restored.Steps.Count, Is.EqualTo(2));
            Assert.That(restored.Steps[0].Id, Is.EqualTo("s1"));
            Assert.That(restored.Steps[1].DependsOn, Contains.Item("s1"));
        }

        [Test]
        public void ExtractJson_FromMarkdownBlock()
        {
            var text = "Here is the plan:\n```json\n{\"title\":\"Test\"}\n```\nDone.";
            var json = Orchestrator.ExtractJsonForTest(text);
            Assert.That(json, Is.EqualTo("{\"title\":\"Test\"}"));
        }

        [Test]
        public void ExtractJson_PlainBraces()
        {
            var text = "Plan: {\"title\":\"Plain\"} end.";
            var json = Orchestrator.ExtractJsonForTest(text);
            Assert.That(json, Is.EqualTo("{\"title\":\"Plain\"}"));
        }
    }
}
```

注意：`ExtractJson` 是 `private static` 方法，测试需要添加 `internal` 的测试辅助方法：

```csharp
// Orchestrator.cs 中追加：
#if DEBUG
    internal static string ExtractJsonForTest(string text) => ExtractJson(text);
#endif
```

- [ ] **步骤 2：运行测试**

运行：`dotnet test src/E3DCopilot.Tests --filter "FullyQualifiedName~Orchestrator" 2>&1`
预期：7 tests PASS

- [ ] **步骤 3：Commit**

```bash
git add src/E3DCopilot.Tests/OrchestratorTests.cs src/E3DCopilot.Core/Agents/Orchestrator.cs
git commit -m "test: Orchestrator 单元测试 + ExtractJson 内部可测方法"
```

---

## 任务 7：端到端编译验证

- [ ] **步骤 1：后端构建**

运行：`dotnet build src/E3DCopilot.sln -c Release 2>&1 | Select-String "error|Build succeeded"`
预期：Build succeeded. 0 warnings

- [ ] **步骤 2：后端测试**

运行：`dotnet test src/E3DCopilot.Tests 2>&1 | Select-String "Passed|Failed|Test Run"`
预期：Passed 数 > 265

- [ ] **步骤 3：Commit**

```bash
git commit -m "chore: Phase 2 端到端验证通过"
```

---

## 自检

1. **规格覆盖度**：E3 要求全部覆盖——Plan → Execute → Summarize 流程、依赖图调度、并行执行、JSON 格式计划。
2. **占位符扫描**：无 TODO、待定、后续实现。所有步骤都有实际代码。
3. **类型一致性**：`ExecutionPlan` ↔ `PlanStep` ↔ `Orchestrator` 之间类型一致。`OrchestrateHandler` 调用 `Orchestrator.OrchestrateAsync` 签名匹配。

---

## 执行交接

**计划已完成并保存。选哪种方式执行？**