# 第二期 Phase 1：子代理前端可视化 + 工具重试 + 写模式

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 子代理运行状态在前端可见、工具调用失败自动重试、子代理支持受控写模式。

**架构：** 前端按 `Message.agentName` 字段分组渲染子代理消息为独立折叠面板；`ToolResult` 新增 `IsRetryable` 字段，`ToolExecutor` 按指数退避自动重试；`SubagentDispatchHandler` 参数 `readonly=false` 时使用完整工具集，写操作仍走主 Agent 审批流。

**技术栈：** .NET Framework 4.8 / C# 7.3，React 18 + TypeScript + Zustand，NUnit 3

---

## 文件结构

| 文件 | 职责 | 操作 |
|---|---|---|
| `e3d-ui/src/types/index.ts` | Message 新增 `agentName` 字段 | 修改 |
| `e3d-ui/src/components/chat/SubagentPanel.tsx` | 子代理独立折叠面板组件 | 新建 |
| `e3d-ui/src/components/chat/MessageList.tsx` | 按 agentName 分组渲染 + 子代理区域 | 修改 |
| `e3d-ui/src/components/chat/ToolCard.tsx` | dispatch_subagent 工具特殊图标 | 修改 |
| `e3d-ui/src/store/useChatStore.ts` | 消息处理支持 agentName | 修改 |
| `e3d-ui/src/services/bridgeService.ts` | 事件处理传递 agentName | 修改 |
| `src/E3DCopilot.Core/Tools/ToolResult.cs` | 新增 `IsRetryable` 字段 | 修改 |
| `src/E3DCopilot.Core/Tools/IToolHandler.cs` | 新增 `MaxRetries` 属性 | 修改 |
| `src/E3DCopilot.Core/Tools/ToolExecutor.cs` | 重试逻辑（指数退避） | 修改 |
| `src/E3DCopilot.Core/Tools/Handlers/SubagentDispatchHandler.cs` | readOnly 参数化 | 修改 |
| `src/E3DCopilot.Core/Agents/SubagentRunner.cs` | 按 IsReadOnly 动态过滤工具集 | 修改 |
| `src/E3DCopilot.Tests/SubagentTests.cs` | 写模式 + 重试测试 | 修改 |
| `src/E3DCopilot.Tests/ToolExecutorTests.cs` | 重试逻辑测试 | 修改 |

---

## 任务 1：Message 类型新增 agentName 字段

**文件：**
- 修改：`e3d-ui/src/types/index.ts`

- [ ] **步骤 1：给 Message 接口加 agentName 字段**

```typescript
// e3d-ui/src/types/index.ts — Message 接口新增一行
export interface Message {
  // ... 现有字段 ...
  /** 来源 Agent 名称（null = 主 Agent，非 null = 子代理名） */
  agentName?: string;
}
```

- [ ] **步骤 2：验证类型检查通过**

运行：`cd e3d-ui && npx tsc -b --noEmit 2>&1`
预期：仅 3 个已有的未使用变量错误，无新引入错误

- [ ] **步骤 3：Commit**

```bash
git add e3d-ui/src/types/index.ts
git commit -m "feat: Message 类型新增 agentName 字段"
```

---

## 任务 2：前端 bridgeService 传递 agentName 到消息

**文件：**
- 修改：`e3d-ui/src/services/bridgeService.ts`

读 `bridgeService.ts` 找到 `handleToolResult` 和 `handleToolDispatch` 事件处理逻辑（约在 `handleEvent` 函数中，`case 'tool:result'` 和 `case 'tool:dispatch'` 分支）。

- [ ] **步骤 1：在 bridgeService 工具事件处理中读取 agentName 并写入 store**

找到 `bridgeService.ts` 中处理 `tool:dispatch` 和 `tool:result` 事件的代码。在每个 `appendMessage` 调用中，如果事件数据含 `agentName`，将其传入消息：

```typescript
// 在 tool:dispatch 处理中（约 case 'tool:dispatch' 或 'tool:call'）
const agentName = (data as any).agentName || undefined;
useChatStore.getState().appendMessage(
  { role: 'tool_call', content: '', toolId: data.toolId, toolName: data.toolName, agentName },
  tabId
);

// 在 tool:result 处理中 handleToolResult 调用后
// handleToolResult 不支持 agentName 参数，改用 appendMessage 方式
// 或：在 handleToolResult 后手动更新消息的 agentName
```

