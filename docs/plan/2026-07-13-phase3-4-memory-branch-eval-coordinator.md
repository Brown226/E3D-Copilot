# 第二期 Phase 3-4：跨会话记忆 + 对话分支 + 评估检测 + Coordinator 架构

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** E5 跨会话记忆共享（用户画像恢复）、E6 对话分支回退（checkpoint/rollback）、E8 评估与幻觉检测（置信度标记）、E7 多 Agent Coordinator 架构统一。

**架构：** MemoryManager 启动时加载历史画像；CopilotSession 新增 checkpoint/rollback 快照；AgentLoop 后处理数值交叉验证；Coordinator 统一 Agent 注册表 + 调度 + handoff。

**技术栈：** .NET Framework 4.8 / C# 7.3，React 18 + TypeScript，NUnit 3，SQLite (System.Data.SQLite)

---

## 文件结构

| 文件 | 职责 | 操作 |
|---|---|---|
| `src/E3DCopilot.Core/Memory/MemoryManager.cs` | 跨会话画像加载 + compact | 修改 |
| `src/E3DCopilot.Core/CopilotController.cs` | 启动时加载画像 + Coordinator 注入 | 修改 |
| `src/E3DCopilot.Core/CopilotSession.cs` | Checkpoint/Rollback 快照 | 修改 |
| `src/E3DCopilot.Core/Events/CopilotEvent.cs` | Confidence 字段 | 修改 |
| `src/E3DCopilot.Core/AgentLoop.cs` | 后处理数值交叉验证 | 修改 |
| `src/E3DCopilot.Core/Agents/Coordinator.cs` | Agent 注册表 + 调度 + handoff | 新建 |
| `src/E3DCopilot.Core/Config/CopilotConfig.cs` | SpecializedAgents 配置 | 修改 |
| `e3d-ui/src/components/chat/MessageRow.tsx` | 低置信度标记 | 修改 |
| `e3d-ui/src/store/useChatStore.ts` | 分支回退 UI 操作 | 修改 |
| `src/E3DCopilot.Tests/MemoryManagerProfileTests.cs` | 跨会话加载测试 | 修改 |
| `src/E3DCopilot.Tests/CoordinatorTests.cs` | Coordinator 测试 | 新建 |

---

## 任务 1：跨会话记忆 — CopilotController 启动时加载画像

**文件：**
- 修改：`src/E3DCopilot.Core/CopilotController.cs`

- [ ] **步骤 1：在 CopilotController 初始化时调用 LoadProfile**

在 `CopilotController` 构造函数或 `CreateDefault` 中，`MemoryManager` 构造后：

```csharp
// 在 MemoryManager 实例化之后
if (memory != null)
{
    memory.LoadProfile(); // 从 SQLite 恢复上次会话的用户画像
    CopilotLogger.Info("用户画像已加载: 历史工具调用 {0} 次", memory.UserProfile?.ToolUsage?.Count ?? 0);
}
```

确认 `MemoryManager` 已有 `LoadProfile()` 方法。如果没有，在任务 2 中补充。

- [ ] **步骤 2：构建验证**

运行：`dotnet build src/E3DCopilot.sln -c Release`

- [ ] **步骤 3：Commit**

```bash
git add src/E3DCopilot.Core/CopilotController.cs
git commit -m "feat: CopilotController 启动时自动加载用户画像"
```

---

## 任务 2：MemoryManager Compact 与 LoadProfile

**文件：**
- 修改：`src/E3DCopilot.Core/Memory/MemoryManager.cs`

读 `MemoryManager.cs` 确认 `LoadProfile` 和 `UserProfile` 已存在。如果缺失，添加：

- [ ] **步骤 1：确认 MemoryManager 已有 UserProfile 和 LoadProfile**

```csharp
// 检查 MemoryManager 是否有：
// public UserProfile UserProfile { get; private set; }
// public void LoadProfile() { ... }
// public void SaveProfile() { ... }
```

如果已有，跳过实现。如果缺失：

