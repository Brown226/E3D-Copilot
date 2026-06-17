# E3D-E小智 路径 A 保守修复 — 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 2-3 周内打通"查询 DN100 管道属性"端到端闭环 — 用户输入自然语言 → AI 流式回复 → 调用 query 工具 → 真实 E3D API 返回数据 → UI 展示结果。

**Architecture:** 保持现有 Core → Tools → WebHost 三层架构不变。核心改动：(1) 创建 `E3DToolDispatcher` 实现 `IToolDispatcher` 接口，桥接 Core 与 Tools；(2) 替换 `CsharpBridge` 的 mock 实现为真实 E3D API 调用；(3) 精简 web-ui 到 15 个核心组件。

**Tech Stack:** C# / .NET Framework 4.8 / WinForms / WebView2 / React 18 / vLLM + Qwen (OpenAI 兼容 API)

## Global Constraints

- **目标框架**: .NET Framework 4.8 (net48)，禁止使用 .NET Core+ 特性
- **C# 版本**: 7.3，禁止 `IAsyncEnumerable` / `await foreach` / record / target-typed `new()`
- **E3D API 调用**: 必须通过 `ThreadMarshaller.Invoke()` 切换到 UI 线程
- **PML 执行**: 统一走 `Command.CreateCommand(str).RunInPdms()`（命名空间 `Aveva.Core.Utilities.CommandLine`）
- **属性读取**: `DbElement.GetAsString(DbAttribute.GetDbAttribute("NAME"))`，禁止 `GetAttribute("NAME").ToString()`
- **流式处理**: 用 `Task` + `Action<Chunk>` 回调，不用 `IAsyncEnumerable`

---

## 文件结构总览

### 新建文件

| 文件路径 | 职责 |
|----------|------|
| `src/E3DCopilot.Tools/Bridge/E3DToolDispatcher.cs` | 实现 `IToolDispatcher`，桥接 Core 与 E3D API |
| `src/E3DCopilot.Tools/Bridge/IE3DEnvironment.cs` | E3D 环境抽象接口（可测试） |
| `src/E3DCopilot.Tools/Bridge/RealE3DEnvironment.cs` | 真实 E3D 环境实现 |
| `src/E3DCopilot.Tools/Bridge/SimulatedE3DEnvironment.cs` | 模拟 E3D 环境（TestHost 用） |
| `src/E3DCopilot.Tests/E3DCopilot.Tests.csproj` | 单元测试项目 |
| `src/E3DCopilot.Tests/ToolExecutorTests.cs` | ToolExecutor 单元测试 |
| `src/E3DCopilot.Tests/DbQueryHandlerTests.cs` | DbQueryHandler 单元测试 |
| `src/E3DCopilot.Tests/E3DToolDispatcherTests.cs` | E3DToolDispatcher 单元测试 |
| `deploy.cmd` | 部署脚本 |

### 修改文件

| 文件路径 | 改动内容 |
|----------|----------|
| `src/E3DCopilot.Tools/Bridge/CsharpBridge.cs` | 替换 mock 为真实 E3D API 调用 |
| `src/E3DCopilot.Core/Tools/Handlers/DbQueryHandler.cs` | 增强查询逻辑，支持属性返回 |
| `src/E3DCopilot.WebHost/Bridge.cs` | 修复 `HandleUserMessage` 中的模拟响应 |
| `src/E3DCopilot.WebHost/CopilotAddinBoot.cs` | 注册 E3DToolDispatcher |
| `src/E3DCopilot.TestHost/MainForm.cs` | 注册 SimulatedE3DEnvironment |
| `web-ui/src/App.tsx` | 精简路由，删除不需要的视图 |
| `web-ui/src/components/` | 删除不需要的组件目录 |
| `docs/verification/Phase1a-开发报告.md` ~ `Phase3-开发报告.md` | 标注真实状态 |

---

## Task 1: 更新 Phase 报告，标注真实状态

**Files:**
- Modify: `docs/verification/Phase1a-开发报告.md`
- Modify: `docs/verification/Phase1b-开发报告.md`
- Modify: `docs/verification/Phase1c-开发报告.md`
- Modify: `docs/verification/Phase1d-开发报告.md`
- Modify: `docs/verification/Phase2-开发报告.md`
- Modify: `docs/verification/Phase3-开发报告.md`

**Interfaces:**
- Consumes: 无
- Produces: 准确的进度文档，后续任务不依赖此任务的产出

- [ ] **Step 1: 在每份 Phase 报告顶部添加状态声明**

在每份 Phase 报告的标题下方（第一个 `---` 之后）添加以下声明块：

```markdown
> ⚠️ **状态修正（2026-06-17）**：本报告描述的 WinForms UI 文件（E3DCopilot.UI 项目）**从未实际创建**。报告内容为设计意图，非实现事实。实际 UI 层已迁移到 WebView2 + React 方案（E3DCopilot.WebHost 项目）。详见 [项目现状梳理报告](../superpowers/specs/2026-06-17-project-status-review-design.md)。
```

- [ ] **Step 2: 提交变更**

```bash
git add docs/verification/Phase*.md
git commit -m "docs: 标注 Phase 报告真实状态（WinForms UI 未实现）"
```

---

## Task 2: 创建 IE3DEnvironment 抽象接口

**Files:**
- Create: `src/E3DCopilot.Tools/Bridge/IE3DEnvironment.cs`

