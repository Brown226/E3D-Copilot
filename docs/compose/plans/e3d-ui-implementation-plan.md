# E小智 v2.0 前端实施计划

> 关联设计文档：`docs/前端重设计划.md`  
> 创建日期：2026-06-23  
> 预计工期：9 天（含 20% 缓冲）  
> 负责人：待分配  
> 状态：待启动

---

## 总览

| Phase | 名称 | 工期 | 前置依赖 | 交付物 |
|---|---|---|---|---|
| 0 | 前置调研 | 0.5 天 | 无 | 调研报告 + 消息覆盖率清单 |
| 1 | 搭建骨架 | 0.5 天 | Phase 0 | 可构建的 `e3d-ui/` 项目 |
| 2 | 桥接层与状态层 | 1.5 天 | Phase 1 | bridgeService + useChatStore + mock |
| 3 | 聊天核心 UI | 1.5 天 | Phase 2 | 完整聊天界面（不含 Markdown） |
| 4 | Markdown 渲染 | 0.5 天 | Phase 3 | MarkdownBlock 裁剪完成 |
| 5 | Provider 管理 UI | 1.5 天 | Phase 2 | SettingsPanel + ModelSwitcher |
| 6 | 联调与 C# 集成 | 2 天 | Phase 3-5 | 全流程可运行 |
| 7 | 清理与替换 | 0.5 天 | Phase 6 | 旧前端移出构建 |
| 8 | 优化与收尾 | 1 天 | Phase 7 | 验收通过 |

**总计：9 天**

> 注：Phase 3 和 Phase 5 可并行（都仅依赖 Phase 2），但建议串行以确保质量。

---

## Phase 0: 前置调研（0.5 天）

### 目标
确认所有前置假设成立，消除实施过程中的不确定性。

### 任务清单

- [ ] **T0.1** 验证 `web-ui/` 源码构建
  - 执行 `cd web-ui && npm install && npm run build`
  - 记录构建结果（成功/失败/警告数）
  - 产出：`docs/verification/web-ui-build-report.md`

- [ ] **T0.2** 检查 C# 端 bridge 消息处理覆盖率
  - 对比 `src/E3DCopilot.WebHost/Bridge.cs` 的 `HandleMessage` switch 分支
  - 对比 `web-ui/src/messageContracts.ts` 的 `MessageTypes` 常量
  - 标记哪些消息类型 C# 端已实现、哪些未实现
  - 产出：消息覆盖率矩阵表

- [ ] **T0.3** 确认 WebView2 宿主方案
  - 确认使用 `E3DCopilot.WebHost/WebViewForm.cs`（已验证可行）
  - 确认 `SetVirtualHostNameToFolderMapping` 已配置
  - 确认 `E3D_COPILOT_DEV_URL` 开发模式环境变量生效
  - 产出：宿主方案确认记录

- [ ] **T0.4** 梳理 ProviderInfo 前后端字段差异
  - 对照 `CopilotConfig.ProviderConfig`（C#）和 `ProviderInfo`（TS）
  - 确认 `TimeoutMs`/`Temperature`/`MaxTokens` 前端不暴露的决策
  - 确认 `Enabled`/`BuiltIn` 前端独有字段的 C# 端处理
  - 产出：字段对照表（已写入设计文档 Section 0.5）

- [ ] **T0.5** 确认 C# 端新消息类型实现状态
  - `llm:thinking`：C# 端 `AgentLoop` 是否已推送？
  - `llm:turn_started`：C# 端是否已推送？
  - `tool:progress`：C# 端是否已推送？
  - `llm:usage`：C# 端是否已推送？
  - 未实现的标记为"前端预留 UI，后端后续补齐"
  - 产出：缺失消息清单

### 验收标准
- [x] 所有 5 项任务完成
- [x] 调研报告归档到 `docs/verification/`
- [x] 无阻塞性发现（如有，更新设计文档风险表）

---

## Phase 1: 搭建骨架（0.5 天）

### 目标
建立 `e3d-ui/` 项目骨架，验证工具链和构建流程。

### 任务清单