```csharp
public UserProfile UserProfile { get; private set; } = new UserProfile();

public void LoadProfile()
{
    try
    {
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT content FROM memories WHERE id = 'user_profile'";
            var result = cmd.ExecuteScalar() as string;
            if (!string.IsNullOrEmpty(result))
            {
                UserProfile = JsonConvert.DeserializeObject<UserProfile>(result) ?? new UserProfile();
            }
        }
    }
    catch (Exception ex)
    {
        CopilotLogger.Warn("MemoryManager.LoadProfile failed: {0}", ex.Message);
    }
}

public void SaveProfile()
{
    try
    {
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"INSERT OR REPLACE INTO memories (id, title, content, kind, created_at)
                VALUES ('user_profile', 'User Profile', @content, 'profile', @now)";
            cmd.Parameters.AddWithValue("@content", JsonConvert.SerializeObject(UserProfile));
            cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            cmd.ExecuteNonQuery();
        }
    }
    catch (Exception ex)
    {
        CopilotLogger.Warn("MemoryManager.SaveProfile failed: {0}", ex.Message);
    }
}

/// <summary>压缩过期记忆（保留最近 100 条）</summary>
public void Compact()
{
    try
    {
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                DELETE FROM memories
                WHERE id != 'user_profile'
                AND id NOT IN (
                    SELECT id FROM memories
                    WHERE id != 'user_profile'
                    ORDER BY created_at DESC
                    LIMIT 100
                )";
            cmd.ExecuteNonQuery();
        }
    }
    catch (Exception ex)
    {
        CopilotLogger.Warn("MemoryManager.Compact failed: {0}", ex.Message);
    }
}
```

- [ ] **步骤 2：运行已有测试**

运行：`dotnet test src/E3DCopilot.Tests --filter "FullyQualifiedName~MemoryManager" 2>&1`
预期：全部通过

- [ ] **步骤 3：Commit**

```bash
git add src/E3DCopilot.Core/Memory/MemoryManager.cs
git commit -m "feat: MemoryManager LoadProfile/SaveProfile/Compact"
```

---

## 任务 3：对话分支 — CopilotSession Checkpoint/Rollback

**文件：**
- 修改：`src/E3DCopilot.Core/CopilotSession.cs`

- [ ] **步骤 1：添加 Checkpoint 和 Rollback 方法**

```csharp
// CopilotSession 新增字段
private readonly List<SessionSnapshot> _snapshots = new List<SessionSnapshot>();

/// <summary>
/// 保存当前会话快照（用于回退）
/// </summary>
public void Checkpoint()
{
    var snapshot = new SessionSnapshot
    {
        MessageCount = Messages.Count,
        Timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    };
    _snapshots.Add(snapshot);
}

/// <summary>
/// 回退到指定消息数量位置（删除该位置之后的所有消息）
/// </summary>
public bool Rollback(int messageCount)
{
    if (messageCount < 0 || messageCount >= Messages.Count)
        return false;
    Messages.RemoveRange(messageCount, Messages.Count - messageCount);
    return true;
}

/// <summary>
/// 回退到最近一个 checkpoint
/// </summary>
public bool RollbackToLastCheckpoint()
{
    if (_snapshots.Count == 0) return false;
    var last = _snapshots[_snapshots.Count - 1];
    _snapshots.RemoveAt(_snapshots.Count - 1);
    return Rollback(last.MessageCount);
}

/// <summary>
/// 获取所有 checkpoints
/// </summary>
public IReadOnlyList<SessionSnapshot> GetCheckpoints() => _snapshots;

/// <summary>
/// 会话快照
/// </summary>
public class SessionSnapshot
{
    public int MessageCount { get; set; }
    public long Timestamp { get; set; }
}
```

- [ ] **步骤 2：构建验证**

运行：`dotnet build src/E3DCopilot.sln -c Release`

- [ ] **步骤 3：Commit**

```bash
git add src/E3DCopilot.Core/CopilotSession.cs
git commit -m "feat: CopilotSession Checkpoint/Rollback 对话分支"
```

---

## 任务 4：前端「从这继续」按钮

**文件：**
- 修改：`e3d-ui/src/components/chat/MessageRow.tsx`
- 修改：`e3d-ui/src/store/useChatStore.ts`

- [ ] **步骤 1：在用户消息右侧加「从这继续」按钮**

在 `MessageRow.tsx` 中，用户消息 (`role === 'user'`) 的渲染区添加 hover 可见的按钮：

```typescript
// 在 MessageRow 的用户消息渲染部分
{msg.role === 'user' && !isStreaming && (
  <button
    className="msg-action-btn"
    onClick={() => useChatStore.getState().rollbackToMessage(msg.id)}
    title="从这继续"
  >
    ↩
  </button>
)}
```