**Interfaces:**
- Consumes: 无（新接口）
- Produces: `IE3DEnvironment` 接口，Task 3/4/5 依赖

- [ ] **Step 1: 创建 IE3DEnvironment 接口文件**

```csharp
// src/E3DCopilot.Tools/Bridge/IE3DEnvironment.cs
using System.Collections.Generic;

namespace E3DCopilot.Tools.Bridge
{
    /// <summary>
    /// E3D 环境抽象接口
    /// 隔离 E3D API 调用，使 Tools 层可测试
    /// 真实环境：RealE3DEnvironment（调用 Aveva.* DLL）
    /// 测试环境：SimulatedE3DEnvironment（返回模拟数据）
    /// </summary>
    public interface IE3DEnvironment
    {
        /// <summary>查询元素（按类型和名称模式）</summary>
        List<ElementInfo> QueryElements(string elementType, string namePattern, string scope, int limit);

        /// <summary>读取元素属性</summary>
        string GetAttribute(string elementName, string attributeName);

        /// <summary>写入元素属性</summary>
        void SetAttribute(string elementName, string attributeName, string value);

        /// <summary>检查元素是否存在</summary>
        bool CheckExists(string elementName);

        /// <summary>执行 PML 命令</summary>
        string ExecutePml(string pmlCommand);

        /// <summary>获取当前元素名称</summary>
        string GetCurrentElementName();
    }

    /// <summary>
    /// 元素信息（查询结果）
    /// </summary>
    public class ElementInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string DbUri { get; set; }
        public Dictionary<string, string> Attributes { get; set; }

        public ElementInfo()
        {
            Attributes = new Dictionary<string, string>();
        }
    }
}
```

- [ ] **Step 2: 提交**

```bash
git add src/E3DCopilot.Tools/Bridge/IE3DEnvironment.cs
git commit -m "feat: 添加 IE3DEnvironment 抽象接口，隔离 E3D API 调用"
```

---

## Task 3: 创建 SimulatedE3DEnvironment（TestHost 用）

**Files:**
- Create: `src/E3DCopilot.Tools/Bridge/SimulatedE3DEnvironment.cs`

**Interfaces:**
- Consumes: `IE3DEnvironment`（Task 2）
- Produces: `SimulatedE3DEnvironment` 类，Task 5（TestHost 集成）依赖

- [ ] **Step 1: 创建 SimulatedE3DEnvironment**

```csharp
// src/E3DCopilot.Tools/Bridge/SimulatedE3DEnvironment.cs
using System.Collections.Generic;
using System.Linq;

namespace E3DCopilot.Tools.Bridge
{
    /// <summary>
    /// 模拟 E3D 环境（用于 TestHost 独立测试，无需真实 E3D）
    /// 提供预设的管道/设备/结构数据
    /// </summary>
    public class SimulatedE3DEnvironment : IE3DEnvironment
    {
        private readonly Dictionary<string, Dictionary<string, string>> _elements;

        public SimulatedE3DEnvironment()
        {
            _elements = new Dictionary<string, Dictionary<string, string>>();

            // 预设管道数据
            AddElement("PIPE-001", "PIPE", new Dictionary<string, string>
            {
                { "NAME", "PIPE-001" },
                { "TYPE", "PIPE" },
                { "DIA", "DN100" },
                { "SPEC", "SCH40" },
                { "MATERIAL", "CS" },
                { "LENGTH", "15000" },
                { "INSULATION", "50mm Mineral Wool" }
            });

            AddElement("PIPE-002", "PIPE", new Dictionary<string, string>
            {
                { "NAME", "PIPE-002" },
                { "TYPE", "PIPE" },
                { "DIA", "DN200" },
                { "SPEC", "SCH80" },
                { "MATERIAL", "SS316" },
                { "LENGTH", "8500" }
            });

            AddElement("EQUI-001", "EQUI", new Dictionary<string, string>
            {
                { "NAME", "EQUI-001" },
                { "TYPE", "EQUIPMENT" },
                { "SUBTYPE", "PUMP" },
                { "DESCRIPTION", "Centrifugal Pump P-101A" },
                { "POSITION", "X=1000 Y=2000 Z=500" }
            });

            AddElement("STRU-001", "STRU", new Dictionary<string, string>
            {
                { "NAME", "STRU-001" },
                { "TYPE", "STRUCTURE" },
                { "SUBTYPE", "BEAM" },
                { "SECTION", "H200x200x8x12" },
                { "LENGTH", "6000" }
            });
        }

        private void AddElement(string name, string type, Dictionary<string, string> attrs)
        {
            _elements[name.ToUpper()] = attrs;
        }

        public List<ElementInfo> QueryElements(string elementType, string namePattern, string scope, int limit)
        {
            var results = new List<ElementInfo>();
            var queryType = (elementType ?? "").ToUpper();
            var pattern = (namePattern ?? "*").Replace("*", "");

            foreach (var kvp in _elements)
            {
                var attrs = kvp.Value;
                var elemType = attrs.ContainsKey("TYPE") ? attrs["TYPE"] : "";

                // 类型过滤
                if (!string.IsNullOrEmpty(queryType) && !elemType.ToUpper().Contains(queryType))
                    continue;

                // 名称模式过滤
                if (!string.IsNullOrEmpty(pattern) && !kvp.Key.Contains(pattern.ToUpper()))
                    continue;

                var info = new ElementInfo
                {
                    Name = kvp.Key,
                    Type = elemType,
                    DbUri = $"/{kvp.Key}",
                    Attributes = new Dictionary<string, string>(attrs)
                };
                results.Add(info);

                if (results.Count >= limit)
                    break;
            }

            return results;
        }

        public string GetAttribute(string elementName, string attributeName)
        {
            var key = (elementName ?? "").ToUpper();
            if (!_elements.TryGetValue(key, out var attrs))
                return null;

            var attrKey = (attributeName ?? "").ToUpper();
            return attrs.ContainsKey(attrKey) ? attrs[attrKey] : null;
        }

        public void SetAttribute(string elementName, string attributeName, string value)
        {
            var key = (elementName ?? "").ToUpper();
            if (!_elements.ContainsKey(key))
                return;

            _elements[key][(attributeName ?? "").ToUpper()] = value;
        }

        public bool CheckExists(string elementName)
        {
            return _elements.ContainsKey((elementName ?? "").ToUpper());
        }

        public string ExecutePml(string pmlCommand)
        {
            return $"[模拟] PML 执行成功: {pmlCommand}";
        }

        public string GetCurrentElementName()
        {
            return "PIPE-001";
        }
    }
}
```

