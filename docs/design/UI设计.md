# E小智 v1.0 — UI 设计方案（完整版）

> 深度借鉴 Cline CLI v3.0.24 + DeepSeek-Reasonix v1.8.1，统合为 E3D WinForms 实现方案。
> **每个组件均标注参考源码的具体文件路径，设计时可直接对照查阅。**

---

## 项目路径速查

| 项目 | 代号 | 根路径 |
|------|:--:|------|
| Cline CLI v3.0.24 | **C** | `E:\工作\E3D-E小智\参考开源项目\cline-cli-v3.0.24\apps\vscode\webview-ui\src\` |
| Reasonix v1.8.1 | **R** | `E:\工作\E3D-E小智\参考开源项目\DeepSeek-Reasonix-desktop-v1.8.1\desktop\frontend\src\` |
| E小智 v1.0 | **EZ** | `E:\工作\E3D-E小智\E小智-v1.0-开发中\src\E3DCopilot.UI\` |

> 以下章节中 `[C: ...]` 表示 Cline 源码路径，`[R: ...]` 表示 Reasonix 源码路径（均相对于上表根路径）。

---

## 一、架构映射

```
参考项目                                E3D Copilot (WinForms)
═══════════════════════════════════════════════════════════════════
                               
C: WebviewProvider (抽象基类)       →    ICopilotWindow (接口)
C: Provider 层级 (多层 Context)     →    WinForms 单层事件驱动
R: useController.ts 状态机          →    CopilotController (已有)
                               
C: gRPC StateServiceClient          →    event Action<CopilotEvent>
R: Go<->JS IPC bridge.ts            →    EventSink.OnEvent 委托
                               
C: Extension.activate()             →    Addin.Start()
R: App.tsx 路由切换                 →    无路由；单 DockedWindow Panel
                               
C: ChatView (始终挂载)              →    CopilotForm (始终可见)
R: Transcript.tsx 三层渲染          →    ChatListBox 三层渲染
                               
C: react-virtuoso 虚拟滚动          →    WinForms 自定义 Panel + ListView
R: Cold/Warm/Hot 分区分层           →    Hot/Warm/Cold 三级渲染策略
```

---

## 二、组件完整层次图

```
CopilotForm (主 UserControl, grid: 1fr auto 布局)
│
├── [顶部] NavToolbar (工具栏)
│   ├── lblPlanActToggle (Plan / Act 模式切换, 带颜色指示)
│   ├── btnSettings (设置按钮 → 弹出 SettingsPanel)
│   ├── btnHistory (历史按钮 → 弹出 HistoryPanel)
│   └── btnStop (红色停止按钮, 仅 AI 运行中可见)
│
├── [中部] ChatListBox (消息列表, 三级渲染 + 消息合并管线)
│   │
│   ├── [Cold Zone] 最早历史, 分页加载, "加载更多..." 按钮
│   │
│   ├── [Warm Zone] 中间轮次, 折叠为 WarmTurnCard
│   │   └── WarmTurnCard (点击展开)  [R: components/Transcript.tsx warm-collapse]
│   │       ├── 摘要文本 (前 80 字)
│   │       ├── 工具调用统计 ("3 queries · 1 modify")
│   │       └── GSAP-style 高度展开动画 → 恢复完整渲染
│   │
│   ├── [Hot Zone] 最近 30 轮, 完全渲染
│   │   │
│   │   ├── UserMessageControl (用户消息气泡)  [R: components/Message.tsx UserMessage]
│   │   │   ├── 用户头像/图标
│   │   │   ├── 消息文本 (支持多行)
│   │   │   ├── AttachmentPreviewPanel (附件缩略图)  [R: lib/attachmentDisplay.ts]
│   │   │   └── 发送时间戳
│   │   │
│   │   ├── AssistantMessageControl (AI 回复卡片)  [R: components/Message.tsx AssistantMessage]
│   │   │   ├── ThinkingPanel (可折叠推理面板)  [R: Message.tsx .reasoning]
│   │   │   ├── MarkdownPanel (Markdown 渲染正文)
│   │   │   ├── QuoteButton (选中文本后浮出的引用按钮)  [C: components/chat/QuoteButton.tsx]
│   │   │   └── TurnActionsBar (轮次操作: 复制/摘要/回滚)  [R: Message.tsx TurnActions]
│   │   │
│   │   ├── ToolCardControl (工具调用卡片)  [R: components/ToolCard.tsx]
│   │   │   ├── 状态图标 (运行中 ⏳ / 完成 ✅ / 错误 ❌ / 停止 ◼)
│   │   │   ├── 工具名 + 执行时长 (ms)
│   │   │   ├── 参数摘要 (一行文本)
│   │   │   ├── 可折叠详情:
│   │   │   │   ├── 参数 JSON (CodeBlock 渲染)
│   │   │   │   ├── 执行结果 (CodeBlock 渲染)
│   │   │   │   ├── DiffViewControl (修改前后对比)  [R: components/InlineDiff.tsx]
│   │   │   │   └── 错误信息 (.tool__err)
│   │   │   └── 嵌套 ToolCard (子工具递归渲染)
│   │   │
│   │   ├── ReadOnlyBatchPanel (只读工具聚合)  [R: components/ReadOnlyBatch.tsx]
│   │   │   ├── 折叠摘要: "3 queries · 2 reads · 1 calc"
│   │   │   ├── 展开 → 多个 ToolCard
│   │   │   └── GSAP-style 动画
│   │   │
│   │   ├── SearchResultsPanel (知识库搜索结果)  [C: components/chat/SearchResultsDisplay.tsx]
│   │   │   ├── 结果来源标记 (api/pml/pattern/domain)
│   │   │   ├── 置信度标记 (verified/medium)
│   │   │   ├── 代码片段 (CodeBlock)
│   │   │   └── 来源文件路径
│   │   │
│   │   ├── TaskTrackingPanel (任务进度条)  [R: components/TodoPanel.tsx]
│   │   │   ├── 折叠态: "T1✅ T2⚙ T3 — 2/3 完成"
│   │   │   ├── 展开 → 任务列表
│   │   │   │   ├── 状态图标 (⏳pending/⚙running/✅done/❌failed)
│   │   │   │   ├── 任务摘要
│   │   │   │   └── 执行证据 (evidence)
│   │   │   └── 完成计数 "2/3"
│   │   │
│   │   ├── PromptShelfControl (审批/问答条)  [R: components/PromptShelf.tsx]
│   │   │   ├── approval 模式: 审批按钮 [1-4]
│   │   │   ├── clarify 模式: 选项按钮 (+ 自定义输入)
│   │   │   ├── notify 模式: 纯信息提示
│   │   │   └── 键盘快捷键 1/2/3/4/Escape
│   │   │
│   │   ├── CompletionCard (任务完成卡片)
│   │   │   ├── 完成摘要 + Token/费用统计
│   │   │   └── 操作: [查看详情] [导出 PML] [复制结果]
│   │   │
│   │   └── ErrorPanel (错误面板)
│   │       ├── 错误描述
│   │       ├── 堆栈/详情 (可折叠)
│   │       └── [重试] [取消]
│   │
│   └── [底部] InputPanel (输入区域)  [R: components/Composer.tsx]
│       ├── TaskTrackingBar (任务进度条, 输入框上方)
│       ├── AttachmentBar (附件预览条)
│       ├── RichInputBox (主输入框, 自动高度)
│       │   ├── @ElementSearchMenu (E3D 元素搜索菜单)  [R: Composer @文件引用]
│       │   ├── /SlashCommandMenu (斜杠命令菜单)  [R: components/SlashMenu.tsx]
│       │   └── Plan/Act ModeSwitch (模式切换按钮)  [C: ChatTextArea Plan/Act toggle]
│       ├── btnSend (发送按钮)
│       ├── btnAttach (附件按钮)
│       └── btnStop (停止按钮, 仅 AI 运行中显示)
│
├── [底部] StatusBarControl (状态栏)  [R: components/StatusBar.tsx]
│   ├── Plan/Act 模式指示
│   ├── Token 用量 (本回合 / 总计)
│   ├── 上下文窗口使用率
│   ├── 模型名称
│   └── 缓存命中率
│
└── [弹窗层]
    ├── ApprovalDialog (审批模态框)  [R: components/ApprovalModal.tsx]
    ├── SettingsPanel (设置面板)  [R: components/SettingsPanel.tsx]
    ├── HistoryPanel (历史会话)  [C: components/history/HistoryView.tsx]
    └── FilePickerDialog (文件选择 → read_file 工具)
