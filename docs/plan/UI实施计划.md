# E小智 v1.0 — UI 实施计划

> 基于 `docs/design/UI设计.md` 完整版（17 组件），对齐 `docs/plan/文档索引.md` 总计划（Phase 0-3）。
> **目标**：确保每个 UI 组件的实施有明确的 Phase 归属、文件清单、依赖关系、验收标准和参考源码。

---

## 一、UI 组件清单与 Phase 归属

| # | 组件 | 文件（位于 `src/E3DCopilot.UI/`） | Phase | 优先级 |
|:--:|------|------|:----:|:------:|
| 1 | CopilotForm | `Forms/CopilotForm.cs` | 1a | 🔴 P0 |
| 2 | ChatListBox | `Controls/ChatListBox.cs` | 1a | 🔴 P0 |
| 3 | InputPanel (基础版) | `Controls/InputPanel.cs` | 1a | 🔴 P0 |
| 4 | StatusBarControl | `Controls/StatusBarControl.cs` | 1a | 🔴 P0 |
| 5 | MarkdownPanel | `Controls/MarkdownPanel.cs` | 1a | 🔴 P0 |
| 6 | UserMessageControl | `Controls/UserMessageControl.cs` | 1a | 🔴 P0 |
| 7 | ToolCardControl (基础版) | `Controls/ToolCardControl.cs` | 1b | 🔴 P0 |
| 8 | ThinkingPanel | `Controls/ThinkingPanel.cs` | 1b | 🔴 P0 |
| 9 | ErrorPanel | `Controls/ErrorPanel.cs` | 1b | 🔴 P0 |
| 10 | PromptShelfControl | `Controls/PromptShelfControl.cs` | 1c | 🔴 P0 |
| 11 | ReadOnlyBatchPanel | `Controls/ReadOnlyBatchPanel.cs` | 1b | 🟡 P1 |
| 12 | DiffViewControl | `Controls/DiffViewControl.cs` | 1b | 🟡 P1 |
| 13 | SearchResultsPanel | `Controls/SearchResultsPanel.cs` | 1b | 🟡 P1 |
| 14 | TaskTrackingPanel | `Controls/TaskTrackingPanel.cs` | 1b | 🟡 P1 |
| 15 | CodeBlock | `Controls/CodeBlock.cs` | 1b | 🟡 P1 |
| 16 | CompletionCard | `Controls/CompletionCard.cs` | 1b | 🟡 P1 |
| 17 | TurnActionsBar | `Controls/TurnActionsBar.cs` | 1c | 🟡 P1 |
| 18 | NavToolbar | `Controls/NavToolbar.cs` | 1d | 🟡 P1 |
| 19 | WarmTurnCard | `Controls/WarmTurnCard.cs` | 1d | 🟡 P1 |
| 20 | SettingsPanel | `Forms/SettingsPanel.cs` | 2 | 🟢 P2 |
| 21 | HistoryPanel | `Forms/HistoryPanel.cs` | 2 | 🟢 P2 |
| 22 | VirtualScrollPanel | `Controls/VirtualScrollPanel.cs` | 2 | 🟢 P2 |
| 23 | AttachmentThumbControl | `Controls/AttachmentThumbControl.cs` | 2 | 🟢 P2 |
| 24 | TaskItemControl | `Controls/TaskItemControl.cs` | 2 | 🟢 P2 |
| 25 | DiffLineControl | `Controls/DiffLineControl.cs` | 2 | 🟢 P2 |
| 26 | SearchResultItemControl | `Controls/SearchResultItemControl.cs` | 2 | 🟢 P2 |
| 27 | SyntaxHighlightTextBox | `Controls/SyntaxHighlightTextBox.cs` | 2 | 🟢 P2 |

### 基础设施文件