- [ ] **Step 2: 提交**

```bash
git add src/E3DCopilot.Tools/Bridge/SimulatedE3DEnvironment.cs
git commit -m "feat: 添加 SimulatedE3DEnvironment（TestHost 模拟数据）"
```

---

## Task 4: 创建 RealE3DEnvironment（真实 E3D API）

**Files:**
- Create: `src/E3DCopilot.Tools/Bridge/RealE3DEnvironment.cs`

**Interfaces:**
- Consumes: `IE3DEnvironment`（Task 2）
- Produces: `RealE3DEnvironment` 类，Task 5（E3DToolDispatcher）依赖

- [ ] **Step 1: 创建 RealE3DEnvironment**

```csharp
// src/E3DCopilot.Tools/Bridge/RealE3DEnvironment.cs
using System;
using System.Collections.Generic;
using E3DCopilot.Core.Threading;

namespace E3DCopilot.Tools.Bridge
{
    /// <summary>
    /// 真实 E3D 环境实现
    /// 所有 API 调用通过 ThreadMarshaller 切换到 UI 线程
    /// 
    /// 关键 API 签名（已核实）：
    /// - DbElement.GetElement(string dbUri) → DbElement
    /// - DbAttribute.GetDbAttribute(string name) → DbAttribute
    /// - DbElement.GetAsString(DbAttribute) → string
    /// - Command.CreateCommand(string) → Command
    /// - Command.RunInPdms() → bool
    /// </summary>
    public class RealE3DEnvironment : IE3DEnvironment
    {
        public List<ElementInfo> QueryElements(string elementType, string namePattern, string scope, int limit)
        {
            return ThreadMarshaller.Invoke(() =>
            {
                var results = new List<ElementInfo>();

                // 构建 PML 查询命令
                // 真实 E3D 中通过 Filter + CE 遍历实现
                // 这里使用 PML 脚本查询
                string pmlQuery = BuildPmlQuery(elementType, namePattern, scope, limit);
                string output = ExecutePml(pmlQuery);

                // 解析 PML 输出为 ElementInfo 列表
                if (!string.IsNullOrEmpty(output) && !output.StartsWith("Error"))
                {
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('|');
                        if (parts.Length >= 2)
                        {
                            var info = new ElementInfo
                            {
                                Name = parts[0].Trim(),
                                Type = parts[1].Trim(),
                                DbUri = parts.Length > 2 ? parts[2].Trim() : ""
                            };
                            results.Add(info);
                        }
                    }
                }

                return results;
            });
        }

        public string GetAttribute(string elementName, string attributeName)
        {
            return ThreadMarshaller.Invoke(() =>
            {
                // 真实 API 调用链路（已核实）：
                // var element = Aveva.Core.Database.DbElement.GetElement(elementName);
                // if (element == null || element.IsNull) return null;
                // var attr = Aveva.Core.Database.DbAttribute.GetDbAttribute(attributeName);
                // return element.GetAsString(attr);

                // 通过 PML 间接读取（更安全，避免 DLL 版本差异）
                string pml = $"$p val = !{elementName}.{attributeName}; $p val";
                string result = ExecutePml(pml);
                return string.IsNullOrEmpty(result) ? null : result.Trim();
            });
        }

        public void SetAttribute(string elementName, string attributeName, string value)
        {
            ThreadMarshaller.Invoke(() =>
            {
                // 真实 API：
                // var element = Aveva.Core.Database.DbElement.GetElement(elementName);
                // var attr = Aveva.Core.Database.DbAttribute.GetDbAttribute(attributeName);
                // element.SetAttribute(attr, value);

                string pml = $"$p {elementName}.{attributeName} = {value}";
                ExecutePml(pml);
            });
        }

        public bool CheckExists(string elementName)
        {
            return ThreadMarshaller.Invoke(() =>
            {
                // 通过 PML exist 命令检查
                string pml = $"if exist {elementName} then $p YES else $p NO";
                string result = ExecutePml(pml);
                return result != null && result.Trim().Contains("YES");
            });
        }

        public string ExecutePml(string pmlCommand)
        {
            return ThreadMarshaller.Invoke(() =>
            {
                // 真实 API（已核实）：
                // Aveva.Core.Utilities.CommandLine.Command cmd =
                //     Aveva.Core.Utilities.CommandLine.Command.CreateCommand(pmlCommand);
                // bool ok = cmd.RunInPdms();
                // return ok ? cmd.Result : ("Error: " + cmd.Error.MessageText);

                // 注意：此处需要 E3D 运行时环境
                // 如果 DLL 未加载，会抛出 TypeLoadException
                try
                {
                    var cmdType = Type.GetType("Aveva.Core.Utilities.CommandLine.Command, Aveva.Core");
                    if (cmdType == null)
                        return "Error: E3D API 未加载";

                    var createMethod = cmdType.GetMethod("CreateCommand",
                        new[] { typeof(string) });
                    if (createMethod == null)
                        return "Error: CreateCommand 方法未找到";

                    var cmd = createMethod.Invoke(null, new object[] { pmlCommand });
                    var runMethod = cmdType.GetMethod("RunInPdms");
                    if (runMethod == null)
                        return "Error: RunInPdms 方法未找到";

                    bool ok = (bool)runMethod.Invoke(cmd, null);
                    var resultProp = cmdType.GetProperty("Result");
                    string result = resultProp?.GetValue(cmd)?.ToString() ?? "";

                    return ok ? result : "Error: PML 执行失败";
                }
                catch (Exception ex)
                {
                    return $"Error: {ex.Message}";
                }
            });
        }

        public string GetCurrentElementName()
        {
            return ThreadMarshaller.Invoke(() =>
            {
                // 真实 API：
                // var ce = Aveva.Core.Database.DbElement.GetElement();
                // return ce.GetAsString(DbAttribute.GetDbAttribute("NAME"));

                string pml = "$p !ce.Name";
                string result = ExecutePml(pml);
                return string.IsNullOrEmpty(result) ? null : result.Trim();
            });
        }

        /// <summary>
        /// 构建 PML 查询脚本
        /// </summary>
        private string BuildPmlQuery(string elementType, string namePattern, string scope, int limit)
        {
            // PML 查询脚本模板
            // 实际实现需要根据 E3D Filter API 调整
            string typeFilter = string.IsNullOrEmpty(elementType) ? "" : $"type eq {elementType}";
            string nameFilter = string.IsNullOrEmpty(namePattern) ? "" : $"name like {namePattern}";
            string scopeUri = string.IsNullOrEmpty(scope) ? "!" : scope;

            return $"$p query elements in {scopeUri} where {typeFilter} {nameFilter} limit {limit}";
        }
    }
}
```

