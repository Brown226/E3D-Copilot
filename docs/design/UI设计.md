# E小智 — UI 设计

> 版本：v2.0（基于 cline-chinese-main webview-ui 适配）
> 状态：WinForms UI 旧方案已废弃，全面转向 WebView2 + React SPA
> 参考：`参考开源项目/cline-chinese-main/webview-ui/`（直接适配对象）

---

## 一、设计决策

### 1.1 为什么采用 cline-chinese-main 的前端

| 维度 | 自建 React UI | 适配 cline-chinese-main |
|------|-------------|----------------------|
| 聊天视图 | 需从头实现 | ✅ 完整 ChatView + 虚拟滚动 + 流式打字机 |
| 消息类型 | 基础文本 | ✅ 20+ 消息类型（text/reasoning/tool/command/diff/browser 等） |
| 工具卡片 | 基础 | ✅ 可折叠参数/输出/进度/错误 + 自动分组 |
| 思考过程 | 基础 | ✅ ThinkingRow + 推理过程流式渲染 |
| 设置面板 | 需自建 | ✅ 完整设置页（API/模型/审批/主题/MCP） |
| 历史记录 | 需自建 | ✅ 搜索/过滤/右键菜单 |
| Message 协议 | 需定义 | ✅ ExtensionMessage/WebviewMessage 契约 |
| 主题 | 手工 CSS | ✅ Tailwind + CSS 变量 + 暗色主题 |
| 测试 | 无 | ✅ Storybook + 组件测试 |
| 开发体验 | 一般 | ✅ Vite HMR 热更新 |

**结论**：直接在 cline-chinese-main 的 webview-ui 基础上修改，比从零开始构建节省 80%+ 的 UI 开发工作量。

### 1.2 VS Code 扩展 → E3D Addin 架构对应

cline-chinese-main 是 VS Code 扩展，其插件端架构与 E3D Addin **高度吻合**：

| cline-chinese-main (VS Code) | E3D 对应 | 适配方式 |
|---|---|---|
| `extension.ts` activate/deactivate | `AddinBoot.cs` Start/Stop | 直接对应 |
| `VscodeWebviewProvider` | `WebViewForm` (UserControl + WebView2) | C# 端适配 |
| `webview.postMessage()` | `CoreWebView2.PostWebMessageAsString()` | 直接对应 |
| `webview.onDidReceiveMessage()` | `WebMessageReceived` 事件 | 直接对应 |
| `Controller` + `Task` | `CopilotController` + `AgentLoop` | 已有，微调 |
| `ToolExecutor` + `IToolHandler` | `ToolExecutor` + `IToolHandler` | 已有，完全一致 |
| protobuf gRPC 消息 | JSON `{ type, payload }` | 简化协议 |
| `webview-ui/` React SPA | 适配后的 E小智 前端 | 前端侧修改 |

### 1.3 适配策略

```
cline-chinese-main webview-ui/
├── src/
│   ├── components/
│   │   ├── chat/       → 保留 + 修改消息类型映射 + 添加 E3D 工具渲染
│   │   ├── settings/   → 保留 + 替换模型提供商配置（vLLM）
│   │   ├── history/    → 保留
│   │   ├── welcome/    → 保留 + E3D 品牌化
│   │   ├── mcp/        → 保留（后续扩展 MCP 工具）
│   │   ├── common/     → 保留（MarkdownBlock, CodeBlock）
│   │   └── ui/         → 保留（button, dialog, switch...）
│   ├── hooks/          → 保留 + 适配 E3D 特有逻辑
│   ├── context/        → 保留 + 适配 E3D 状态
│   ├── services/       → 保留 + 替换通信层（bridgeClient.ts）
│   ├── i18n/           → 保留 + 补充 E3D 专业术语
│   └── lib/            → 保留
├── package.json        → 保留 + 添加 E3D 专用依赖
├── vite.config.ts      → 保留 + 适配 E3D 构建输出路径
└── tailwind.config.mjs → 保留 + E3D 主题色
```

