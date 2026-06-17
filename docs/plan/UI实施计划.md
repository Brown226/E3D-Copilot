# E小智 — UI 实施计划

> 版本：v2.0 — 基于 cline-chinese-main webview-ui 适配
> 状态：旧 WinForms UI 实施计划已废弃，全面替换为 WebView2 + React SPA 适配计划
> 参考：`参考开源项目/cline-chinese-main/webview-ui/`

---

## 概述

**核心策略**：fork cline-chinese-main 的 webview-ui 并适配为 E小智 前端，而非从零构建 React UI。

**优势**：
- 节省 ~80% UI 开发工作量
- 获得成熟的消息系统、流式渲染、虚拟滚动、主题系统
- 前端架构与 C# 后端的 Handler 模式自然对应
- 可跟随上游更新获取 bug 修复和新功能

**三个阶段**：

| 阶段 | 周期 | 目标 | 交付 |
|------|------|------|------|
| **Phase A** | 2-3天 | WebView2 环境 + 基础通信 + 最小聊天闭环 | web-ui/ 可运行，浏览器→E3D 双向通信 |
| **Phase B** | 2-3天 | E3D 工具渲染 + 审批流 + 设置面板 | 完整工具展示 + 用户审批 + 配置持久化 |
| **Phase C** | 1-2天 | 主题适配 + 品牌化 + 性能优化 | 正式版前端 |

---

## Phase A — 基础适配（2-3 天）

### 目标
将 cline-chinese-main 的 webview-ui fork 为 E小智 前端，实现 WebView2 中的基础聊天闭环。

### 步骤

#### ✅ A1. Fork 并初始化项目（已完成）

- [x] 复制 `参考开源项目/cline-chinese-main/webview-ui/` → `web-ui/`
- [x] 清理 VSCode 特有依赖
- [x] 修改 `package.json`（名称 `e3d-copilot-webui`）
- [x] 安装依赖（`npm install`，含 devDependencies）
- [x] 补充缺失的 `src/shared/` 类型导出
- [x] 验证：`npm run dev` 启动成功，`http://localhost:25463/` 可访问

#### A2. 替换通信层（0.5 天）

cline-chinese-main 使用 VSCode `acquireVsCodeApi().postMessage()` 通信。E3D 中使用 `window.chrome.webview.postMessage()`。

- [x] 创建 `src/bridge.ts`：WebView2 postMessage/onMessage 封装
- [ ] 创建 `src/services/bridgeClient.ts`：上层 API（`sendMessage()`, `sendApproval()`, `onStateUpdate()` 等）
- [ ] 替换 `PLATFORM_CONFIG` 中 `postMessage` 和 `decodeMessage` 的实现
- [ ] 移除 protobuf/gRPC 相关代码（保持简单 JSON）
- [ ] 验证：`bridge.send('ping', {})` → C# 端收到并回复

#### ✅ A3. 替换主题变量（已完成）

- [x] 修改 `src/theme.css`：83 个 `var(--vscode-*)` → E3D 暗色固定值
- [x] 保留 Tailwind 配置，调整主题色调
- [x] 创建 `src/index.css`：E3D 全局样式入口
- [ ] 验证：暗色主题正常显示，文字/卡片/输入框可读

#### A4. 移除不适用的组件（0.5 天）

- [ ] 移除 `BrowserSessionRow` 及相关 browser 组件
- [ ] 移除 `checkpoint` / `checkpoint_diff` 消息类型处理
- [ ] 移除 VSCode 特有功能（diff editor 适配、terminal 适配等）
- [ ] 简化设置页：只保留 LLM 配置 + 工具权限 + 常用设置

#### A5. E3D 端对接（0.5 天）

- [ ] 确认 `WebViewForm.cs` 正确加载 React 构建产物
- [ ] 确认 `Bridge.cs` 正确解析 JSON 消息并路由到 `CopilotController`
- [ ] 实现 `state:update` 消息：后端推送完整 ExtensionState → 前端渲染
- [ ] 验证 `llm:stream:*` 消息：输入 → LLM 响应 → 流式显示
- [ ] 验证 `tool:*` 消息：工具调用触发、结果显示

### 验收标准

- [ ] `npm run dev` + `npm run build` 正常
- [ ] WebView2 加载 React SPA，暗色主题显示
- [ ] 输入消息 → 后端处理 → LLM 流式回复 → 前端逐 token 显示
- [ ] 工具调用在消息列表中以卡片形式展示

---

## Phase B — E3D 工具渲染（2-3 天）

### 目标
适配前端展示 E3D 特有工具调用和结果，实现完整的审批流和设置面板。

### 步骤

#### B1. PML 命令输出适配（1 天）

cline-chinese-main 的 `CommandOutputRow` 设计用于 shell 命令输出，需适配 PML 格式。

- [ ] 修改 `CommandOutputRow.tsx`：支持 PML 行号前缀、错误高亮
- [ ] PML 输出格式化：`$p` 消息行灰色，`handle/code` 错误红色，表格对齐
- [ ] 执行状态指示（success/fail + 图标 + 颜色）

#### B2. E3D 工具卡片扩展（1 天）

在 `ToolCard` 基础上扩展 E3D 工具的渲染：