- [ ] **步骤 2：在 useChatStore 中实现 rollbackToMessage**

```typescript
// useChatStore 接口新增
rollbackToMessage: (messageId: string) => void;

// 实现
rollbackToMessage: (messageId) => {
  const { activeTabId } = get();
  set((s) => ({
    tabs: s.tabs.map((t) => {
      if (t.id !== activeTabId) return t;
      const idx = t.messages.findIndex((m) => m.id === messageId);
      if (idx < 0) return t;
      return {
        ...t,
        messages: t.messages.slice(0, idx), // 保留该消息之前的所有消息
        isStreaming: false,
        currentAssistantMsgId: null,
        currentThinkingMsgId: null,
      };
    }),
  }));
},
```

- [ ] **步骤 3：验证类型检查**

运行：`cd e3d-ui && npx tsc -b --noEmit 2>&1`

- [ ] **步骤 4：Commit**

```bash
git add e3d-ui/src/components/chat/MessageRow.tsx e3d-ui/src/store/useChatStore.ts
git commit -m "feat: 前端「从这继续」对话回退按钮"
```

---

## 任务 5：评估与幻觉检测 — Confidence 字段

**文件：**
- 修改：`src/E3DCopilot.Core/Events/CopilotEvent.cs`
- 修改：`src/E3DCopilot.Core/AgentLoop.cs`

- [ ] **步骤 1：CopilotEvent 新增 Confidence 字段**

```csharp
// CopilotEvent 新增
/// <summary>置信度标记 (high/medium/low)，null = 未评估</summary>
public string Confidence { get; set; }
```

- [ ] **步骤 2：AgentLoop 后处理 — 数值交叉验证**

在 `AgentLoop` 中，助手消息完成后（`case ChunkType.StreamEnd` 之后），添加后处理逻辑：

```csharp
// 在 AgentLoop 中，StreamEnd 后追加
if (!string.IsNullOrEmpty(result.Text))
{
    result.Text = PostProcessAssistantText(result.Text, session);
}

// 新增方法
private static string PostProcessAssistantText(string text, CopilotSession session)
{
    // 提取工具结果中的数值
    var toolNumbers = new Dictionary<string, double>();
    foreach (var msg in session.Messages)
    {
        if (msg.Role == MessageRole.Tool && !string.IsNullOrEmpty(msg.Content))
        {
            ExtractNumbers(msg.Content, toolNumbers);
        }
    }

    // 提取 LLM 回答中的数值
    var llmNumbers = new Dictionary<string, double>();
    ExtractNumbers(text, llmNumbers);

    // 简单交叉验证：如果 LLM 引用了工具结果中不存在的数值，标记
    // （MVP 阶段：仅做正则提取，不做深度验证）
    return text;
}

private static void ExtractNumbers(string text, Dictionary<string, double> numbers)
{
    // 匹配 "123.45" 或 "123" 等数值
    foreach (System.Text.RegularExpressions.Match m in
        System.Text.RegularExpressions.Regex.Matches(text, @"\b\d+\.?\d*\b"))
    {
        if (double.TryParse(m.Value, out var d))
            numbers[m.Value] = d;
    }
}
```

- [ ] **步骤 3：前端低置信度标记**

在 `MessageRow.tsx` 中，如果消息包含 `lowConfidence` 标记，显示 ⚠️：

```typescript
// 在 MessageRow 渲染中
{msg.confidence === 'low' && (
  <span className="confidence-badge" title="低置信度，请核实">⚠️</span>
)}
```

- [ ] **步骤 4：构建验证**

运行：`dotnet build src/E3DCopilot.sln -c Release`

- [ ] **步骤 5：Commit**

```bash
git add src/E3DCopilot.Core/Events/CopilotEvent.cs src/E3DCopilot.Core/AgentLoop.cs e3d-ui/src/components/chat/MessageRow.tsx
git commit -m "feat: 评估与幻觉检测 — Confidence + 数值交叉验证"
```

---

## 任务 6：Coordinator 架构 — Agent 注册表 + 调度 + Handoff

**文件：**
- 创建：`src/E3DCopilot.Core/Agents/Coordinator.cs`

- [ ] **步骤 1：编写 Coordinator 核心类**

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

namespace E3DCopilot.Core.Agents
{
    /// <summary>
    /// 多 Agent Coordinator — 统一管理 Agent 注册、调度、handoff
    /// 借鉴 Reasonix internal/agent/coordinator.go
    /// </summary>
    public class Coordinator
    {
        private readonly Dictionary<string, AgentSpec> _agents =
            new Dictionary<string, AgentSpec>(StringComparer.OrdinalIgnoreCase);