| 改动类别 | 比例 | 说明 |
|---------|:---:|------|
| 完全保留 | ~60% | 核心聊天框架、UI 组件、hooks、构建工具 |
| 修改适配 | ~30% | 消息协议、通信层、模型配置、主题色、E3D 工具渲染 |
| 移除 | ~5% | BrowserSessionRow（E3D 无网页浏览需求）|
| 新增 | ~5% | E3D 特定工具卡片渲染、PML diff 提交、审批流程增强 |

---

## 二、核心组件架构

### 2.1 组件树

```
App (App.tsx)
 └─ Providers (ExtensionStateContextProvider + HeroUIProvider)
     └─ AppContent
         ├─ [条件] SettingsView    ← 设置面板（覆盖层）
         ├─ [条件] HistoryView     ← 历史记录（覆盖层）
         ├─ [条件] McpView         ← MCP 配置（覆盖层）
         └─ ChatView               ← 主聊天（始终挂载，通过 isHidden 控制显示）
             └─ ChatLayout
                 ├─ MessagesArea   ← 虚拟滚动消息列表
                 │   └─ Virtuoso
                 │       └─ MessageRenderer
                 │           ├─ ToolGroupRenderer  ← 低风险工具折叠
                 │           ├─ ──  (移除 BrowserSessionRow)
                 │           └─ ChatRow            ← 单条消息
                 │               └─ ChatRowContent
                 │                   ├─ "text"          → MarkdownRow
                 │                   ├─ "reasoning"     → ThinkingRow
                 │                   ├─ "command"       → CommandOutputRow (PML 输出)
                 │                   ├─ "tool"          → ToolCard (E3D 工具)
                 │                   ├─ "api_req_started" → RequestStartRow
                 │                   ├─ "completion_result" → CompletionOutputRow
                 │                   ├─ "diff_error"    → ErrorRow
                 │                   ├─ "error"         → ErrorRow
                 │                   └─ "use_mcp_server" → McpResponseDisplay
                 │
                 ├─ AutoApproveBar     ← 自动审批栏
                 ├─ ActionButtons      ← 操作按钮（批准/拒绝/取消）
                 └─ InputSection       ← 输入区
                     └─ ChatTextArea   ← 多行输入（@提及 + 发送）
```

### 2.2 消息类型映射（cline-chinese-main → E小智）

cline-chinese-main 有 20+ 消息 `say`/`ask` 类型，E小智 保留大部分，修改小部分：

| Cline 消息类型 | say/ask | E3D 适配 | 说明 |
|---|---|---|---|
| `"text"` | say | ✅ 保留 | AI 回复文本 |
| `"reasoning"` | say | ✅ 保留 | 推理过程 |
| `"command"` | say | ✅ 保留 | PML 命令输出 |
| `"api_req_started"` | say | ✅ 保留 | LLM 请求开始 |
| `"completion_result"` | say | ✅ 保留 | 任务完成 |
| `"tool"` | ask/say | ✅ **修改** | E3D 工具渲染（PML 预览/参数展示） |
| `"use_mcp_server"` | ask/say | ✅ 保留 | MCP 工具调用 |
| `"browser_action"` / `"browser_session"` | ask/say | ❌ **移除** | E3D 无网页浏览需求 |
| `"diffic_error"` | say | ✅ **修改** | PML 代码 diff 错误 |
| `"user_feedback"` | say | ✅ 保留 | 用户反馈 |
| `"error"` | say | ✅ 保留 | 错误信息 |
| `"followup"` | ask | ✅ 保留 | AI 追问 |
| `"mistake_limit_reached"` | say | ✅ 保留 | 错误限制 |
| `"plan_mode_respond"` | ask | ✅ 保留 | Plan Mode |
| `"new_task"` | ask | ✅ 保留 | 新任务确认 |
| `"hook_status"` | say | ✅ 保留 | 钩子状态 |
| `"task_progress"` | say | ✅ 保留 | 任务进度 |
| `"subagent"` | say | ✅ 保留 | 子代理 |
| `"checkpoint"` / `"checkpoint_diff"` | say | ❌ **移除** | E3D 无 git checkpoint |

### 2.3 需要修改的关键组件

#### ChatRowContent.tsx（消息分发核心）