- [ ] **Step 2: 提交**

```bash
git add src/E3DCopilot.Tools/Bridge/RealE3DEnvironment.cs
git commit -m "feat: 添加 RealE3DEnvironment（真实 E3D API 调用，通过反射避免编译时依赖）"
```

---

## Task 5: 创建 E3DToolDispatcher（实现 IToolDispatcher）

**Files:**
- Create: `src/E3DCopilot.Tools/Bridge/E3DToolDispatcher.cs`

**Interfaces:**
- Consumes: `IToolDispatcher`（Core 层）、`IE3DEnvironment`（Task 2）
- Produces: `E3DToolDispatcher` 类，Task 7（TestHost 集成）和 Task 8（WebHost 集成）依赖

- [ ] **Step 1: 创建 E3DToolDispatcher**

```csharp
// src/E3DCopilot.Tools/Bridge/E3DToolDispatcher.cs
using System;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Tools.Bridge
{
    /// <summary>
    /// E3D 工具调度器 — 实现 Core 层的 IToolDispatcher 接口
    /// 将 Core 的工具调用请求路由到 IE3DEnvironment 的真实/模拟实现
    /// 
    /// 这是 Core → Tools 依赖的关键桥梁：
    /// Core (DbQueryHandler) → IToolDispatcher → E3DToolDispatcher → IE3DEnvironment → E3D API
    /// </summary>
    public class E3DToolDispatcher : IToolDispatcher
    {
        private readonly IE3DEnvironment _env;

        public E3DToolDispatcher(IE3DEnvironment environment)
        {
            _env = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        /// <summary>
        /// 按名称执行工具并返回结果 JSON 字符串
        /// </summary>
        public Task<string> ExecuteAsync(string name, string args)
        {
            switch ((name ?? "").ToLower())
            {
                case "query":
                    return Task.FromResult(HandleQuery(args));

                case "modify":
                    return Task.FromResult(HandleModify(args));

                case "check":
                    return Task.FromResult(HandleCheck(args));

                case "calculate":
                    return Task.FromResult(HandleCalculate(args));

                case "export":
                    return Task.FromResult(HandleExport(args));

                case "execute_pml":
                    return Task.FromResult(HandleExecutePml(args));

                default:
                    return Task.FromResult($"{{\"error\": \"未知工具: {name}\"}}");
            }
        }

        /// <summary>
        /// 处理查询请求
        /// </summary>
        private string HandleQuery(string args)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                var type = json["type"]?.ToString() ?? "";
                var name = json["name"]?.ToString() ?? "";
                var scope = json["scope"]?.ToString() ?? "";
                var limit = json["limit"]?.Value<int>() ?? 50;

                var elements = _env.QueryElements(type, name, scope, limit);

                // 构建结果 JSON
                var resultArray = new JArray();
                foreach (var elem in elements)
                {
                    var obj = new JObject
                    {
                        ["name"] = elem.Name,
                        ["type"] = elem.Type,
                        ["dbUri"] = elem.DbUri
                    };

                    // 附加属性
                    if (elem.Attributes != null)
                    {
                        var attrs = new JObject();
                        foreach (var attr in elem.Attributes)
                        {
                            attrs[attr.Key] = attr.Value;
                        }
                        obj["attributes"] = attrs;
                    }

                    resultArray.Add(obj);
                }

                var result = new JObject
                {
                    ["success"] = true,
                    ["count"] = elements.Count,
                    ["elements"] = resultArray
                };

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }

        /// <summary>
        /// 处理修改请求
        /// </summary>
        private string HandleModify(string args)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                var element = json["element"]?.ToString();
                var attribute = json["attribute"]?.ToString();
                var value = json["value"]?.ToString();

                if (string.IsNullOrEmpty(element) || string.IsNullOrEmpty(attribute))
                {
                    return "{\"success\": false, \"error\": \"缺少 element 或 attribute 参数\"}";
                }

                _env.SetAttribute(element, attribute, value);

                return $"{{\"success\": true, \"message\": \"已设置 {element}.{attribute} = {value}\"}}";
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }

        /// <summary>
        /// 处理检查请求
        /// </summary>
        private string HandleCheck(string args)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                var element = json["element"]?.ToString();

                if (string.IsNullOrEmpty(element))
                {
                    return "{\"success\": false, \"error\": \"缺少 element 参数\"}";
                }

                bool exists = _env.CheckExists(element);

                return $"{{\"success\": true, \"exists\": {exists.ToString().ToLower()}}}";
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }

        /// <summary>
        /// 处理计算请求（占位）
        /// </summary>
        private string HandleCalculate(string args)
        {
            return "{\"success\": true, \"message\": \"计算工具尚未实现\"}";
        }

        /// <summary>
        /// 处理导出请求（占位）
        /// </summary>
        private string HandleExport(string args)
        {
            return "{\"success\": true, \"message\": \"导出工具尚未实现\"}";
        }

        /// <summary>
        /// 处理 PML 执行请求
        /// </summary>
        private string HandleExecutePml(string args)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                var command = json["command"]?.ToString();

                if (string.IsNullOrEmpty(command))
                {
                    return "{\"success\": false, \"error\": \"缺少 command 参数\"}";
                }

                string result = _env.ExecutePml(command);

                return $"{{\"success\": true, \"result\": \"{result.Replace("\"", "\\\"")}\"}}";
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }
    }
}
```