- [ ] **T1.1** 初始化项目
  - `npm create vite@latest e3d-ui -- --template react-ts`
  - 配置 `tsconfig.json`：路径别名 `@/*` → `src/*`
  - 配置 `vite.config.ts`：构建输出 `dist/`，路径别名

- [ ] **T1.2** 安装依赖
  ```
  npm install zustand lucide-react react-markdown remark-gfm rehype-highlight clsx
  npm install -D @tailwindcss/vite tailwindcss rollup-plugin-visualizer
  ```

- [ ] **T1.3** 配置 Tailwind CSS 4
  - 配置 `@tailwindcss/vite` 插件
  - 创建 `src/index.css`，引入 Tailwind 指令
  - 配置 dark 模式 `class` 策略

- [ ] **T1.4** 配置 bundle 分析
  - `vite.config.ts` 中引入 `rollup-plugin-visualizer`
  - 构建后自动生成 `stats.html`

- [ ] **T1.5** 验证构建
  - `npm run build` 成功
  - `npm run dev` 启动，浏览器可访问
  - 产出：可运行的 Hello World 页面

### 目录结构

```
e3d-ui/
├── index.html
├── package.json
├── vite.config.ts
├── tsconfig.json
├── tsconfig.app.json
├── tsconfig.node.json
├── public/
│   └── vite.svg
└── src/
    ├── main.tsx              ← 入口
    ├── App.tsx               ← 根组件
    ├── index.css             ← Tailwind 入口
    └── vite-env.d.ts
```

### 验收标准
- [x] `npm run build` 0 错误
- [x] `npm run dev` 可访问
- [x] Tailwind 类生效
- [x] dark 模式 class 切换生效

---

## Phase 2: 桥接层与状态层（1.5 天）

### 目标
实现 bridge → store 的完整数据通路，支持 standalone 开发模式。

### 任务清单

- [ ] **T2.1** 复制并适配 `messageContracts.ts`
  - 从 `web-ui/src/messageContracts.ts` 复制到 `e3d-ui/src/services/messageContracts.ts`
  - 移除对 `@shared/*` 的引用（如有）
  - 确认所有 Section 2.4 中的消息类型常量完整

- [ ] **T2.2** 复制并改造 `bridge.ts` → `bridgeService.ts`
  - 从 `web-ui/src/bridge.ts` 复制核心类
  - 保留：`send()`、`sendAndWait()`、`on()`、`once()`、standalone 模式
  - 保留：所有类型化便利方法（`sendUserMessage`、`cancel`、`newSession` 等）
  - 新增：`isConnected()` 方法，检测 `chrome.webview` 可用性
  - 新增：`onDisconnected` / `onReconnected` 事件

- [ ] **T2.3** 实现 `useChatStore`（Zustand）
  - 按 Section 2.2 定义的 `ChatStore` interface 完整实现
  - `messages` 数组操作：append / update / finalize / clear
  - 流式状态机：`isStreaming` / `currentAssistantMsgId` / `currentThinkingMsgId`
  - Provider/Model 缓存：`providers` / `models` / `currentProvider` / `currentModel`
  - UI 状态：`showSettings` / `isLoadingModels` / `error`
  - 连接状态：`bridgeConnected` / `lastPingTime`

- [ ] **T2.4** 实现 bridge 事件 → store 映射
  - 在 `bridgeService.ts` 中注册 `bridge.on()` 回调
  - 按 Section 2.4 所有消息类型实现 switch 分支
  - 每个分支调用 `useChatStore.getState().xxx()` 更新状态
  - 关键映射：
    ```
    host:ready       → setBridgeConnected(true)
    config:sync      → 更新 providers/models/currentProvider/currentModel
    llm:turn_started → set isStreaming=true, 创建 assistant message
    llm:stream:delta → updateAssistantMessage(id, delta)
    llm:stream:end   → finalizeAssistantMessage(id)
    llm:thinking     → updateThinkingMessage(id, text)
    tool:dispatch    → appendMessage({ role: "tool_call" })
    tool:result      → updateToolResult(toolId, result)
    tool:error       → updateToolResult(toolId, error)
    tool:approval    → appendMessage + 设置待审批状态
    error            → appendMessage({ role: "error" })
    turn:done        → set isStreaming=false
    pong             → set lastPingTime=Date.now()
    ```