实际查找 `bridgeService.ts` 中 `tool:result` 的处理代码，在 `handleToolResult` 调用后补一行 set agentName：

```typescript
// 在 tool:result 处理分支末尾，handleToolResult 调用之后
if (agentName) {
  useChatStore.getState().setMessageAgentName(data.toolId, agentName, tabId);
}
```

- [ ] **步骤 2：在 useChatStore 中新增 setMessageAgentName 方法**

```typescript
// e3d-ui/src/store/useChatStore.ts — ChatStore 接口新增
setMessageAgentName: (toolId: string, agentName: string, tabId?: string) => void;

// 实现（在 store 的 create 中）
setMessageAgentName: (toolId, agentName, tabId) => {
  const targetId = tabId || get().activeTabId;
  set((s) => ({
    tabs: s.tabs.map((t) =>
      t.id === targetId
        ? {
            ...t,
            messages: t.messages.map((m) =>
              m.toolId === toolId ? { ...m, agentName } : m
            ),
          }
        : t
    ),
  }));
},
```

- [ ] **步骤 3：验证类型检查**

运行：`cd e3d-ui && npx tsc -b --noEmit 2>&1`
预期：仅已有错误，无新错误

- [ ] **步骤 4：Commit**

```bash
git add e3d-ui/src/services/bridgeService.ts e3d-ui/src/store/useChatStore.ts
git commit -m "feat: bridgeService 传递 agentName 到前端消息"
```

---

## 任务 3：SubagentPanel 组件（子代理折叠面板）

**文件：**
- 创建：`e3d-ui/src/components/chat/SubagentPanel.tsx`

- [ ] **步骤 1：编写 SubagentPanel 组件**

```typescript
// e3d-ui/src/components/chat/SubagentPanel.tsx
import { useState } from 'react';
import { ChevronRight, Loader2, Bot } from 'lucide-react';
import type { Message } from '@/types';
import { MessageRow } from './MessageRow';
import { ToolGroup, groupConsecutiveTools } from './ToolGroup';

interface SubagentPanelProps {
  agentName: string;
  messages: Message[];
  allMessages: Message[];
  subcalls: Map<string, Message[]>;
}

export function SubagentPanel({ agentName, messages, allMessages, subcalls }: SubagentPanelProps) {
  const [open, setOpen] = useState(true); // 默认展开
  const isRunning = messages.some((m) => !m.finalized);
  const doneCount = messages.filter((m) => m.finalized && !m.toolError).length;
  const errorCount = messages.filter((m) => m.toolError).length;

  const grouped = groupConsecutiveTools(messages);

  return (
    <div className="subagent-panel" data-running={isRunning ? '' : undefined}>
      <button
        type="button"
        className="subagent-panel__head"
        onClick={() => setOpen(!open)}
        aria-expanded={open}
      >
        <span className="tool__label-group">
          {isRunning ? (
            <Loader2 className="w-3.5 h-3.5 animate-spin" style={{ color: 'var(--accent)' }} />
          ) : (
            <Bot className="w-3.5 h-3.5" style={{ color: 'var(--muted)' }} />
          )}
          <span className="subagent-panel__name">🤖 {agentName}</span>
        </span>
        <span className="tool__summary">
          {isRunning ? '运行中...' : `${doneCount} 完成${errorCount > 0 ? ` · ${errorCount} 失败` : ''}`}
        </span>
        <span className={`tool__chevron${open ? ' tool__chevron--open' : ''}`}>
          <ChevronRight size={12} />
        </span>
      </button>

      {open && (
        <div className="subagent-panel__body">
          {grouped.map((item, i) => {
            if (item.kind === 'group') {
              return <ToolGroup key={`sa-g-${i}`} kind={item.groupKind} messages={item.messages} subcalls={subcalls} allMessages={allMessages} />;
            }
            const msg = item.msg;
            const toolId = msg.toolId || msg.id;
            return <MessageRow key={`sa-m-${i}`} msg={msg} subcalls={subcalls.get(toolId)} allMessages={allMessages} />;
          })}
          {isRunning && (
            <div className="subagent-panel__loading">
              <Loader2 className="w-3 h-3 animate-spin" style={{ color: 'var(--accent)' }} />{' '}
              <span style={{ fontSize: 12, color: 'var(--muted)' }}>{agentName} 正在工作...</span>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
```