| # | 文件 | Phase | 说明 |
|:--:|------|:----:|------|
| 28 | `Theme/AppTheme.cs` | 1a (基础色) → 2 (完善) | 颜色/字体常量 |
| 29 | `Theme/AppFonts.cs` | 1a (基础) → 2 (完善) | 字体规格 |
| 30 | `Animation/AnimationHelper.cs` | 1b | 缓动函数 + Timer 动画 |
| 31 | `Models/CopilotEvent.cs` | 1a | 事件类型枚举 (22 种) |
| 32 | `Models/CopilotEventKind.cs` | 1a | EventKind 枚举 |
| 33 | `Models/ChatMessage.cs` | 1a | 消息/Turn/ToolItem/TaskItem 数据模型 |
| 34 | `Services/EventDispatcher.cs` | 1a | 线程安全事件分发 |
| 35 | `Services/MarkdownParser.cs` | 1a | MD 文本→控件树 |
| 36 | `Services/LcsDiff.cs` | 1b | LCS Diff 算法 |
| 37 | `Services/SyntaxHighlighter.cs` | 2 | PML/JSON/C# 语法着色 |
| 38 | `Controls/PasteTagControl.cs` | 2 | 大文本粘贴折叠标签 |

---

## 二、Phase 1a — 最小闭环 UI（2 天）

> **目标**：输入文字 → AI 回复 → 显示。E3D Addin 可加载空面板。

### D1 上午：UI 骨架搭建

| 序号 | 文件 | 任务 | 参考源码 |
|:--:|------|------|---------|
| 1 | `CopilotForm.cs` | 主 UserControl，`TableLayoutPanel` 三行布局 (消息/输入/状态) | `[C] ChatView.tsx` `[C] ChatLayout.tsx` |
| 2 | `Models/CopilotEvent.cs` | 定义 22 种 `EventKind` 枚举 | `[R] lib/types.ts` (Item 类型) |
| 3 | `Models/ChatMessage.cs` | `Turn`/`ChatMessage`/`ToolItem` 数据类 | `[R] lib/types.ts` |
| 4 | `Services/EventDispatcher.cs` | `IEventSink` + `BeginInvoke` 线程安全分发 | `[R] lib/useController.ts` |
| 5 | `Theme/AppTheme.cs` | 仅基础色 (Background/Foreground/Border) | `[C] theme.css` |

**验收标准**：
- [ ] CopilotForm 在 E3D DockedWindow 中可显示（空界面）
- [ ] 事件分发链路通（发 EventKind.Text → BeginInvoke → 无异常）
- [ ] 编译通过，无警告

### D1 下午：消息显示骨架

| 序号 | 文件 | 任务 | 参考源码 |
|:--:|------|------|---------|
| 6 | `ChatListBox.cs` | 基础版消息列表（Panel 滚动，无虚拟化，无 Hot/Warm/Cold） | `[R] Transcript.tsx` |
| 7 | `UserMessageControl.cs` | 用户消息气泡（头像+文本+时间戳，固定宽度右侧） | `[R] Message.tsx` (UserMessage) |
| 8 | `StatusBarControl.cs` | 状态栏（模式+Token+模型，基础版） | `[R] StatusBar.tsx` |

**验收标准**：
- [ ] 用户消息可添加并正确显示
- [ ] 多条消息可正确堆叠
- [ ] 状态栏正确显示模式（Plan/Act）

### D2 上午：AI 回复 + 流式 + Markdown

| 序号 | 文件 | 任务 | 参考源码 |
|:--:|------|------|---------|
| 9 | `MarkdownPanel.cs` | Markdown → FlowLayoutPanel 转换器 | `[R] Markdown.tsx` `[C] MarkdownRow.tsx` |
| 10 | `Services/MarkdownParser.cs` | 正则解析 Markdown 块（标题/粗斜体/代码/列表/表格/引用） | `[C] MarkdownBlock.tsx` |
| 11 | `Controls/ChatListBox.cs` (追加) | `AppendStreamText()` 流式追加 | `[R] Transcript.tsx` 流式滚动 |
| 12 | `Models/ChatMessage.cs` (追加) | `AppendText()` 增量文本方法 | — |

**验收标准**：
- [ ] AI 回复 Markdown 正确渲染（标题/粗体/列表/代码块/表格）
- [ ] 流式文本逐 chunk 追加，界面不卡顿
- [ ] 流式等待时状态栏显示"AI 回复中..."

### D2 下午：InputPanel 基础版 + 端到端