```

---

## 三、核心技术约束：WinForms 的"减法编程"

> E小智使用 WinForms GDI+ 渲染，需要将 React/Tailwind/GSAP 的现代化 UI 模式"翻译"为等效的 WinForms 实现。

### 3.0 能力映射表

| React/Web 能力 | WinForms 等效 | 说明 |
|---------------|-------------|------|
| Tailwind CSS 原子类 | 自定义 `StyleHelper` 静态方法 | 颜色/间距/字体集中管理 |
| CSS Variables 主题 | WinForms `AppTheme` 静态类 + `OnSystemColorsChanged` | 动态切换，无需重绘 |
| GSAP 动画 | `System.Windows.Forms.Timer` + 高度/透明度插值 | ~16ms 一帧，60fps 近似 |
| react-virtuoso 虚拟滚动 | 自定义 `VirtualScrollPanel`，只渲染可视区行 | 消息量大时的核心性能优化 |
| JSX 组件组合 | WinForms `UserControl` 嵌套 | 一一对应 |
| gRPC/事件流 | `event Action<CopilotEvent>` + `BeginInvoke()` | 线程安全的 UI 更新 |
| React Context | 静态 `AppState` 单例 + 事件订阅 | 全局状态共享 |
| CSS `backdrop-filter` | `Graphics.DrawImage` + 半透明层 | 玻璃拟态效果 |
| `border-radius: 12px` | `Region = new Region(GetRoundedRectPath(...))` | 圆角控件 |
| Syntax Highlighting | 自定义 `SyntaxHighlightTextBox` (RichTextBox 子类) | 代码/PML 着色 |
| Markdown 渲染 | 自定义 `MarkdownRenderer` → FlowLayoutPanel | 标题/表格/代码/列表/粗斜体 |
| IME 组合输入 | `TextBox.ImeMode = ImeMode.On`，监听 `WM_IME_COMPOSITION` | 中文输入法支持 |
| `grid-template-rows: 1fr auto` | WinForms `TableLayoutPanel` + `Percent=100;AutoSize` | 布局驱动 |

---

## 四、核心组件详细设计

---

### 4.1 CopilotForm — 主窗口

> 参考：
> [C: `components/chat/ChatView.tsx`] — ChatView 主路由 + 消息管线
> [C: `components/chat/chat-view/components/layout/ChatLayout.tsx`] — CSS Grid 1fr auto 布局
> [R: `components/Transcript.tsx`] — 三层渲染容器 + 滚动管理

```csharp
public class CopilotForm : UserControl
{
    // ===== 布局 =====
    private TableLayoutPanel mainLayout;  // 1行: 100% (消息) + AutoSize (输入) + AutoSize (状态)
    private NavToolbar toolbar;
    private ChatListBox chatList;
    private InputPanel inputPanel;
    private StatusBarControl statusBar;

    // ===== 弹窗 =====
    private ApprovalDialog approvalDialog;
    private SettingsPanel settingsPanel;
    private HistoryPanel historyPanel;

    // ===== 状态 =====
    private CopilotController controller;
    private AppMode currentMode = AppMode.Act;  // Plan / Act

    public CopilotForm()
    {
        InitializeComponent();
        // 主布局：消息区占满剩余空间
        mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // 子控件
        toolbar = new NavToolbar { Dock = DockStyle.Top, Height = 36 };
        chatList = new ChatListBox { Dock = DockStyle.Fill };
        inputPanel = new InputPanel { Dock = DockStyle.Fill };
        statusBar = new StatusBarControl { Dock = DockStyle.Fill, Height = 24 };

        mainLayout.Controls.Add(chatList, 0, 0);
        mainLayout.Controls.Add(inputPanel, 0, 1);
        mainLayout.Controls.Add(statusBar, 0, 2);

        // 事件订阅（仿 Cline gRPC streaming → WinForms Event）
        CopilotController.EventSink.OnEvent += HandleEvent;
    }

    private void HandleEvent(CopilotEvent evt)
    {
        this.BeginInvoke(() =>  // 确保 UI 线程
        {
            switch (evt.Kind)
            {
                case EventKind.UserMessage:
                    chatList.AddUserMessage(evt.Text, evt.Attachments);
                    break;
                case EventKind.TextChunk:          // 流式文本
                    chatList.AppendStreamText(evt.Text);
                    break;
                case EventKind.ReasoningChunk:     // 流式推理
                    chatList.AppendReasoning(evt.Text);
                    break;
                case EventKind.ReasoningEnd:        // 推理完成
                    chatList.FinalizeReasoning();
                    break;
                case EventKind.ToolDispatch:
                    chatList.AddToolCard(evt.ToolId, evt.ToolName, evt.Summary);
                    break;
                case EventKind.ToolResult:
                    chatList.UpdateToolResult(evt.ToolId, evt.Result, evt.IsError, evt.Diff);
                    break;
                case EventKind.SearchResult:        // search_knowledge 结果
                    chatList.AddSearchResults(evt.SearchResults);
                    break;
                case EventKind.ApprovalRequest:
                    chatList.ShowPromptShelf(evt.Approval);
                    break;
                case EventKind.TaskUpdate:          // task 进度更新
                    chatList.UpdateTaskProgress(evt.Tasks);
                    break;
                case EventKind.Error:
                    chatList.ShowError(evt.ErrorText, evt.IsRetryable);
                    break;
                case EventKind.TurnDone:
                    chatList.FinalizeTurn();
                    statusBar.UpdateTokens(evt.Tokens);
                    break;
                case EventKind.Completion:
                    chatList.ShowCompletion(evt.Summary, evt.Tokens, evt.Cost);
                    break;
            }
        });
    }
}
```

---

### 4.2 ChatListBox — 消息列表（三级渲染 + 消息合并管线）

> 参考：
> [R: `components/Transcript.tsx`] — Cold/Warm/Hot 分区策略 + scrollVersion 流式滚动
> [R: `lib/useScrollManager.ts`] — 自动滚动到底部 + 滚动到消息
> [C: `components/chat/ChatView.tsx`] — combineApiRequests / combineHookSequences 消息合并管线
> [C: `components/chat/chat-view/components/layout/MessagesArea.tsx`] — react-virtuoso 虚拟滚动

#### 4.2.1 三级渲染策略

```
数据模型:
  Session = {
    coldItems:  List<Turn>,     // 最早历史，分页加载，仅存 ID + 摘要
    warmItems:  List<WarmTurn>,  // 中间轮次，存摘要 + 工具统计
    hotItems:   List<Turn>,      // 最近 30 轮，完整渲染
  }

视觉布局:
  ┌─────────────────────────────────────────┐
  │  [Cold Zone]  ← 分页加载按钮             │
  │  [加载更多 (第 1-50 轮)...]               │
  ├─────────────────────────────────────────┤
  │  [Warm Zone]  折叠摘要卡片               │
  │  [▶ 第 51 轮: PIPE 查询 (3 queries)]     │
  │  [▶ 第 52 轮: 批量属性修改 (38 根管道)]   │
  │  [▶ 第 53 轮: 间距检查 (5 处违规)]       │
  ├─────────────────────────────────────────┤
  │  [Hot Zone]  最近 30 轮，完全渲染         │
  │  用户消息 / AI 回复 / ToolCard / ...    │
  └─────────────────────────────────────────┘
```

#### 4.2.2 消息合并管线（借鉴 Cline）

```
Cline 管线: combineHookSequences → combineErrorRetryMessages → combineApiRequests → combineCommandSequences
E小智管线:  combineToolBatches    → combineSearchResults  → groupLowRiskTools
```

- `combineToolBatches`：连续的只读 query/check/calculate 聚合为 ReadOnlyBatchPanel
- `combineSearchResults`：连续的 search_knowledge 调用合并到一个 SearchResultsPanel
- `groupLowRiskTools`：低风险工具（read_file 等）折叠显示

#### 4.2.3 核心代码

```csharp
public class ChatListBox : VirtualScrollPanel  // 自定义虚拟滚动 Panel
{
    // ===== 数据模型 =====
    private const int HOT_TURNS = 30;
    private const int PAGE_SIZE = 20;
    private Session session = new Session();
    private Turn currentStreamingTurn;

    // ===== 消息合并管线状态 =====
    private ReadOnlyBatchPanel currentBatch;
    private SearchResultsPanel currentSearchBatch;

    // ===== 流式渲染 =====
    public void AppendStreamText(string text)
    {
        if (currentStreamingTurn == null)
        {
            currentStreamingTurn = new Turn { IsStreaming = true };
            session.hotItems.Add(currentStreamingTurn);
        }
        currentStreamingTurn.AssistantText += text;
        InvalidateItem(session.hotItems.IndexOf(currentStreamingTurn));
    }

    public void AppendReasoning(string text)
    {
        // 首次推理创建 ThinkingPanel
        if (currentStreamingTurn.ReasoningPanel == null)
        {
            currentStreamingTurn.ReasoningPanel = new ThinkingPanel();
            AddChildControl(currentStreamingTurn.ReasoningPanel);
        }
        currentStreamingTurn.ReasoningText += text;
        // 流式期间自动展开（仿 Reasonix 行为）
        currentStreamingTurn.ReasoningPanel.Expand(animated: false);
    }

    public void FinalizeReasoning()
    {
        if (currentStreamingTurn?.ReasoningPanel != null)
        {
            currentStreamingTurn.ReasoningPanel.SetComplete(DateTime.Now);
            // 完成后自动折叠（除非用户已手动操作）
            if (!currentStreamingTurn.ReasoningPanel.IsManualToggle)
                currentStreamingTurn.ReasoningPanel.Collapse(animated: true);
        }
    }

    // ===== 消息合并：只读工具聚合 =====
    public void AddToolCard(string toolId, string name, string summary)
    {
        var isReadOnly = IsReadOnlyTool(name);

        if (isReadOnly && currentBatch != null)
        {
            // 追加到当前批次
            currentBatch.AddTool(new ToolItem(toolId, name, summary));
            return;
        }

        if (isReadOnly && currentBatch == null)
        {
            // 创建新批次
            currentBatch = new ReadOnlyBatchPanel();
            currentBatch.AddTool(new ToolItem(toolId, name, summary));
            session.hotItems.Last().AddItem(currentBatch);
            return;
        }

        // 写操作工具 → 结束当前批次
        FinalizeBatch();
        var card = new ToolCardControl(toolId, name, summary);
        session.hotItems.Last().AddItem(card);
    }