- [ ] **Step 2: 提交**

```bash
git add src/E3DCopilot.Tools/Bridge/E3DToolDispatcher.cs
git commit -m "feat: 添加 E3DToolDispatcher，实现 IToolDispatcher 桥接 Core 与 E3D API"
```

---

## Task 6: 创建单元测试项目

**Files:**
- Create: `src/E3DCopilot.Tests/E3DCopilot.Tests.csproj`
- Create: `src/E3DCopilot.Tests/ToolExecutorTests.cs`
- Create: `src/E3DCopilot.Tests/DbQueryHandlerTests.cs`
- Create: `src/E3DCopilot.Tests/E3DToolDispatcherTests.cs`

**Interfaces:**
- Consumes: `IToolHandler`、`ToolExecutor`、`DbQueryHandler`、`E3DToolDispatcher`、`SimulatedE3DEnvironment`
- Produces: 可运行的单元测试，后续任务依赖

- [ ] **Step 1: 创建测试项目文件**

```xml
<!-- src/E3DCopilot.Tests/E3DCopilot.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.6.0" />
    <PackageReference Include="NUnit" Version="3.13.3" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.5.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\E3DCopilot.Core\E3DCopilot.Core.csproj" />
    <ProjectReference Include="..\E3DCopilot.Tools\E3DCopilot.Tools.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: 创建 E3DToolDispatcher 测试**

```csharp
// src/E3DCopilot.Tests/E3DToolDispatcherTests.cs
using System.Threading.Tasks;
using E3DCopilot.Tools.Bridge;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class E3DToolDispatcherTests
    {
        private E3DToolDispatcher _dispatcher;
        private SimulatedE3DEnvironment _env;

        [SetUp]
        public void SetUp()
        {
            _env = new SimulatedE3DEnvironment();
            _dispatcher = new E3DToolDispatcher(_env);
        }

        [Test]
        public async Task Query_WithType_ReturnsMatchingElements()
        {
            // Arrange
            var args = "{\"type\": \"PIPE\"}";

            // Act
            var resultJson = await _dispatcher.ExecuteAsync("query", args);
            var result = JObject.Parse(resultJson);

            // Assert
            Assert.IsTrue((bool)result["success"]);
            Assert.Greater((int)result["count"], 0);
        }

        [Test]
        public async Task Query_WithName_ReturnsSpecificElement()
        {
            // Arrange
            var args = "{\"type\": \"PIPE\", \"name\": \"001\"}";

            // Act
            var resultJson = await _dispatcher.ExecuteAsync("query", args);
            var result = JObject.Parse(resultJson);

            // Assert
            Assert.IsTrue((bool)result["success"]);
            var elements = (JArray)result["elements"];
            Assert.AreEqual(1, elements.Count);
            Assert.AreEqual("PIPE-001", elements[0]["name"].ToString());
        }

        [Test]
        public async Task Query_WithLimit_RespectsLimit()
        {
            // Arrange
            var args = "{\"type\": \"PIPE\", \"limit\": 1}";

            // Act
            var resultJson = await _dispatcher.ExecuteAsync("query", args);
            var result = JObject.Parse(resultJson);

            // Assert
            Assert.IsTrue((bool)result["success"]);
            var elements = (JArray)result["elements"];
            Assert.LessOrEqual(elements.Count, 1);
        }

        [Test]
        public async Task Check_ExistingElement_ReturnsTrue()
        {
            // Arrange
            var args = "{\"element\": \"PIPE-001\"}";

            // Act
            var resultJson = await _dispatcher.ExecuteAsync("check", args);
            var result = JObject.Parse(resultJson);

            // Assert
            Assert.IsTrue((bool)result["success"]);
            Assert.IsTrue((bool)result["exists"]);
        }

        [Test]
        public async Task Check_NonExistingElement_ReturnsFalse()
        {
            // Arrange
            var args = "{\"element\": \"PIPE-999\"}";

            // Act
            var resultJson = await _dispatcher.ExecuteAsync("check", args);
            var result = JObject.Parse(resultJson);

            // Assert
            Assert.IsTrue((bool)result["success"]);
            Assert.IsFalse((bool)result["exists"]);
        }

        [Test]
        public async Task Modify_ExistingElement_UpdatesAttribute()
        {
            // Arrange
            var args = "{\"element\": \"PIPE-001\", \"attribute\": \"DIA\", \"value\": \"DN150\"}";

            // Act
            var resultJson = await _dispatcher.ExecuteAsync("modify", args);
            var result = JObject.Parse(resultJson);

            // Assert
            Assert.IsTrue((bool)result["success"]);

            // 验证属性已更新
            var newValue = _env.GetAttribute("PIPE-001", "DIA");
            Assert.AreEqual("DN150", newValue);
        }

        [Test]
        public async Task ExecutePml_ReturnsResult()
        {
            // Arrange
            var args = "{\"command\": \"$p hello\"}";

            // Act
            var resultJson = await _dispatcher.ExecuteAsync("execute_pml", args);
            var result = JObject.Parse(resultJson);

            // Assert
            Assert.IsTrue((bool)result["success"]);
            Assert.IsNotNull(result["result"]);
        }

        [Test]
        public async Task UnknownTool_ReturnsError()
        {
            // Arrange
            var args = "{}";

            // Act
            var resultJson = await _dispatcher.ExecuteAsync("unknown_tool", args);
            var result = JObject.Parse(resultJson);

            // Assert
            Assert.IsNotNull(result["error"]);
        }
    }
}
```

- [ ] **Step 3: 创建 DbQueryHandler 测试**

```csharp
// src/E3DCopilot.Tests/DbQueryHandlerTests.cs
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;
using E3DCopilot.Core.Tools.Handlers;
using E3DCopilot.Tools.Bridge;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class DbQueryHandlerTests
    {
        private DbQueryHandler _handler;
        private E3DToolDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            var env = new SimulatedE3DEnvironment();
            _dispatcher = new E3DToolDispatcher(env);
            _handler = new DbQueryHandler(_dispatcher);
        }

        [Test]
        public void Name_ReturnsQuery()
        {
            Assert.AreEqual("query", _handler.Name);
        }

        [Test]
        public void IsReadOnly_ReturnsTrue()
        {
            Assert.IsTrue(_handler.IsReadOnly);
        }

        [Test]
        public async Task ExecuteAsync_WithValidArgs_ReturnsSuccess()
        {
            // Arrange
            var args = "{\"type\": \"PIPE\"}";

            // Act
            var result = await _handler.ExecuteAsync(args);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Text);
        }

        [Test]
        public async Task ExecuteAsync_WithEmptyArgs_ReturnsSuccess()
        {
            // Arrange
            var args = "{}";

            // Act
            var result = await _handler.ExecuteAsync(args);

            // Assert
            Assert.IsTrue(result.Success);
        }
    }
}
```

- [ ] **Step 4: 创建 ToolExecutor 测试**

```csharp
// src/E3DCopilot.Tests/ToolExecutorTests.cs
using System.Threading.Tasks;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Tools;
using E3DCopilot.Core.Tools.Handlers;
using E3DCopilot.Tools.Bridge;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class ToolExecutorTests
    {
        private ToolExecutor _executor;
        private E3DToolDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            var env = new SimulatedE3DEnvironment();
            _dispatcher = new E3DToolDispatcher(env);
            _executor = ToolExecutor.CreateDefault(_dispatcher, null);
        }

        [Test]
        public void HasHandler_Query_ReturnsTrue()
        {
            Assert.IsTrue(_executor.HasHandler("query"));
        }

        [Test]
        public void HasHandler_Modify_ReturnsTrue()
        {
            Assert.IsTrue(_executor.HasHandler("modify"));
        }

        [Test]
        public void HasHandler_Unknown_ReturnsFalse()
        {
            Assert.IsFalse(_executor.HasHandler("unknown_tool"));
        }

        [Test]
        public async Task ExecuteAsync_Query_ReturnsSuccess()
        {
            // Arrange
            var args = "{\"type\": \"PIPE\"}";

            // Act
            var result = await _executor.ExecuteAsync("query", args);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Text);
        }

        [Test]
        public async Task ExecuteAsync_UnknownTool_ReturnsFail()
        {
            // Act
            var result = await _executor.ExecuteAsync("nonexistent", "{}");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("未知工具"));
        }

        [Test]
        public void GetAllHandlers_ReturnsSixHandlers()
        {
            var handlers = _executor.GetAllHandlers();
            Assert.AreEqual(6, handlers.Count);
        }
    }
}
```

- [ ] **Step 5: 将测试项目添加到解决方案**

```bash
cd src
dotnet sln E3DCopilot.sln add E3DCopilot.Tests/E3DCopilot.Tests.csproj
```

- [ ] **Step 6: 运行测试验证**

```bash
cd src/E3DCopilot.Tests
dotnet test --verbosity normal
```

Expected: 所有测试通过（14 个测试）

- [ ] **Step 7: 提交**

```bash
git add src/E3DCopilot.Tests/
git add src/E3DCopilot.sln
git commit -m "test: 添加单元测试项目（ToolExecutor, DbQueryHandler, E3DToolDispatcher）"
```

---

## Task 7: 集成 SimulatedE3DEnvironment 到 TestHost

**Files:**
- Modify: `src/E3DCopilot.TestHost/MainForm.cs`

**Interfaces:**
- Consumes: `E3DToolDispatcher`、`SimulatedE3DEnvironment`（Task 3/5）
- Produces: TestHost 可独立运行并返回模拟数据

- [ ] **Step 1: 修改 MainForm.cs，注册 E3DToolDispatcher**

在 `MainForm.cs` 的初始化代码中，找到创建 `CopilotController` 的位置，添加：

```csharp
// 在 MainForm 构造函数或 Init 方法中
var env = new SimulatedE3DEnvironment();
var dispatcher = new E3DToolDispatcher(env);
var toolExecutor = ToolExecutor.CreateDefault(dispatcher, eventSink);