| 序号 | 文件 | 任务 | 参考源码 |
|:--:|------|------|---------|
| 13 | `InputPanel.cs` (基础版) | 多行 TextBox + 发送按钮 + Ctrl+Enter 快捷键 | `[C] ChatTextArea.tsx` `[R] Composer.tsx` |
| 14 | `CopilotForm.cs` (集成) | 串联输入→Controller→事件→显示 全链路 | `[C] ChatView.tsx` |

**验收标准**：
- [ ] 输入文字 + 回车 → AI 回复显示
- [ ] 流式效果正常（逐字/逐 chunk）
- [ ] 基础 Markdown 渲染正确

### Phase 1a 产出一览

```
新增文件数: 14 个
E3DCopilot.UI/
├── Forms/
│   └── CopilotForm.cs          ← 主窗口
├── Controls/
│   ├── ChatListBox.cs          ← 消息列表
│   ├── UserMessageControl.cs   ← 用户消息
│   ├── MarkdownPanel.cs        ← Markdown 渲染
│   ├── StatusBarControl.cs     ← 状态栏
│   └── InputPanel.cs           ← 输入区域 (基础版)
├── Models/
│   ├── CopilotEvent.cs         ← 事件枚举
│   └── ChatMessage.cs          ← 消息数据模型
├── Services/
│   ├── EventDispatcher.cs      ← 线程安全分发
│   └── MarkdownParser.cs       ← MD 解析器
└── Theme/
    ├── AppTheme.cs             ← 基础色
    └── AppFonts.cs             ← 基础字体
```

---

## 三、Phase 1b — UI MVP 工具可视化（2 天）

> **目标**：ToolCard / Thinking / Error / Diff / Batch / SearchResults / Task 组件全部到位。

### D3 上午：核心渲染组件

| 序号 | 文件 | 任务 | 参考源码 |
|:--:|------|------|---------|
| 1 | `ToolCardControl.cs` | 工具卡片（状态图标+工具名+时长+摘要+可折叠参数/结果） | `[R] ToolCard.tsx` `[R] ProcessCard.tsx` |
| 2 | `ThinkingPanel.cs` | 推理面板（流式展开+完成自动折叠+耗时显示） | `[R] Message.tsx` (.reasoning) `[R] reasoningDisplay.ts` |
| 3 | `ErrorPanel.cs` | 错误面板（描述+详情折叠+重试/取消按钮） | `[C] ErrorRow.tsx` |
| 4 | `Animation/AnimationHelper.cs` | 缓动函数 + Timer 帧动画辅助类 | `[R] gsapAnimations.ts` `[R] useGSAPCollapse.ts` |

**验收标准**：
- [ ] ToolCard 正确显示 running/done/error 三种状态
- [ ] ToolCard 折叠/展开动画流畅 (60fps)
- [ ] ThinkingPanel 流式展开，完成自动折叠
- [ ] ErrorPanel 显示错误 + 重试按钮响应

### D3 下午：Diff + 批量 + 搜索 + 任务

| 序号 | 文件 | 任务 | 参考源码 |
|:--:|------|------|---------|
| 5 | `DiffViewControl.cs` | 行级差异对比（+N -N 统计+复制+展开全部） | `[R] InlineDiff.tsx` `[R] HljsDiff.tsx` |
| 6 | `Services/LcsDiff.cs` | LCS 行级差异算法 | `[R] lib/diff.ts` |
| 7 | `ReadOnlyBatchPanel.cs` | 只读工具批聚合（折叠摘要+展开+分类计数） | `[R] ReadOnlyBatch.tsx` |
| 8 | `SearchResultsPanel.cs` | 知识库搜索结果（来源+置信度+代码片段） | `[C] SearchResultsDisplay.tsx` |
| 9 | `TaskTrackingPanel.cs` | 任务追踪面板（折叠+完成计数+evidence） | `[R] TodoPanel.tsx` `[C] TaskHeader.tsx` |

**验收标准**：
- [ ] Diff 正确显示行级差异，+N/-N 统计正确
- [ ] 连续 3+ 个只读工具聚合为一个 ReadOnlyBatch
- [ ] SearchResults 正确渲染各来源（api/pml/pattern/domain）
- [ ] TaskTracking 折叠/展开 + 状态图标正确

### D4 上午：消息整合 + 辅助组件