    // ===== 搜索结果显示 =====
    public void AddSearchResults(List<SearchResultItem> results)
    {
        if (currentSearchBatch == null)
        {
            currentSearchBatch = new SearchResultsPanel();
            session.hotItems.Last().AddItem(currentSearchBatch);
        }
        currentSearchBatch.AppendResults(results);
    }

    // ===== 任务追踪 =====
    public void UpdateTaskProgress(List<TaskItem> tasks)
    {
        var panel = session.hotItems.Last()?.TaskPanel;
        if (panel == null)
        {
            panel = new TaskTrackingPanel();
            session.hotItems.Last().AddItem(panel);
        }
        panel.Update(tasks);
    }

    // ===== 审批条 =====
    public void ShowPromptShelf(PendingApproval approval)
    {
        FinalizeBatch();
        var shelf = new PromptShelfControl(approval);
        session.hotItems.Last().AddItem(shelf);
    }

    // ===== 轮次结束 =====
    public void FinalizeTurn()
    {
        FinalizeBatch();
        FinalizeSearchBatch();
        currentStreamingTurn.IsStreaming = false;
        currentStreamingTurn = null;

        // Hot Zone 超过 30 轮 → 移动最旧的到 Warm Zone
        if (session.hotItems.Count > HOT_TURNS)
        {
            var oldest = session.hotItems.First();
            session.hotItems.RemoveAt(0);
            session.warmItems.Add(WarmTurn.FromTurn(oldest));
        }
    }

    private void FinalizeBatch()
    {
        if (currentBatch != null)
        {
            currentBatch.Finalize();
            currentBatch = null;
        }
    }
}
```

---

### 4.3 ToolCardControl — 工具调用卡片（完整版）

> 参考：
> [R: `components/ToolCard.tsx`] — 完整的工具卡片实现 (状态图标/嵌套/时长/GSAP动画)
> [R: `components/ProcessCard.tsx`] — 通用可折叠卡片基类
> [R: `components/DiffView.tsx`] — 编辑器 Seam Diff 视图
> [R: `components/InlineDiff.tsx`] — 内联紧凑 Diff 视图
> [R: `lib/tools.ts`] — 工具名→图标/摘要映射

#### 4.3.1 工具状态图标

| 状态 | 图标 | 说明 |
|------|:----:|------|
| running | ⏳ 动画点 | 执行中，黄色/蓝色脉冲动画 |
| done | ✅ | 成功完成，绿色 |
| done_quiet | ✅ 低调 | 只读工具完成，无特殊样式 |
| error | ❌ | 执行失败，红色 |
| stopped | ◼ | 用户手动停止 |
| nested | ⊞N | 包含 N 个嵌套子工具调用 |

动画：running 状态使用 `System.Windows.Forms.Timer` 实现脉冲透明度动画（在 WinForms 中等效 CSS `@keyframes iconPulse`）。

#### 4.3.2 完整的卡片结构

```
┌──────────────────────────────────────────────────────────┐
│ ⊞3 │ 🔍 search_knowledge · 89 ms                        │  ← 头部
│     │ 搜索: "管道壁厚属性" → 3 条结果                     │  ← 摘要
│     │                                                    │
│     │ ▼ 展开详情                                         │  ← 折叠/展开按钮
│ ┌───┴──────────────────────────────────────────────────┐ │
│ │  参数 (JSON):                                        │ │
│ │  { "source": "domain", "query": "壁厚 属性" }        │ │
│ │                                                      │ │
│ │  结果 (3 条):                                        │ │
│ │  ┌───────────────────────────────────────────────┐   │ │
│ │  │ ✅ [domain] WTHK = 壁厚 (wall thickness)      │   │ │  ← 已验证标记
│ │  │    PML: WTHK of <element>                     │   │ │
│ │  │    file: knowledge/domain/attribute_map.md    │   │ │
│ │  ├───────────────────────────────────────────────┤   │ │
│ │  │ ✅ [api] DbElement.GetAsString(DbAttribute)   │   │ │  ← 置信度 high
│ │  │    namespace: Aveva.Core.Database              │   │ │
│ │  │    verified: true                             │   │ │
│ │  └───────────────────────────────────────────────┘   │ │
│ └──────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│ ✏️ modify · 45 ms                                        │
│ 修改 PIPE-001.WTHK = "SCH40"                             │
│                                                          │
│ ▼ 展开详情                                               │
│ ┌──────────────────────────────────────────────────────┐ │
│ │ 修改前后对比:                         +1  −1  [复制] │ │  ← DiffViewControl
│ │ ─────────────────────────────────────────────────── │ │
│ │  line 10:  WTHK = "SCH30"    (删除, 红色)          │ │
│ │ +line 10:  WTHK = "SCH40"    (新增, 绿色)          │ │
│ │  line 11:  FLUID = "WATER"   (不变, 灰色)          │ │
│ └──────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────┘
```

#### 4.3.3 核心代码（完整版）

```csharp
public class ToolCardControl : UserControl
{
    // ===== UI 子控件 =====
    private PictureBox statusIcon;      // 状态图标 (⏳/✅/❌/◼)
    private Label nestedBadge;          // ⊞N 嵌套计数
    private Label headerLabel;          // 工具名 + 时长
    private Label summaryLabel;         // 操作摘要
    private Button toggleButton;        // [▶展开]/[▼收起]
    private Panel detailPanel;          // 可折叠详情面板
    private CodeBlock argsCode;         // 参数 JSON
    private CodeBlock resultCode;       // 结果/输出
    private DiffViewControl diffView;   // Diff 对比
    private Label errorLabel;           // 错误信息 (红色)
    private FlowLayoutPanel nestedTools;// 嵌套子工具列表

    // ===== 状态 =====
    private bool isOpen = false;
    private ToolStatus status;
    private int targetDetailHeight;
    private Timer animationTimer;

    // ===== 公共方法 =====
    public void SetStatus(ToolStatus status, int durationMs)
    {
        this.status = status;
        headerLabel.Text = $"{GetToolIcon(status, ToolName)} {ToolName} · {durationMs} ms";
        switch (status)
        {
            case ToolStatus.Running:
                statusIcon.Image = Resources.Spinner;
                StartPulseAnimation();  // 脉冲动画
                break;
            case ToolStatus.Done:
                statusIcon.Image = Resources.Check;
                StopPulseAnimation();
                ApplyStyle(ToolStyle.Done);
                break;
            case ToolStatus.Error:
                statusIcon.Image = Resources.Error;
                StopPulseAnimation();
                ApplyStyle(ToolStyle.Error);
                break;
        }
    }

    public void SetDiff(string before, string after)
    {
        diffView.Visible = true;
        resultCode.Visible = false;
        diffView.SetDiff(before, after);
    }

    public void SetResult(string text, bool isError = false)
    {
        diffView.Visible = false;
        resultCode.Visible = true;
        resultCode.SetCode(text, isError ? SyntaxHighlight.HighlightType.PML : SyntaxHighlight.HighlightType.JSON);
    }

    public void AddNestedTool(ToolCardControl child)
    {
        nestedTools.Controls.Add(child);
        nestedBadge.Text = $"⊞{nestedTools.Controls.Count}";
        nestedBadge.Visible = true;
    }

    // ===== 动画 =====
    private void ToggleExpand()
    {
        isOpen = !isOpen;
        // WinForms 等效 GSAP 折叠动画: Timer 逐帧插值
        AnimateHeight(detailPanel, isOpen ? targetDetailHeight : 0, durationMs: 340);
    }

    private void AnimateHeight(Control ctrl, int targetHeight, int durationMs)
    {
        int startHeight = ctrl.Height;
        int stepCount = durationMs / 16;  // ~16ms 一帧 ≈ 60fps
        int step = 0;

        animationTimer = new Timer { Interval = 16 };
        animationTimer.Tick += (s, e) =>
        {
            step++;
            float progress = Math.Min(1f, (float)step / stepCount);
            // Ease out cubic: 仿 GSAP power2.out
            float eased = 1 - (1 - progress) * (1 - progress) * (1 - progress);
            ctrl.Height = (int)(startHeight + (targetHeight - startHeight) * eased);
            if (progress >= 1) animationTimer.Stop();
        };
        animationTimer.Start();
    }
}
```

---

### 4.4 ThinkPanel — 可折叠推理面板

> 参考：
> [R: `components/Message.tsx`] — AssistantMessage 内 `.reasoning` 区域
> [R: `lib/reasoningDisplay.ts`] — 推理文本展示逻辑
> [C: `components/chat/ThinkingRow.tsx`] — 推理行渲染 + FeatureTip

```csharp
public class ThinkingPanel : UserControl
{
    // ===== UI =====
    private PictureBox brainIcon;    // 💭 大脑图标
    private Label titleLabel;        // "思考中..." / "思考完成 (3.2s)"
    private Button toggleButton;     // 展开/折叠
    private Panel bodyPanel;         // 推理内容 (可折叠)
    private RichTextBox contentBox;  // 推理文本

    // ===== 状态 =====
    private bool isStreaming = false;
    private bool isManualToggle = false;  // 用户是否手动操作过
    private DateTime startTime;

    // ===== 流式状态 =====
    public void AppendChunk(string chunk)
    {
        isStreaming = true;
        contentBox.AppendText(chunk);
        // 流式期间自动展开
        if (!bodyPanel.Visible)
            Expand(animated: false);
    }