// 将 toolExecutor 传递给 CopilotController
```

具体修改位置需要根据 `MainForm.cs` 的实际代码结构调整。关键是在创建 `CopilotController` 时，传入使用 `SimulatedE3DEnvironment` 的 `E3DToolDispatcher`。

- [ ] **Step 2: 运行 TestHost 验证**

```bash
cd src/E3DCopilot.TestHost
dotnet run
```

Expected: TestHost 启动成功，发送"查询管道"消息，返回模拟的管道数据（PIPE-001, PIPE-002）

- [ ] **Step 3: 提交**

```bash
git add src/E3DCopilot.TestHost/MainForm.cs
git commit -m "feat: TestHost 集成 SimulatedE3DEnvironment，支持独立测试"
```

---

## Task 8: 集成 RealE3DEnvironment 到 WebHost

**Files:**
- Modify: `src/E3DCopilot.WebHost/CopilotAddinBoot.cs`

**Interfaces:**
- Consumes: `E3DToolDispatcher`、`RealE3DEnvironment`（Task 4/5）
- Produces: WebHost 在 E3D 环境中使用真实 API

- [ ] **Step 1: 修改 CopilotAddinBoot.cs，注册 E3DToolDispatcher**

在 `CopilotAddinBoot.cs` 的 `Start()` 方法中，添加：

```csharp
// 在 E3D 环境中使用真实 API
var env = new RealE3DEnvironment();
var dispatcher = new E3DToolDispatcher(env);
var toolExecutor = ToolExecutor.CreateDefault(dispatcher, eventSink);

