# E小智 — UI 设计

> 版本：v3.0（实际实现，自研 React SPA）
> 状态：✅ 已实现，WebView2 + React 18 + Tailwind CSS v4
> 前端项目：`e3d-ui/`（独立 Vite 项目，非 cline-chinese-main fork）

---

## 一、设计决策

### 1.1 技术栈（实际实现）

| 技术 | 版本 | 用途 |
|------|------|------|
| React | 18.3 | UI 框架 |
| Vite | 6.0 | 构建工具 + HMR |
| Tailwind CSS | v4 (`@tailwindcss/vite`) | 原子化 CSS |
| zustand | 5.0 | 状态管理 |
| @tanstack/react-virtual | 3.14 | 虚拟滚动 |
| react-markdown | 9.0 | Markdown 渲染 |
| rehype-highlight | 7.0 | 代码高亮 |
| remark-gfm | 4.0 | GFM 表格/任务列表 |
| remark-math + rehype-katex | 6.0 / 0.17 | LaTeX 公式 |
| lucide-react | 0.468 | 图标库 |
| rollup-plugin-visualizer | 7.0 | 构建分析 |

### 1.2 架构特点

- **自研 SPA**：非 cline-chinese-main fork，完全独立实现
- **多 Tab 支持**：每个 Tab 独立消息流、流式状态、审批状态
- **Bridge 通信层**：JSON over `window.chrome.webview`，支持请求/响应 + 通知双模式
- **Standalone Mock 模式**：脱离 E3D 宿主时自动激活，模拟完整 AI 回复流程
- **会话持久化**：localStorage 保存/恢复会话历史

---

## 二、核心组件架构

### 2.1 组件树（实际实现）

```
App (App.tsx)
 └─ ErrorBoundary
     └─ AppInner
         ├─ useHeartbeat()          ← 心跳检测
         ├─ useKeyboardShortcuts()  ← 快捷键
         ├─ DisconnectScreen         ← 断连遮罩（bridge 断连时显示）
         ├─ Header                   ← Logo + 标题 + 模型名 + 新建/历史/设置
         ├─ TabBar                   ← 多 Tab 切换栏
         ├─ [条件] WelcomeScreen     ← 无消息时显示欢迎页
         ├─ [条件] MessageList       ← 有消息时显示（虚拟滚动）
         │   └─ useVirtualizer (react-virtual)
         │       └─ MessageRow       ← 消息分发器
         │           ├─ UserBubble          ← 用户消息（右对齐蓝色气泡）
         │           ├─ AssistantBubble     ← AI 回复（左对齐白色气泡 + Markdown）
         │           │   ├─ MarkdownBlock   ← react-markdown + 代码高亮 + LaTeX
         │           │   └─ TurnActions     ← 回复操作栏（复制等）
         │           ├─ ThinkingBlock       ← 推理过程（可折叠灰色块）
         │           ├─ ToolCard            ← 工具调用 + 结果（可折叠卡片）
         │           │   ├─ 状态图标（running/done/error）
         │           │   ├─ 参数预览（可展开 JSON）
         │           │   ├─ DiffView        ← modify 工具的 diff 展示
         │           │   ├─ Shell 输出预览（前 10 行 + "显示全部"）
         │           │   └─ SubToolRow[]    ← 子代理嵌套调用
         │           └─ ErrorCard           ← 错误信息
         ├─ InputBar                 ← 输入区（底部固定）
         │   ├─ 附件预览区（图片/文件）
         │   ├─ 粘贴块预览（长文本折叠）
         │   ├─ ModelSwitcher        ← 模型选择器
         │   ├─ textarea（自动伸缩 + IME 兼容）
         │   └─ 发送/取消按钮
         ├─ HistoryPanel             ← 会话历史面板
         ├─ SettingsPanel (lazy)     ← 设置面板（React.lazy 懒加载）
         ├─ ToastContainer           ← Toast 通知
         └─ CommandPalette           ← 命令面板（Ctrl+K）
```

### 2.2 消息类型映射（实际实现）

| role 值 | 组件 | 说明 |
|---------|------|------|
| `user` | UserBubble | 用户消息，右对齐蓝色气泡，支持附件 |
| `assistant` | AssistantBubble | AI 回复，左对齐白色气泡，Markdown 渲染 + 闪烁光标 |
| `thinking` | ThinkingBlock | 推理过程，可折叠灰色块 |
| `tool_call` | ToolCard | 工具调用中（running 状态，旋转图标） |
| `tool_result` | ToolCard | 工具完成（done/error 状态，可展开参数+结果） |
| `error` | ErrorCard | 系统错误信息 |