    public void SetComplete(DateTime endTime)
    {
        isStreaming = false;
        var elapsed = endTime - startTime;
        titleLabel.Text = $"思考完成 ({elapsed.TotalSeconds:F1}s)";
        // 自动折叠（除非用户已手动操作）
        if (!isManualToggle)
            Collapse(animated: true);
    }

    private void ToggleManual()
    {
        isManualToggle = true;  // 标记用户已手动操作，不再自动折叠
        if (bodyPanel.Visible)
            Collapse(animated: true);
        else
            Expand(animated: true);
    }

    public void Expand(bool animated)
    {
        if (animated)
            AnimateHeight(bodyPanel, targetHeight: 200, durationMs: 300);
        else
            bodyPanel.Visible = true;
    }

    public void Collapse(bool animated)
    {
        if (animated)
            AnimateHeight(bodyPanel, targetHeight: 0, durationMs: 300);
        else
            bodyPanel.Visible = false;
    }

    // 仿 Reasonix 行为:
    // - 流式期间 → 始终展开
    // - 推理完成 → 自动折叠（除非用户点过手动开关）
    // - "Thought for X.Xs" 显示推理耗时
}
```

---

### 4.5 PromptShelfControl — 统一审批/问答容器

> 参考：
> [R: `components/PromptShelf.tsx`] — 通用 PromptShelf 布局 (bar + panel + crumbs + quickActions)
> [R: `components/ApprovalModal.tsx`] — 审批对话框 (Plan 审批 / 工具审批)
> [R: `components/AskCard.tsx`] — AI 提问卡片
> [R: `components/ClearContextCard.tsx`] — 上下文清理确认卡片
> [C: `components/chat/OptionsButtons.tsx`] — Followup 问题选项按钮组

```csharp
public class PromptShelfControl : UserControl
{
    // ===== 布局 (仿 Reasonix .prompt-shelf) =====
    // .prompt-shelf__bar (.prompt-shelf__summary + .prompt-shelf__actions)
    // .prompt-shelf__panel (展开面板内容)
    private Panel barPanel;             // 提示栏
    private PictureBox iconBox;         // 状态图标
    private Label titleLabel;           // 标题
    private Label metaLabel;            // 元信息
    private FlowLayoutPanel actionButtons; // 操作按钮组 (带编号)
    private Panel detailPanel;          // 详情面板 (可展开)
    private RichTextBox revisionBox;    // 修订输入框 (Plan 模式)

    public void Show(PromptType type, string title, string meta, List<PromptAction> actions)
    {
        iconBox.Image = GetPromptIcon(type);
        titleLabel.Text = title;
        metaLabel.Text = meta;

        actionButtons.Controls.Clear();
        for (int i = 0; i < actions.Count; i++)
        {
            var btn = new PromptActionButton
            {
                KeyNumber = i + 1,
                Text = actions[i].Label,
                IsDefault = actions[i].IsDefault,
                Tag = actions[i]
            };
            btn.Click += (s, e) => actions[i].Callback();
            actionButtons.Controls.Add(btn);
        }

        this.Visible = true;
        // 键盘焦点
        this.Focus();
    }

    // 键盘快捷键 (仿 Reasonix 1/2/3/4/Escape)
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.D1: ((Button)actionButtons.Controls[0]).PerformClick(); return true;
            case Keys.D2: ((Button)actionButtons.Controls[1]).PerformClick(); return true;
            case Keys.D3: ((Button)actionButtons.Controls[2]).PerformClick(); return true;
            case Keys.D4: ((Button)actionButtons.Controls[3]).PerformClick(); return true;
            case Keys.Escape: RejectAction(); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
```

**PromptType 枚举**（映射 ask_user 工具的 type 参数）：

| PromptType | 对应 ask_user type | 图标 | 按钮配置 |
|-----------|-------------------|:----:|------|
| `approve_plan` | `approve_plan` | 📋 | [1]修订 [2]执行 [3]退出 |
| `approve_tool` | `approve_tool` | ⚠️ | [1]执行 [2]会话允许 [3]持久化 [4]拒绝 |
| `confirm` | `confirm` | ❓ | [1]确认 [2]取消 |
| `clarify` | `clarify` | ❓ | 动态选项 + 自定义输入 |
| `notify` | `notify` | ℹ️ | [1]知道了 |
| `destructive` | (风险level=destructive) | 🔴 | [1]确认删除(红色) [2]取消 |

---

### 4.6 TaskTrackingPanel — 任务进度面板

> 参考：
> [R: `components/TodoPanel.tsx`] — Todo 面板 (输入框上方，可折叠，完成计数)
> [C: `components/chat/task-header/TaskHeader.tsx`] — 任务标题栏 (token 统计 + FocusChain)

```csharp
public class TaskTrackingPanel : UserControl
{
    // ===== 布局 =====
    private Label summaryLabel;          // "T1✅ T2⚙ T3 — 2/3 完成"
    private FlowLayoutPanel taskList;    // 展开的任务项列表
    private bool isExpanded = false;

    public void Update(List<TaskItem> tasks)
    {
        int done = tasks.Count(t => t.Status == TaskStatus.Done);
        int total = tasks.Count;
        summaryLabel.Text = $"{string.Join(" ", tasks.Select(TaskIcon))} — {done}/{total} 完成";

        taskList.Controls.Clear();
        foreach (var task in tasks)
        {
            taskList.Controls.Add(new TaskItemControl
            {
                Status = task.Status,
                Summary = task.Summary,
                Evidence = task.Evidence  // [R: complete_step evidence]
            });
        }
    }

    private static string TaskIcon(TaskItem t) => t.Status switch
    {
        TaskStatus.Pending   => $"T{t.Id}⏳",
        TaskStatus.Running   => $"T{t.Id}⚙",
        TaskStatus.Done      => $"T{t.Id}✅",
        TaskStatus.Failed    => $"T{t.Id}❌",
        _ => ""
    };
}

public class TaskItemControl : UserControl
{
    // 显示单条任务：
    // 状态图标 + 任务摘要 + (if Done) evidence 摘要
    // (if Failed) 错误原因 + [重试] 按钮
}
```

---

### 4.7 DiffViewControl — 完整差异视图

> 参考：
> [R: `components/InlineDiff.tsx`] — 紧凑内联差异视图 (12行默认，展开全部，+N -N 统计)
> [R: `editors/HljsDiff.tsx`] — highlight.js 行级 Diff 渲染
> [R: `lib/diff.ts`] — LCS diff 算法
> [C: `components/chat/DiffEditRow.tsx`] — 文件 patch diff 预览

```csharp
public class DiffViewControl : UserControl
{
    // ===== 布局 =====
    private Label statsLabel;           // "+3 −1"
    private Button copyButton;          // [复制]
    private Panel diffContent;          // 差异行列表
    private bool isExpanded = false;

    // ===== 配置 =====
    private const int DEFAULT_VISIBLE_LINES = 12;  // 仿 Reasonix，默认显示 12 行

    public void SetDiff(string before, string after)
    {
        // 使用 LCS 算法计算行级差异
        var diffLines = LcsDiff.Compute(before, after);

        int added = diffLines.Count(l => l.Type == DiffType.Add);
        int removed = diffLines.Count(l => l.Type == DiffType.Remove);
        statsLabel.Text = $"+{added} −{removed}";

        // 渲染差异行
        diffContent.Controls.Clear();
        for (int i = 0; i < Math.Min(diffLines.Count, DEFAULT_VISIBLE_LINES); i++)
        {
            diffContent.Controls.Add(new DiffLineControl(diffLines[i]));
        }

        if (diffLines.Count > DEFAULT_VISIBLE_LINES)
        {
            // "显示全部 458 行" 按钮 (仿 Reasonix)
            var showAll = new Button { Text = $"▼ 显示全部 {diffLines.Count} 行" };
            showAll.Click += (s, e) => ExpandAll(diffLines);
            diffContent.Controls.Add(showAll);
        }
    }

    private void CopyDiff()
    {
        var sb = new StringBuilder();
        foreach (DiffLineControl line in diffContent.Controls.OfType<DiffLineControl>())
            sb.AppendLine(line.GetDiffLine());
        Clipboard.SetText(sb.ToString());
    }
}

public class DiffLineControl : UserControl
{
    // ===== 行渲染 =====
    // 背景色: 新增=LightGreen, 删除=LightCoral, 不变=White
    // 格式: "行号 | 符号 | 代码内容"
    //   新增: "+ line 10: WTHK = 'SCH40'"  (绿色)
    //   删除: "- line 10: WTHK = 'SCH30'"  (红色)
    //   不变: "  line 11: FLUID = 'WATER'" (灰色)
}
```

---

### 4.8 SearchResultsPanel — 知识库搜索结果

> 参考：
> [C: `components/chat/SearchResultsDisplay.tsx`] — 搜索结果展示组件
> [R: `components/ToolCard.tsx`] — 工具结果的 JSON / CodeBlock 渲染

```csharp
public class SearchResultsPanel : UserControl
{
    private FlowLayoutPanel resultList;