修改 `type` 的 switch 分发：

```typescript
// 修改前（cline-chinese-main）
case "browser_action_result":
  return <BrowserSessionRow />
case "browser_action_launch":
  return <BrowserSessionRow />

// 修改后（E小智）
// 移除 browser 相关 case
// 保留其他所有 case
```

#### CommandOutputRow.tsx（PML 命令输出适配）

E3D 的 PML 命令输出格式与 shell 命令不同，需要：

```typescript
// 修改渲染逻辑：PML 输出的行号前缀、错误高亮、结果表格
function PmlOutputFormatter(output: string) {
  // PML $p 消息行 → 灰色
  // PML handle/code 错误 → 红色高亮
  // PML 表格输出 → 识别表格边界并渲染
  // 执行状态（ok/fail）→ 图标 + 颜色
}
```

#### ToolCard.tsx / ToolGroupRenderer.tsx（E3D 工具渲染增强）

新增 E3D 工具专用的渲染逻辑：

| E3D 工具类型 | 渲染方式 |
|---|---|
| `query_elements` | 结果表格 + 元素类型图标 + 数量统计 |
| `get_attributes` | 属性名=值 键值对展示 |
| `execute_pml` | PML 代码预览 + 执行状态 + 输出 |
| `modify` / `set_attribute` | 修改摘要 + 回滚按钮 |
| `check_*` | 通过/失败状态 + 错误列表 |
| `calculate_*` | 数值结果 + 单位 + 几何图示（后续） |
| `export_*` | 文件路径 + 记录数统计 |

#### 新增：PmlDiffViewRow.tsx

专为 PML 脚本预览设计的 diff 组件，展示 PML 代码的插入/删除/修改：

```typescript
interface PmlDiffViewProps {
  oldScript: string  // 原始 PML
  newScript: string  // 修改后的 PML
  onApprove: () => void
  onReject: () => void
}
```

#### 新增：E3DStatusBar.tsx（可选）

在 StatusBar 基础上添加 E3D 特有信息：

```
[模型名称: ProjectX] [当前元素: PIPE-001] [CE: /WORLD/.../PIPE-001]
```

---

## 三、通信协议

### 3.1 协议格式

所有通信使用 JSON over `WebView2.PostWebMessageAsString()`，而非 cline-chinese-main 的 protobuf gRPC。

```typescript
// 前端 JS Bridge (bridge.ts)
class Bridge {
  send(type: string, payload: any): void
  on(callback: (msg: Message) => void): () => void  // 返回 unsubscribe
}

interface Message {
  type: string
  payload: any
}
```

```csharp
// C# Bridge (Bridge.cs)
public class Bridge
{
    public void SendToFrontend(string type, object payload);
    private void OnWebMessageReceived(string rawJson);
    // 消息路由：
    // "user:message"    → Controller.SendAsync(payload.text)
    // "user:cancel"     → Controller.CancelAsync()
    // "user:approve"    → Controller.Approve(payload.toolId, payload.allow)
    // "user:settings"   → Controller.UpdateSettings(payload)
    // "user:new_session"→ Controller.NewSession()
}
```

### 3.2 消息类型

**C# → 前端（后端驱动 UI 更新）**：

| type | payload | 触发时机 |
|------|---------|---------|
| `host:ready` | `{ version, model, platform }` | WebView2 加载完成 |
| `state:update` | `{ messages, settings, isRunning, ... }` | 状态变更（完整推） |
| `llm:stream:delta` | `{ delta }` | LLM 流式 token |
| `llm:stream:end` | `{ usage }` | 流结束 |
| `llm:thinking` | `{ text }` | 推理过程 |
| `tool:dispatch` | `{ id, name, args }` | 工具触发展示 |
| `tool:result` | `{ id, result, error? }` | 工具完成 |
| `tool:progress` | `{ id, progress }` | 进度更新 |
| `tool:approval` | `{ id, name, args, description }` | 请求审批 |
| `error` | `{ message, code? }` | 错误通知 |

**前端 → C#（用户操作）**：

