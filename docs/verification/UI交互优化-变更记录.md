# E小智 UI 交互优化变更记录

> **日期**: 2026-06-24
> **范围**: e3d-ui 前端交互层全面优化（P1/P2/P0 三阶段）
> **场景**: E3D 内网环境右侧长条状插件面板（类 VSCode 插件尺寸）
> **参考**: DeepSeek-Reasonix-desktop-v1.11.1 前端设计

---

## 一、变更总览

| 阶段 | 功能 | 涉及文件 | 状态 |
|------|------|----------|------|
| P1 | 工具模式切换按钮（询问/自动/Yolo） | InputBar.tsx, useChatStore.ts, bridgeService.ts | ✅ 完成 |
| P2 | 流式状态显示（token 计数 + 时间统计） | InputBar.tsx, useChatStore.ts, bridgeService.ts, index.css | ✅ 完成 |
| P2 | 思考过程和工具调用展示简化 | AssistantBubble.tsx, ToolCard.tsx, ToolGroup.tsx, index.css | ✅ 完成 |
| P0 | IME 中文输入安全处理 | InputBar.tsx | ✅ 完成 |
| P0 | Composer 高度拖拽调整 | InputBar.tsx, index.css | ✅ 完成 |
| P0 | Prompt 历史导航（↑↓） | InputBar.tsx | ✅ 完成 |
| P0 | 工具审批内联卡片 | ApprovalCard.tsx（新建）, MessageList.tsx | ✅ 完成 |
| P0 | 消息分层折叠（Hot/Warm/Cold） | MessageList.tsx | ✅ 完成 |
| P0 | Compact 步骤折叠 | MessageList.tsx | ✅ 完成 |

**构建验证**: `npx vite build` → ✓ 2102 modules transformed, 0 errors

---

## 二、P1 — 工具模式切换按钮

### 2.1 需求
在底部输入栏添加三段式工具审批模式切换器，支持「询问 / 自动 / Yolo」三种模式。

### 2.2 实现

**useChatStore.ts** — 新增状态与动作：
```typescript
export type ToolApprovalMode = 'ask' | 'auto' | 'yolo';

// 状态
toolApprovalMode: ToolApprovalMode;  // 默认 'ask'

// 动作
setToolApprovalMode: (mode: ToolApprovalMode) => void;
// 切换时通过 bridge.send('user:set_approval_mode', { mode }) 通知后端
```

**InputBar.tsx** — 三段切换器 UI：
- 使用 `Shield` / `ShieldCheck` / `ShieldAlert` 图标区分三种模式
- 滑块动画：`translateX` 随 `approvalIndex` 平移，`transition-all duration-200`
- 三种模式视觉区分：
  - ask：中性灰底
  - auto：绿色（success 语义）
  - yolo：红色（error 语义）
- 窄面板适配：标签文字在 `< 420px` 时隐藏，仅显示图标
- `minWidth: 180px` 确保触摸友好

**bridgeService.ts** — 后端通知：
- `setToolApprovalMode` 内调用 `bridge.send('user:set_approval_mode', { mode })`

### 2.3 交互细节
| 模式 | 图标 | 颜色 | 行为 |
|------|------|------|------|
| 询问 | Shield | 灰 | 每次工具调用前弹出审批卡片 |
| 自动 | ShieldCheck | 绿 | 只读操作自动执行，写操作仍需确认 |
| Yolo | ShieldAlert | 红 | 所有操作全自动执行，无需确认 |

---

## 三、P2 — 流式状态显示优化

### 3.1 需求
在流式生成时显示旋转提示词、运行时长、token 计数，并提供停止按钮。

### 3.2 实现

**useChatStore.ts** — 新增流式状态追踪：
```typescript
turnStartAt: number | null;     // 当前轮次开始时间戳
turnTokens: number;             // 当前轮次 token 用量
sessionTokens: number;          // 会话累计 token 用量

setTurnStart: (timestamp: number | null) => void;
addTurnTokens: (tokens: number) => void;
resetTurnStats: () => void;
```

**bridgeService.ts** — 事件映射：
| 后端事件 | 前端动作 |
|----------|----------|
| `llm:turn_started` | `startStreaming` + `setTurnStart(Date.now())` + `resetTurnStats()` |
| `llm:stream:end` | `finalizeAssistantMessage` + 解析 `usage.total_tokens` → `addTurnTokens` |
| `turn:done` | `stopStreaming` + `setTurnStart(null)` |
| `llm:usage` | 增量更新 `addTurnTokens` |