    public void AppendResults(List<SearchResultItem> results)
    {
        foreach (var r in results)
        {
            resultList.Controls.Add(new SearchResultItemControl
            {
                Source = r.Source,           // api / pml / pattern / domain
                Verified = r.Verified,       // true → 绿色 ✅, false → 灰色 ◻
                Signature = r.Signature,     // API 签名 / PML 语法 / 黄金代码
                Description = r.Description, // 中文说明
                File = r.File,              // 来源文件路径
                Code = r.Code,              // 代码片段 (CodeBlock 渲染)
            });
        }
    }
}

public class SearchResultItemControl : UserControl
{
    // ===== 渲染结构 =====
    // ┌──────────────────────────────────────┐
    // │ ✅ [api] DbElement.GetAsString(...)  │  ← 已验证标记 + 来源 + 签名
    // │ 获取元素的指定属性值                   │  ← 描述
    // │ source: knowledge/api/Core.Database/ │  ← 文件路径
    // │ code: var wthk = element.GetAsString │  ← 代码片段
    // │        (DbAttributeInstance.WTHK);   │
    // └──────────────────────────────────────┘
}
```

---

### 4.9 InputPanel — 完整输入区域

> 参考：
> [R: `components/Composer.tsx`] — ~3400 行超大型输入组件 (文本输入/附件/斜杠命令/@文件引用/意图菜单/Paste管理)
> [R: `components/SlashMenu.tsx`] — 斜杠命令菜单
> [R: `components/VirtualMenu.tsx`] — 虚拟化菜单 (大数据量自动补全)
> [C: `components/chat/ChatTextArea.tsx`] — 核心输入框 (DynamicTextArea + @mention + Plan/Act toggle)
> [C: `components/chat/SlashCommandMenu.tsx`] — 斜杠命令自动完成

```csharp
public class InputPanel : UserControl
{
    // ===== 布局 =====
    private TaskTrackingBar taskBar;     // 任务进度条 (输入框上方)
    private AttachmentBar attachmentBar; // 附件预览条
    private RichTextBox inputBox;        // 主输入框
    private Button btnSend;              // 发送
    private Button btnAttach;            // 附件
    private Button btnStop;              // 停止 (AI 运行时显示)

    // ===== 浮层菜单 =====
    private ContextMenuStrip slashMenu;     // / 命令菜单
    private ContextMenuStrip elementMenu;   // @ 元素搜索菜单
    private ModeSwitchButton modeSwitch;    // Plan/Act 模式切换

    // ===== IME 支持 =====
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        inputBox.ImeMode = ImeMode.On;  // 开启中文输入法
    }

    // ===== @提及 → E3D 元素搜索 =====
    private void InputBox_TextChanged(object sender, EventArgs e)
    {
        int cursorPos = inputBox.SelectionStart;
        string textBeforeCursor = inputBox.Text.Substring(0, cursorPos);

        // 检测 @ 触发
        int atPos = textBeforeCursor.LastIndexOf('@');
        if (atPos >= 0 && (atPos == 0 || textBeforeCursor[atPos - 1] == ' '))
        {
            string searchText = textBeforeCursor.Substring(atPos + 1);
            ShowElementSearchMenu(searchText);
        }

        // 检测 / 触发
        int slashPos = textBeforeCursor.LastIndexOf('/');
        if (slashPos >= 0 && (slashPos == 0 || textBeforeCursor[slashPos - 1] == ' '))
        {
            string searchText = textBeforeCursor.Substring(slashPos + 1);
            ShowSlashCommandMenu(searchText);
        }
    }

    // ===== E3D 元素搜索菜单 (仿 Cline/Reasonix 的 @文件引用) =====
    private void ShowElementSearchMenu(string filter)
    {
        // 调用 E3D API 搜索当前模型中的元素
        // 返回匹配的元素列表 (NAME + TYPE + 位置)
        var matches = CopilotController.QueryElements(filter, limit: 20);

        elementMenu.Items.Clear();
        foreach (var elem in matches)
        {
            elementMenu.Items.Add(new ToolStripMenuItem
            {
                Text = $"{elem.Type} {elem.Name} ({elem.Zone})",
                Tag = elem,
            });
        }
        elementMenu.Show(inputBox, new Point(0, inputBox.Height));
    }

    // ===== 斜杠命令菜单 =====
    private void ShowSlashCommandMenu(string filter)
    {
        slashMenu.Items.Clear();
        var commands = new[]
        {
            ("/plan", "进入计划模式，先生成执行计划"),
            ("/act", "进入执行模式，直接操作"),
            ("/query", "查询 E3D 元素"),
            ("/check", "检查模型"),
            ("/export", "导出数据"),
            ("/clear", "清空当前会话"),
            ("/help", "显示帮助"),
        };
        foreach (var (cmd, desc) in commands)
        {
            if (string.IsNullOrEmpty(filter) || cmd.Contains(filter))
            {
                slashMenu.Items.Add(new ToolStripMenuItem { Text = $"{cmd} — {desc}", Tag = cmd });
            }
        }
        slashMenu.Show(inputBox, new Point(0, inputBox.Height));
    }

    // ===== 附件管理 =====
    public void AddAttachment(string filePath)
    {
        // 支持: 图片 (.png/.jpg)、Excel (.xlsx/.xls)、CSV (.csv)、PDF (.pdf)、TXT (.txt)
        var thumb = new AttachmentThumbControl(filePath);
        thumb.RemoveClicked += (s, e) => attachmentBar.Remove(thumb);
        attachmentBar.Add(thumb);
    }

    // ===== 大文本粘贴管理 (仿 Reasonix) =====
    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.V)  // Ctrl+V
        {
            string clipText = Clipboard.GetText();
            if (clipText.Length > 5000)
            {
                // 大文本 → 折叠为标签，可展开/替换/删除
                e.SuppressKeyPress = true;
                var tag = new PasteTagControl(clipText);
                attachmentBar.AddPasteTag(tag);
            }
        }
    }

    // ===== 发送 =====
    public event EventHandler<SendEventArgs> SendClicked;
    private void OnSend()
    {
        string text = inputBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var args = new SendEventArgs
        {
            Text = text,
            Attachments = attachmentBar.GetAttachments(),
            Mode = modeSwitch.CurrentMode,  // Plan / Act
        };
        SendClicked?.Invoke(this, args);

        inputBox.Clear();
        attachmentBar.Clear();
    }
}
```

---

### 4.10 StatusBarControl — 可配置状态栏

> 参考：
> [R: `components/StatusBar.tsx`] — 15 项可配置指标 + icon/text 显示模式 + JobsChip
> [C: `components/chat/chat-view/components/layout/ChatLayout.tsx`] — 底部状态区域布局

```csharp
public class StatusBarControl : UserControl
{
    // ===== 可配置的度量指标 (仿 Reasonix 15 项) =====
    // 通过 Settings 控制显示/隐藏哪些指标
    private Label lblMode;     // [Plan] 或 [Act]
    private Label lblModel;    // Qwen3.6-72B
    private Label lblTokens;   // 本回合: 12.3k / 总计: 1.2M
    private Label lblContext;  // 上下文: 67% (8K/12K)
    private Label lblCache;    // 缓存: 45%
    private Label lblCost;     // ¥0.42

    // ===== 运行中: StopButton 替换为停止按钮 =====
    private Button stopButton; // [■ 停止] (仅 AI 运行中显示)

    public void UpdateTokens(TokenStats stats)
    {
        lblTokens.Text = $"Token: {FormatK(stats.TurnTokens)} / {FormatK(stats.SessionTokens)}";
        lblContext.Text = $"上下文: {stats.ContextUsage:P0} ({FormatK(stats.ContextUsed)}/{FormatK(stats.ContextWindow)})";
        lblCache.Text = $"缓存: {stats.CacheHitRate:P0}";
        lblCost.Text = $"¥{stats.Cost:F2}";
    }

    public void SetRunning(bool isRunning)
    {
        stopButton.Visible = isRunning;
    }
}
```

---

### 4.11 MarkdownPanel — Markdown 渲染

> 参考：
> [R: `components/Markdown.tsx`] — react-markdown 渲染
> [C: `components/chat/MarkdownRow.tsx`] — Markdown 行渲染 + MarkdownBlock

```csharp
public class MarkdownPanel : FlowLayoutPanel
{
    // ===== 支持的 Markdown 元素 → WinForms 控件映射 =====
    // # 标题     →  Label (font: bold, size: +4)
    // ## 标题    →  Label (font: bold, size: +2)
    // **粗体**   →  Label (font: bold)
    // *斜体*     →  Label (font: italic)
    // `代码`     →  Label (font: monospace, bg: gray)
    // ```代码块``` → CodeBlock (SyntaxHighlightTextBox)
    // - 列表     →  FlowLayoutPanel + BulletLabel
    // 1. 有序    →  FlowLayoutPanel + NumberLabel
    // | 表格 |   →  TableLayoutPanel
    // > 引用     →  Panel (左边框 3px 蓝色)

    // 解析流程: 正则切分 Markdown → 逐块生成对应 WinForms 控件 → 添加到 FlowLayoutPanel
}
```

---

### 4.12 CodeBlock — 代码块渲染

> 参考：
> [C: `components/common/CodeBlock.tsx`] — 代码块渲染 (语法高亮 + 复制按钮)
> [C: `components/common/CodeAccordian.tsx`] — 可折叠代码区域
> [R: `editors/HljsCode.tsx`] — highlight.js 代码渲染 (编辑器 Seam 模式)

```csharp
public class CodeBlock : UserControl
{
    private SyntaxHighlightTextBox codeBox;  // 自定义 RichTextBox (带语法着色)
    private Button copyButton;               // [复制]
    private Label langLabel;                 // 语言标签 (PML/JSON/C#)