- [ ] **T2.5** 实现 `useHeartbeat` hook
  - 每 30s 发送 `ping`
  - 10s 内无 `pong` 则 `setBridgeConnected(false)`
  - 组件卸载时清理 interval

- [ ] **T2.6** 实现 standalone 模式 mock
  - 当 `!bridge.isAvailable()` 时，使用 `setInterval` 模拟流式消息
  - 模拟数据：用户发送 → AI 流式回复 → 工具调用 → 工具结果
  - 开发时可脱离 C# 宿主独立调试 UI

- [ ] **T2.7** 实现 `ErrorBoundary` 组件
  - 捕获渲染错误，显示错误信息 + 堆栈（dev）+ 重新加载按钮
  - 包裹 `App` 根组件

- [ ] **T2.8** 实现 `DisconnectScreen` 组件
  - 显示 "与 E3D 的连接已断开" 提示
  - 提供 "重新连接" 按钮
  - 重新连接后触发 `config:sync`

### 文件清单

```
src/
├── services/
│   ├── messageContracts.ts    ← 复制自 web-ui
│   └── bridgeService.ts       ← 改造自 bridge.ts
├── store/
│   └── useChatStore.ts        ← Zustand store
├── hooks/
│   └── useHeartbeat.ts        ← 心跳检测
├── components/
│   ├── ErrorBoundary.tsx      ← 错误边界
│   └── DisconnectScreen.tsx   ← 断连提示
└── types/
    └── index.ts               ← Message type + ProviderInfo + ModelInfo
```

### 验收标准
- [x] `bridgeService.ts` 可在 standalone 模式下工作
- [x] `useChatStore` 所有 action 可调用且状态正确更新
- [x] mock 数据能驱动 UI 渲染
- [x] `useHeartbeat` 在无宿主时正确标记断连
- [x] TypeScript 类型检查通过

---

## Phase 3: 聊天核心 UI（1.5 天）

### 目标
实现完整聊天界面（Markdown 渲染在 Phase 4 补充）。

### 任务清单

- [ ] **T3.1** 实现 `Header`
  - Logo（lucide `Bot` 图标）+ 标题 "E小智"
  - 设置按钮（`Settings` 图标），点击 `toggleSettings()`
  - 连接状态指示：绿点/红点 + tooltip
  - 新会话按钮（`Plus` 图标），点击 `clearSession()`

- [ ] **T3.2** 实现 `WelcomeScreen` + `QuickAction`
  - 条件渲染：`messages.length === 0 && !isStreaming`
  - 品牌标题 + 副标题
  - 3-5 个 QuickAction 按钮
  - 点击 QuickAction → `setInputValue(text)` + `sendMessage()`
  - 快捷操作列表：
    - `查询所有设备`
    - `创建一根管道`
    - `帮我设计一个泵房`
    - `列出当前项目的管嘴信息`

- [ ] **T3.3** 实现 `MessageList` + `MessageRow`
  - `MessageList`：flex-1、overflow-y-auto、自动滚动到底部
  - 自动滚动：新消息时 scrollIntoView，用户手动上滚时暂停自动滚动
  - `MessageRow`：根据 `message.role` 分发到对应组件
  - CSS containment 优化：`contain: strict` 在消息行上

- [ ] **T3.4** 实现 `UserBubble`
  - 右对齐、品牌色背景
  - 显示 `message.text`
  - 时间戳显示

- [ ] **T3.5** 实现 `AssistantBubble`
  - 左对齐、浅色背景
  - 流式光标：`partial === true` 时末尾显示 ▌闪烁动画
  - 暂时纯文本渲染（Phase 4 接入 MarkdownBlock）

- [ ] **T3.6** 实现 `ThinkingBlock`
  - 💭 图标 + "思考过程" 折叠标题
  - `partial === true` 时自动展开
  - `partial === false` 时自动折叠
  - 折叠时：显示首行（截断 80 字符）+ "..."
  - 展开时：完整推理文本
  - 样式：`text-gray-500 italic`、左侧 `border-l-2 border-blue-300`

