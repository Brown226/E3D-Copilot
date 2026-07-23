# 集成 ProgressGuard + Mermaid 渲染 实现计划 (v2)

> **面向 AI 代理的工作者:** 必需子技能:使用 superpowers:subagent-driven-development 或 superpowers:executing-plans 逐任务实现。步骤使用复选框(`- [ ]`)语法跟踪进度。

**目标:** 
1. **模块 A**:把已实现但孤立的 `ProgressGuard`(8轮nudge/16轮pause/精确重复检测,对齐 Reasonix SPEC 5)集成到 `AgentLoop`,并补单元测试。**不新建 ProgressLease,不修改 EvidenceLedger**。
2. **模块 B**:Mermaid 图表渲染(同 v1 计划,无变更)。

**架构:**
- 模块 A:`ProgressGuard` 已存在于 `src/E3DCopilot.Core/Agents/ProgressGuard.cs`,仅缺:(1)单元测试,(2)被 `AgentLoop` 调用。集成方式:AgentLoop 持有 ProgressGuard 实例,turn 开始 FullReset,工具执行后调 RecordToolCompletion,当 call.Name=="todo_write" 时解析 in_progress 项调 SetActiveTodo。纯 C# 7.3,只改 1 个现有文件 + 新建 1 个测试文件。
- 模块 B:同 v1(React MermaidDiagram 组件 + MarkdownBlock 集成)。

**v1 → v2 变更原因:** v1 计划新建 `ProgressLease` + 改 `EvidenceLedger`,但审查发现工作区已有更贴近 Reasonix 的 `ProgressGuard`(动作签名判定 vs 证据计数判定,前者更准确)。经用户决策,改为集成现有 ProgressGuard,避免重复造轮子。

**技术栈:** C# 7.3 / .NET Framework 4.8 / NUnit | React 18 / Vite / Vitest

---

## 全局约束

- .NET Framework 4.8 / C# 7.3 — 禁用 `IAsyncEnumerable`/record/target-typed `new()`
- 内网/离线优先 — Mermaid 本地打包,不走 CDN
- Surgical changes — 只触碰必要文件
- TDD — 先写测试

---

## 文件结构总览

### 模块 A:集成 ProgressGuard(C# 后端)

| 文件路径 | 改动 | 职责 |
|----------|------|------|
| `src/E3DCopilot.Tests/ProgressGuardTests.cs` | **新建** | 为已存在的 ProgressGuard 补 characterization 测试 |
| `src/E3DCopilot.Core/AgentLoop.cs` | 修改 | 注入 ProgressGuard,turn 重置,工具执行后调用 RecordToolCompletion/SetActiveTodo |

### 模块 B:Mermaid 渲染(React 前端,同 v1)

| 文件路径 | 改动 | 职责 |
|----------|------|------|
| `e3d-ui/package.json` | 修改 | 加 mermaid 依赖 |
| `e3d-ui/src/components/common/MermaidDiagram.tsx` | **新建** | Mermaid 渲染组件 |
| `e3d-ui/src/components/common/MarkdownBlock.tsx` | 修改 | CodeBlock 识别 mermaid 分流 |
| `e3d-ui/src/__tests__/components/MermaidDiagram.test.tsx` | **新建** | 测试 |

---

## 模块 A:集成 ProgressGuard

### 任务 A1:为 ProgressGuard 补单元测试(TDD — characterization)

**背景:** `ProgressGuard.cs` 已实现但无测试。先写测试描述并锁定其现有行为,为集成提供安全网。

**文件:**
- 创建:`src/E3DCopilot.Tests/ProgressGuardTests.cs`

- [ ] **步骤 A1.1:编写测试(基于现有 ProgressGuard 实现)**

创建 `src/E3DCopilot.Tests/ProgressGuardTests.cs`:

```csharp
using E3DCopilot.Core.Agents;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class ProgressGuardTests
    {
        [Test]
        public void RecordToolCompletion_NoActiveTodo_ReturnsNull()
        {
            // 无活跃 todo 时不监控
            var guard = new ProgressGuard();
            var msg = guard.RecordToolCompletion("query", "result1");
            Assert.IsNull(msg);
        }

        [Test]
        public void RecordToolCompletion_ActiveTodo_NewAction_ReturnsNull()
        {
            var guard = new ProgressGuard();
            guard.SetActiveTodo(true);
            // 首次动作,无历史 → 有进度
            var msg = guard.RecordToolCompletion("query", "result1");
            Assert.IsNull(msg);
        }

        [Test]
        public void RecordToolCompletion_ActiveTodo_ExactRepeat_DoesNotRenew()
        {
            var guard = new ProgressGuard();
            guard.SetActiveTodo(true);
            guard.RecordToolCompletion("query", "result1");
            // 精确重复 → 不算进度
            var msg = guard.RecordToolCompletion("query", "result1");
            // 1 次重复,不到 8 轮阈值 → null
            Assert.IsNull(msg);
        }

        [Test]
        public void RecordToolCompletion_AtNudgeThreshold_ReturnsNudgeMessage()
        {
            var guard = new ProgressGuard();
            guard.SetActiveTodo(true);
            // 首次动作有进度,后续 8 次精确重复 → 第 8 次触发 nudge
            guard.RecordToolCompletion("query", "result1");
            string msg = null;
            for (int i = 0; i < 8; i++)
                msg = guard.RecordToolCompletion("query", "result1");
            Assert.IsNotNull(msg);
            Assert.IsTrue(msg.Contains("progress guard") || msg.Contains("Reassess") || msg.Contains("reassess"));
        }

        [Test]
        public void RecordToolCompletion_AtPauseThreshold_ReturnsPauseMessage()
        {
            var guard = new ProgressGuard();
            guard.SetActiveTodo(true);
            guard.RecordToolCompletion("query", "result1");
            string msg = null;
            for (int i = 0; i < 16; i++)
                msg = guard.RecordToolCompletion("query", "result1");
            // 第 16 次触发 pause
            Assert.IsNotNull(msg);
            Assert.IsTrue(msg.Contains("PAUSED") || msg.Contains("paused") || msg.Contains("Pause"));
            Assert.IsTrue(guard.IsPaused);
        }

        [Test]
        public void RecordToolCompletion_NewActionAfterRepeats_RenewsLease()
        {
            var guard = new ProgressGuard();
            guard.SetActiveTodo(true);
            guard.RecordToolCompletion("query", "result1");
            // 5 次重复(不到 nudge 阈值)
            for (int i = 0; i < 5; i++)
                guard.RecordToolCompletion("query", "result1");
            // 新动作 → 续约
            var msg = guard.RecordToolCompletion("modify", "different-result");
            Assert.IsNull(msg);
            // 计数器应重置:再走 7 次重复不应触发 nudge(总共 7 < 8)
            for (int i = 0; i < 7; i++)
            {
                msg = guard.RecordToolCompletion("modify", "different-result");
                Assert.IsNull(msg, $"after renew step {i} should not nudge");
            }
        }

        [Test]
        public void FullReset_ClearsAllState()
        {
            var guard = new ProgressGuard();
            guard.SetActiveTodo(true);
            guard.RecordToolCompletion("query", "result1");
            for (int i = 0; i < 10; i++)
                guard.RecordToolCompletion("query", "result1");
            guard.FullReset();
            Assert.IsFalse(guard.IsPaused);
            // 重置后首次动作应正常(无进度历史)
            var msg = guard.RecordToolCompletion("query", "result1");
            Assert.IsNull(msg);
        }

        [Test]
        public void SetActiveTodo_False_StopsMonitoring()
        {
            var guard = new ProgressGuard();
            guard.SetActiveTodo(true);
            guard.SetActiveTodo(false);
            // 无活跃 todo → 不监控
            var msg = guard.RecordToolCompletion("query", "result1");
            Assert.IsNull(msg);
        }

        [Test]
        public void Resume_AfterPause_ClearsPausedState()
        {
            var guard = new ProgressGuard();
            guard.SetActiveTodo(true);
            guard.RecordToolCompletion("query", "result1");
            for (int i = 0; i < 16; i++)
                guard.RecordToolCompletion("query", "result1");
            Assert.IsTrue(guard.IsPaused);
            guard.Resume();
            Assert.IsFalse(guard.IsPaused);
        }
    }
}
```

- [ ] **步骤 A1.2:运行测试验证通过(ProgressGuard 已实现)**

运行:`dotnet test src/E3DCopilot.Tests --filter "FullyQualifiedName~ProgressGuardTests"`
预期:PASS — 9 个测试全绿(ProgressGuard 已实现,测试为 characterization 性质)。

**注意:** 如有测试失败,说明 ProgressGuard 实际行为与预期不符。**不要修改 ProgressGuard.cs**(它是预存已提交代码),而是修正测试以匹配实际行为,并在自审中报告差异。

- [ ] **步骤 A1.3:Commit**

```bash
git add src/E3DCopilot.Tests/ProgressGuardTests.cs
git commit -m "test(core): add characterization tests for ProgressGuard"
```

---

### 任务 A2:AgentLoop 集成 ProgressGuard