**InputBar.tsx** — 流式状态栏：
- 旋转词数组：`['嘎吱运算', '飞速思考', '搜索中', '分析中', '推理中', '生成中']`，每 3 秒切换
- 时间格式：`< 60s` 显示 `Ns`，`≥ 60s` 显示 `Nm Ns`
- Token 格式：`≥ 1000` 显示 `Nk`，否则显示原始数字
- 使用 `useTick` hook 每秒刷新
- 停止按钮：红色调，`Square` 图标 + "停止" 文字
- 底部工具栏右侧显示会话累计 token（`Σ Nk tok`）

**index.css** — 脉冲动画：
```css
@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.4; }
}
```

---

## 四、P2 — 思考过程和工具调用展示简化

### 4.1 需求
简化 `ReasoningBlock` 和 `ToolCard` 的视觉风格，从图标+动画改为纯文字行内展示。

### 4.2 实现

**AssistantBubble.tsx — ReasoningBlock**：
- 移除大脑图标和脉冲动画
- 改为纯文字状态：「正在思考…」/「思考过程」+ 「运行中」/「已完成」
- 右侧 `ChevronRight` 箭头指示展开状态
- 流式时默认展开，完成后自动折叠

**index.css — .reasoning-block**：
- `.reasoning-block__head`：行内 flex，`text-xs` 灰色文字
- `.reasoning-block__head[data-running]`：shimmer 渐变文字动画（5s 循环）
- `.reasoning-block__body`：左侧 2px 边框，`max-h-[300px]` 可滚动

**ToolCard.tsx**：
- 状态指示器从 `CheckCircle2` / `XCircle` 改为纯符号 `✓` / `✗`
- 运行中仍保留 `Loader2` 旋转图标
- 参数和结果区域使用更紧凑的间距
- `SubToolRow` 同步简化

**ToolGroup.tsx**：
- `kindLabel` 改为简洁措辞：「已读 N 个文件」「已修改 N 个文件」「N 个子任务」「N 个命令」

---

## 五、P0 — IME 中文输入安全处理

### 5.1 需求
中文输入法组字期间，Enter 键不应触发发送，↑↓ 不应触发历史导航。

### 5.2 实现

**InputBar.tsx**：
```typescript
// 状态
const composingRef = useRef(false)
const lastCompositionEndAt = useRef(0)

// 事件处理
const handleCompositionStart = () => { composingRef.current = true }
const handleCompositionEnd = () => {
  composingRef.current = false
  lastCompositionEndAt.current = Date.now()
}

// handleKeyDown 中的 IME 守卫
const isIme = composingRef.current
  || (e.nativeEvent as ...).isComposing === true
  || Date.now() - lastCompositionEndAt.current < IME_CONFIRM_GRACE_MS  // 100ms 宽限期
if (isIme) return
```

### 5.3 关键设计
- **三重检测**：`composingRef` + `isComposing` 原生属性 + 时间宽限期
- **100ms 宽限期**：解决部分浏览器 `compositionEnd` 和 `keydown(Enter)` 事件顺序不确定的问题
- 所有快捷键（Enter/↑↓/Escape）在 IME 期间均被屏蔽

---

## 六、P0 — Composer 高度拖拽调整

### 6.1 需求
用户可拖拽调整输入框高度，高度持久化到 localStorage，双击重置。

### 6.2 实现

**InputBar.tsx**：
```typescript
// 常量
const COMPOSER_MIN_HEIGHT = 56
const COMPOSER_MAX_HEIGHT = 280
const COMPOSER_MAX_VIEWPORT_RATIO = 0.35  // 不超过视口 35%
const COMPOSER_HEIGHT_KEY = 'e3d-composer-height'

// 状态
const composerCardRef = useRef<HTMLDivElement>(null)
const [composerHeight, setComposerHeight] = useState<number | null>(loadComposerHeight)
const [composerResizing, setComposerResizing] = useState(false)

// 拖拽逻辑
const onComposerResizeStart = useCallback((e: ReactPointerEvent) => {
  // pointerdown → 记录起始位置 → document 级 pointermove/pointerup 监听
  // 拖拽中添加 body.composer-resizing 类（禁用文本选择）
  // 释放时保存高度到 localStorage
}, [composerHeight])

// 双击重置
const resetComposerHeight = useCallback(() => {
  setComposerHeight(null)  // null = 自动伸缩模式
  clearComposerHeight()
}, [])
```

**UI 元素**：
- 顶部居中 `GripHorizontal` 拖拽手柄（`w-16 h-2`，`cursor-ns-resize`）
- 拖拽时蓝色 `ring-2` 高亮
- `composerHeight === null` 时走自动伸缩逻辑（根据内容高度）