| 序号 | 文件 | 任务 | 参考源码 |
|:--:|------|------|---------|
| 10 | `CodeBlock.cs` | 代码块（PML/JSON/C# 标签+复制按钮） | `[C] CodeBlock.tsx` `[C] CodeAccordian.tsx` |
| 11 | `CompletionCard.cs` | 完成卡片（摘要+Token+操作按钮） | `[C] CompletionOutputRow.tsx` |
| 12 | `ChatListBox.cs` (升级) | 消息合并管线（combineToolBatches + groupLowRiskTools） | `[C] ChatView.tsx` 合并管线 |

**验收标准**：
- [ ] CodeBlock 正确显示 PML/JSON 代码
- [ ] CompletionCard 在任务完成时显示
- [ ] 消息合并管线正确聚合只读工具

### D4 下午：集成测试

| 序号 | 测试场景 | 验收标准 |
|:--:|---------|---------|
| 1 | 用户查询管道 → ToolCard 显示 | running→done 状态切换正确 |
| 2 | 连续 5 个只读查询 → Batch | 聚合为一个 ReadOnlyBatchPanel |
| 3 | 工具执行失败 → ErrorPanel | 红色错误 + 重试按钮 |
| 4 | 推理过程显示 → ThinkingPanel | 流式展开→完成折叠 |
| 5 | 修改属性完成 → DiffView | 新旧值差异对比显示 |

### Phase 1b 产出一览

```
新增文件数: 12 个
E3DCopilot.UI/
├── Controls/
│   ├── ToolCardControl.cs      ← 工具调用卡片
│   ├── ThinkingPanel.cs        ← 推理面板
│   ├── ErrorPanel.cs           ← 错误面板
│   ├── DiffViewControl.cs      ← 差异对比
│   ├── ReadOnlyBatchPanel.cs   ← 只读工具聚合
│   ├── SearchResultsPanel.cs   ← 搜索结果
│   ├── TaskTrackingPanel.cs    ← 任务追踪
│   ├── CodeBlock.cs            ← 代码块
│   └── CompletionCard.cs       ← 完成卡片
├── Animation/
│   └── AnimationHelper.cs      ← 动画辅助
└── Services/
    └── LcsDiff.cs              ← Diff 算法
```

---

## 四、Phase 1c — 安全 + 审批 UI（1 天）

> **目标**：PromptShelf 审批条 + TurnActionsBar 到位，审批流 UI 完整。

| 序号 | 文件 | 任务 | 参考源码 |
|:--:|------|------|---------|
| 1 | `PromptShelfControl.cs` | 统一审批/问答容器（6 种 PromptType 模式 + 键盘驱动 1-4） | `[R] PromptShelf.tsx` `[R] ApprovalModal.tsx` |
| 2 | `TurnActionsBar.cs` | 轮次操作栏（复制/摘要/回滚+两步确认） | `[R] Message.tsx` (TurnActions) |
| 3 | `InputPanel.cs` (升级) | @ElementSearchMenu + /SlashCommandMenu | `[C] ChatTextArea.tsx` `[C] SlashCommandMenu.tsx` |
| 4 | `CopilotForm.cs` (升级) | `CancellationToken` 停止机制 | `[C] ChatView.tsx` 停止按钮 |
| 5 | `PromptShelfControl.cs` (追加) | approval/confirm/clarify/notify/destructive 5 种模式全部实现 | `[R] PromptShelf.tsx` |

**验收标准**：
- [ ] PromptShelf 6 种 PromptType 均正确渲染
- [ ] 键盘快捷键 1/2/3/4/Escape 触发正确回调
- [ ] 危险操作 (destructive) 显示红色警告
- [ ] TurnActionsBar 回滚两步确认 + 3 秒自动恢复
- [ ] @元素搜索菜单弹出正确结果
- [ ] /命令菜单弹出并可用

### Phase 1c 产出一览

```
新增/修改文件数: 5 个
E3DCopilot.UI/
├── Controls/
│   ├── PromptShelfControl.cs   ← 新增
│   ├── TurnActionsBar.cs       ← 新增
│   ├── InputPanel.cs           ← 升级 (@搜索 + /命令)
│   └── CopilotForm.cs          ← 升级 (停止机制)
```