| type | payload | 说明 |
|------|---------|------|
| `user:message` | `{ text, images?, files? }` | 用户发送消息 |
| `user:cancel` | `{}` | 取消当前处理 |
| `user:new_session` | `{}` | 新建会话 |
| `user:approve` | `{ toolId, allow, persist? }` | 审批工具 |
| `user:settings` | `{ key, value }` | 保存设置 |
| `user:export` | `{ format }` | 导出会话 |
| `ping` | `{}` | 心跳 |

### 3.3 与 cline-chinese-main 消息协议的差异

| 维度 | cline-chinese-main | E小智（简化） |
|------|-------------------|-------------|
| 序列化 | protobuf | JSON |
| 消息路由 | service + method (gRPC) | type (直接路由) |
| 请求关联 | request_id (UUID) | 异步无等待（fire-and-forget）|
| 流式 | gRPC server-streaming | 多次 send (type 区分) |
| 状态推送 | 全量 ExtensionState | 全量 + 增量 |
| 前端 SDK | 自动生成 gRPC 客户端 | 手动 Bridge 封装 |

---

## 四、主题设计

基于 cline-chinese-main 的 `theme.css` 适配 E3D 暗色风格：

### 4.1 颜色系统

| Token | E小智 值 | Cline 原值 | 用途 |
|-------|---------|-----------|------|
| `--bg-primary` | `#0c0c14` | VSCode 变量 | 主背景 |
| `--bg-secondary` | `#141420` | VSCode 变量 | 次级背景 |
| `--bg-tertiary` | `#1c1c2a` | VSCode 变量 | 卡片/气泡 |
| `--text-primary` | `#e8e8f0` | VSCode 变量 | 主文字 |
| `--text-secondary` | `#9898b0` | VSCode 变量 | 次要文字 |
| `--text-muted` | `#686880` | VSCode 变量 | 弱化文字 |
| `--accent-blue` | `#3b82f6` | VSCode 变量 | 品牌色（E3D 蓝） |
| `--accent-green` | `#22c55e` | VSCode 变量 | 成功（PML 执行通过）|
| `--accent-orange` | `#f59e0b` | VSCode 变量 | 警告 |
| `--accent-red` | `#ef4444` | VSCode 变量 | 错误（PML 执行失败）|
| `--font-sans` | `Inter, system-ui` | VSCode 变量 | UI 字体 |
| `--font-mono` | `'JetBrains Mono', Consolas` | VSCode 变量 | 代码字体 |

### 4.2 主题文件结构

```
web-ui/src/
├── theme.css          ← 替换 cline-chinese-main 的 VSCode 变量 → E3D 硬编码变量
├── index.css          ← Tailwind 入口 + 全局样式
└── main.css           ← 应用特定样式
```

`theme.css` 将 cline-chinese-main 的 `--vscode-*` CSS 变量引用替换为固定值，因为 E3D 不提供 VSCode 主题变量：

```css
/* cline-chinese-main（引用 VSCode 变量） */
:root {
  --vscode-editor-background: var(--vscode-editor-background);
  --vscode-editor-foreground: var(--vscode-editor-foreground);
}

/* E小智（固定值，无需外部主题） */
:root {
  --bg-primary: #0c0c14;
  --bg-secondary: #141420;
  --bg-tertiary: #1c1c2a;
  --text-primary: #e8e8f0;
  --text-secondary: #9898b0;
  --text-muted: #686880;
  --accent-blue: #3b82f6;
  /* ...更多 E3D 主题色 */
}
```

---

## 五、开发与构建

### 5.1 开发模式

```
# 终端 1: 启动 React 开发服务器 (HMR)
cd web-ui && npm run dev
→ http://localhost:5173

# 终端 2: 在 E3D 中加载 Addin
#   WebViewForm 检测到 E3D_COPILOT_DEV_URL 环境变量
#   或 wwwroot/index.html 不存在时，自动指向 localhost:5173
```

### 5.2 构建流程

```
# 生产构建
cd web-ui && npm run build
→ 输出到 src/E3DCopilot.WebHost/wwwroot/

# 构建 C# 解决方案
msbuild E3DCopilot.sln
→ 输出 DLL 到 E3DCopilot.Addin/bin/Debug/net48/
```