**index.css**：
```css
body.composer-resizing {
  cursor: ns-resize !important;
  user-select: none !important;
}
body.composer-resizing * {
  pointer-events: none !important;
}
```

### 6.3 交互细节
| 操作 | 效果 |
|------|------|
| 拖拽手柄向上 | 增加高度 |
| 拖拽手柄向下 | 减小高度 |
| 双击手柄 | 重置为自动伸缩 |
| 自动伸缩模式 | 根据内容自动调整，上限 `viewport * 35%` |

---

## 七、P0 — Prompt 历史导航

### 7.1 需求
使用 ↑/↓ 键浏览历史输入，类似终端 shell 体验。

### 7.2 实现

**InputBar.tsx**：
```typescript
const MAX_HISTORY = 100
const historyIndexRef = useRef(-1)        // -1 = 当前输入
const savedTextRef = useRef('')           // 进入历史前的当前文本
const [historyEntries, setHistoryEntries] = useState<string[]>(() => {
  // 从 localStorage 加载
  const saved = localStorage.getItem('e3d-input-history')
  return saved ? JSON.parse(saved) : []
})

// 持久化
useEffect(() => {
  localStorage.setItem('e3d-input-history', JSON.stringify(historyEntries.slice(0, MAX_HISTORY)))
}, [historyEntries])
```

**导航逻辑**：
- `↑`（光标在行首时）：向前浏览历史，首次按下保存当前文本
- `↓`（光标在行尾时）：向后浏览历史，回到 `-1` 时恢复保存的文本
- 发送消息后：添加到历史（去重），重置索引
- `Escape`：清空输入，重置历史索引
- 底部工具栏显示历史计数：`↑↓ N`

### 7.3 触发条件
| 按键 | 条件 | 效果 |
|------|------|------|
| ↑ | `selectionStart === 0 && selectionEnd === 0` | 向前浏览历史 |
| ↓ | `selectionStart === value.length && selectionEnd === value.length` | 向后浏览历史 |
| 任意字符键 | `historyIndexRef !== -1` | 退出历史模式 |

---

## 八、P0 — 工具审批内联卡片

### 8.1 需求
工具调用需要用户确认时，在消息流底部显示内联审批卡片（非全屏模态），支持键盘快捷操作。

### 8.2 实现

**ApprovalCard.tsx**（新建组件）：
- 位置：消息流底部，`pendingApproval` 存在时渲染
- 视觉：琥珀色边框 + 浅黄背景，`AlertTriangle` 警告图标
- 内容：工具名（等宽字体）+ 描述 + 参数 JSON 预览（`max-h-24` 可滚动）

**三个操作按钮**：
| 按钮 | 快捷键 | 颜色 | 行为 |
|------|--------|------|------|
| 允许 | 1 | 绿色 | 允许本次调用 |
| 本次允许 | 2 | 蓝色 | 本轮会话内允许相同工具 |
| 拒绝 | 3 | 红色 | 拒绝调用 |

**键盘交互**：
- `1` / `2` / `3`：直接选择对应操作
- `←` / `→`：切换选中按钮
- `Enter`：执行选中操作
- 输入框聚焦时不响应快捷键

**MessageList.tsx** — 集成：
```tsx
{pendingApproval && <ApprovalCard approval={pendingApproval} onAnswer={handleApproval} />}
```

**handleApproval**：
```typescript
const handleApproval = useCallback((allow: boolean, _session: boolean) => {
  if (!pendingApproval) return
  bridge.sendApproval(pendingApproval.toolId, allow)
  useChatStore.getState().setPendingApproval(null)
}, [pendingApproval])
```

---

## 九、P0 — 消息分层折叠（Hot/Warm/Cold）

### 9.1 需求
长对话时自动分层：最近的对话全量渲染，较早的折叠为摘要卡片，更早的隐藏在"加载更多"之后。

### 9.2 实现

**MessageList.tsx** — 三层策略：

```
┌─────────────────────────────────┐
│ Cold zone: "加载更早 N 条对话…" │  ← 点击加载 WARM_PAGE_SIZE 条
├─────────────────────────────────┤
│ Warm zone: 折叠的摘要卡片       │  ← 点击展开查看详情
│  ┌─────────────────────────┐    │
│  │ ▶ 用户问题预览...  N 步 │    │
│  └─────────────────────────┘    │
├─────────────────────────────────┤
│ Hot zone: 最近 HOT_TURNS 轮     │  ← 全量渲染（含 Compact 折叠）
│  完整消息 + 工具卡片 + 回复     │
├─────────────────────────────────┤
│ 审批卡片（如 pendingApproval）  │
└─────────────────────────────────┘
```