---

## 三、关键组件说明

### 3.1 MessageList — 虚拟滚动消息列表

- 使用 `@tanstack/react-virtual` 实现虚拟滚动（非 Virtuoso）
- 自动滚动到底部 + 手动上滚时暂停
- 滚动到底部浮动按钮
- 子调用关系计算：`task/explore/research/review` 工具的子调用嵌套展示

### 3.2 InputBar — 智能输入框

| 功能 | 说明 |
|------|------|
| 自动伸缩 | textarea 高度随内容变化，上限为视口 35% |
| 输入历史 | ↑/↓ 箭头切换历史输入（最多 100 条，localStorage 持久化） |
| 长粘贴折叠 | >2000 字符或 >20 行自动折叠为 `[Pasted block 1]` 引用 |
| IME 兼容 | compositionStart/End 事件，100ms grace period |
| 拖拽上传 | 支持拖拽图片/文件到输入框 |
| 附件管理 | 图片预览 + 文件大小 + 删除按钮 |
| 快捷键 | Enter 发送，Shift+Enter 换行，Escape 清空 |

### 3.3 ToolCard — 统一工具卡片

- 状态三态：running（蓝色旋转）/ done（绿色勾选）/ error（红色叉号）
- 工具名 + 智能摘要（根据工具名解析参数生成中文摘要）
- 可折叠展开参数（JSON 格式化）和结果
- 子代理嵌套：task/explore 等工具的子调用以紫色边框嵌套展示
- Shell 输出预览：前 10 行 + "显示全部 N 行"
- DiffView：modify 工具的 oldValue/newValue diff 展示
- 复制按钮：一键复制结果

### 3.4 bridgeService — WebView2 通信桥

```
Bridge 类
├─ send(type, payload)           → JSON → chrome.webview.postMessage
├─ sendAndWait(type, payload)    → 带 _requestId 的请求/响应模式
├─ on(callback)                  → 注册消息监听器
├─ once(type)                    → 等待特定消息
├─ isAvailable()                 → 检测 chrome.webview 是否可用
│
├─ 类型化方法：
│   ├─ sendUserMessage / sendApproval / sendAskResponse
│   ├─ cancel / newSession / ping
│   ├─ listModels / switchModel
│   ├─ listProviders / saveProvider / deleteProvider / fetchProviderModels
│   ├─ listSkills / toggleSkill / addSkillSource / removeSkillSource
│   ├─ listMemories / saveMemory / deleteMemory
│   ├─ listSessions / deleteSession
│   └─ saveSetting
│
├─ Store 映射（registerStoreMappings）：
│   ├─ host:ready        → setBridgeConnected(true)
│   ├─ config:sync       → setConfig (providers/model)
│   ├─ llm:turn_started  → startStreaming
│   ├─ llm:stream:delta  → appendAssistantDelta
│   ├─ llm:stream:end    → finalizeAssistantMessage
│   ├─ llm:thinking      → handleThinkingDelta
│   ├─ tool:dispatch     → appendMessage (tool_call)
│   ├─ tool:result       → handleToolResult
│   ├─ tool:approval     → setPendingApproval
│   ├─ turn:done         → stopStreaming
│   ├─ error             → appendMessage + Toast
│   └─ notice            → Toast
│
└─ Standalone Mock 模式：
    ├─ startMock()          → 模拟 host:ready + config:sync
    └─ mockStreamResponse() → 模拟完整 AI 回复（thinking + stream + tool）
```

---

## 四、状态管理（zustand）

### 4.1 多 Tab 架构

```
ChatStore
├─ tabs: Tab[]           ← 每个 Tab 独立状态
│   └─ Tab {
│       id, title, messages[],
│       isStreaming, currentAssistantMsgId,
│       currentThinkingMsgId, pendingApproval
│   }
├─ activeTabId: string
├─ inputValue: string
├─ currentProvider / currentModel / providers[]
├─ bridgeConnected: boolean
├─ showSettings / showHistory / showCommandPalette
├─ sessions: SessionMeta[]  ← localStorage 持久化
└─ 操作：createTab / closeTab / setActiveTab / ...
```