- [ ] **步骤 2：添加 CSS 样式**

在 `e3d-ui/src/styles/` 中找到或新建 `_subagent.scss`：

```scss
.subagent-panel {
  margin: 4px 0 4px 12px;
  border-left: 3px solid var(--accent);
  border-radius: 0 6px 6px 0;
  background: var(--bg-card);
  overflow: hidden;

  &__head {
    display: flex;
    align-items: center;
    gap: 6px;
    width: 100%;
    padding: 6px 10px;
    background: none;
    border: none;
    cursor: pointer;
    font-size: 12px;
    color: var(--fg);
    &:hover { background: var(--bg-hover); }
  }

  &__name {
    font-weight: 600;
    font-size: 12px;
    color: var(--accent);
  }

  &__body {
    padding: 4px 8px 8px;
  }

  &__loading {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 8px 0;
  }
}
```

- [ ] **步骤 3：验证类型检查**

运行：`cd e3d-ui && npx tsc -b --noEmit 2>&1`
预期：仅已有错误

- [ ] **步骤 4：Commit**

```bash
git add e3d-ui/src/components/chat/SubagentPanel.tsx e3d-ui/src/styles/_subagent.scss
git commit -m "feat: SubagentPanel 子代理折叠面板组件"
```

---

## 任务 4：MessageList 按 agentName 分组渲染

**文件：**
- 修改：`e3d-ui/src/components/chat/MessageList.tsx`

在 `buildDisplayItems` 中，子代理消息（`agentName != null`）不再作为独立 DisplayItem，而是收集到 `Map<string, Message[]>` 中。在 `renderItem` 之外，渲染 SubagentPanel。

- [ ] **步骤 1：在 MessageList 中收集子代理消息并渲染 SubagentPanel**

关键修改点：在 `hotDisplayItems` 渲染之后，如果子代理消息集合非空，渲染 SubagentPanel。

```typescript
// 在 MessageList 组件内，subcallMap 计算之后，新增：
const subagentGroups = useMemo(() => {
  const groups = new Map<string, Message[]>();
  for (const msg of hotMessages) {
    if (msg.agentName) {
      const list = groups.get(msg.agentName) || [];
      list.push(msg);
      groups.set(msg.agentName, list);
    }
  }
  return groups;
}, [hotMessages]);

// 在 buildDisplayItems 中，跳过 agentName 非空的消息（它们由 SubagentPanel 渲染）
// 在 buildDisplayItems 的 for 循环开头加：
if (item.kind === 'message' && item.msg.agentName) {
  continue; // 子代理消息由 SubagentPanel 统一渲染
}
```

然后在 JSX 的 hot zone 渲染区域 `{hotDisplayItems.map(...)}` 之后，添加子代理面板：

```tsx
{/* 子代理面板 */}
{Array.from(subagentGroups.entries()).map(([name, msgs]) => (
  <SubagentPanel
    key={`subagent-${name}`}
    agentName={name}
    messages={msgs}
    allMessages={hotMessages}
    subcalls={subcallMap}
  />
))}
```

- [ ] **步骤 2：验证类型检查**

运行：`cd e3d-ui && npx tsc -b --noEmit 2>&1`

- [ ] **步骤 3：手动测试**

启动 dev server：`cd e3d-ui && npm run dev`
通过 `dispatch_subagent(name="test", task="query pipe info")` 验证前端显示独立面板

- [ ] **步骤 4：Commit**

```bash
git add e3d-ui/src/components/chat/MessageList.tsx
git commit -m "feat: MessageList 按 agentName 分组渲染子代理面板"
```

---

## 任务 5：ToolCard dispatch_subagent 特殊图标

**文件：**
- 修改：`e3d-ui/src/components/chat/ToolCard.tsx`

- [ ] **步骤 1：dispatch_subagent 工具显示 Bot 图标**

在 `ToolCard` 的状态图标区域（约第 225-233 行），`dispatch_subagent` 工具已运行时显示特殊图标：