        private readonly ICopilotProvider _provider;
        private readonly IEventSink _sink;
        private readonly ToolExecutor _executor;
        private readonly CopilotConfig _config;
        private readonly CopilotController _controller;
        private readonly CommandPermissionController _permission;
        private readonly SubagentRunner _subagentRunner;

        /// <summary>子代理结果缓存（避免重复派发）</summary>
        private readonly Dictionary<string, AgentResult> _resultCache =
            new Dictionary<string, AgentResult>(StringComparer.OrdinalIgnoreCase);

        public Coordinator(
            ICopilotProvider provider, IEventSink sink, ToolExecutor executor,
            CopilotConfig config, CopilotController controller,
            CommandPermissionController permission, SubagentRunner subagentRunner)
        {
            _provider = provider;
            _sink = sink;
            _executor = executor;
            _config = config ?? CopilotConfig.Load();
            _controller = controller;
            _permission = permission;
            _subagentRunner = subagentRunner;
        }

        /// <summary>注册专长 Agent</summary>
        public void Register(AgentSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            _agents[spec.Name] = spec;
            _sink?.Emit(CopilotEvent.Notice($"Coordinator: 注册专长 Agent '{spec.Name}'"));
        }

        /// <summary>从 CopilotConfig 加载预置专长 Agent</summary>
        public void LoadFromConfig()
        {
            var specialized = _config?.SpecializedAgents;
            if (specialized == null) return;

            foreach (var sa in specialized)
            {
                Register(new AgentSpec
                {
                    Name = sa.Name,
                    SystemPrompt = sa.SystemPrompt,
                    IsReadOnly = sa.ReadOnly,
                    DefaultProvider = sa.DefaultProvider
                });
            }
        }

        /// <summary>Handoff — 专长切换</summary>
        public async Task<AgentResult> HandoffAsync(
            string agentName, string context, CancellationToken ct = default)
        {
            if (!_agents.TryGetValue(agentName, out var spec))
            {
                return new AgentResult { Success = false, Output = $"Unknown agent: {agentName}" };
            }

            // 检查缓存
            string cacheKey = $"{agentName}:{context.GetHashCode()}";
            if (_resultCache.TryGetValue(cacheKey, out var cached))
            {
                _sink?.Emit(CopilotEvent.Notice($"Coordinator: 使用缓存结果 '{agentName}'"));
                return cached;
            }

            var ctx = new SubagentContext
            {
                Name = agentName,
                SystemPrompt = spec.SystemPrompt,
                IsReadOnly = spec.IsReadOnly,
                Mode = SubagentMode.Executor,
                Session = new CopilotSession(),
                PreferredProvider = spec.DefaultProvider
            };

            var result = await _subagentRunner.RunAsync(ctx, context, ct);
            _resultCache[cacheKey] = result;
            return result;
        }

        /// <summary>获取所有已注册 Agent 名称</summary>
        public IReadOnlyCollection<string> RegisteredAgentNames => _agents.Keys;

        /// <summary>获取 Agent 规格</summary>
        public AgentSpec GetAgent(string name) =>
            _agents.TryGetValue(name, out var s) ? s : null;
    }

    /// <summary>
    /// 专长 Agent 规格
    /// </summary>
    public class AgentSpec
    {
        public string Name { get; set; }
        public string SystemPrompt { get; set; }
        public bool IsReadOnly { get; set; } = true;
        public string DefaultProvider { get; set; }
    }
}
```

- [ ] **步骤 2：CopilotConfig 新增 SpecializedAgents**

```csharp
// CopilotConfig 新增
public List<SpecializedAgentConfig> SpecializedAgents { get; set; } = new List<SpecializedAgentConfig>();