### 4.2 会话持久化

- 会话元数据（标题、预览、消息数、时间戳）保存到 localStorage
- 最多保存 100 个会话
- 新建会话时自动保存当前会话
- HistoryPanel 展示历史会话列表，支持删除

---

## 五、通信协议

### 5.1 协议格式

JSON over `window.chrome.webview.postMessage()` / `addEventListener('message')`

```typescript
// 前端 → C#
interface OutgoingMessage {
  type: string       // 如 "user:message"
  payload?: unknown
  _requestId?: string // 请求/响应模式
}

// C# → 前端
interface IncomingMessage {
  type: string       // 如 "llm:stream:delta"
  payload?: unknown
  _requestId?: string // 匹配请求的响应
}
```

### 5.2 消息类型

**C# → 前端（后端推送）**：

| type | payload | 触发时机 |
|------|---------|---------|
| `host:ready` | `{ version, platform, timestamp }` | WebView2 加载完成 |
| `config:sync` | `{ provider, model, baseUrl, providers[] }` | 配置同步 |
| `llm:turn_started` | `{}` | LLM 开始回复 |
| `llm:stream:delta` | `{ delta }` | 流式 token |
| `llm:stream:end` | `{ usage }` | 流结束 |
| `llm:thinking` | `{ text }` | 推理过程 |
| `tool:dispatch` | `{ id, name, args }` | 工具触发展示 |
| `tool:result` | `{ id, result, error? }` | 工具完成 |
| `tool:error` | `{ id, error }` | 工具失败 |
| `tool:approval` | `{ id, name, args, description }` | 请求审批 |
| `ask_user` | `{ questionId, question, data? }` | AI 提问用户 |
| `notice` | `{ text }` | 信息通知 |
| `error` | `{ message, code? }` | 错误通知 |
| `pong` | `{ timestamp }` | 心跳响应 |
| `turn:done` | `{}` | 一轮对话完成 |
| `models:list:result` | `{ models[], currentProvider, currentModel }` | 模型列表 |
| `providers:list:result` | `{ providers[], currentProvider, currentModel }` | 供应商列表 |
| `bridge:disconnected` | — | 连接断开 |
| `bridge:reconnected` | — | 连接恢复 |

**前端 → C#（用户操作）**：

| type | payload | 说明 |
|------|---------|------|
| `user:message` | `{ text, images?, files?, tabId? }` | 发送消息 |
| `user:cancel` | `{}` | 取消生成 |
| `user:new_session` | `{}` | 新建会话 |
| `user:approve` | `{ id, allow }` | 审批工具 |
| `user:ask_response` | `{ questionId, answer }` | 回答 AI 提问 |
| `ping` | `null` | 心跳检测 |
| `models:list` | `null` | 获取模型列表 |
| `model:switch` | `{ ref }` | 切换模型 |
| `providers:list` | `null` | 获取供应商列表 |
| `provider:save` | `{ ... }` | 保存供应商 |
| `provider:delete` | `{ name }` | 删除供应商 |
| `provider:fetch_models` | `{ name }` | 拉取供应商模型 |
| `provider:set_key` | `{ name, apiKey }` | 设置 API Key |
| `skills:list` / `skills:toggle` / `skills:add_source` | ... | 技能管理 |
| `memory:list` / `memory:save` / `memory:delete` | ... | 记忆管理 |
| `settings:save` | `{ key, value }` | 保存设置 |
| `sessions:list` / `sessions:delete` | ... | 会话管理 |

---

## 六、主题设计

### 6.1 Tailwind v4 暗色主题

使用 Tailwind CSS v4 的 `dark:` 变体实现明暗主题切换：

- 背景渐变：`bg-gradient-to-br from-slate-50 to-blue-50 dark:from-slate-900 dark:to-slate-800`
- 用户气泡：蓝色（`bg-blue-600`）
- AI 气泡：白色/暗灰（`bg-white dark:bg-slate-800`）
- 工具卡片：`bg-slate-50 dark:bg-slate-800/50`
- 状态色：绿色（成功）/ 蓝色（运行中）/ 红色（错误）

### 6.2 构建输出

```
# 开发模式
cd e3d-ui && npm run dev     → http://localhost:5173（HMR 热更新）

# 生产构建
cd e3d-ui && npm run build   → 输出到 src/E3DCopilot.WebHost/wwwroot/
```