- [ ] `query_elements` / `get_attributes` → 表格 + 键值对渲染
- [ ] `execute_pml` → PML 代码预览 + 执行状态
- [ ] `modify` / `batch_set_attribute` → 修改摘要 + 回滚按钮（后续）
- [ ] `check_*` → 通过/失败状态 + 错误列表
- [ ] `calculate_*` → 数值结果 + 单位

#### B3. PML Diff 组件（0.5 天）

- [ ] 新增 `PmlDiffRow.tsx`：PML 脚本的插入/删除/修改 diff
- [ ] 支持单行 diff 和完整文件 diff
- [ ] 与审批流集成：用户查看 diff 后批准/拒绝

#### B4. 设置面板适配（0.5 天）

- [ ] 替换模型提供商配置为 vLLM（`http://localhost:8000/v1`）
- [ ] 工具权限配置（auto/ask/planOnly per-tool）
- [ ] 主题/语言设置
- [ ] 保存设置 → `user:settings` → C# 端持久化

#### B5. 审批流完整对接（0.5 天）

- [ ] `tool:approval` 消息 → 前端的审批对话框 → 用户交互
- [ ] 用户批准/拒绝 → `user:approve` → `Controller.Approve()`
- [ ] 会话级持久化（"本次会话中记住此选择"）

### 验收标准

- [ ] PML 命令输出正确格式化（行号/错误/状态）
- [ ] 5 类 E3D 工具渲染正常（查询/修改/PML/检查/计算）
- [ ] PML diff 展示 + 用户审批流程完整
- [ ] 设置面板可以配置模型和权限
- [ ] 审批对话框正确显示工具参数

---

## Phase C — 完善（1-2 天）

### 目标
品牌化、性能优化、边缘情况处理。

### 步骤

#### C1. 品牌化（0.5 天）

- [ ] Logo 替换（E小智 品牌图标）
- [ ] 标题/描述/欢迎页文案 E3D 定制
- [ ] 欢迎页引导功能（示例 prompt 按钮）
- [ ] favicon 替换

#### C2. 主题微调（0.5 天）

- [ ] 颜色微调以匹配 E3D 设计规范
- [ ] 字体调整（E3D 环境常用字体）
- [ ] 高对比度模式支持（可选）

#### C3. 性能优化（0.5 天）

- [ ] 大量 PML 输出时的虚拟滚动性能（react-virtuoso）
- [ ] 流式渲染性能（50+ token/s 无卡顿）
- [ ] 首屏加载优化（代码分割）
- [ ] 内存占用监控

#### C4. 错误边界 + 国际化（0.5 天）

- [ ] React ErrorBoundary 适配 E3D 场景
- [ ] 网络断开 / LLM 超时 / E3D 异常的前端展示
- [ ] 补充 E3D 专业术语国际化（中英文）

### 验收标准

- [ ] 全暗色主题，E3D 品牌元素就位
- [ ] 流式渲染 60fps
- [ ] 首屏加载 < 1s
- [ ] PML 输出 1000+ 行无卡顿
- [ ] 中英文界面完整

---

## 与旧 WinForms UI 计划的对比

| 旧计划（WinForms） | 新计划（WebView2 + React） | 工作量对比 |
|-------------------|--------------------------|-----------|
| Phase 1a-1d: 17 个自定义控件 | Phase A: fork → 适配通信层 | 新计划 ~20% |
| Phase 2: 设置/历史/虚拟滚动 | Phase B: 继承 cline 已有实现 | 新计划 ~10% |
| Phase 3: 导出/错误边界/撤销 | Phase C: 品牌化 + 微调 | 新计划 ~30% |
| 总计: 29 个文件 100% 自建 | 总计: ~60% 保留 + ~30% 修改 + ~10% 新增 | **新计划约 1/5 工作量** |

---

## 开发环境

```bash
# 终端 1: 启动 Vite HMR
cd web-ui
npm install
npm run dev
# → http://localhost:5173

# 终端 2: 启动 E3D（或用 TestHost）
# WebViewForm 在 E3D_COPILOT_DEV_URL 环境变量存在时指向 dev server

# 构建
npm run build
# → 输出到 src/E3DCopilot.WebHost/wwwroot/
```

## 文件结构变化

```
新增/修改的文件（相对于 cline-chinese-main webview-ui）：
├── src/
│   ├── bridge.ts                          # 新增：WebView2 通信桥
│   ├── services/bridgeClient.ts           # 新增：Bridge API 封装
│   ├── theme.css                          # 修改：VSCode 变量 → E3D 固定值
│   ├── index.css                          # 新增：E3D 全局样式
│   ├── components/
│   │   ├── chat/
│   │   │   ├── CommandOutputRow.tsx        # 修改：PML 输出适配
│   │   │   ├── ToolGroupRenderer.tsx       # 修改：E3D 工具分组
│   │   │   └── PmlDiffRow.tsx             # 新增：PML Diff
│   │   └── chat/ChatRowContent.tsx         # 修改：移除 browser
│   └── locales/
│       └── zh-CN.json                     # 修改：补充 E3D 术语

移除的文件：
├── src/components/chat/BrowserSessionRow.tsx    # E3D 无浏览器工具
├── src/components/chat/browser/                   # 整个 browser 目录
└── src/**/*.browser.*                             # 所有 browser 相关
```