**常量**：
- `HOT_TURNS = 8`：Hot zone 保留最近 8 轮对话
- `WARM_PAGE_SIZE = 5`：每次"加载更多"展开 5 轮

**Turn 分组逻辑** (`buildTurnGroups`)：
- 以 `user` 消息为界划分 Turn
- 每个 Turn 记录：`userText`（前 60 字）、`toolCount`、`assistantPreview`（前 100 字）

**Warm zone 渲染**：
- 折叠态：`ChevronRight` + 用户问题摘要 + 工具步数
- 展开态：`ChevronDown` + 完整 `buildDisplayItems` 渲染
- 展开状态用 `Set<number>` 管理（按 turnIdx）

**Cold zone**：
- `coldTurnCount > 0` 时显示"加载更早 N 条对话…"按钮
- 点击 `setColdPage((p) => p + 1)` 递增

### 9.3 滚动管理
- 用户手动上滚时停止自动滚动（`autoScrollRef.current = false`）
- 显示"滚动到底部"悬浮按钮
- 新消息到达且 `autoScrollRef` 为 true 时平滑滚动到底部

---

## 十、P0 — Compact 步骤折叠

### 10.1 需求
Hot zone 内连续的已完成工具调用折叠为"已处理 · N 步"摘要，减少视觉噪音。

### 10.2 实现

**MessageList.tsx** — `buildDisplayItems` + `FoldedStep`：

**折叠逻辑** (`buildDisplayItems`)：
1. 先用 `groupConsecutiveTools` 分组连续同类工具
2. 遍历分组结果，收集连续的已完成工具/工具组
3. 遇到 `user` / `assistant`（有内容）/ `error` 消息时 flush 折叠缓冲区
4. 流式期间（`isStreaming`）不折叠，确保用户看到实时进度
5. 生成 `folded` 类型 DisplayItem

**FoldedStep 组件**：
- 折叠态：`ChevronRight` + "已处理 · N 步 · Ns"
- 展开态：`ChevronDown` + 递归渲染内部 DisplayItem（支持嵌套折叠）
- 点击切换 `open` 状态

**判定条件**：
```typescript
const isComplete = msg.role === 'tool_result' || (msg.role === 'tool_call' && msg.finalized)
if (isComplete && !isStreaming) {
  // 加入折叠缓冲区
  foldedMessages.push(msg)
  if (msg.role === 'tool_call') foldedToolCount++
  foldedDuration += msg.durationMs ?? 0
} else {
  // 正在进行的工具：全量渲染
  flushFolded()
  result.push(item)
}
```

---

## 十一、文件变更清单

### 新增文件
| 文件 | 说明 |
|------|------|
| `e3d-ui/src/components/chat/ApprovalCard.tsx` | 工具审批内联卡片组件 |

### 修改文件
| 文件 | 变更内容 |
|------|----------|
| `e3d-ui/src/components/chat/InputBar.tsx` | IME 处理、Composer 拖拽、历史导航、工具模式切换器、流式状态栏 |
| `e3d-ui/src/store/useChatStore.ts` | `toolApprovalMode` / `turnStartAt` / `turnTokens` / `sessionTokens` 状态与动作 |
| `e3d-ui/src/services/bridgeService.ts` | `llm:turn_started` / `llm:stream:end` / `turn:done` / `llm:usage` 事件映射 |
| `e3d-ui/src/components/chat/MessageList.tsx` | Hot/Warm/Cold 分层、Compact 步骤折叠、审批卡片集成 |
| `e3d-ui/src/components/chat/AssistantBubble.tsx` | ReasoningBlock 简化风格 |
| `e3d-ui/src/components/chat/ToolCard.tsx` | 紧凑行内风格、符号状态指示器 |
| `e3d-ui/src/components/chat/ToolGroup.tsx` | 简洁标签措辞 |
| `e3d-ui/src/index.css` | `.reasoning-block` 样式、`@keyframes pulse`、`.composer-resizing` |
| `e3d-ui/src/components/chat/UserBubble.tsx` | 样式微调 |
| `e3d-ui/src/components/settings/sections/AppearanceSection.tsx` | 样式微调 |
| `e3d-ui/src/components/settings/sections/ModelsSection.tsx` | 样式微调 |
| `e3d-ui/src/services/messageContracts.ts` | 消息契约扩展 |
| `e3d-ui/src/components/TabBar.tsx` | 样式微调 |

---

## 十二、后端配套变更

以下 C# 后端文件已同步修改以支持前端新功能：