Vite 配置 `build.outDir = '../src/E3DCopilot.WebHost/wwwroot'`，构建产物直接被 C# WebHost 服务。

---

## 七、目录结构（实际）

```
e3d-ui/
├── package.json              ← react 18 + tailwind v4 + zustand + virtual
├── vite.config.ts            ← 输出到 WebHost/wwwroot
├── tsconfig.json
├── index.html
│
└── src/
    ├── main.tsx              ← createRoot + bridgeService 导入
    ├── App.tsx               ← 根组件（ErrorBoundary + TabBar + 条件渲染）
    ├── index.css             ← Tailwind 入口 + 全局样式
    │
    ├── types.ts              ← Message / MessageRole / Attachment 类型定义
    │
    ├── store/
    │   ├── useChatStore.ts   ← Zustand 多 Tab 状态管理（532 行）
    │   └── useToastStore.ts  ← Toast 通知状态
    │
    ├── services/
    │   ├── bridgeService.ts  ← WebView2 通信桥（693 行）
    │   └── messageContracts.ts ← 消息类型契约定义
    │
    ├── hooks/
    │   ├── useHeartbeat.ts   ← 心跳检测（定时 ping）
    │   └── useKeyboardShortcuts.ts ← 快捷键（Ctrl+K 命令面板等）
    │
    ├── components/
    │   ├── chat/
    │   │   ├── MessageList.tsx     ← 虚拟滚动 + 子调用关系计算
    │   │   ├── MessageRow.tsx      ← 消息类型分发器
    │   │   ├── UserBubble.tsx      ← 用户消息气泡
    │   │   ├── AssistantBubble.tsx ← AI 回复（Markdown + 复制 + 操作栏）
    │   │   ├── ThinkingBlock.tsx   ← 推理过程
    │   │   ├── ToolCard.tsx        ← 工具卡片（状态/参数/结果/子调用）
    │   │   ├── DiffView.tsx        ← 修改 diff 展示
    │   │   ├── ErrorCard.tsx       ← 错误信息
    │   │   ├── WelcomeScreen.tsx   ← 欢迎页
    │   │   ├── InputBar.tsx        ← 输入框（伸缩/历史/粘贴/拖拽）
    │   │   ├── ModelSwitcher.tsx   ← 模型切换下拉
    │   │   └── TurnActions.tsx     ← 回复操作栏
    │   ├── common/
    │   │   ├── MarkdownBlock.tsx   ← react-markdown + highlight + katex
    │   │   └── Toast.tsx           ← Toast 通知容器
    │   ├── settings/
    │   │   └── SettingsPanel.tsx   ← 设置面板（lazy 加载）
    │   ├── Header.tsx              ← 顶栏（Logo + 模型名 + 按钮）
    │   ├── TabBar.tsx              ← 多 Tab 切换栏
    │   ├── HistoryPanel.tsx        ← 会话历史
    │   ├── CommandPalette.tsx      ← 命令面板（Ctrl+K）
    │   ├── DisconnectScreen.tsx    ← 断连遮罩
    │   └── ErrorBoundary.tsx       ← 错误边界
    │
    └── locales/              ← zh-CN / en 语言包（如已实现）
```

---

## 八、与旧文档差异说明

| 旧文档描述 | 实际实现 |
|-----------|---------|
| 基于 cline-chinese-main webview-ui 适配 | 自研 React SPA，独立实现 |
| 使用 VS Code `postMessage` API | `window.chrome.webview` 直接通信 |
| `ChatRowContent` + Virtuoso | `MessageRow` + `@tanstack/react-virtual` |
| cline 的 20+ 消息类型 | 6 种 role（user/assistant/thinking/tool_call/tool_result/error） |
| `ChatTextArea` 输入组件 | `InputBar` 自研（含历史/粘贴折叠/IME/拖拽） |
| `AutoApproveBar` 审批栏 | `PendingApproval` 状态 + 审批响应 |
| `ExtensionStateContextProvider` | `useChatStore` (zustand) |
| `web-ui/` 目录 | `e3d-ui/` 目录 |
| HeroUI 组件库 | Tailwind CSS 原子化样式（无 UI 组件库依赖） |
| 工作清单（Phase 1-3 待完成） | ✅ 已实现核心功能 |