**文件:**
- 修改:`src/E3DCopilot.Core/AgentLoop.cs`

- [ ] **步骤 A2.1:在 AgentLoop 字段区添加 ProgressGuard**

打开 `src/E3DCopilot.Core/AgentLoop.cs`,找到字段区(约 59 行 `_evidence` 附近),添加:

```csharp
        // ── 进度守卫(对齐 Reasonix v1.17.19 TodoProgressMonitor / SPEC 5) ──
        // ProgressGuard 已实现于 Agents/ProgressGuard.cs,此处集成
        private readonly Agents.ProgressGuard _progressGuard;
```

- [ ] **步骤 A2.2:在构造函数初始化 ProgressGuard**

在构造函数末尾(`_compactor = ...` 后,约 83 行)添加:

```csharp
            _progressGuard = new Agents.ProgressGuard(_sink);
```

- [ ] **步骤 A2.3:在 RunAsync turn 重置块加 FullReset**

找到 RunAsync 中每 turn 重置状态块(约 210 行 `_evidence?.Reset();`),在该块末尾追加:

```csharp
            _progressGuard?.FullReset();
```

- [ ] **步骤 A2.4:在 ExecuteOneAsync 工具执行后调用 ProgressGuard**

找到 `ExecuteOneAsync` 方法中工具执行成功后的 evidence.Record 调用(约 633 行):

```csharp
                // Record evidence（对齐 Reasonix evidence.Record）
                _evidence?.Record(call.Name, call.Arguments, toolResult.Success, !IsWriteTool(call.Name));
```

在其**之后**插入 ProgressGuard 集成逻辑:

```csharp
                // ── 进度守卫集成（对齐 Reasonix SPEC 5 adaptive progress lease）──
                if (toolResult.Success)
                {
                    // todo_write 成功时:解析是否有 in_progress 项,激活/停用监控
                    if (call.Name == "todo_write")
                    {
                        bool hasInProgress = ParseTodosHasInProgress(call.Arguments);
                        _progressGuard?.SetActiveTodo(hasInProgress);
                    }

                    // 所有工具执行后:评估进度,返回非 null 时注入 nudge/pause
                    string progressMsg = _progressGuard?.RecordToolCompletion(
                        call.Name,
                        toolResult.Text?.Length > 200 ? toolResult.Text.Substring(0, 200) : toolResult.Text);
                    if (!string.IsNullOrEmpty(progressMsg))
                    {
                        session.AddSystemMessage(progressMsg);
                        _sink?.Emit(CopilotEvent.Notice(progressMsg.Contains("PAUSED")
                            ? "进度守卫: 连续 16 轮无新进度,已暂停"
                            : "进度守卫: 连续 8 轮无新进度,已注入重评估提示"));
                        _tracer?.RecordSystemEvent(progressMsg.Contains("PAUSED")
                            ? "进度 pause: 16 轮无新进度"
                            : "进度 nudge: 8 轮无新进度");
                    }
                }
```

- [ ] **步骤 A2.5:添加 ParseTodosHasInProgress 辅助方法**

在 `AgentLoop` 类中(建议放在 `IsWriteTool` 方法附近,约 1100 行)添加:

```csharp
        /// <summary>
        /// 解析 todo_write 参数,判断是否有 in_progress 项。
        /// 用于激活 ProgressGuard 监控。
        /// </summary>
        private static bool ParseTodosHasInProgress(string args)
        {
            try
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(args ?? "{}");
                var todos = json["todos"] as Newtonsoft.Json.Linq.JArray;
                if (todos == null) return false;
                foreach (var t in todos)
                {
                    string status = t["status"]?.Value<string>();
                    if (status == "in_progress") return true;
                }
                return false;
            }
            catch { return false; }
        }
```

- [ ] **步骤 A2.6:编译验证**

运行:`dotnet build src/E3DCopilot.sln -c Release`
预期:编译成功,0 错误。

- [ ] **步骤 A2.7:运行全部测试验证无回归**

运行:`dotnet test src/E3DCopilot.Tests`
预期:全部测试通过,包括 ProgressGuardTests 9 项,且现有测试不回归。

- [ ] **步骤 A2.8:Commit**

```bash
git add src/E3DCopilot.Core/AgentLoop.cs
git commit -m "feat(core): integrate ProgressGuard into AgentLoop for stalled-progress detection"
```

---

## 模块 B:Mermaid 图表渲染

### 任务 B1:MermaidDiagram 组件(TDD)

**文件:**
- 创建:`e3d-ui/src/components/common/MermaidDiagram.tsx`
- 创建:`e3d-ui/src/__tests__/components/MermaidDiagram.test.tsx`
- 修改:`e3d-ui/package.json`