| 文件 | 变更 |
|------|------|
| `src/E3DCopilot.Core/Config/CopilotConfig.cs` | 配置结构扩展 |
| `src/E3DCopilot.Core/CopilotController.cs` | `user:set_approval_mode` / `user:set_plan_mode` 处理 |
| `src/E3DCopilot.Core/Messaging/MessageContracts.cs` | 消息契约扩展 |
| `src/E3DCopilot.WebHost/Bridge.cs` | Bridge 消息路由扩展 |

---

## 十三、设计决策与取舍

### 13.1 为什么不用全屏模态审批？
E3D 插件面板宽度有限（~300-400px），全屏模态会遮挡对话上下文。内联卡片在消息流底部显示，用户可以看到前文判断是否允许工具调用。

### 13.2 为什么 Hot zone 设为 8 轮？
窄面板下单轮对话可能包含多个工具调用，8 轮已覆盖大多数"当前任务"上下文。超出部分折叠为 Warm zone 摘要卡片，保持滚动流畅。

### 13.3 为什么流式期间不折叠工具？
用户需要实时看到工具执行进度。流式结束后才触发 Compact 折叠，将已完成的步骤收起为"已处理 · N 步"。

### 13.4 为什么 IME 需要 100ms 宽限期？
部分浏览器（特别是 WebView2）的 `compositionEnd` 事件可能在 `keydown(Enter)` 之后触发，导致组字确认的 Enter 被误判为发送。100ms 宽限期覆盖了这个竞态窗口。

### 13.5 为什么 Composer 高度用 `null` 而非 `0` 表示自动模式？
`null` 明确区分"用户未手动设定"和"高度为 0"两种语义。自动伸缩逻辑通过 `composerHeight !== null` 判断是否跳过。

---

## 十四、后续规划（场景适配修订）

> 以下基于实际使用场景（**中文、内网、E3D 右侧窄面板、工程用户**）对原计划进行了修订。
> 原计划中 7 项功能，经分析后：2 项已完成、3 项砍掉、1 项降级改造、1 项维持现状。

### 14.1 已完成（代码中已实现，无需再做）

| 功能 | 代码位置 | 说明 |
|------|----------|------|
| 长文本粘贴折叠 | `InputBar.tsx` — `shouldFoldPaste()` + `pastedBlocks` | 超过 2000 字符/20 行的粘贴内容自动折叠为卡片，输入框插入 `[Pasted block N]` 占位符 |
| 拖拽文件到输入区 | `InputBar.tsx` — `handleDragOver` / `handleDrop` / `addAttachment` | 拖拽文件时高亮反馈，释放后自动添加为附件，图片类型自动生成预览 |

### 14.2 砍掉（不适合当前场景）

| 功能 | 原优先级 | 砍掉原因 |
|------|----------|----------|
| 多语言支持（i18n） | P2 | 用户为中文工程师，内网环境不需要英文。i18n 增加 bundle 体积和代码复杂度，纯成本零收益 |
| GSAP 消息动画 | P2 | GSAP 库 ~30KB+，WebView2 内网环境资源有限。当前 CSS `transition` + `@keyframes` 已覆盖折叠展开、shimmer、脉冲等效果，重型动画库反而拖慢窄面板渲染 |
| 消息搜索 | P2 | E3D 工程场景对话是任务导向的（查属性、改参数、导出报表），每轮短且聚焦，很少需要翻历史搜索。窄面板搜索 UI 抢占空间，搜索结果列表难以展示 |

### 14.3 砍掉（与 E3D 现有交互重复）

| 功能 | 原优先级 | 砍掉原因 |
|------|----------|----------|
| `@` 元素引用菜单 | P1 | E3D 左侧 Explorer 已提供完整的元素树浏览和选择功能，用户习惯在 Explorer 里点选元素。在 E小智 窄面板里再做一套搜索菜单是重复造轮子，且窄面板搜索体验不如 Explorer。已有 `GetCurrentElementName()` / `GetSelectedElementNames()` 机制，用户在 E3D 中选好元素后直接对话即可，AI 自动获取选中元素上下文 |

### 14.4 维持现状

| 功能 | 原优先级 | 决策 | 原因 |
|------|----------|------|------|
| 命令面板（Cmd+K） | P2 | 保留现有框架，不主动增强 | `CommandPalette.tsx` 已存在，`showCommandPalette` 状态已接入。但 E3D 用户是工程师不是开发者，Cmd+K 心智模型不一定是他们的习惯。窄面板里弹出全宽搜索框体验受限。按需使用，用户反馈后再增强 |

### 14.5 修订后真正待办

| 优先级 | 功能 | 前置条件 |
|--------|------|----------|
| 按需 | 命令面板增强 | 用户反馈驱动 |

---

*文档结束*