    public void SetCode(string code, SyntaxHighlight.HighlightType lang)
    {
        langLabel.Text = lang.ToString().ToUpper();
        codeBox.Text = code;
        codeBox.ApplyHighlight(lang);  // PML / JSON / C# 语法着色
    }
}
```

---

### 4.13 TurnActionsBar — 轮次操作栏

> 参考：
> [R: `components/Message.tsx`] — TurnActions 组件 (复制/摘要/回滚 + 两步确认)
> [C: `components/chat/task-header/buttons/`] — NewTask/CopyTask/DeleteTask 等按钮

```csharp
public class TurnActionsBar : UserControl
{
    // 操作按钮:
    // [📋 复制] — 复制本轮 AI 回复文本
    // [📝 摘要] — 请求 AI 生成本轮摘要
    // [↩ 回滚] — 两步确认: 首次点击→"确认回滚?", 二次点击→执行
    //             (仿 Reasonix TurnActions rollback 两步确认模式)

    private Button btnCopy;
    private Button btnSummarize;
    private Button btnRollback;
    private bool rollbackConfirm = false;

    private void OnRollbackClick()
    {
        if (!rollbackConfirm)
        {
            btnRollback.Text = "确认回滚?";
            btnRollback.BackColor = Color.FromArgb(220, 50, 50);  // 红色警告
            rollbackConfirm = true;
            // 3 秒后自动恢复 (仿 Reasonix)
            var timer = new Timer { Interval = 3000 };
            timer.Tick += (s, e) => { ResetRollback(); timer.Stop(); };
            timer.Start();
        }
        else
        {
            // 执行回滚
            RollbackRequested?.Invoke(this, EventArgs.Empty);
            ResetRollback();
        }
    }

    private void ResetRollback()
    {
        btnRollback.Text = "↩ 回滚";
        btnRollback.BackColor = SystemColors.Control;
        rollbackConfirm = false;
    }
}
```

---

### 4.14 NavToolbar — 顶部导航工具栏

> 参考：
> [C: `components/menu/Navbar.tsx`] — 5 标签页导航 (Chat/MCP/History/Account/Settings)
> [R: `components/AppChrome.tsx`] — 窗口标题栏 + Tab 切换

```csharp
public class NavToolbar : ToolStrip
{
    // [Plan/Act 模式切换]  — 仿 Cline Plan/Act toggle (颜色变化)
    //   Plan: 黄色/橙色背景  Act: 蓝色/绿色背景
    // [⚙ 设置]              — 弹出 SettingsPanel
    // [📜 历史]              — 弹出 HistoryPanel
    // [■ 停止]              — 红色，仅 AI 运行中可见，停止当前 AgentLoop

    private ToolStripButton btnModeToggle;
    private ToolStripButton btnSettings;
    private ToolStripButton btnHistory;
    private ToolStripButton btnStop;

    public void SetMode(AppMode mode)
    {
        btnModeToggle.Text = mode == AppMode.Plan ? "[Plan]" : "[Act]";
        btnModeToggle.BackColor = mode == AppMode.Plan
            ? Color.FromArgb(255, 200, 100)   // 黄色 (仿 Cline Plan 色)
            : Color.FromArgb(100, 180, 255);  // 蓝色 (仿 Cline Act 色)
    }

    public void SetRunning(bool isRunning)
    {
        btnStop.Visible = isRunning;
    }
}
```

---

### 4.15 SettingsPanel — 设置面板

> 参考：
> [R: `components/SettingsPanel.tsx`] — 12 标签页设置面板 (general/models/bots/mcp/skills/memory/hooks/permissions/sandbox/network/appearance/updates)
> [C: `components/settings/SettingsView.tsx`] — 多标签页设置面板 (API/Features/Browser/Terminal/General/About/Debug)

```csharp
public class SettingsPanel : Form
{
    // E小智 适配的标签页:
    // [API]        — vLLM 地址/端口/模型选择
    // [审批]       — 自动审批规则 / 风险等级映射
    // [界面]       — 主题/字体大小/状态栏显示项
    // [记忆]       — 记忆系统开关/存储位置 (Phase 3)
    // [快捷键]     — 发送/停止/审批 快捷键配置
    // [关于]       — 版本/日志/调试信息
}
```

---

### 4.16 HistoryPanel — 历史会话

> 参考：
> [C: `components/history/HistoryView.tsx`] — 历史任务列表 + 搜索 + 恢复
> [R: `components/HistoryPanel.tsx`] — 历史会话面板

```csharp
public class HistoryPanel : Form
{
    // 会话列表 (日期 + 第一句话摘要 + Token/工具统计)
    // 搜索框 → Filter 会话列表
    // 双击 → 恢复该会话 (加载历史消息到 ChatListBox)
    // 右键菜单 → [删除] [导出] [复制链接]
}
```

---

### 4.17 CompletionCard — 任务完成卡片

> 参考：
> [C: `components/chat/CompletionOutputRow.tsx`] — 任务完成结果 + 操作按钮
> [C: `components/chat/PlanCompletionOutputRow.tsx`] — Plan 模式完成输出
> [R: `lib/sessionExport.tsx`] — 会话导出 (PDF/PNG/MD)

```csharp
public class CompletionCard : UserControl
{
    // ✅ 任务完成
    // 摘要: "已修改 38 根管道壁厚为 SCH40"
    // Token: 12.3k | 费用: ¥0.42 | 耗时: 34s | 工具调用: 8 次
    //
    // [查看详情] [导出 PML 脚本] [导出 Excel] [复制结果]
}
```

---

## 五、完整消息类型路由表

| EventKind | 说明 | 渲染组件 | 操作 |
|-----------|------|---------|------|
| `UserMessage` | 用户发送的消息 | UserMessageControl | 编辑/删除/重新发送 |
| `TextChunk` | 流式 AI 回复文本 | MarkdownPanel (追加) | — |
| `ReasoningChunk` | 流式推理文本 | ThinkingPanel (追加, 自动展开) | 手动折叠切换 |
| `ReasoningEnd` | 推理完成 | ThinkingPanel → 显示耗时, 自动折叠 | — |
| `ToolDispatch` | 工具开始执行 | ToolCardControl (running 状态) | — |
| `ToolResult` | 工具执行完成 | ToolCardControl (done/error) + Diff | 折叠/展开/复制 |
| `ToolBatchDispatch` | 只读工具组开始 | ReadOnlyBatchPanel (创建) | — |
| `ToolBatchComplete` | 只读工具组完成 | ReadOnlyBatchPanel → 折叠摘要 | 展开查看详情 |
| `SearchResult` | 知识库搜索结果 | SearchResultsPanel | 查看代码/跳转来源 |
| `ApprovalRequest` | 需要用户审批 | PromptShelfControl (审批模式) | 键盘 1-4/Escape |
| `ClarifyRequest` | 需要用户澄清 | PromptShelfControl (澄清模式) | 选项/自定义输入 |
| `NotifyMessage` | 通知消息 | PromptShelfControl (通知模式) | [知道了] |
| `TaskCreate` | 创建子任务 | TaskTrackingPanel (新增) | — |
| `TaskStart` | 子任务开始 | TaskTrackingPanel (状态 → running) | — |
| `TaskComplete` | 子任务完成 | TaskTrackingPanel (状态 → done) | 查看 evidence |
| `TaskFail` | 子任务失败 | TaskTrackingPanel (状态 → failed) | [重试] |
| `TaskList` | 任务汇总 | TaskTrackingPanel → 显示全貌 | — |
| `Error` | 错误信息 | ErrorPanel + 错误描述 | [重试] [取消] |
| `TurnDone` | 本轮结束 | 消息完结 + TurnActionsBar 显示 | 复制/摘要/回滚 |
| `Completion` | 任务完成 | CompletionCard | 导出/复制 |
| `ApiRequestStart` | API 请求开始 | StatusBarControl → 模型名 | — |
| `ApiRequestEnd` | API 请求结束 | StatusBarControl → Token/费用 | — |

---

## 六、完整事件流

```
AgentLoop (后台线程) → IEventSink.Emit(CopilotEvent)
                              ↓
                   WinForms EventDispatcher (SynchronizationContext)
                              ↓
                   ┌── BeginInvoke() ──┐
                   ↓         ↓         ↓
              CopilotForm  SettingsPanel  HistoryPanel
              (主事件分发)  (独立弹窗)     (独立弹窗)
                   ↓
         ┌────────┼────────┬──────────┬─────────┐
         ↓        ↓        ↓          ↓         ↓
    ChatListBox  InputPanel  StatusBar  ApprovalDialog
    ┌──┬──┬──┐   ┌──┬──┐    ┌──┬──┐
    ↓  ↓  ↓  ↓   ↓  ↓  ↓    ↓  ↓  ↓
    UserMessageControl       TaskTrackingBar
    MarkdownPanel            AttachmentBar
    ThinkingPanel
    ToolCardControl
    ReadOnlyBatchPanel
    SearchResultsPanel
    TaskTrackingPanel
    PromptShelfControl
    CompletionCard
    ErrorPanel
    TurnActionsBar
```

---

## 七、主题系统（WinForms 适配 Cline/Reasonix 主题方案）

> 参考：
> [C: `theme.css`] — VSCode 主题变量映射 (使用 OKLCH 色彩空间)
> [R: `styles.css`] — CSS 自定义属性主题系统 + `prefers-color-scheme` 响应式
> [R: `lib/theme.ts`] — 主题管理器

### 7.1 颜色变量

```csharp
public static class AppTheme
{
    // ===== 主色调 (仿 Cline/Reasonix 暗色主题) =====
    public static Color Background    = Color.FromArgb(30, 30, 30);   // 主背景 (Cline sidebar-bg)
    public static Color Surface       = Color.FromArgb(45, 45, 45);   // 卡片/面板背景
    public static Color SurfaceHover  = Color.FromArgb(55, 55, 55);   // 悬停态
    public static Color Foreground    = Color.FromArgb(212, 212, 212); // 主文字
    public static Color MutedText     = Color.FromArgb(140, 140, 140); // 次要文字
    public static Color Border        = Color.FromArgb(60, 60, 60);    // 边框