// 将 toolExecutor 传递给 CopilotController
```

- [ ] **Step 2: 编译验证**

```bash
cd src
dotnet build E3DCopilot.WebHost/E3DCopilot.WebHost.csproj
```

Expected: 编译成功，0 errors

- [ ] **Step 3: 提交**

```bash
git add src/E3DCopilot.WebHost/CopilotAddinBoot.cs
git commit -m "feat: WebHost 集成 RealE3DEnvironment，使用真实 E3D API"
```

---

## Task 9: 精简 web-ui 前端

**Files:**
- Modify: `web-ui/src/App.tsx`
- Delete: `web-ui/src/components/mcp/`（整个目录）
- Delete: `web-ui/src/components/onboarding/`（整个目录）
- Delete: `web-ui/src/components/chat/BrowserSessionRow.tsx`
- Delete: `web-ui/src/components/chat/DiffEditRow.tsx`
- Delete: `web-ui/src/components/chat/auto-approve-menu/`（整个目录）
- Delete: `web-ui/src/components/settings/` 中除 `SettingsView.tsx` 外的所有 `*Picker.tsx` 文件

**Interfaces:**
- Consumes: 无
- Produces: 精简后的 React 前端，`npm run build` 通过

- [ ] **Step 1: 备份当前 web-ui**

```bash
cd web-ui
git stash
```

- [ ] **Step 2: 删除不需要的组件目录**

```bash
# 删除 MCP 配置组件
rm -rf src/components/mcp/

# 删除 Onboarding 组件
rm -rf src/components/onboarding/

# 删除不需要的聊天组件
rm -f src/components/chat/BrowserSessionRow.tsx
rm -f src/components/chat/DiffEditRow.tsx
rm -rf src/components/chat/auto-approve-menu/
```

- [ ] **Step 3: 精简 settings 组件**

保留 `SettingsView.tsx`，删除所有 Provider 选择器：

```bash
cd src/components/settings/
# 保留 SettingsView.tsx，删除其他 *Picker.tsx
ls *Picker.tsx | xargs rm -f
```

- [ ] **Step 4: 修改 App.tsx，移除对已删除组件的引用**

在 `App.tsx` 中，删除对 `McpView`、`OnboardingView`、`BrowserSessionRow`、`DiffEditRow` 的 import 和路由配置。

- [ ] **Step 5: 构建验证**

```bash
cd web-ui
npm run build
```

Expected: 构建成功，无 TypeScript 错误

- [ ] **Step 6: 提交**

```bash
git add web-ui/
git commit -m "refactor: 精简 web-ui，删除不需要的组件（MCP、Onboarding、40+ Provider 选择器）"
```

---

## Task 10: 创建部署脚本

**Files:**
- Create: `deploy.cmd`

**Interfaces:**
- Consumes: 编译后的 DLL
- Produces: 可执行的部署脚本

- [ ] **Step 1: 创建 deploy.cmd**

```batch
@echo off
REM E3D-E小智 部署脚本
REM 用法: deploy.cmd [E3D安装目录]