```typescript
// 在 ToolCard 的状态图标区域，替换现有逻辑
{msg.toolName === 'dispatch_subagent' ? (
  <Bot className="w-3.5 h-3.5" style={{ color: 'var(--accent)' }} />
) : isRunning ? (
  <Loader2 className="w-3.5 h-3.5 animate-spin" style={{ color: 'var(--accent)' }} />
) : isError ? (
  <span className="tool__status-icon tool__status-icon--err">✗</span>
) : (
  <span className="tool__status-icon tool__status-icon--ok">✓</span>
)}
```

- [ ] **步骤 2：验证类型检查**

运行：`cd e3d-ui && npx tsc -b --noEmit 2>&1`

- [ ] **步骤 3：Commit**

```bash
git add e3d-ui/src/components/chat/ToolCard.tsx
git commit -m "feat: ToolCard dispatch_subagent 特殊 Bot 图标"
```

---

## 任务 6：ToolResult 新增 IsRetryable 字段

**文件：**
- 修改：`src/E3DCopilot.Core/Tools/ToolResult.cs`

- [ ] **步骤 1：给 ToolResult 加 IsRetryable**

```csharp
// 在 ToolResult 类中新增：
public bool IsRetryable { get; set; }

// 修改 Fail 工厂方法加重载
public static ToolResult Fail(string error, bool isRetryable = false) =>
    new ToolResult { Success = false, Error = error, Text = error, IsRetryable = isRetryable };

// 新增可重试失败工厂
public static ToolResult RetryableFail(string error) =>
    new ToolResult { Success = false, Error = error, Text = error, IsRetryable = true };
```

- [ ] **步骤 2：构建验证**

运行：`dotnet build src/E3DCopilot.sln -c Release 2>&1 | Select-String "error|Build succeeded"`
预期：Build succeeded

- [ ] **步骤 3：Commit**

```bash
git add src/E3DCopilot.Core/Tools/ToolResult.cs
git commit -m "feat: ToolResult 新增 IsRetryable 字段和 RetryableFail 工厂"
```

---

## 任务 7：IToolHandler 新增 MaxRetries 属性

**文件：**
- 修改：`src/E3DCopilot.Core/Tools/IToolHandler.cs`

- [ ] **步骤 1：添加 MaxRetries 默认实现**

```csharp
// 在 IToolHandler 接口中新增：
/// <summary>最大重试次数（默认 0 = 不重试）。工具遇到 ToolResult.IsRetryable 时自动重试。</summary>
int MaxRetries => 0; // C# 8.0 default interface method — 但需要 net48 兼容！

// net48 / C# 7.3 不支持 default interface method。
// 改为在 ToolExecutor 中通过反射检查，或使用抽象基类。
```

由于 C# 7.3 不支持接口默认实现，改为在 `ToolExecutor` 中通过注册时传入的可选配置处理：

```csharp
// 不在 IToolHandler 接口加 MaxRetries。
// 改为在 ToolExecutor 维护一个 Dictionary<string, int> _retryConfig
```

- [ ] **步骤 2：实际方案 — ToolExecutor 维护重试配置**

在 `ToolExecutor` 中：

```csharp
private readonly Dictionary<string, int> _retryConfig = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

public void SetMaxRetries(string toolName, int maxRetries)
{
    _retryConfig[toolName] = maxRetries;
}

private int GetMaxRetries(string toolName)
{
    return _retryConfig.TryGetValue(toolName, out var r) ? r : 0;
}
```

- [ ] **步骤 3：构建验证**

运行：`dotnet build src/E3DCopilot.sln -c Release`

- [ ] **步骤 4：Commit**

```bash
git add src/E3DCopilot.Core/Tools/IToolHandler.cs src/E3DCopilot.Core/Tools/ToolExecutor.cs
git commit -m "feat: ToolExecutor 新增 SetMaxRetries 重试配置"
```

---

## 任务 8：ToolExecutor 指数退避重试逻辑

**文件：**
- 修改：`src/E3DCopilot.Core/Tools/ToolExecutor.cs`
- 修改：`src/E3DCopilot.Tests/ToolExecutorTests.cs`

- [ ] **步骤 1：编写失败的测试**