    // ===== 语义色 =====
    public static Color Primary       = Color.FromArgb(0, 122, 204);   // 主强调色 (蓝色)
    public static Color Success       = Color.FromArgb(52, 168, 83);   // 成功 (绿色)
    public static Color Error         = Color.FromArgb(234, 67, 53);   // 错误 (红色)
    public static Color Warning       = Color.FromArgb(251, 188, 4);   // 警告 (黄色)
    public static Color PlanMode      = Color.FromArgb(255, 200, 100); // Plan 模式 (橙黄)
    public static Color ActMode       = Color.FromArgb(100, 180, 255); // Act 模式 (蓝色)

    // ===== Diff 色 =====
    public static Color DiffAdded     = Color.FromArgb(0, 100, 0, 40);  // 添加行背景 (Cline diff-added)
    public static Color DiffRemoved   = Color.FromArgb(100, 0, 0, 40);  // 删除行背景 (Cline diff-removed)
    public static Color DiffAddedText = Color.FromArgb(52, 168, 83);    // 添加文字色
    public static Color DiffRemovedText = Color.FromArgb(234, 67, 53);  // 删除文字色

    // ===== 代码块 =====
    public static Color CodeBg        = Color.FromArgb(40, 40, 40);     // 代码背景 (Cline editor-bg)
    public static Color CodeBorder    = Color.FromArgb(80, 80, 80);     // 代码边框

    // ===== 工具卡片着色 (仿 Reasonix ToolCard) =====
    public static Color ToolDoneBg    = Color.FromArgb(50, 60, 50);     // 完成工具背景
    public static Color ToolErrorBg   = Color.FromArgb(60, 40, 40);     // 错误工具背景
    public static Color ToolRunningBg = Color.FromArgb(50, 55, 65);     // 运行中工具背景
    public static Color ToolQuietBg   = Color.FromArgb(48, 48, 48);     // 低调完成 (只读工具)
}
```

### 7.2 字体系统

```csharp
public static class AppFonts
{
    public static Font Base       = new Font("Microsoft YaHei UI", 9f);     // 基础 UI 文字
    public static Font Mono       = new Font("Consolas", 9f);               // 等宽字体 (代码)
    public static Font Heading1   = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold);
    public static Font Heading2   = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);
    public static Font Caption    = new Font("Microsoft YaHei UI", 8f);     // 小号文字
    public static Font Code       = new Font("Consolas", 9.5f);             // 代码块
}
```

---

## 八、动画系统（WinForms 等效 GSAP）

> 参考：
> [R: `lib/gsapAnimations.ts`] — GSAP 动画常量 (计时/缓动函数)
> [R: `lib/useGSAPCollapse.ts`] — GSAP 折叠动画 Hook
> [C: `theme.css`] — CSS 动画定义 (fadeIn/cursorBlink/iconPulse/shimmer/fadeSlideIn)

### 8.1 动画常量

```csharp
public static class Animations
{
    // ===== 时长 (仿 Reasonix) =====
    public const int FastMs   = 120;   // 颜色/hover 过渡
    public const int NormalMs = 180;   // 菜单/popover
    public const int SlowMs   = 340;   // 抽屉/modal 展开

    // ===== 缓动函数 (仿 GSAP power2.out) =====
    public static float EaseOutCubic(float t) => 1 - (1 - t) * (1 - t) * (1 - t);
    public static float EaseInOutCubic(float t) => t < 0.5
        ? 4 * t * t * t
        : 1 - (-2 * t + 2) * (-2 * t + 2) * (-2 * t + 2) / 2;