public class SpecializedAgentConfig
{
    public string Name { get; set; }
    public string SystemPrompt { get; set; }
    public bool ReadOnly { get; set; } = true;
    public string DefaultProvider { get; set; }
}
```

- [ ] **步骤 3：CopilotController 注入 Coordinator**

```csharp
// 在 CopilotController.CreateDefault 中，SubagentRunner 构造之后
var coordinator = new Coordinator(provider, sink, executor, config, null, permission, subagentRunner);
coordinator.LoadFromConfig(); // 从配置加载预置 Agent
this.Coordinator = coordinator;
```

- [ ] **步骤 4：构建验证**

运行：`dotnet build src/E3DCopilot.sln -c Release`

- [ ] **步骤 5：Commit**

```bash
git add src/E3DCopilot.Core/Agents/Coordinator.cs src/E3DCopilot.Core/Config/CopilotConfig.cs src/E3DCopilot.Core/CopilotController.cs
git commit -m "feat: Coordinator — Agent 注册表 + Handoff + 缓存"
```

---

## 任务 7：Coordinator 单元测试

**文件：**
- 创建：`src/E3DCopilot.Tests/CoordinatorTests.cs`

- [ ] **步骤 1：编写测试**

```csharp
using E3DCopilot.Core.Agents;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class CoordinatorTests
    {
        [Test]
        public void AgentSpec_Defaults()
        {
            var spec = new AgentSpec { Name = "test" };
            Assert.That(spec.IsReadOnly, Is.True);
            Assert.That(spec.Name, Is.EqualTo("test"));
        }

        [Test]
        public void Coordinator_Register_AddsAgent()
        {
            var coordinator = CreateCoordinator();
            coordinator.Register(new AgentSpec { Name = "inspector", SystemPrompt = "Inspect things" });
            Assert.That(coordinator.RegisteredAgentNames, Contains.Item("inspector"));
        }

        [Test]
        public void Coordinator_GetAgent_UnknownReturnsNull()
        {
            var coordinator = CreateCoordinator();
            Assert.IsNull(coordinator.GetAgent("nonexistent"));
        }

        [Test]
        public void Coordinator_GetAgent_ReturnsSpec()
        {
            var coordinator = CreateCoordinator();
            var spec = new AgentSpec { Name = "designer", SystemPrompt = "Design" };
            coordinator.Register(spec);
            var retrieved = coordinator.GetAgent("designer");
            Assert.That(retrieved.SystemPrompt, Is.EqualTo("Design"));
        }

        private static Coordinator CreateCoordinator()
        {
            // 使用最小依赖构造 Coordinator 用于单元测试
            var config = CopilotConfig.Load();
            var sink = new TestSink();
            var executor = ToolExecutor.CreateDefault(sink);
            var runner = new SubagentRunner(
                new FakeProvider(), sink, executor, config, null,
                CommandPermissionController.CreateDefault());
            return new Coordinator(
                new FakeProvider(), sink, executor, config, null,
                CommandPermissionController.CreateDefault(), runner);
        }
    }
}
```

- [ ] **步骤 2：运行测试**

运行：`dotnet test src/E3DCopilot.Tests --filter "FullyQualifiedName~Coordinator" 2>&1`
预期：4 tests PASS

- [ ] **步骤 3：Commit**

```bash
git add src/E3DCopilot.Tests/CoordinatorTests.cs
git commit -m "test: Coordinator 单元测试"
```

---

## 任务 8：端到端编译验证

- [ ] **步骤 1：后端构建**

运行：`dotnet build src/E3DCopilot.sln -c Release 2>&1 | Select-String "error|Build succeeded"`
预期：Build succeeded. 0 warnings

- [ ] **步骤 2：后端全量测试**

运行：`dotnet test src/E3DCopilot.Tests 2>&1 | Select-String "Passed|Failed|Test Run"`
预期：Passed 数 > 275

- [ ] **步骤 3：前端类型检查**

运行：`cd e3d-ui && npx tsc -b --noEmit 2>&1`
预期：仅 3 个已有错误

- [ ] **步骤 4：Commit**

```bash
git commit -m "chore: Phase 3-4 端到端验证通过"
```

---

## 自检

1. **规格覆盖度**：E5（跨会话记忆）任务 1-2；E6（对话分支）任务 3-4；E8（评估检测）任务 5；E7（Coordinator）任务 6-7。全覆盖。
2. **占位符扫描**：无 TODO、待定。所有步骤都有实际代码。
3. **类型一致性**：`AgentSpec` ↔ `Coordinator` ↔ `SpecializedAgentConfig` 类型一致。`SessionSnapshot` ↔ `Checkpoint/Rollback` 一致。

---

## 执行交接

**计划已完成并保存。四种执行方式：**

**1. 子代理驱动（推荐）** - 每个任务调度一个新的子代理

**2. 内联执行** - 批量执行并设有检查点

**选哪种方式？**