### 5.3 目录结构

```
web-ui/
├── package.json        ← 依赖: react 18, tailwind 4, @heroui/react,
│                          lucide-react, react-markdown, react-virtuoso
├── vite.config.ts      ← build.outDir = ../src/E3DCopilot.WebHost/wwwroot
├── tsconfig.json
├── index.html
├── tailwind.config.mjs
│
└── src/
    ├── main.tsx        ← ReactDOM.createRoot
    ├── App.tsx         ← 根组件（视图路由）
    ├── bridge.ts       ← WebView2 通信桥
    ├── index.css / theme.css / main.css
    ├── components/
    │   ├── chat/       ← 核心聊天组件（源自 cline-chinese-main）
    │   ├── settings/   ← 设置面板
    │   ├── history/    ← 历史记录
    │   ├── welcome/    ← 欢迎页
    │   ├── mcp/        ← MCP 配置
    │   ├── common/     ← MarkdownBlock, CodeBlock...
    │   └── ui/         ← 基础 UI 组件
    ├── hooks/          ← 自定义 hooks
    ├── context/        ← React Context
    ├── services/       ← bridgeClient.ts
    ├── i18n/           ← 国际化配置
    └── locales/        ← zh-CN / en 语言包
```

---

## 六、适配工作清单

### Phase 1 — 基础适配（核心功能可用）

- [ ] fork cline-chinese-main webview-ui 到 web-ui/
- [ ] 替换 VSCode postMessage API 为 `window.chrome.webview.postMessage`
- [ ] 实现 bridge.ts（WebView2 通信桥）
- [ ] 实现 bridgeClient.ts（上层 API 封装）
- [ ] 替换 `theme.css` 的 VSCode 变量为 E3D 固定值
- [ ] 移除 BrowserSessionRow 及相关 browser 组件
- [ ] 修改 vite.config.ts 输出路径
- [ ] 修改 package.json 依赖（去掉 VSCode 特有依赖）
- [ ] 替换设置页的模型提供商配置为 vLLM
- [ ] 实现 `state:update` / `llm:stream:*` / `tool:*` 消息的处理
- [ ] 验证：输入消息 → AI 流式回复 → 正常显示

### Phase 2 — E3D 工具渲染

- [ ] CommandOutputRow 适配 PML 输出格式
- [ ] ToolCard 扩展 E3D 工具渲染（表格/属性/PML 代码）
- [ ] 添加 PML diff 渲染组件
- [ ] 添加 E3D 状态栏（当前元素/模型名）
- [ ] 审批对话框适配 E3D 工具参数显示
- [ ] 添加 E3D 专业词汇国际化

### Phase 3 — 完善

- [ ] E3D 品牌化：Logo、标题、欢迎页
- [ ] 主题微调：E3D 设计规范颜色
- [ ] 性能优化：大量 PML 输出的虚拟滚动
- [ ] 错误边界适配 E3D 场景
- [ ] 快捷键适配（E3D 快捷键不冲突）

---

## 七、与旧方案（WinForms）的对比

| 维度 | WinForms（已废弃） | WebView2 + React（现方案） |
|------|-------------------|--------------------------|
| 开发语言 | C# WinForms | TypeScript + React |
| UI 能力 | GDI+ 基础绘图 | CSS 全能力 + GPU 渲染 |
| 动画 | Timer 插值 | CSS transitions + framer-motion |
| 虚拟滚动 | 自建 VirtualScrollPanel | react-virtuoso 成熟库 |
| Markdown | 自建解析器 | react-markdown 生态 |
| 代码高亮 | 自建 SyntaxHighlighter | rehype-highlight + shiki |
| 开发体验 | 编译才能看变化 | Vite HMR 即时生效 |
| 组件库 | 自建 29 个控件 | cline-chinese-main 20+ 成熟组件 |
| 主题 | 手写暗色调 | Tailwind 设计系统 |
| 维护成本 | 高 | 低（跟随上游更新） |

**结论**：WebView2 + React 方案在 UI 能力、开发效率、维护成本上全面优于 WinForms 方案。