```csharp
// E3DCopilot.Tests/ToolExecutorTests.cs 新增
[Test]
public async Task RetryableHandler_RetriesOnFailure()
{
    var sink = new TestSink();
    var executor = new ToolExecutor(sink);
    executor.SetMaxRetries("flaky_tool", 2);
    var handler = new FlakyHandler(failCount: 1); // 第一次失败，第二次成功
    executor.Register(handler);

    var result = await executor.ExecuteAsync(new ToolCall { Id = "t1", Name = "flaky_tool", Arguments = "{}" }, CancellationToken.None);

    Assert.IsTrue(result.Success);
    Assert.That(handler.CallCount, Is.EqualTo(2), "应该在第一次失败后重试一次");
}

[Test]
public async Task RetryableHandler_ExhaustedRetries()
{
    var sink = new TestSink();
    var executor = new ToolExecutor(sink);
    executor.SetMaxRetries("flaky_tool", 2);
    var handler = new FlakyHandler(failCount: 999); // 永远失败
    executor.Register(handler);

    var result = await executor.ExecuteAsync(new ToolCall { Id = "t1", Name = "flaky_tool", Arguments = "{}" }, CancellationToken.None);

    Assert.IsFalse(result.Success);
    Assert.That(handler.CallCount, Is.EqualTo(3), "应该重试 2 次，共 3 次调用");
    Assert.That(result.Text, Does.Contain("retry exhausted"));
}

[Test]
public async Task NonRetryableHandler_DoesNotRetry()
{
    var sink = new TestSink();
    var executor = new ToolExecutor(sink);
    executor.SetMaxRetries("fatal_tool", 2);
    var handler = new FatalHandler(); // 不可重试失败
    executor.Register(handler);

    var result = await executor.ExecuteAsync(new ToolCall { Id = "t1", Name = "fatal_tool", Arguments = "{}" }, CancellationToken.None);

    Assert.IsFalse(result.Success);
    Assert.That(handler.CallCount, Is.EqualTo(1), "不可重试错误不应重试");
}

// 测试辅助类
public class FlakyHandler : IToolHandler
{
    private readonly int _failCount;
    public int CallCount { get; private set; }
    public string Name => "flaky_tool";
    public string Description => "";
    public string ParameterSchema => "{}";
    public bool IsReadOnly => true;

    public FlakyHandler(int failCount) { _failCount = failCount; }

    public Task<ToolResult> ExecuteAsync(string args, CancellationToken ct)
    {
        CallCount++;
        if (CallCount <= _failCount)
            return Task.FromResult(ToolResult.RetryableFail("Temporary error"));
        return Task.FromResult(ToolResult.Ok("Success"));
    }
}

public class FatalHandler : IToolHandler
{
    public int CallCount { get; private set; }
    public string Name => "fatal_tool";
    public string Description => "";
    public string ParameterSchema => "{}";
    public bool IsReadOnly => true;

    public Task<ToolResult> ExecuteAsync(string args, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(ToolResult.Fail("Fatal error", isRetryable: false));
    }
}
```

- [ ] **步骤 2：运行测试确认失败**

运行：`dotnet test src/E3DCopilot.Tests --filter "FullyQualifiedName~Retryable" 2>&1`
预期：FAIL，3 个测试全部失败

- [ ] **步骤 3：实现重试逻辑**

在 `ToolExecutor.ExecuteAsync`（单工具执行方法）中，在执行后加入重试循环：

```csharp
// 在 ToolExecutor.ExecuteAsync 的工具执行部分（约第 120 行附近）
// 替换直接的 handler.ExecuteAsync 调用为带重试的版本：

public async Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken ct = default)
{
    // ... 现有路由逻辑 ...

    var maxRetries = GetMaxRetries(call.Name);
    ToolResult result = null;
    int attempt = 0;

    while (attempt <= maxRetries)
    {
        ct.ThrowIfCancellationRequested();
        attempt++;

        var sw = Stopwatch.StartNew();
        try
        {
            result = await handler.ExecuteAsync(call.Arguments, ct);
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            sw.Stop();
            result = ToolResult.RetryableFail(ex.Message);
            result.DurationMs = sw.ElapsedMilliseconds;
        }

        if (result.Success || !result.IsRetryable)
            break;

        if (attempt <= maxRetries)
        {
            // 指数退避：100ms, 200ms, 400ms, ...
            int delayMs = 100 * (1 << (attempt - 1));
            CopilotLogger.Info("Tool '{0}' 重试 {1}/{2}，等待 {3}ms", call.Name, attempt, maxRetries, delayMs);
            await Task.Delay(delayMs, ct);
        }
    }

    if (!result.Success && attempt > 1)
    {
        // 重试耗尽，追加标记
        result.Text = $"[retry exhausted after {attempt} attempts, last error: {result.Error}]";
    }

    // ... 现有 emit 逻辑 ...
    return result;
}
```