---

## 五、Phase 1d — 集成 + 部署 UI（1 天）

> **目标**：NavToolbar + WarmTurnCard + 三级渲染基础 + 整体验收。

| 序号 | 文件 | 任务 | 参考源码 |
|:--:|------|------|---------|
| 1 | `NavToolbar.cs` | 顶部工具栏（Plan/Act 切换+设置+历史+停止） | `[C] Navbar.tsx` `[R] AppChrome.tsx` |
| 2 | `WarmTurnCard.cs` | Warm Zone 折叠卡片（摘要+工具统计+点击展开） | `[R] Transcript.tsx` (warm-collapse) |
| 3 | `ChatListBox.cs` (三级渲染基础) | Hot/Warm 分区（Cold 暂用"加载更多"按钮） | `[R] Transcript.tsx` (cold/warm/hot) |
| 4 | `CopilotForm.cs` (最终集成) | 所有组件串联 + 全部 EventKind 路由 | `[C] ChatView.tsx` |

**验收标准**：
- [ ] NavToolbar Plan/Act 切换颜色变化正确
- [ ] WarmTurnCard 折叠/展开流畅
- [ ] Hot Zone 超过 30 轮后正确转移至 Warm Zone
- [ ] 全部 22 种 EventKind 路由正确
- [ ] E3D 内加载正常，无 DPI 缩放问题

### Phase 1d 产出一览

```
新增/修改文件数: 4 个
E3DCopilot.UI/
├── Controls/
│   ├── NavToolbar.cs           ← 新增
│   └── WarmTurnCard.cs         ← 新增
├── Controls/
│   └── ChatListBox.cs          ← 升级 (三级渲染)
└── Forms/
    └── CopilotForm.cs          ← 升级 (最终集成)
```

---

## 六、Phase 2 — UI 完善（第 2 周）

> **目标**：设置/历史/虚拟滚动/高级渲染/附件系统 全面完善。

### 2a: SettingsPanel + HistoryPanel（2 天）

| 序号 | 文件 | 任务 | 参考源码 |
|:--:|------|------|---------|
| 1 | `SettingsPanel.cs` | 6 标签页设置（API/审批/界面/记忆/快捷键/关于） | `[R] SettingsPanel.tsx` `[C] SettingsView.tsx` |
| 2 | `HistoryPanel.cs` | 历史会话列表+搜索+恢复+右键操作 | `[C] HistoryView.tsx` `[R] HistoryPanel.tsx` |

### 2b: 虚拟滚动 + 附件系统（2 天）

| 序号 | 文件 | 任务 | 参考源码 |
|:--:|------|------|---------|
| 3 | `VirtualScrollPanel.cs` | 虚拟滚动面板（仅渲染可视区行） | `[C] MessagesArea.tsx` (react-virtuoso) |
| 4 | `ChatListBox.cs` (升级) | 切换到 VirtualScrollPanel 驱动 | — |
| 5 | `AttachmentThumbControl.cs` | 附件缩略图（图片/Excel/CSV/PDF/TXT） | `[C] ChatTextArea.tsx` |
| 6 | `InputPanel.cs` (升级) | 附件拖拽上传+AttachmentBar 预览+删除 | `[C] ChatTextArea.tsx` |

### 2c: 高级渲染 + 主题完善（2 天）

| 序号 | 文件 | 任务 | 参考源码 |
|:--:|------|------|---------|
| 7 | `SyntaxHighlightTextBox.cs` | 自定义 RichTextBox（PML/JSON/C# 语法着色） | `[R] HljsCode.tsx` |
| 8 | `Services/SyntaxHighlighter.cs` | PML/JSON/C# 语法规则+着色器 | `[R] HljsCode.tsx` |
| 9 | `Theme/AppTheme.cs` (完善) | 完整 17 色 + 语义色 + Diff 色 + ToolCard 色 | `[R] styles.css` |
| 10 | `Theme/AppFonts.cs` (完善) | 7 种字体规格完善 | — |
| 11 | `DiffLineControl.cs` | 独立差异行控件 | `[R] InlineDiff.tsx` |
| 12 | `SearchResultItemControl.cs` | 独立搜索结果项控件 | — |
| 13 | `TaskItemControl.cs` | 独立任务项控件 | — |
| 14 | `PasteTagControl.cs` | 大文本粘贴折叠标签 | `[R] Composer.tsx` |