setlocal

set E3D_DIR=%1
if "%E3D_DIR%"=="" (
    echo 用法: deploy.cmd [E3D安装目录]
    echo 示例: deploy.cmd "C:\Program Files\AVEVA\E3D"
    exit /b 1
)

echo ========================================
echo E3D-E小智 部署脚本
echo ========================================
echo.

REM 检查 E3D 目录
if not exist "%E3D_DIR%" (
    echo 错误: E3D 目录不存在: %E3D_DIR%
    exit /b 1
)

REM 编译项目
echo [1/3] 编译项目...
cd src
dotnet build -c Release
if %ERRORLEVEL% neq 0 (
    echo 错误: 编译失败
    exit /b 1
)
cd ..

REM 复制 DLL 到 E3D 目录
echo [2/3] 复制 DLL 到 E3D 目录...
set ADDIN_DIR=%E3D_DIR%\addins\E3DCopilot
if not exist "%ADDIN_DIR%" mkdir "%ADDIN_DIR%"

copy /Y src\E3DCopilot.Addin\bin\Release\E3DCopilot.Addin.dll "%ADDIN_DIR%\"
copy /Y src\E3DCopilot.Core\bin\Release\E3DCopilot.Core.dll "%ADDIN_DIR%\"
copy /Y src\E3DCopilot.Tools\bin\Release\E3DCopilot.Tools.dll "%ADDIN_DIR%\"
copy /Y src\E3DCopilot.WebHost\bin\Release\E3DCopilot.WebHost.dll "%ADDIN_DIR%\"

REM 复制前端资源
echo [3/3] 复制前端资源...
set WWWROOT_DIR=%ADDIN_DIR%\wwwroot
if not exist "%WWWROOT_DIR%" mkdir "%WWWROOT_DIR%"
xcopy /E /Y src\E3DCopilot.WebHost\wwwroot\* "%WWWROOT_DIR%\"

echo.
echo ========================================
echo 部署完成！
echo 目标目录: %ADDIN_DIR%
echo ========================================
echo.
echo 请重启 E3D 以加载插件。

endlocal
```

- [ ] **Step 2: 提交**

```bash
git add deploy.cmd
git commit -m "feat: 添加部署脚本 deploy.cmd"
```

---

## Task 11: 端到端验证 + 演示

**Files:**
- 无新建文件

**Interfaces:**
- Consumes: 所有前序任务的产出
- Produces: 端到端演示视频/截图

- [ ] **Step 1: 在 TestHost 中验证端到端流程**

```bash
cd src/E3DCopilot.TestHost
dotnet run
```

在 TestHost 界面中输入："查询 DN100 管道"

Expected:
1. AI 流式回复："我来帮您查询 DN100 管道..."
2. 调用 query 工具
3. 返回 PIPE-001 的属性（DIA=SCH40, MATERIAL=CS 等）
4. UI 展示工具执行卡片和结果

- [ ] **Step 2: 在真实 E3D 环境中验证（如可用）**

```bash
deploy.cmd "C:\Program Files\AVEVA\E3D"
```

重启 E3D，打开 E小智 面板，输入："查询当前管道属性"

Expected: 返回真实 E3D 数据库中的管道数据

- [ ] **Step 3: 截图/录屏**

截取以下场景：
1. TestHost 启动界面
2. 用户输入"查询 DN100 管道"
3. AI 流式回复 + 工具调用
4. 结果展示

- [ ] **Step 4: 提交最终验证报告**

```bash
git add docs/verification/端到端验证报告.md
git commit -m "docs: 添加端到端验证报告"
```

---

## 实施时间线

| 周次 | 任务 | 预计耗时 |
|------|------|:-------:|
| **Week 1** | Task 1-5（文档更新 + 核心实现） | 3 天 |
| **Week 2** | Task 6-8（测试 + 集成） | 2 天 |
| **Week 2** | Task 9（前端精简） | 1 天 |
| **Week 3** | Task 10-11（部署 + 验证） | 1 天 |
| **缓冲** | 应对意外 | 1 周 |

**总计：4 天核心工作 + 1 周缓冲 = 2 周完成**

---

## 成功标准

Phase 0 结束时，必须满足以下**全部条件**：

1. ✅ **文档准确**：所有 Phase 报告反映真实代码状态（Task 1）
2. ✅ **前端精简**：web-ui 只保留 15 个核心组件，`npm run build` 通过（Task 9）
3. ✅ **真实调用**：CsharpBridge 调用真实 E3D API（不是 mock）（Task 4/5/8）
4. ✅ **端到端**：用户说"查询 DN100 管道"→ AI 回复管道属性 → 数据来自 E3D 数据库（Task 11）
5. ✅ **可复现**：在 TestHost 中可以独立运行演示（Task 7）
6. ✅ **测试通过**：14 个单元测试全部通过（Task 6）