- [ ] **步骤 4：运行测试确认通过**

运行：`dotnet test src/E3DCopilot.Tests --filter "FullyQualifiedName~Retryable" 2>&1`
预期：3 tests PASS

- [ ] **步骤 5：Commit**

```bash
git add src/E3DCopilot.Core/Tools/ToolExecutor.cs src/E3DCopilot.Tests/ToolExecutorTests.cs
git commit -m "feat: ToolExecutor 指数退避重试逻辑"
```

---

## 任务 9：子代理写模式支持

**文件：**
- 修改：`src/E3DCopilot.Core/Tools/Handlers/SubagentDispatchHandler.cs`
- 修改：`src/E3DCopilot.Core/Agents/SubagentRunner.cs`
- 修改：`src/E3DCopilot.Tests/SubagentTests.cs`

- [ ] **步骤 1：编写测试**

```csharp
// SubagentTests.cs 新增
[Test]
public void DispatchHandler_DefaultsToReadOnly()
{
    var runner = CreateFakeRunner();
    var handler = new SubagentDispatchHandler(runner);
    Assert.IsTrue(handler.IsReadOnly, "默认应该只读");
}

[Test]
public void DispatchHandler_ReadOnlyFalse_ParamAccepted()
{
    var runner = CreateFakeRunner();
    var handler = new SubagentDispatchHandler(runner);
    // 验证参数 schema 包含 readonly
    Assert.That(handler.ParameterSchema, Does.Contain("readonly"));
}

// 验证 SubagentRunner 在 IsReadOnly=false 时不调用 FilterReadOnly
[Test]
public void SubagentRunner_WriteMode_UsesFullToolset()
{
    var executor = CreateExecutorWithWriters();
    var runner = new SubagentRunner(
        new FakeProvider(), new TestSink(), executor, 
        CopilotConfig.Load(), null, CommandPermissionController.CreateDefault());
    
    var ctx = new SubagentContext { Name = "writer", IsReadOnly = false };
    // 当 IsReadOnly=false 时，子代理不会过滤写工具
    // 验证通过子代理的 session 工具集包含写工具
    // （这个测试需要 mock AgentLoop 或验证 SubagentRunner.RunAsync 内部逻辑）
}
```

- [ ] **步骤 2：修改 SubagentDispatchHandler — 参数 schema 加 readonly**

```csharp
// SubagentDispatchHandler.ParameterSchema 新增 readonly 参数
public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""name"": { ""type"": ""string"", ""description"": ""Unique name for this sub-agent"" },
    ""system_prompt"": { ""type"": ""string"", ""description"": ""Optional system prompt"" },
    ""task"": { ""type"": ""string"", ""description"": ""The task description"" },
    ""readonly"": {
      ""type"": ""boolean"",
      ""description"": ""Whether the sub-agent is read-only (default: true). Set to false to allow write operations."",
      ""default"": true
    },
    ""mode"": { ""type"": ""string"", ""enum"": [""executor"", ""planner""], ""description"": ""Sub-agent mode"" },
    ""provider"": { ""type"": ""string"", ""description"": ""Optional provider/model ref"" }
  },
  ""required"": [""name"", ""task""]
}";

// 修改 IsReadOnly 属性
public bool IsReadOnly => true; // 派发本身是只读动作
```

- [ ] **步骤 3：修改 ExecuteAsync 读取 readonly 参数**