- [ ] **T3.7** 实现 `ToolCallCard`
  - 🔧 图标 + `toolName`
  - 参数折叠展示（默认折叠，点击展开）
  - 运行中状态：旋转动画
  - 样式：左侧 `border-l-3 border-amber-400`

- [ ] **T3.8** 实现 `ToolResultCard`
  - ✅ 图标 + `toolName` + "结果"
  - 结果内容折叠展示（默认折叠，内容超 200 字符时折叠）
  - 错误状态：❌ 图标 + 错误信息
  - 样式：左侧 `border-l-3 border-emerald-400`

- [ ] **T3.9** 实现 `ErrorCard`
  - ❌ 图标 + 错误信息
  - 红色边框 + 淡红背景
  - 可复制错误信息

- [ ] **T3.10** 实现 `InputBar`
  - `ModelSwitcher` 占位（Phase 5 实现）
  - `<textarea>` 自动伸缩（1-6 行）
  - `SendButton`：`Send` 图标，`disabled` 当 `!inputValue.trim() || isStreaming`
  - Enter 发送，Shift+Enter 换行
  - 流式中显示取消按钮替代发送按钮

- [ ] **T3.11** 实现 `ChatView`
  - 组合 `WelcomeScreen` / `MessageList` / `InputBar`
  - 条件渲染 `WelcomeScreen` vs `MessageList`

### 文件清单

```
src/components/
├── Header.tsx
└── chat/
    ├── ChatView.tsx
    ├── WelcomeScreen.tsx
    ├── QuickAction.tsx
    ├── MessageList.tsx
    ├── MessageRow.tsx
    ├── UserBubble.tsx
    ├── AssistantBubble.tsx
    ├── ThinkingBlock.tsx
    ├── ToolCallCard.tsx
    ├── ToolResultCard.tsx
    ├── ErrorCard.tsx
    └── InputBar.tsx
```

### 验收标准
- [x] WelcomeScreen 在无消息时显示
- [x] QuickAction 点击后发送消息
- [x] UserBubble / AssistantBubble 正确渲染
- [x] ThinkingBlock 流式展开/结束后折叠
- [x] ToolCallCard / ToolResultCard 正确渲染
- [x] ErrorCard 正确渲染
- [x] InputBar Enter 发送、Shift+Enter 换行
- [x] 流式中有闪烁光标
- [x] 自动滚动到底部
- [x] 100 条消息不卡顿

---

## Phase 4: Markdown 渲染（0.5 天）

### 目标
裁剪旧 `MarkdownBlock`，接入 `AssistantBubble`。

### 任务清单

- [ ] **T4.1** 裁剪 `MarkdownBlock`
  - 从 `web-ui/src/components/common/MarkdownBlock.tsx` 复制
  - 移除 `@shared/proto/cline/*` 引用
  - 移除 `useExtensionState` / `ExtensionStateContext`
  - 移除 `i18next` / `useTranslation`，替换为固定中文文案（"复制"、"已复制"）
  - 移除 `FileServiceClient` 引用
  - 移除 `MermaidBlock`（首版不需要）
  - `UnsafeImage` → 替换为原生 `<img>`

- [ ] **T4.2** 按需引入 hljs 语言包
  - 创建 `src/utils/highlight.ts`，注册常用语言
  - 语言列表：json, python, bash, sql, xml, yaml, plaintext
  - **禁止** `import 'highlight.js'` 全量引入
  - 配置 `rehype-highlight` 使用自定义注册

- [ ] **T4.3** 实现代码块复制按钮
  - 从旧项目复制 `CopyButton` 逻辑
  - 使用 `navigator.clipboard.writeText`
  - 复制后显示 ✓ 1.5s

- [ ] **T4.4** 接入 `AssistantBubble`
  - `AssistantBubble` 中用 `MarkdownBlock` 替换纯文本渲染
  - 流式中传入 `partial` 控制光标

### 文件清单