### 2d: Markdown 完整语法（1 天）

| 序号 | 任务 | 参考源码 |
|:--:|------|---------|
| 15 | MarkdownPanel 完整语法支持（所有元素+嵌套+表格对齐） | `[R] Markdown.tsx` `[C] MarkdownRow.tsx` |
| 16 | MarkdownParser 完整解析（含 backtick 转义、link、image、footnote） | `[C] MarkdownBlock.tsx` |

---

## 七、Phase 3 — UI 增强（第 3 周）

> **目标**：IME 优化/Session 导出/ErrorBoundary/UndoRewind。这些是锦上添花，不影响 MVP 功能。

| 序号 | 任务 | 参考源码 |
|:--:|------|---------|
| 1 | InputPanel IME 优化 (WM_IME_COMPOSITION) | — |
| 2 | Session 导出 (PML/MD/Excel) | `[R] sessionExport.tsx` |
| 3 | ErrorBoundary（全局异常捕获+crash恢复） | `[R] ErrorBoundary.tsx` |
| 4 | UndoRewindBanner（撤销横幅确认） | `[R] UndoRewindBanner.tsx` |
| 5 | ChatListBox Cold Zone 分页加载完善 | `[R] Transcript.tsx` |

---

## 八、UI 组件依赖图

```
                    CopilotForm (1a)
                    /    |    \
            NavToolbar ChatListBox  InputPanel
              (1d)      (1a)        (1a→1c)
               /          |           |     \
    SettingsPanel         |     AttachmentBar  btnStop
      HistoryPanel        |        (2)         (1c)
         (2)              |
              ┌───────────┼───────────┬───────────┬──────────┐
              ↓           ↓           ↓           ↓          ↓
        UserMessage    Markdown     ToolCard    Thinking   ErrorPanel
        Control        Panel        Control     Panel       (1b)
         (1a)          (1a)         (1b)        (1b)
                                     /  \
                                    /    \
                       ReadOnlyBatch    DiffViewControl
                       Panel (1b)       (1b)
                                     /      \
                          SearchResults    TurnActionsBar
                          Panel (1b)       (1c)

              PromptShelfControl    TaskTrackingPanel    CompletionCard
                    (1c)                 (1b)                  (1b)

              CodeBlock              WarmTurnCard
                (1b)                    (1d)
```

**关键依赖规则**：
- **CopilotForm** 必须在所有组件之前完成（主容器）
- **ChatListBox** 依赖 MarkdownPanel + UserMessageControl
- **ToolCardControl** 依赖 DiffViewControl + CodeBlock + AnimationHelper
- **ReadOnlyBatchPanel** 依赖 ToolCardControl
- **SearchResultsPanel** 依赖 ToolCardControl + CodeBlock
- **InputPanel 完整版** (1c) 依赖 CopilotController.QueryElements（后端支持）
- **PromptShelfControl** 依赖 ToolPolicy（后端支持）
- **SettingsPanel / HistoryPanel** (2) 可在 1d 之后独立开发，不阻塞主链

---

## 九、验收检查清单

### Phase 1a 验收（M1: 最小闭环）

- [ ] CopilotForm 在 E3D DockedWindow 正确显示，DPI 缩放正常
- [ ] 输入文字 + 发送 → AI 流式回复显示
- [ ] Markdown 基础语法正确渲染（标题/粗体/列表）
- [ ] 用户消息气泡和 AI 回复正确区分
- [ ] 状态栏显示当前模式（Plan/Act）
- [ ] 编译通过无警告，4 个 DLL 生成

### Phase 1b 验收（M2: MVP 可用）

- [ ] ToolCard running/done/error 三种状态动画完整
- [ ] ToolCard 折叠/展开动画流畅（60fps 无掉帧）
- [ ] ThinkingPanel 流式展开→完成自动折叠
- [ ] DiffViewControl 正确显示行级差异
- [ ] ReadOnlyBatchPanel 正确聚合 3+ 连续只读工具
- [ ] SearchResultsPanel 正确渲染 api/pml/pattern/domain 四源结果
- [ ] TaskTrackingPanel 状态图标+计数正确
- [ ] CodeBlock 显示完整代码 + 复制功能