- [ ] **步骤 B1.1:安装 mermaid 依赖**

```bash
cd e3d-ui
npm install mermaid@^11
```

- [ ] **步骤 B1.2:编写失败的测试**

创建 `e3d-ui/src/__tests__/components/MermaidDiagram.test.tsx`:

```tsx
import { describe, it, expect, vi } from 'vitest'
import { render } from '@testing-library/react'

vi.mock('mermaid', () => ({
  default: {
    initialize: vi.fn(),
    render: vi.fn(async (id: string, _code: string) => ({
      svg: '<svg data-testid="mock-svg"></svg>',
      bindFunctions: vi.fn(),
    })),
  },
}))

import MermaidDiagram from '@/components/common/MermaidDiagram'

describe('MermaidDiagram', () => {
  it('renders mermaid code as SVG', async () => {
    const code = 'graph TD\n  A --> B'
    const { container, findByTestId } = render(<MermaidDiagram code={code} />)
    const svg = await findByTestId('mock-svg')
    expect(svg).toBeTruthy()
    expect(container.querySelector('.mermaid-container')).toBeTruthy()
  })

  it('shows error message on invalid mermaid syntax', async () => {
    const { Mermaid } = await import('mermaid')
    ;(Mermaid.render as any).mockRejectedValueOnce(new Error('parse error'))
    const code = 'invalid syntax @@@'
    const { findByText } = render(<MermaidDiagram code={code} />)
    const errEl = await findByText(/mermaid 渲染失败/i)
    expect(errEl).toBeTruthy()
  })
})
```

- [ ] **步骤 B1.3:运行测试验证失败**

```bash
cd e3d-ui
npm run test -- MermaidDiagram
```
预期:FAIL — `Cannot find module '@/components/common/MermaidDiagram'`。

- [ ] **步骤 B1.4:实现 MermaidDiagram 组件**

创建 `e3d-ui/src/components/common/MermaidDiagram.tsx`:

```tsx
/**
 * MermaidDiagram — Mermaid 图表渲染组件
 *
 * 职责:
 * - 接收 mermaid 源码,异步渲染为 SVG
 * - 懒初始化 mermaid(仅首次渲染时 initialize)
 * - 渲染失败时显示错误提示,不阻塞其他 Markdown 内容
 * - SSR 安全:mermaid 依赖 DOM,只在 useEffect 中调用
 *
 * 内网约束:mermaid 库本地打包,不走 CDN
 */
import { useEffect, useRef, useState } from 'react'
import mermaid from 'mermaid'

let initialized = false

interface MermaidDiagramProps {
  code: string
  id?: string
}

export default function MermaidDiagram({ code, id }: MermaidDiagramProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const [svgHtml, setSvgHtml] = useState<string>('')
  const [error, setError] = useState<string>('')

  useEffect(() => {
    if (!initialized) {
      mermaid.initialize({
        startOnLoad: false,
        theme: 'default',
        securityLevel: 'strict',
      })
      initialized = true
    }

    const renderId = id ?? `mmd-${Math.random().toString(36).slice(2, 10)}`
    let cancelled = false

    mermaid
      .render(renderId, code)
      .then(({ svg }) => {
        if (!cancelled) {
          setSvgHtml(svg)
          setError('')
        }
      })
      .catch((err: Error) => {
        if (!cancelled) {
          setError(err?.message ?? 'unknown error')
          setSvgHtml('')
        }
      })

    return () => { cancelled = true }
  }, [code, id])

  if (error) {
    return (
      <div className="mermaid-error text-xs text-red-500 border border-red-300 rounded p-2 my-1">
        mermaid 渲染失败: {error}
        <pre className="mt-1 text-[10px] text-slate-500 whitespace-pre-wrap">{code}</pre>
      </div>
    )
  }

  return (
    <div
      ref={containerRef}
      className="mermaid-container my-2 overflow-x-auto"
      dangerouslySetInnerHTML={svgHtml ? { __html: svgHtml } : undefined}
    >
      {!svgHtml && <div className="text-xs text-slate-400">渲染中...</div>}
    </div>
  )
}
```

- [ ] **步骤 B1.5:运行测试验证通过**

```bash
cd e3d-ui
npm run test -- MermaidDiagram
```
预期:PASS — 2 个测试通过。

- [ ] **步骤 B1.6:Commit**

```bash
cd e3d-ui
git add package.json package-lock.json src/components/common/MermaidDiagram.tsx src/__tests__/components/MermaidDiagram.test.tsx
git commit -m "feat(ui): add MermaidDiagram component with async SVG rendering"
```

---