```
src/components/common/
├── MarkdownBlock.tsx         ← 裁剪自旧项目
└── CopyButton.tsx            ← 裁剪自旧项目
src/utils/
└── highlight.ts              ← hljs 按需注册
```

### 验收标准
- [x] Markdown 标题、列表、表格、代码块正常渲染
- [x] 代码块语法高亮生效（json/python/bash/sql）
- [x] 代码块复制按钮工作
- [x] URL 自动转链接
- [x] GFM 表格渲染正确
- [x] hljs 语言包体积 < 20KB

---

## Phase 5: Provider 管理 UI（1.5 天）

### 目标
实现 ModelSwitcher、SettingsPanel、ProviderSettingsSection。

### 任务清单

- [ ] **T5.1** 改写 `ModelSwitcher`
  - 从 `useChatStore` 读取 `models`、`currentProvider`、`currentModel`
  - 下拉列表：按 provider 分组显示模型
  - 当前模型高亮
  - 选择后调用 `store.switchModel(ref)`
  - 打开时调用 `store.loadProviders()`（如未加载）
  - 加载中显示 spinner

- [ ] **T5.2** 实现 `SettingsPanel`
  - 右侧滑出面板，半透明遮罩
  - 遮罩点击关闭
  - ESC 键关闭
  - 滑出动画（`transition-transform duration-300`）
  - 内部渲染 `ProviderSettingsSection`

- [ ] **T5.3** 改写 `ProviderSettingsSection`
  - Provider 列表：卡片式布局
  - 每个 Provider 卡片显示：名称、类型、Base URL、模型数、默认模型
  - 当前激活 Provider 高亮
  - 操作按钮：设为默认、编辑、删除、刷新模型
  - 底部 "添加 Provider" 按钮

- [ ] **T5.4** 实现 Provider 编辑表单
  - 字段：名称、类型（openai/anthropic）、Base URL、API Key
  - API Key 输入：password 类型 + 显示/隐藏切换
  - 保存后调用 `bridge.send('provider:save', payload)`
  - 删除前确认弹窗

- [ ] **T5.5** 实现模型拉取
  - 点击 "刷新模型" 调用 `bridge.send('provider:fetch_models', { name })`
  - 拉取中显示 spinner
  - 拉取失败显示错误信息
  - 拉取成功更新 Provider 卡片的模型列表

- [ ] **T5.6** 实现 API Key 设置
  - 掩码显示：`sk-****xxxx`（显示前 3 位 + 后 4 位）
  - 点击编辑进入输入模式
  - 保存调用 `bridge.send('provider:set_key', { name, apiKey })`

### 文件清单

```
src/components/
├── chat/
│   └── ModelSwitcher.tsx         ← 改写
└── settings/
    ├── SettingsPanel.tsx          ← 新建
    ├── ProviderSettingsSection.tsx ← 改写
    ├── ProviderCard.tsx           ← 新建
    ├── ProviderEditForm.tsx       ← 新建
    └── ApiKeyInput.tsx            ← 新建
```

### 验收标准
- [x] ModelSwitcher 下拉显示模型列表
- [x] 选择模型后 UI 更新 + 通知后端
- [x] SettingsPanel 右侧滑出
- [x] Provider 列表正确显示
- [x] 添加 Provider 后保存到后端
- [x] 删除 Provider 前有确认
- [x] API Key 掩码显示
- [x] 刷新模型工作正常
- [x] ESC / 点击遮罩关闭 SettingsPanel

---

## Phase 6: 联调与 C# 集成（2 天）

### 目标
验证前端与 C# 后端的完整通信链路。

### 任务清单

- [ ] **T6.1** 配置 C# 宿主加载 `e3d-ui/dist/`
  - 修改 `WebViewForm.cs` 中 `wwwrootDir` 指向 `e3d-ui/dist/`
  - 或配置构建输出 `vite.config.ts` → `../src/E3DCopilot.WebHost/wwwroot/`
  - 验证 WebView2 加载新前端

- [ ] **T6.2** 验证初始化流程
  - E3D 启动 → Addin 加载 → WebView2 初始化 → 前端加载
  - `host:ready` 消息收到 → `bridgeConnected = true`
  - `config:sync` 消息收到 → providers/models 正确填充
  - ModelSwitcher 显示当前模型