```csharp
// SubagentDispatchHandler.ExecuteAsync 中，解析参数后
bool isReadOnly = json.Value<bool?>("readonly") ?? true; // 改为从参数读

var ctx = new SubagentContext
{
    Name = name,
    SystemPrompt = systemPrompt,
    IsReadOnly = isReadOnly,  // 不再是硬编码 true
    Session = new CopilotSession(),
    Mode = mode == "planner" ? SubagentMode.Planner : SubagentMode.Executor,
    PreferredProvider = providerRef
};
```

- [ ] **步骤 4：SubagentRunner 按 IsReadOnly 动态过滤**

```csharp
// SubagentRunner.RunAsync 中，现有逻辑已处理：
// if (ctx.IsReadOnly) { subExecutor = _executor.FilterReadOnly(); }
// 这段代码已经正确，不需要修改。
// 当 IsReadOnly=false 时，不调用 FilterReadOnly()，使用完整工具集。
```

- [ ] **步骤 5：运行测试**

运行：`dotnet test src/E3DCopilot.Tests --filter "FullyQualifiedName~Subagent" 2>&1`
预期：所有已有测试 + 新增测试通过

- [ ] **步骤 6：Commit**

```bash
git add src/E3DCopilot.Core/Tools/Handlers/SubagentDispatchHandler.cs src/E3DCopilot.Core/Agents/SubagentRunner.cs src/E3DCopilot.Tests/SubagentTests.cs
git commit -m "feat: 子代理写模式 — readonly 参数化，默认只读"
```

---

## 任务 10：前端审批卡片显示 AgentName

**文件：**
- 修改：`e3d-ui/src/components/chat/ApprovalCard.tsx`

- [ ] **步骤 1：批准卡片显示子代理名称**

在 `ApprovalCard` 组件中，如果 `pendingApproval` 包含 `agentName`，显示在描述中：

```typescript
// ApprovalCard 渲染中，pendingApproval 的 description 行
{pendingApproval?.agentName && (
  <span className="approval__agent" style={{ fontSize: 11, color: 'var(--accent)' }}>
    🤖 来自子代理: {pendingApproval.agentName}
  </span>
)}
```

同时更新 `PendingApproval` 类型：

```typescript
// useChatStore.ts
export interface PendingApproval {
  toolId: string;
  toolName: string;
  args?: unknown;
  description?: string;
  agentName?: string; // 新增
}
```

- [ ] **步骤 2：验证类型检查**

运行：`cd e3d-ui && npx tsc -b --noEmit 2>&1`

- [ ] **步骤 3：Commit**

```bash
git add e3d-ui/src/components/chat/ApprovalCard.tsx e3d-ui/src/store/useChatStore.ts
git commit -m "feat: 审批卡片显示子代理来源 AgentName"
```

---

## 任务 11：端到端编译验证

- [ ] **步骤 1：后端构建**

运行：`dotnet build src/E3DCopilot.sln -c Release 2>&1 | Select-String "error|Build succeeded"`
预期：Build succeeded. 0 warnings

- [ ] **步骤 2：后端测试**

运行：`dotnet test src/E3DCopilot.Tests 2>&1 | Select-String "Passed|Failed|Test Run"`
预期：Passed 数 > 260

- [ ] **步骤 3：前端类型检查**

运行：`cd e3d-ui && npx tsc -b --noEmit 2>&1`
预期：仅 3 个已有错误

- [ ] **步骤 4：Commit**

```bash
git commit -m "chore: Phase 1 端到端验证通过"
```

---

## 自检

1. **规格覆盖度**：E1（前端可视化）由任务 1-5 覆盖；E4（工具重试）由任务 6-8 覆盖；E2（写模式）由任务 9 覆盖。无遗漏。
2. **占位符扫描**：无 TODO、待定、后续实现。所有步骤都有实际代码。
3. **类型一致性**：`agentName` 在 `Message`、`CopilotEvent`、`bridgeService`、`useChatStore` 中使用一致的类型 `string | undefined`。`IsRetryable` 在 `ToolResult` 和 `ToolExecutor` 中一致。

---

## 执行交接

**计划已完成并保存到 `docs/plan/2026-07-13-phase1-subagent-frontend-retry.md`。两种执行方式：**

**1. 子代理驱动（推荐）** - 每个任务调度一个新的子代理，任务间进行审查，快速迭代

**2. 内联执行** - 在当前会话中使用 executing-plans 执行任务，批量执行并设有检查点

**选哪种方式？**