### 任务 B2:MarkdownBlock 集成 Mermaid

**文件:**
- 修改:`e3d-ui/src/components/common/MarkdownBlock.tsx`

- [ ] **步骤 B2.1:在 CodeBlock 中识别 mermaid 语言**

打开 `e3d-ui/src/components/common/MarkdownBlock.tsx`,在顶部 import 区添加:

```tsx
import MermaidDiagram from './MermaidDiagram'
```

找到 `CodeBlock` 组件(约 153-179 行),替换为:

```tsx
const CodeBlock = ({ children, className, ...rest }: React.HTMLAttributes<HTMLElement>) => {
  const isBlock = className?.includes('hljs')
  const langMatch = className?.match(/\blang-(\w+)/)
  const lang = langMatch?.[1] ?? ''

  // Mermaid 代码块:分流到 MermaidDiagram
  if (lang === 'mermaid') {
    const codeStr = typeof children === 'string'
      ? children
      : Array.isArray(children)
        ? children.filter(c => typeof c === 'string').join('')
        : ''
    if (codeStr.trim()) {
      return <MermaidDiagram code={codeStr.trim()} />
    }
  }

  if (isBlock) {
    return (
      <div className="relative group">
        {lang && (
          <span className="absolute top-2 right-12 text-[10px] font-mono text-[var(--fg-faint)] uppercase select-none z-10">
            {lang.toUpperCase()}
          </span>
        )}
        <code className={className} {...rest}>{children}</code>
      </div>
    )
  }
  return <code className="md-code" {...rest}>{children}</code>
}
```

- [ ] **步骤 B2.2:追加集成测试**

在 `e3d-ui/src/__tests__/components/MermaidDiagram.test.tsx` 末尾追加:

```tsx
import MarkdownBlock from '@/components/common/MarkdownBlock'

describe('MarkdownBlock mermaid integration', () => {
  it('renders mermaid code block as MermaidDiagram', async () => {
    const md = '```mermaid\ngraph TD\n  A --> B\n```'
    const { findByTestId } = render(<MarkdownBlock markdown={md} />)
    const svg = await findByTestId('mock-svg')
    expect(svg).toBeTruthy()
  })

  it('does not render normal code blocks as mermaid', async () => {
    const md = '```javascript\nconst x = 1\n```'
    const { container } = render(<MarkdownBlock markdown={md} />)
    expect(container.querySelector('.mermaid-container')).toBeNull()
  })
})
```

- [ ] **步骤 B2.3:运行测试 + typecheck + lint**

```bash
cd e3d-ui
npm run test -- MermaidDiagram
npm run typecheck
npm run lint
```
预期:4 个测试通过,typecheck 无错误,lint 无错误。

- [ ] **步骤 B2.4:Commit**

```bash
cd e3d-ui
git add src/components/common/MarkdownBlock.tsx src/__tests__/components/MermaidDiagram.test.tsx
git commit -m "feat(ui): route mermaid code blocks in MarkdownBlock to MermaidDiagram"
```

---

## 自检

### 1. 规格覆盖度

| 需求 | 任务 |
|------|------|
| 进度监控 8 轮 nudge | A2(集成 ProgressGuard,其内部已实现) |
| 进度监控 16 轮暂停 | A2(同上) |
| 精确重复不算进度 | A2(ProgressGuard 已实现) |
| 仅在有 active todo 时监控 | A2(SetActiveTodo + ParseTodosHasInProgress) |
| 为 ProgressGuard 补测试 | A1 |
| Mermaid 渲染 | B1 + B2 |

### 2. 占位符扫描
无 TODO/待定。所有代码块完整。

### 3. 类型一致性
- `ProgressGuard.RecordToolCompletion(string, string)` 在 A2.4 调用 ✅
- `ProgressGuard.SetActiveTodo(bool)` 在 A2.4 调用 ✅
- `ProgressGuard.FullReset()` 在 A2.3 调用 ✅
- `ParseTodosHasInProgress(string)` 在 A2.4 定义并调用 ✅

### 4. 约束检查
- ✅ 纯 C# 7.3
- ✅ Mermaid 本地打包
- ✅ Surgical:模块 A 仅改 AgentLoop.cs 1 个文件 + 新建 1 个测试文件
- ✅ TDD:A1 先写测试锁定现有行为,A2 集成后运行验证无回归

---

## 执行交接

计划已完成,保存于 `docs/superpowers/plans/2026-07-23-todo-progress-and-mermaid.md`(v2 覆盖 v1)。

模块 A 和 B 文件不重叠,但因 skill 红线"不并行分派实现子智能体",建议串行:先 A 后 B。