- [ ] **T6.3** 验证聊天全流程
  - 用户输入 → `user:message` → C# 收到
  - C# → `llm:stream:delta` × N → 前端流式渲染
  - C# → `llm:stream:end` → 前端光标消失
  - C# → `tool:dispatch` → 前端显示 ToolCallCard
  - C# → `tool:result` → 前端显示 ToolResultCard
  - C# → `turn:done` → 前端 isStreaming=false

- [ ] **T6.4** 验证推理过程
  - C# → `llm:thinking` × N → ThinkingBlock 流式展开
  - `llm:stream:end` → ThinkingBlock 折叠
  - 如 C# 未实现 `llm:thinking`，记录为待补齐

- [ ] **T6.5** 验证工具审批
  - C# → `tool:approval` → 前端显示审批请求
  - 用户点击批准 → `user:approve` → C# 收到
  - 用户点击拒绝 → `user:approve(id, false)` → C# 收到

- [ ] **T6.6** 验证 Provider 管理
  - `providers:list` → 列表正确
  - `provider:save` → 新增/更新成功
  - `provider:delete` → 删除成功
  - `provider:fetch_models` → 模型列表更新
  - `model:switch` → 切换成功

- [ ] **T6.7** 验证心跳和断连
  - 正常运行时 `bridgeConnected = true`
  - 关闭 E3D → 心跳超时 → `bridgeConnected = false` → DisconnectScreen 显示
  - 重新打开 E3D → 重连成功 → `config:sync` 自动同步

- [ ] **T6.8** 修复发现的问题
  - 消息格式不一致
  - 字段命名差异（C# PascalCase vs JS camelCase）
  - 时序问题（消息乱序、重复）
  - 记录所有修复到联调日志

### 验收标准
- [x] 初始化流程完整
- [x] 聊天全流程可运行
- [x] 工具调用/结果正确展示
- [x] Provider CRUD 正常
- [x] 心跳检测工作
- [x] 无 JS 控制台错误
- [x] 联调日志归档

---

## Phase 7: 清理与替换（0.5 天）

### 目标
将 `e3d-ui` 替换为正式前端，旧 `web-ui` 退出构建。

### 任务清单

- [ ] **T7.1** 保留 `web-ui/` 作为参考
  - 不删除目录，但从构建流程中移除
  - 在 `web-ui/package.json` 中添加 `"deprecated": true` 标记

- [ ] **T7.2** 更新构建流程
  - `e3d-ui/vite.config.ts` 的 `build.outDir` 指向 `../src/E3DCopilot.WebHost/wwwroot/`
  - 或创建 `deploy.cmd` 脚本：`cd e3d-ui && npm run build && xcopy dist/ ../src/E3DCopilot.WebHost/wwwroot/ /Y`

- [ ] **T7.3** 更新 C# 引用
  - 确认 `WebViewForm.cs` 加载的是新前端产物
  - 移除对旧 `web-ui/build/` 的引用（如有）

- [ ] **T7.4** 更新文档
  - `README.md`：更新前端目录说明
  - `开发引领手册.md`：更新开发流程
  - `docs/arch/源码结构.md`：更新目录结构图

### 验收标准
- [x] `npm run build` 产物正确输出到 `wwwroot/`
- [x] E3D 启动后加载的是新前端
- [x] 旧 `web-ui/` 不参与构建
- [x] 文档已更新

---

## Phase 8: 优化与收尾（1 天）

### 目标
性能优化、bundle 体积验收、文档完善。

### 任务清单

- [ ] **T8.1** 代码分割
  - `SettingsPanel` 使用 `React.lazy()` + `Suspense` 懒加载
  - 验证首屏不加载 Settings 相关代码

- [ ] **T8.2** Bundle 体积验收
  - `npm run build` 后检查 `dist/assets/` 体积
  - 打开 `stats.html`（visualizer 报告）分析占比
  - JS < 800KB ✅ / ❌
  - CSS < 100KB ✅ / ❌
  - 总计 < 1MB ✅ / ❌
  - 如超标：定位大模块，按需裁剪