### Phase 1c 验收（M3: 安全就绪）

- [ ] PromptShelf 6 种模式 (approve_plan/approve_tool/confirm/clarify/notify/destructive) 全部正确
- [ ] 键盘 1/2/3/4/Escape 输入正确触发
- [ ] destructive 模式红色警告 + 默认拒绝
- [ ] TurnActionsBar 回滚两步确认正确
- [ ] @元素搜索菜单正确弹出（需要 E3D API 支持）
- [ ] /命令菜单正确弹出

### Phase 1d 验收（M4: MVP 发布）

- [ ] NavToolbar Plan/Act 颜色切换
- [ ] WarmTurnCard 折叠/展开 + 工具统计
- [ ] Hot Zone → Warm Zone 转移正确
- [ ] 全部 22 种 EventKind 路由无漏
- [ ] 停止按钮可中断 AgentLoop

### Phase 2 验收（M5: 工具完备）

- [ ] SettingsPanel 6 标签页正常弹出和切换
- [ ] HistoryPanel 会话列表搜索和恢复
- [ ] 虚拟滚动 100+ 消息不卡
- [ ] 附件拖拽上传和预览
- [ ] PML 语法着色正确
- [ ] 完整 Markdown 语法支持（表格/引用/嵌套/链接）

---

## 十、关键风险和缓解

| 风险 | 影响 | 缓解 |
|------|:----:|------|
| WinForms RichTextBox 流式性能不足 | ChatListBox 卡顿 | 使用 SuspendLayout + Invoke 批处理 + 对象池 |
| WinForms Timer 动画帧率不稳定 | 折叠动画掉帧 | 用精准的 16ms Timer + EaseOutCubic，降级方案用 Instant |
| Markdown 解析性能差 | AI 长回复卡顿 | 分块解析，每 500ms 渲染一次，总时长 > 2s 降级为纯文本 |
| 虚拟滚动实现复杂度高 | Phase 2 延期 | Phase 1 仅 Hot/Warm/Cold 分区延迟渲染，Phase 2 补虚拟滚动 |
| DPI 缩放下布局错乱 | 控件重叠 | 使用 TableLayoutPanel + AutoSize + Percent，避免 Absolute 定位 |
| E3D 内嵌 WinForms 兼容性 | 控件不显示 | Phase 0 先用一个空 Form 测试 E3D DockedWindow 加载 |

---

## 十一、开发规范

### 文件组织

```
E3DCopilot.UI/
├── Forms/                    ← 顶层 Form 或 UserControl (主窗口/弹窗)
│   ├── CopilotForm.cs
│   ├── SettingsPanel.cs
│   └── HistoryPanel.cs
├── Controls/                 ← 可复用 UserControl 组件
│   ├── ChatListBox.cs
│   ├── ToolCardControl.cs
│   └── ... (25 个控件)
├── Models/                   ← 数据模型 / 事件类型
│   ├── CopilotEvent.cs
│   └── ChatMessage.cs
├── Services/                 ← 非 UI 逻辑服务
│   ├── EventDispatcher.cs
│   ├── MarkdownParser.cs
│   ├── LcsDiff.cs
│   └── SyntaxHighlighter.cs
├── Theme/                    ← 主题常量
│   ├── AppTheme.cs
│   └── AppFonts.cs
└── Animation/                ← 动画工具
    └── AnimationHelper.cs
```

### 命名规范

- 控件类：`{功能}Control` / `{功能}Panel` / `{功能}Bar`
- 事件：`{动作}Clicked` / `{动作}Requested`
- 回调：`On{动作}` / `Handle{事件}`
- 私有字段：`camelCase` 不含前缀

### 编码规范

- 所有 UI 更新必须通过 `this.BeginInvoke()`（线程安全）
- 批量添加控件时使用 `SuspendLayout()` / `ResumeLayout()`
- 颜色使用 `AppTheme.*` 常量，禁止硬编码
- 动画使用 `AnimationHelper.Animate()` 统一管理
- 所有公开方法标注 XML 注释（`/// <summary>`）