    // ===== Timer 动画辅助 =====
    public static void Animate(Timer timer, int durationMs, Action<float> onFrame, Action onComplete = null)
    {
        int step = 0;
        int frameInterval = 16;  // ~60fps
        int totalFrames = durationMs / frameInterval;

        timer.Interval = frameInterval;
        timer.Tick += (s, e) =>
        {
            float progress = Math.Min(1f, (float)++step / totalFrames);
            onFrame(progress);
            if (progress >= 1f)
            {
                timer.Stop();
                onComplete?.Invoke();
            }
        };
        timer.Start();
    }
}
```

### 8.2 动画效果映射

| CSS/GSAP 动画 | WinForms 等效 | 使用场景 |
|--------------|-------------|---------|
| `fadeIn` 淡入 | 透明度 Timer 插值 (255→0) | 新消息入场 |
| `iconPulse` 图标脉冲 | Timer + 透明度正弦波 | 工具运行中指示 |
| `shimmer` 闪烁加载 | Timer + 渐变画刷偏移 | "AI 思考中..." |
| GSAP collapse | Timer + Height 插值 | 折叠/展开面板 |
| `fadeSlideIn` | 透明 + Top 偏移 插值 | 审批条入场 |
| `cursorBlink` | 无 (WinForms 原生支持) | 输入框光标 |

---

## 九、WinForms 性能优化策略

> 参考：
> [R: `components/Transcript.tsx`] — 三层渲染 (Cold/Warm/Hot)
> [R: `lib/useScrollManager.ts`] — RAF 流式滚动
> [C: `components/chat/chat-view/components/layout/MessagesArea.tsx`] — react-virtuoso 虚拟滚动

| 策略 | WinForms 实现 | 效果 |
|------|-------------|------|
| **三级渲染** | Cold/Warm/Hot 分区分层 | 大型会话减少 80% 渲染量 |
| **虚拟滚动** | 自定义 VirtualScrollPanel | 仅绘制可视区控件 |
| **消息合并** | combineToolBatches + groupLowRiskTools | 连续 20 个只读工具 → 1 个批次卡片 |
| **懒加载** | Cold Zone 按需加载 | 历史会话秒级打开 |
| **Tick 批处理** | BeginInvoke 合并高频事件 | 流式文本不卡顿 |
| **控件复用** | ToolCard/DiffView 对象池 | 减少 GC 压力 |
| **SuspendLayout** | 批量添加控件时暂停布局 | 避免中间态闪烁 |

---

## 十、参考源码文件路径索引

### 10.1 Cline CLI v3.0.24 关键文件

| E小智组件 | Cline 参考文件 | 核心借鉴点 |
|-----------|---------------|----------|
| CopilotForm | `C:components/chat/ChatView.tsx` | ChatLayout 布局 + 消息管线 |
| CopilotForm | `C:components/chat/chat-view/components/layout/ChatLayout.tsx` | CSS Grid: `grid-template-rows: 1fr auto` |
| ChatListBox | `C:components/chat/chat-view/components/layout/MessagesArea.tsx` | react-virtuoso 虚拟滚动 |
| ChatListBox | `C:components/chat/chat-view/components/messages/MessageRenderer.tsx` | 消息类型路由分发 |
| ChatListBox | `C:components/chat/chat-view/components/messages/ToolGroupRenderer.tsx` | 工具组渲染 (TypewriterText) |
| ChatRow | `C:components/chat/ChatRow.tsx` | 单条消息的完整渲染逻辑 (最复杂组件) |
| InputPanel | `C:components/chat/ChatTextArea.tsx` | 输入框 + @mention + SlashCommand + Plan/Act |
| InputPanel | `C:components/chat/SlashCommandMenu.tsx` | 斜杠命令自动完成 |
| InputPanel | `C:components/chat/ContextMenu.tsx` | @mention 上下文菜单 |
| PromptShelf | `C:components/chat/OptionsButtons.tsx` | followup 问题选项按钮组 |
| PromptShelf | `C:components/chat/auto-approve-menu/AutoApproveBar.tsx` | 自动审批状态栏 |
| PromptShelf | `C:components/chat/auto-approve-menu/AutoApproveModal.tsx` | 自动审批配置弹窗 |
| TaskTracking | `C:components/chat/task-header/TaskHeader.tsx` | TaskHeader (token 统计 + FocusChain) |
| TaskTracking | `C:components/chat/task-header/FocusChain.tsx` | FocusChain 进度条 |
| CompletionCard | `C:components/chat/CompletionOutputRow.tsx` | 任务完成结果 + 按钮 |
| CompletionCard | `C:components/chat/PlanCompletionOutputRow.tsx` | Plan 模式完成输出 |
| ThinkingPanel | `C:components/chat/ThinkingRow.tsx` | 推理行 + FeatureTip |
| ThinkingPanel | `C:components/chat/TypewriterText.tsx` | 打字机效果文本 |
| DiffViewControl | `C:components/chat/DiffEditRow.tsx` | 文件 patch diff 预览 |
| ErrorPanel | `C:components/chat/ErrorRow.tsx` | 错误行渲染 |
| NavToolbar | `C:components/menu/Navbar.tsx` | 5 标签页导航栏 |
| SettingsPanel | `C:components/settings/SettingsView.tsx` | 多标签页设置面板 |
| HistoryPanel | `C:components/history/HistoryView.tsx` | 历史任务列表 + 搜索 + 恢复 |
| SearchResultsPanel | `C:components/chat/SearchResultsDisplay.tsx` | 搜索结果展示 |
| MarkdownPanel | `C:components/chat/MarkdownRow.tsx` | Markdown 行渲染 |
| MarkdownPanel | `C:components/common/MarkdownBlock.tsx` | Markdown 块渲染 |
| CodeBlock | `C:components/common/CodeBlock.tsx` | 代码块 (语法高亮 + 复制) |
| CodeBlock | `C:components/common/CodeAccordian.tsx` | 可折叠代码区域 |
| StatusBarControl | `C:components/chat/task-header/ContextWindow.tsx` | 上下文窗口指示器 |
| **主题系统** | `C:theme.css` | VSCode 主题变量映射 (CSS 变量 → .NET Color) |
| **动画系统** | `C:theme.css` (fadeIn/cursorBlink/iconPulse/shimmer) | CSS 动画 → WinForms Timer 插值 |
| **全局状态** | `C:context/ExtensionStateContext.tsx` | gRPC 状态订阅 → WinForms 事件驱动 |
| **Provider 层** | `C:Providers.tsx` | React Context Provider → AppTheme 静态类 |
| UI 组件库 | `C:components/ui/button.tsx` | Radix UI Button → WinForms Button 样式 |
| UI 组件库 | `C:components/ui/dialog.tsx` | Radix Dialog → WinForms Form |
| UI 组件库 | `C:components/ui/progress.tsx` | Radix Progress → WinForms ProgressBar |

### 10.2 Reasonix v1.8.1 关键文件

| E小智组件 | Reasonix 参考文件 | 核心借鉴点 |
|-----------|-----------------|----------|
| ChatListBox | `R:components/Transcript.tsx` | 三层渲染 (Cold/Warm/Hot) + 滚动管理 |
| ChatListBox | `R:lib/useScrollManager.ts` | 自动滚动 + 滚动到消息 |
| UserMessageControl | `R:components/Message.tsx` (UserMessage) | 用户消息气泡 + 附件预览 |
| AssistantMessage | `R:components/Message.tsx` (AssistantMessage) | AI 回复 + 推理面板 |
| ThinkingPanel | `R:components/Message.tsx` (.reasoning 区域) | 推理流式展开 + 自动折叠 |
| ThinkingPanel | `R:lib/reasoningDisplay.ts` | 推理文本状态管理 |
| TurnActionsBar | `R:components/Message.tsx` (TurnActions) | 复制/摘要/回滚 + 两步确认 |
| ToolCardControl | `R:components/ToolCard.tsx` | **核心组件**：状态图标/嵌套/时长/GSAP折叠 |
| ToolCardControl | `R:components/ProcessCard.tsx` | 通用可折叠卡片基类 |
| ToolCardControl | `R:lib/tools.ts` | 工具名→图标/摘要映射 |
| ReadOnlyBatchPanel | `R:components/ReadOnlyBatch.tsx` | 只读工具批量折叠+分类计数 |
| DiffViewControl | `R:components/InlineDiff.tsx` | 紧凑内联 Diff (12行默认, 展开全部) |
| DiffViewControl | `R:components/DiffView.tsx` | 编辑器 Seam Diff 视图 |
| DiffViewControl | `R:editors/HljsDiff.tsx` | highlight.js 行级 Diff 渲染 |
| DiffViewControl | `R:lib/diff.ts` | LCS diff 算法逻辑 |
| PromptShelfControl | `R:components/PromptShelf.tsx` | **通用交互容器** (bar+panel+crumbs+quickActions) |
| ApprovalDialog | `R:components/ApprovalModal.tsx` | 审批对话框 (Plan/工具两种模式 + 键盘) |
| AskCard | `R:components/AskCard.tsx` | AI 提问卡片 |
| InputPanel | `R:components/Composer.tsx` | **最复杂组件**：~3400 行输入面板 |
| InputPanel | `R:components/SlashMenu.tsx` | 斜杠命令菜单 |
| InputPanel | `R:components/VirtualMenu.tsx` | 虚拟化菜单 (大数据量自动补全) |
| TaskTrackingPanel | `R:components/TodoPanel.tsx` | Todo 面板 (输入框上方, 折叠, 完成计数) |
| StatusBarControl | `R:components/StatusBar.tsx` | 15 项可配置指标 + JobsChip |
| CodeBlock | `R:components/CodeViewer.tsx` | 代码查看器 (编辑器 Seam) |
| CodeBlock | `R:editors/HljsCode.tsx` | highlight.js 代码着色 |
| MarkdownPanel | `R:components/Markdown.tsx` | react-markdown 渲染 |
| NavToolbar | `R:components/AppChrome.tsx` | 窗口标题栏 |
| HistoryPanel | `R:components/HistoryPanel.tsx` | 历史会话面板 |
| SettingsPanel | `R:components/SettingsPanel.tsx` | 12 标签页设置面板 |
| CompletionCard | `R:lib/sessionExport.tsx` | 会话导出 (PDF/PNG/MD) |
| CopyButton | `R:components/CopyButton.tsx` | 复制按钮 |
| ErrorBoundary | `R:components/ErrorBoundary.tsx` | 错误边界 + crash 上报 |
| UndoRewindBanner | `R:components/UndoRewindBanner.tsx` | 撤销/回滚横幅 (两步确认) |
| **主题系统** | `R:styles.css` | 21,559 行 CSS 主题系统 (BEM + CSS 变量) |
| **动画系统** | `R:lib/gsapAnimations.ts` | GSAP 动画常量 (时长/缓动) |
| **动画系统** | `R:lib/useGSAPCollapse.ts` | GSAP 折叠动画 Hook |
| **状态管理** | `R:lib/useController.ts` | 事件驱动状态机 (Item 类型定义 + reducer) |
| **消息类型** | `R:lib/types.ts` | Item 类型定义 (user/assistant/tool/phase/notice/compaction) |

---

## 十一、实现优先级

| 优先级 | 组件 | 依赖 | Phase |
|:------:|------|------|:----:|
| 🔴 P0 | CopilotForm (主布局) | — | 1a |
| 🔴 P0 | InputPanel (基础版, 纯文本) | — | 1a |
| 🔴 P0 | ChatListBox (基础版, 无虚拟滚动) | — | 1a |
| 🔴 P0 | StatusBarControl | — | 1a |
| 🔴 P0 | UserMessageControl + MarkdownPanel | — | 1a |
| 🔴 P0 | ToolCardControl (基础版, 无 Diff) | — | 1b |
| 🔴 P0 | ThinkingPanel | — | 1b |
| 🔴 P0 | ErrorPanel | — | 1b |
| 🔴 P0 | PromptShelfControl (仅审批模式) | — | 1c |
| 🟡 P1 | ReadOnlyBatchPanel | ToolCardControl | 1b |
| 🟡 P1 | DiffViewControl | — | 1b |
| 🟡 P1 | SearchResultsPanel | ToolCardControl | 1b |
| 🟡 P1 | TaskTrackingPanel | — | 1b |
| 🟡 P1 | CodeBlock | — | 1b |
| 🟡 P1 | CompletionCard | — | 1b |
| 🟡 P1 | TurnActionsBar | — | 1c |
| 🟡 P1 | InputPanel (完整版: @元素搜索 + /命令) | SlashMenu | 1c |
| 🟡 P1 | NavToolbar | — | 1d |
| 🟡 P1 | 三级渲染 (Hot/Warm/Cold) | ChatListBox | 1d |
| 🟢 P2 | SettingsPanel | NavToolbar | 2 |
| 🟢 P2 | HistoryPanel | NavToolbar | 2 |
| 🟢 P2 | 虚拟滚动 (VirtualScrollPanel) | ChatListBox | 2 |
| 🟢 P2 | 主题系统 (AppTheme + 完整配色) | — | 2 |
| 🟢 P2 | 动画系统 (Animations 类) | — | 2 |
| 🟢 P2 | MarkdownPanel (完整 Markdown 语法) | — | 2 |
| 🔵 P3 | IME 优化 (WM_IME_COMPOSITION) | InputPanel | 3 |
| 🔵 P3 | Session 导出 (PML/MD/Excel) | ChatListBox | 3 |
| 🔵 P3 | ErrorBoundary | — | 3 |
| 🔵 P3 | UndoRewindBanner | — | 3 |

---

## 十二、与参考项目的差异总结

### E小智不需要的组件（不引入）

| 组件 | 原因 |
|------|------|
| Cline AutoApproveBar/AutoApproveModal | E小智 用 ask_user 工具的 risk_level 分级替代 |
| Cline MCP Configuration View | E小智 不依赖 MCP 协议 |
| Cline Browser Session Row | E小智 不需要浏览器自动化 |
| Cline Shell Integration Warning | E小智 不执行 Shell 命令 |
| Reasonix OnboardingOverlay | E小智 是 E3D 插件，由 Addin 加载决定 |
| Reasonix WorkspacePanel/ProjectTree | E小智 不管理文件项目 |
| Reasonix CommandPalette | E小智 用 SlashCommand 替代 |
| Reasonix ModelSwitcher/EffortSwitcher | E小智 vLLM 本地部署，单模型 |

### E小智独有的组件（参考项目没有）

| 组件 | 原因 |
|------|------|
| SearchResultsPanel (knowledge db) | RAG 知识库搜索 → search_knowledge 工具 |
| ElementSearchMenu (@E3D 元素) | E3D 专有：@mention 搜索模型元素而非文件 |
| PML 语法高亮 CodeBlock | E3D 专有：PML 代码着色 |
| DiffViewControl (属性值对比) | E3D 专有：属性修改前后对比 (非文件 diff) |