- [ ] **T8.3** 优化 Markdown 渲染
  - 长消息（> 5000 字符）分块渲染
  - 或使用 `react-markdown` 的 `urlTransform` 优化

- [ ] **T8.4** 错误处理完善
  - `ErrorBoundary` 捕获边界组件错误
  - `bridgeService` 全局错误处理
  - 网络错误友好提示

- [ ] **T8.5** 日志和诊断（可选）
  - 开发模式下 bridge 消息日志（`[Bridge → Host]` / `[Host → Bridge]`）
  - 简单诊断面板：消息统计、bridge 状态、store 快照

- [ ] **T8.6** 编写前端 README
  - 技术栈说明
  - 开发/构建/部署流程
  - 目录结构
  - 与 C# 宿主的通信协议说明

- [ ] **T8.7** 验收检查
  - 按设计文档 Section 7 全部验收标准逐项检查
  - 产出验收报告

### 验收标准
- [x] JS bundle < 800KB
- [x] CSS bundle < 100KB
- [x] `SettingsPanel` 懒加载生效
- [x] `npm run build` 0 错误 0 警告
- [x] TypeScript 类型检查通过
- [x] 前端 README 完成
- [x] 验收报告归档

---

## 关键依赖时间线

```
Day 1     Phase 0 (0.5d) + Phase 1 (0.5d)
Day 2-3   Phase 2 (1.5d)
Day 3-5   Phase 3 (1.5d)
Day 5     Phase 4 (0.5d)
Day 5-7   Phase 5 (1.5d)
Day 7-9   Phase 6 (2d)
Day 9     Phase 7 (0.5d) + Phase 8 部分开始
Day 10    Phase 8 (1d) 完成
```

> 实际工期 9.5 天（Phase 7 和 Phase 8 有 0.5 天重叠可压缩到 9 天）

---

## 风险预案

| 风险触发条件 | 预案 |
|---|---|
| Phase 0 发现 C# 端缺失大量消息类型 | 优先补齐 C# 端实现；前端先用 mock 占位，Phase 6 再联调 |
| Phase 2 standalone mock 复杂度超预期 | 简化 mock：只模拟 `llm:stream:delta` 和 `tool:dispatch/result`，不模拟全量 |
| Phase 4 MarkdownBlock 裁剪后发现遗漏依赖 | 逐个处理，不回退到旧版；无法解决的标记为 TODO |
| Phase 5 ProviderSettingsSection 改写工作量超预期 | 首版只实现 Provider 列表 + 切换，编辑/新增/删除放到 v2.1 |
| Phase 6 联调发现 C# 消息格式不一致 | 确认 C# 序列化策略（`JsonNamingPolicy.CamelCase`），前端按实际格式适配 |
| Bundle 体积超标 | 优先裁剪 hljs 语言包 → 裁剪 remark 插件 → 考虑替换 react-markdown |

---

## 验收检查清单（终验）

### 功能

- [ ] WelcomeScreen 显示
- [ ] QuickAction 点击发送
- [ ] 用户消息发送 + AI 流式回复
- [ ] ThinkingBlock 展开/折叠
- [ ] ToolCallCard / ToolResultCard 渲染
- [ ] 工具审批批准/拒绝
- [ ] ModelSwitcher 切换
- [ ] SettingsPanel 打开/关闭
- [ ] Provider 增删改
- [ ] API Key 设置
- [ ] 新会话 / 取消
- [ ] 断连检测 / 重连

### 性能

- [ ] 首屏 < 2s
- [ ] JS bundle < 800KB
- [ ] CSS bundle < 100KB
- [ ] 100 条消息不卡顿
- [ ] 流式渲染 60fps

### 质量

- [ ] `npm run build` 0 错误 0 警告
- [ ] `tsc --noEmit` 通过
- [ ] 无 console.error（正常流程）
- [ ] dark / light 主题切换正常

### 集成

- [ ] E3D 启动后加载新前端
- [ ] 聊天全流程可运行
- [ ] Provider 管理可运行
