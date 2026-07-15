# E小智 v2.0 vs DeepSeek-Reasonix-desktop v1.17.11 全方位对比分析

> 整理日期：2026-07-13
> 对比对象：`E小智-v1.0-开发中/`（E小智 v2.0）vs `参考开源项目/DeepSeek-Reasonix-desktop-v1.17.11/`（Reasonix 重写版 / main-v2 分支）
> 事实依据：E小智 侧来自 `docs/arch/*.md` 设计文档 + 实际源码目录结构；Reasonix 侧来自对其 v1.17.11 源码的逐文件调研（含 `file:line` 佐证）。
> 结论：**两者同源 cline 架构，但长成了两个不同物种** —— E小智 是 E3D 垂直领域嵌入式 Copilot，Reasonix 是通用独立 Coding Agent。

---

## 一、定位与形态（本质差异最大）

| 维度 | **E小智 v2.0** | **DeepSeek-Reasonix v1.17.11** |
|---|---|---|
| 产品定位 | **E3D 工厂设计内嵌 AI Copilot**（AVEVA Everything3D 进程内 Addin） | **通用 AI Coding Agent**（DeepSeek 前缀缓存深度优化） |
| 宿主形态 | 加载进 E3D 的 DLL（进程内 Addin），UI 嵌在 E3D DockedWindow | 独立 CLI 二进制 + Wails 桌面 GUI（非编辑器扩展） |
| 运行环境 | 必须依附 E3D 2.1 进程（32 位 x86 .NET Framework 4.8） | 任意终端/桌面，跨 Win/mac/Linux、amd64+arm64 |
| 主语言 | **C# 7.3 / .NET Framework 4.8**（受 E3D 强约束） | **Go 1.25**（后端内核）+ **TypeScript/React** 前端 |
| 领域知识 | 深度绑定 AVEVA API + PML + 工厂设计（管道/设备/结构） | 通用代码库（文件/Shell/Git/LSP） |
| 约束来源 | E3D 加载器、UI 线程、.NET 4.8 旧语法 | 无宿主约束，纯自主进程 |

**核心结论**：两者虽都"借鉴 cline 架构做 AI Agent"，但 E小智 是**垂直领域嵌入式插件**，Reasonix 是**通用独立 Agent** —— 这是一切架构差异的根源。

---

## 二、架构分层对比

| 层 | **E小智** | **DeepSeek-Reasonix** |
|---|---|---|
| UI 层 | WebView2(DockedWindow) + React 18 SPA | Wails v2 绑定 + React 19 SPA / CLI 用 Bubble Tea TUI |
| 通信机制 | `PostWebMessageAsString` JSON `{type,payload}`（薄协议） | Wails 直接 Go↔JS 绑定（`window.runtime`）；CLI 用 `event.Sink` 事件流 |
| 编排层 | `CopilotController`（transport-agnostic 会话驱动） | `internal/control/controller.go`（驱动 CLI+TUI+桌面） |
| 执行层 | `AgentLoop.RunAsync`（for 循环 + 工具批处理） | `agent.Run` (`internal/agent/agent.go:1163`) 多步循环 |
| 工具层 | `ToolExecutor` + `IToolHandler` + `ToolRouter` + `E3DToolDispatcher` | `tool.Registry` + `Tool` 接口 + `builtin/*` + MCP 命名空间 |
| 领域执行层 | `RealE3DEnvironment`/`PmlEngine`（Aveva.* DLL） | 无（直接 OS/文件系统） |
| 抽象隔离 | Core 不依赖 Aveva.*，接口注入 Bridge 层 | provider/tool 抽象，CLI/桌面可插拔 |

**相似的**：都采用"Controller 编排 + Agent 循环 + 工具注册表 + 事件 Sink 推流到 UI"的 cline 式分层，都有 plan mode、approval 流、reasoning 流。

**不同的**：E小智 多一层**领域 Bridge（E3DToolDispatcher → RealE3DEnvironment → PML/C#）**，这是 Reasonix 没有的；Reasonix 多一层**CLI TUI + 独立桌面进程**，E小智 没有。

---

## 三、Agent 循环（核心机制）

| 能力 | **E小智 AgentLoop** | **Reasonix agent.Run** |
|---|---|---|
| 流式调用 | `VllmProvider.StreamAsync` + 回调（`Task`+`Action<Chunk>`，因 net48 不支持 `IAsyncEnumerable`） | `provider.Stream()` 返回 `<-chan Chunk`（Go channel） |
| 工具批处理 | `ExecuteBatchAsync`：连续 ReadOnly 并行、Writer 串行（借鉴 Reasonix） | `executeBatch` |
| 流中断恢复 | 未明确提及 | 最多重试 `maxStreamRecoveries=3` 次 |
| 最终就绪检查 | `FinalReadinessCheck()` | `finalReadinessCheck`（验证 todo/evidence/delivery 合约） |
| Grace Round | 有（循环上限后给模型一次最终回答） | 有（L1367 Grace round） |
| Context Compaction | `MaybeCompactAsync()` **已落地**：`CompactRatio`(默认 0.8) + `CompactTriggerMessages`(默认 15) 可配，keepTail 动态计算（最少 6 条）✅ | `compact.go` **完整实现**（0.8 窗口比、LLM 摘要、snip 旧输出） |
| 证据账本 | `EvidenceLedger.cs` 已存在 | `internal/evidence/` 完整实现 |

**结论**：循环骨架高度同源，Reasonix 的 compaction/证据/流恢复**已落地**；E小智 的 compaction 现已工程化（`MaybeCompactAsync` 可配窗口比与触发消息数，keepTail 动态计算），长对话不再撞上下文上限。

---

## 四、工具系统

| 维度 | **E小智（~18 个 Handler）** | **DeepSeek-Reasonix（29+ 内置 + MCP）** |
|---|---|---|
| 工具接口 | `IToolHandler{Name,Description,ParameterSchema,HandleAsync}` | `Tool{Name,Description,Schema,Execute,ReadOnly}` + 可选 `Previewer`/`ImageTool`/`PlanModeClassifier` |
| 领域工具 | E3D 专属：query/modify/check/export/design/piping/geometry、execute_pml、get_attributes、calculate、4 个 ISO 出图工具 | **无领域工具**，全是通用：bash/read_file/write_file/edit_file/multi_edit/grep/glob/ls/delete_*/notebook_edit/web_fetch/codebase_search/…，以及 task/ask/workspace/session_guard |
| 元能力 | ask_user（风险分级）/task/read_file/search_knowledge | ask/task/workspace/session_guard/managed_config |
| 路由 | `ToolRouter` 路由到 20+ 专用操作（多数回退到 7 个 Dispatcher） | Registry 按前缀 Add/Remove/Suspend，`mcp__` 命名空间 |
| 批量操作 | 通过 PML 集合遍历批量改属性 | 单文件粒度（bash/脚本批量） |

**结论**：
- Reasonix 工具覆盖**文件系统/Shell/Git/LSP/网络/子代理**，是通用 Agent 标配；
- E小智 工具覆盖**E3D 数据库查询/属性修改/PML 执行/ISO 出图**，是工厂设计专属；
- 两者无重叠工具 —— 这正是"垂直 vs 通用"的体现。E小智 的 `search_knowledge` 对应 Reasonix 的 `codebase_search`，但后者基于真实代码索引。

---

## 五、LLM Provider 系统

| 维度 | **E小智** | **DeepSeek-Reasonix** |
|---|---|---|
| 抽象 | `ICopilotProvider` 单实现 `VllmProvider`（OpenAI 兼容） | `Provider` 接口 + `Register(kind,factory)` 注册表，openai/anthropic 两类 |
| 支持的模型 | vLLM 本地 + Qwen 系列（OpenAI 兼容端点） | DeepSeek/MiMo/MiniMax/Zhipu/GLM/LongCat/Ollama/任何 OpenAI 网关 + Anthropic |
| 配置 | JSON（`CopilotConfig.cs`） | **TOML**（`reasonix.toml`）+ 环境变量 `api_key_env` + OS keyring |
| Effort/推理强度 | `CopilotConfig` 已增 `Effort`/`ReasoningProtocol`，`VllmProvider` 按协议分支写入 `thinking`/`reasoning_effort`；前端 `ModelsSection.tsx` 已加「推理配置」区块（强度 5 档 + 协议 3 项）✅ | `effort`(high/max/adaptive/disabled) + `reasoning_protocol`(deepseek/openai/none) 完整 UI |
| 模型列表拉取 | 支持 `/v1/models` | 支持 |

**结论**：E小智 的 Provider 文档自述"已实现 Reasonix 90%"，原差距 **Effort UI、协议细分、环境变量密钥** 已补齐（A2 + `ProviderRegistry.ResolveApiKey` 走 `E3DCOPILOT_KEY_<NAME>`/通用环境变量）。Reasonix 的真正优势是**多供应商注册表架构**（可插拔 kind），E小智 现已补齐 `ProviderRegistry`（`Register(kind,factory)` + `New`），内置 vllm/qwen/deepseek/openai-compatible/minimax → `VllmProvider`，`anthropic` → `AnthropicProvider`，并支持 `SwitchProvider` 运行时切换。

---

## 六、记忆 / 上下文系统

| 维度 | **E小智** | **DeepSeek-Reasonix** |
|---|---|---|
| 长期记忆 | 四层设计：会话JSONL + 用户画像JSON + SQLite FTS5 自动记忆 + 项目知识库 | REASONIX.md/AGENTS.md/CLAUDE.md 层级记忆 + `Store`+`Index`(MEMORY.md) |
| 自动学习 | 用户画像每次工具调用更新偏好/习惯 | 自动摘要更新 |
| Memory 编译器 | 无 | **Memory V5 执行编译器**（`memorycompiler/`）：分类任务/聊天→结构化 IR→注入约束 |
| 上下文压缩 | **已落地**（`MaybeCompactAsync`，0.8 触发比可配，LLM 摘要）✅ | **完整**（compact.go，0.8 触发比，LLM 摘要） |
| 会话注入 | 最近 3 次会话摘要注入 | Compose() 折叠入 system prompt |

**结论**：E小智 记忆系统**设计很厚**（借鉴 Auto-Memory）且已工程化：SQLite FTS5 自动记忆（`remember`/`forget`/`search`）+ 用户画像每次工具调用自动更新（`UpdateProfileFromToolUse`）+ 项目知识库注入 SystemPrompt（`GetSystemPromptContext`）；Reasonix 记忆更轻量但 **Memory V5 编译器 + compaction 已工程化落地**。

---

## 七、UI 层

| 维度 | **E小智** | **DeepSeek-Reasonix** |
|---|---|---|
| 框架 | React 18 + Vite 6 + Tailwind 4 + Zustand 5 | React 19 + Vite 8 + Zustand + GSAP + Mermaid |
| 渲染 | WebView2 内嵌（E3D 窗口右侧 DockedWindow） | Wails 独立窗口 / CLI TUI |
| 流式/Thinking | 支持（`llm:stream:delta`/`llm:thinking`） | 支持（`ChunkReasoning` + 专门推理面板） |
| 工具卡片/diff | ToolCard + PML diff（DiffEditRow） | ToolCard + DiffView（真实代码 diff） |
| 审批弹窗 | ApprovalDialog + PromptShelfControl（键盘审批条） | ApprovalModal |
| Mermaid 图表 | 文档称"WPF 方案不支持，React 支持"但未见明确集成 | **已集成**（mermaid 库） |
| 命令面板/历史/上下文面板 | HistoryPanel 有；命令面板未见 | CommandPalette/HistoryPanel/ContextPanel/TodoPanel/CapabilitiesPanel 齐全 |

**结论**：UI 能力同宗（都源自 cline webview-ui），Reasonix 桌面端组件更全（Todo/Context/Capabilities 面板齐备）；E小智 受限于 WebView2 嵌入尺寸，更聚焦对话+审批。

---

## 八、MCP 支持（关键差距）

| | **E小智** | **DeepSeek-Reasonix** |
|---|---|---|
| MCP 客户端 | **已落地（只读子集）**✅：`McpClient` 支持 stdio / Streamable HTTP（`2024-11-05`），仅暴露 resources/prompts，包装为 `mcp_knowledge` 工具并强制 `IsReadOnly=true` | **全面支持**：stdio/Streamable HTTP/SSE，协议 `2024-11-05`，`mcp__` 命名空间，prompts/resources |
| 扩展工具来源 | 仅内置 E3D 工具 + 元能力 | 内置 + MCP 服务器 + 子代理 |

**结论**：原是最显著差距之一，现已补齐只读 MCP 子集；Reasonix 通过 MCP 可无限扩展工具生态，E小智 工具仍以内置领域工具为主、MCP 仅作只读知识扩展（也因此更可控、更安全）。

---

## 九、子代理 / 多代理

| | **E小智** | **DeepSeek-Reasonix** |
|---|---|---|
| 规划模式 | `SetPlanMode`（只读放行、写操作 block 待审批） | `planmode` + `PlanModeReadOnlyTrustGate` |
| 子代理委派 | `dispatch_subagent` 工具：真实隔离子会话（`SubagentContext` + 独立 `CopilotSession`），`MaxSubagentDepth=2`，复用 `AgentLoop` + `ToolExecutor.FilterReadOnly()` ✅ | `task`→`SubagentSpec` 真实子代理会话，最大嵌套深度 2 |
| Coordinator | 无 | Planner+Executor 双模型协作（`coordinator.go`） |
| 技能系统 | 有 `Skills` 目录（项目内） | `internal/skill/` + `/skill` 斜杠命令 + Profile |

**结论**：E小智 的"子任务"原是**轻量内存追踪**，现已升级为**真实隔离的子会话**（独立 `CopilotSession` + `AgentLoop` 多实例）；双模型编排（Planner/Executor + handoff）经 `SubagentRunner.ResolveProvider` 也已落地，与 Reasonix 的 subagent 能力对齐。

---

## 十、安全机制

| 维度 | **E小智** | **DeepSeek-Reasonix** |
|---|---|---|
| 权限策略 | `ToolPolicy`（auto/ask/yolo）+ 风险分级（readonly/write_single/write_batch/destructive） | `permission` 引擎（Allow/Ask/Deny）+ plan gate |
| OS 沙箱 | 无（操作受限在 E3D 数据库内，天然隔离） | **有**：macOS Seatbelt / Linux bubblewrap / Windows AppContainer + 低完整性令牌 |
| Prompt Injection | PmlValidator 拦截 PURGE/DELETE DB 等危险 PML | `guardian` 子代理审查 + `secrets` 密钥脱敏 |
| 命令分解检测 | PML 正则守卫 | `bash_decompose`/`bash_readonly`/`bash_redirect` 检测危险 shell 模式 |
| 回滚 | Checkpoint 设计（写前备份旧值） | checkpoint（`internal/checkpoint/`，rewind/undo） |

**结论**：E小智 安全靠"PML 危险指令白/黑名单 + E3D 数据库天然隔离 + 审批流"；Reasonix 安全靠"OS 级沙箱 + Guardian 审查 + 密钥脱敏" —— 两者威胁模型不同（前者防误改工厂数据，后者防恶意 shell 执行）。

---

## 十一、持久化 / 会话

| | **E小智** | **DeepSeek-Reasonix** |
|---|---|---|
| 格式 | JSONL 会话 + index.json + `.trash` 回收站（30 天） | `.jsonl` + `session.events.jsonl` + `session.meta` + `session.conflicts.jsonl` |
| 配置存储 | JSON（`config.json` 双层：全局+用户） | TOML + `.env`/`keyring` |
| 分支/恢复 | Checkpoint 设计 | `branches.go` 会话分支 + 冲突恢复 |

---

## 十二、构建与发布

| | **E小智** | **DeepSeek-Reasonix** |
|---|---|---|
| 构建 | `dotnet build` + `npm run build` → 拷贝 5 DLL + wwwroot 到 E3D 目录（`deploy.cmd`） | `go build`（CGO_ENABLED=0 单静态二进制）+ `wails build` + GoReleaser |
| 平台 | 仅 Windows x86（E3D 宿主） | Win/mac/Linux × amd64/arm64 |
| 分发 | 手动 deploy 到 E3D 安装目录 | npm 包 / Homebrew / GoReleaser / 自动更新 |
| 版本号 | `SharedAssemblyInfo.cs` 第四位小步递增 | 语义化（1.17.11），`-ldflags` 注入 |
| 测试 | NUnit（`E3DCopilot.Tests`）+ Vitest + TestHost 离线模拟 | Go test + GoReleaser CI |

---

## 十三、总评与对齐建议

### 本质定位
- **E小智** = E3D 垂直领域嵌入式 Copilot（受 .NET 4.8 / E3D 进程 / UI 线程强约束，工具全为工厂设计专属）。
- **Reasonix** = 通用独立 Coding Agent（Go 单二进制，工具覆盖文件系统/Shell/Git/网络，靠 MCP 无限扩展）。

### E小智 已对齐 Reasonix 的
cline 式分层、Agent 循环骨架、工具注册表、plan mode、审批流（含风险分级键盘审批条）、reasoning 流式、事件 Sink 推流、四层记忆设计理念、Checkpoint 回滚理念。

### E小智 相对 Reasonix 的明显短板（按优先级）
> 注：下列 1–5 项已在 `docs/plan/后期功能优化改进计划.md` 中全部补齐（A1/B1/B2/C1/C2/D1 状态均 ✅ 已完成），此处保留原始短板记录供追溯。
1. ~~⚠️ Context Compaction 未实现~~ → ✅ 已落地（`MaybeCompactAsync` 可配窗口比/触发消息数）。
2. ~~⚠️ MCP 支持缺失~~ → ✅ 已落地（只读 MCP 客户端 stdio/HTTP）。
3. ~~⚠️ Provider 为单实现写死~~ → ✅ 已落地（`ProviderRegistry` 多供应商注册表 + Effort UI + 环境变量密钥）。
4. ~~⚠️ Memory 大量处于设计文档阶段~~ → ✅ 已落地（用户画像自动更新 + 项目知识注入 + SQLite 自动记忆）。
5. ~~子代理仅为内存追踪~~ → ✅ 已升级为真实隔离子会话 + 双模型 Coordinator（handoff）。
6. 缺 OS 级沙箱（但领域隔离使其威胁模型不同，非硬伤）。

### E小智 相对 Reasonix 的独特优势（Reasonix 不具备）
- ✅ **真实工业软件集成**：AVEVA E3D 数据库直读/直写、实时 CE 跟踪、PML 黄金范式、ISO 出图（CNPE.IC.ISO 内核）。
- ✅ **线程安全桥接**：UI 线程强制 `ThreadMarshaller` 切回 E3D 主线程。
- ✅ **内网/离线优先**：vLLM 本地部署，符合工厂无外网的安全要求。
- ✅ **领域安全防护**：PmlValidator 拦截 `PURGE`/`DELETE DB` 等致命操作。

### 一句话总结
两者是"同一套 cline 架构基因，长成了两个完全不同的物种" —— E小智 在工厂设计垂直场景做到了 Reasonix 做不到的事；原工程成熟度短板（compaction、MCP、多 Provider、真实子代理）均已补齐，现差距收敛为 OS 级沙箱与 Memory V5 式执行编译器两类（前者因领域隔离非硬伤）。其官方文档定位也正确："目标不是复制 Reasonix，而是在 E3D 场景提供更好体验。"

---

## 附：Reasonix 关键源码速查（供后续对齐参考）

| 功能点 | 文件 |
|--------|------|
| CLI 入口 | `cmd/reasonix/main.go` |
| 桌面入口 | `desktop/main.go` |
| Agent 主循环 | `internal/agent/agent.go:1163` |
| Provider 接口 | `internal/provider/provider.go:611` |
| OpenAI 实现 | `internal/provider/openai/openai.go` |
| Anthropic 实现 | `internal/provider/anthropic/anthropic.go` |
| 工具接口 | `internal/tool/tool.go:19` |
| 工具注册 | `internal/tool/builtin/*.go` (init) |
| 配置加载 | `internal/config/config.go` |
| 权限策略 | `internal/permission/permission.go` |
| 沙箱 | `internal/sandbox/sandbox.go` |
| MCP 客户端 | `internal/plugin/plugin.go` |
| 事件类型 | `internal/event/event.go` |
| Controller | `internal/control/controller.go` |
| MCP 管理器 | `internal/control/mcp.go` |
| 会话持久化 | `internal/store/session.go` |
| 记忆系统 | `internal/memory/memory.go` |
| 上下文压缩 | `internal/agent/compact.go` |
| 子代理存储 | `internal/agent/subagent_store.go` |
| 协调器 | `internal/agent/coordinator.go` |
| 规划模式 | `internal/planmode/policy.go` |
| Guardian 安全 | `internal/guardian/guardian.go` |
| 证据账本 | `internal/evidence/evidence.go` |
| Thinking 处理 | `internal/provider/openai/think.go` |

---

## 十四、后期优化完善方向（路线图）

> 本章基于第三节~第十二节的对比结论，给出 E小智 的后期迭代方向。
> **核心原则**：在 E3D 垂直场景做深、补工程成熟度短板，**不盲目复制通用能力**。所有方案必须满足 E小智 的三大约束：
> 1. **.NET Framework 4.8 / C# 7.3** —— 无 `IAsyncEnumerable`/`await foreach`/records，流式用 `Task`+`Action<Chunk>` 回调；
> 2. **E3D UI 线程** —— 所有 E3D API 调用必须经 `ThreadMarshaller.InvokeOnUIThread`；子进程/网络/LLM 走后台线程；
> 3. **内网/离线优先** —— Provider 默认本地 vLLM，不强制外网。

### 14.1 保留的优势（不要动）

| 优势 | 说明 |
|------|------|
| 真实工业集成 | AVEVA E3D 直读/直写、实时 CE 跟踪、PML 黄金范式、ISO 出图内核 |
| 线程安全桥接 | `ThreadMarshaller` 强制切回 E3D 主线程 |
| 领域安全防护 | `PmlValidator` 拦截 `PURGE`/`DELETE DB` |
| 审批流 + 风险分级 | `PromptShelfControl` 键盘审批条 |

### 14.2 分阶段路线图

#### Phase A（P0，1~2 周）— 解除体验天花板

**A1. Context Compaction 落地** ✅ 已完成
- **现状**：`AgentLoop.MaybeCompactAsync` 已实现（可配 `CompactRatio`/`CompactTriggerMessages`，keepTail 动态计算），长对话不再撞上限。
- **目标**：token 预算估算 → 超阈值时调用 `VllmProvider` 摘要旧消息，保留最近 N 条 + 旧工具结果 snip。
- **E3D 约束适配**：net48 无 `IAsyncEnumerable`，复用现有 `StreamAsync(ct, Action<Chunk>)` 回调式做摘要；摘要本身在后台线程跑，避免阻塞 UI；compact 期间不触发新工具。
- **借鉴 Reasonix**：`internal/agent/compact.go`（0.8 窗口比 `compact_ratio`、保留 `defaultTailTokens`、`<compaction-summary>` 标签、先 snip 旧工具输出再摘要）。
- **工作量**：M

**A2. Effort / 推理强度 前端 + 配置** ✅ 已完成
- **现状**：已落地——`CopilotConfig` 增 `Effort`/`ReasoningProtocol`，`VllmProvider` 按协议分支写入 `thinking`/`reasoning_effort`；前端 `ModelsSection.tsx` 已加推理配置区块。
- **目标**：`CopilotConfig` 增加 `Effort`/`ReasoningProtocol`/`Vision`/`ContextWindow` 字段；`ModelsSection.tsx` 增加 `EffortSwitcher` 与协议选择。
- **E3D 约束适配**：保持 JSON 配置（不引入 TOML）；按 `ReasoningProtocol`(deepseek/openai/none) 分支解析 reasoning 流。
- **借鉴 Reasonix**：`reasonix.toml` 的 `effort`/`thinking` 配置 + `EffortSwitcher.tsx` 组件。
- **工作量**：S

#### Phase B（P1，2~4 周）— 可配置性与生态扩展

**B1. 多 Provider 注册表** ✅ 已完成
- **现状**：已落地——`ProviderRegistry` 单例注册表（`Register(kind,factory)`+`New`），内置 vllm/qwen/deepseek/openai-compatible/minimax→`VllmProvider`、`anthropic`→`AnthropicProvider`，密钥走环境变量，并支持 `SwitchProvider` 运行时切换。
- **目标**：`IProviderRegistry` + `Register(kind, factory)`，支持 `vllm`/`qwen`/`deepseek`/`openai-compatible`/`anthropic`；密钥走环境变量或加密存储（非硬编码）。
- **E3D 约束适配**：接口签名保持 `StreamAsync(request, Action<Chunk>, ct)`；anthropic 的 reasoning signature 回放可后续补。
- **借鉴 Reasonix**：`internal/provider/provider.go` 的 `Register(kind, Factory)` + `New(kind, cfg)` 工厂。
- **工作量**：M

**B2. 只读 MCP 客户端（知识 / 外部系统桥接）** ✅ 已完成
- **现状**：已落地——`McpClient`（stdio/HTTP，协议 `2024-11-05`）仅暴露只读 resources/prompts，包装为 `mcp_knowledge` 工具并强制 `IsReadOnly=true`。
- **目标**：加 `IMcpClient`（stdio / Streamable HTTP），**仅暴露只读 resources/prompts 作为知识检索扩展**；包装成 `IToolHandler`（强制 `IsReadOnly=true`），不暴露写工具，保持领域安全。
- **E3D 约束适配**：E3D 进程内启动 stdio 子进程须走后台线程 + `TaskCompletionSource` 超时；注意 E3D 锁定 DLL，子进程不可触碰宿主目录；所有结果回传经 UI 线程安全通道。
- **借鉴 Reasonix**：`internal/plugin/plugin.go`（协议 `2024-11-05`、`transport` 接口、按前缀命名空间）；只读子集先行。
- **工作量**：L（先做只读子集）

#### Phase C（P2，1~2 月）— 记忆与智能

**C1. Memory 系统落地** ✅ 已完成
- **现状**：已落地——SQLite FTS5 自动记忆（`remember`/`forget`/`search`）+ 用户画像每次工具调用自动更新（`UpdateProfileFromToolUse`）+ 项目知识库注入 SystemPrompt（`GetSystemPromptContext`）。
- **目标**：① 会话 JSONL + `index.json` + `.trash`；② 用户画像每次工具调用自动更新；③ SQLite FTS5 自动记忆 `remember/forget/search`；④ 项目知识库注入。
- **E3D 约束适配**：net48 用 `System.Data.SQLite` 或轻量 JSON 索引；检索结果注入 `SystemPrompt`，注意注入体积。
- **借鉴 Reasonix**：`internal/memory/`（层级文档记忆 + `Store`/`Index`）+ `memorycompiler/`（简化为"任务 vs 闲聊"分类器，无需完整 V5 编译器）。
- **工作量**：L

**C2. 真实子代理（隔离子会话 + 双模型）** ✅ 已完成
- **现状**：已落地——`dispatch_subagent` 真实隔离子会话（独立 `CopilotSession` + `AgentLoop` 多实例），Planner/Executor 双模型 + handoff 经 `SubagentRunner.ResolveProvider`。
- **目标**：`SubagentSession` 真实隔离（独立 message list + 复用同一 `AgentLoop`），最大嵌套深度 2；可选 Planner（只读产出计划）+ Executor 双模型。
- **E3D 约束适配**：子代理 E3D 调用仍须 `ThreadMarshaller`；复用现有 `AgentLoop` + `ToolExecutor`，无需新架构。
- **借鉴 Reasonix**：`internal/agent/subagent_store.go` + `internal/agent/coordinator.go`。
- **工作量**：L

#### Phase D（P3，可选）— 安全增强

**D1. 密钥脱敏 / 细粒度注入防护** ✅ 已完成
- **现状**：`PmlValidator` + 审批流已覆盖主要风险。
- **目标**：LLM 输出中自动遮蔽连接串/密钥；可选规则式注入标记检测。
- **E3D 约束适配**：领域隔离已足够，优先级最低；`guardian` 子代理开销大，建议简化为规则而非独立审查代理。
- **借鉴 Reasonix**：`internal/secrets/secrets.go`（脱敏）+ `internal/guardian/`（简化为规则）。
- **工作量**：S~M

### 14.3 不建议跟进项（避免无效投入）

| Reasonix 能力 | 不建议照搬的原因 |
|---|---|
| OS 级沙箱（Seatbelt / bubblewrap / AppContainer） | E小智 操作被限制在 E3D 数据库内，天然隔离；且 E3D 是 32 位宿主，沙箱集成收益低、风险高 |
| 跨平台 / 多架构分发 | 仅 Windows x86 E3D 宿主，无 macOS/Linux/arm64 需求 |
| 独立 CLI TUI（Bubble Tea） | 无独立 CLI 形态，宿主是 E3D，TUI 无意义 |
| Memory V5 完整执行编译器 | 过于复杂；E小智 场景用简化分类器即可，不必照搬 IR 编译链 |

### 14.4 优先级汇总

| 优先级 | 项 | 价值 | 工作量 | 状态 |
|:---:|---|---|:---:|:---:|
| P0 | A1 Context Compaction | 解除长对话天花板（刚需）| M | ✅ 已完成 |
| P0 | A2 Effort 前端+配置 | 体验对齐（快赢）| S | ✅ 已完成 |
| P1 | B1 多 Provider 注册表 | 模型灵活性 | M | ✅ 已完成 |
| P1 | B2 只读 MCP | 知识生态扩展 | L | ✅ 已完成 |
| P2 | C1 Memory 落地 | 越用越懂用户 | L | ✅ 已完成 |
| P2 | C2 真实子代理 | 复杂任务分解 | L | ✅ 已完成 |
| P3 | D1 密钥脱敏 | 安全增强（可选）| S~M | ✅ 已完成 |

**一句话**：A1/A2/B1/B2/C1/C2/D1 已全部补齐（详见 `docs/plan/后期功能优化改进计划.md`），原工程成熟度短板已消除；始终守住"E3D 垂直场景做深"的差异化定位。

---

## 十五、多 Agent 系统专项分析（新增需求）

> 用户提出希望引入多 Agent 系统。本节基于联网核实 + 现有代码调研，给出结论与落地方案。
> 核实日期：2026-07-13。

### 15.1 联网核实：现成多 Agent 框架能否直接引入？

| 框架 | 最新版本 | .NET 目标框架 | 对 E小智 的可用性 |
|------|:---:|---|---|
| **Microsoft Agent Framework (MAF)** | 1.13.0 | `net8.0` / `net9.0` / `net10.0` / `netstandard2.0` / `net472`；`net48` 标记为 *computed（不兼容）* | ❌ **不能直接引用**。依赖 `Microsoft.Extensions.AI 10.x`、`System.Text.Json 10.x` 等仅 net8+ 包，E小智 net48 项目会编译失败 |
| **AutoGen.NET** | 0.4.0-dev.3 | **仅 `net8.0`**（且是 maintenance 模式、dev 预览版） | ❌ 完全不可用 |
| **Semantic Kernel** | 1.x | `net462+`（含 net48）兼容 | ⚠️ 中间件可用，但 GroupChat / AgentChat 等**多 Agent 编排高级特性仅在 net8 版完整**；net48 上只能做单 Agent + 插件 |

**核实来源**：
- MAF NuGet 页（`Microsoft.Agents.AI` 1.13.0）"Compatible target framework" 明确 `net48` = computed（不兼容），且依赖 `Microsoft.Extensions.AI (>=10.6.0)` 等 net8+ 包。
- MAF 仓库 `dotnet/src/Microsoft.Agents.AI/Microsoft.Agents.AI.csproj`：`InjectIsExternalInitOnLegacy` / `InjectDiagnosticClassesOnLegacy` 等 shim 仅让**旧 TFM 有限编译核心**，Evaluation 等高级能力 `Condition="net8.0+"` 才引入。
- AutoGen.NET NuGet 页：`Microsoft.AutoGen.Core` 0.4.0-dev.3 仅 `net8.0`，且仓库 README 标注 "AutoGen is now in **maintenance mode**… new users should start with Microsoft Agent Framework"。
- Semantic Kernel Learn 文档：定位为 "lightweight middleware"，多 Agent 编排能力弱于 MAF。

### 15.2 关键结论

1. **不能"引入"现成多 Agent 框架** —— E小智 被硬约束在 **.NET Framework 4.8 / C# 7.3**（E3D 宿主锁定 32 位加载器），所有主流框架要求 .NET 8+，直接 `dotnet add package` 会编译失败，**升级 .NET 不可行**（会破坏 E3D 加载）。
2. **应"借鉴架构模式 + 自建"** —— 多 Agent 的本质是 **「编排器 + 多个角色 Agent + 消息传递 + 工具共享」** 的模式，不依赖特定框架；E小智 现有代码已具备大部分基底。
3. **复用现有骨架，零升级风险** —— `AgentLoop`（已含 Storm Breaker / Grace Round / EvidenceLedger / 真实 `MaybeCompactAsync`）+ `ToolExecutor`（`IToolHandler` 注册表，已实现 30+ handler）+ `ThreadMarshaller`（UI 线程安全）已可直接复用。

### 15.3 E小智 现有代码已具备的多 Agent 基底

| 能力 | 现有实现 | 多 Agent 复用点 |
|------|---------|----------------|
| 角色 Agent 雏形 | `AgentLoop` 类（无状态循环 + 依赖注入 provider/executor/sink） | 每个"子 Agent" = 一个 `AgentLoop` 实例 + 独立 `CopilotSession` |
| 工具共享 | `ToolExecutor.RegisterAll` + `IToolHandler` 注册表 | 主/子 Agent 共用同一 `ToolExecutor`（子 Agent 按 `IsReadOnly` 收紧工具集）|
| 并行/串行调度 | `ExecuteBatchAsync`（只读并行、写串行）| 编排器可并行派发多个只读子 Agent |
| 上下文压缩 | `MaybeCompactAsync` + `BuildCompactSummaryAsync`（**已实现，非空壳**）| 子 Agent 长任务自动压缩，对比文档原文"空壳"已过时 |
| 证据/就绪检查 | `EvidenceLedger.CheckReadiness()` | 子 Agent 完成后回传证据给主 Agent |
| UI 线程安全 | `ThreadMarshaller.InvokeOnUIThread` | 所有子 Agent 的 E3D 调用仍须走 UI 线程 |
| 审批/权限 | `ToolPolicy` + `AskUserHandler` | 子 Agent 写操作复用主审批流 |

> 注：对比文档第九节原称 E小智 子代理"仅为内存追踪"——该描述当时准确，现 Phase C2 + 本节目标已达成，`dispatch_subagent` 已是真实隔离子会话 + 双模型编排。

### 15.4 推荐落地方案：基于 MAF 编排模式的「轻量多 Agent 层」

**不建议**：引入 MAF/AutoGen NuGet 包。
**建议**：在 `E3DCopilot.Core` 新建 `Agents/` 子模块，借鉴 MAF 的 orchestration patterns（sequential / concurrent / handoff / group），用纯 C# 7.3 实现。

#### 15.4.1 新增接口与类型（net48 兼容）

```csharp
// 角色 Agent 定义（轻量，复用现有 AgentLoop）
public interface IRoleAgent
{
    string Name { get; }
    string SystemPrompt { get; }
    bool IsReadOnly { get; }                 // 子 Agent 工具集收紧
    Task<AgentResult> RunAsync(CopilotSession ctx, string task, CancellationToken ct);
}

// 编排器（借鉴 MAF Orchestration Patterns）
public interface IAgentOrchestrator
{
    Task<AgentResult> RunAsync(string goal, CancellationToken ct);
}

// 子 Agent 结果（含证据，回传主 Agent）
public class AgentResult
{
    public string Output;
    public List<string> Evidence;            // 对齐 EvidenceLedger
    public bool Success;
}
```

#### 15.4.2 三种适合 E3D 场景的编排模式

| 模式 | 适用场景 | 实现要点（net48）|
|------|---------|------------------|
| **Planner + Executor（双模型）** | 复杂多步改造任务 | Planner 用只读工具产出计划（`todo_write`），Executor 执行；复用现有 `ToolPolicy` 的 plan mode 思路 |
| **Concurrent 只读派发** | "同时检查管道/设备/结构三类对象" | 主 Agent 并行 `Task.WhenAll` 派发多个 `IsReadOnly=true` 子 Agent（复用 `ExecuteBatchAsync` 并行分区）|
| **Handoff（交接）** | 专长切换（如 ISO 出图 → 属性修改）| 子 Agent 完成本职后 `Handoff` 给下一个专长 Agent，消息链传递 context |

#### 15.4.3 与现有架构的接缝

- **入口**：`AgentLoop` 新增 `task` 工具的真实实现（Phase C2），`task` 调用 `IAgentOrchestrator` 而非仅内存标记。
- **工具集**：子 Agent 复用 `ToolExecutor`，但按 `IRoleAgent.IsReadOnly` 过滤 handler（写工具子 Agent 需 `ToolPolicy` 审批，与主 Agent 同审批流）。
- **线程**：子 Agent 内所有 E3D API 调用经 `ThreadMarshaller.InvokeOnUIThread`（与现状一致）。
- **流式/事件**：子 Agent 的 `IEventSink` 复用主 Agent 的 sink，前端按 `agent` 字段区分来源（需 `CopilotEvent` 增加 `AgentName` 属性）。
- **嵌套深度**：参考 Reasonix `DefaultMaxSubagentDepth = 2`，常量 `MaxSubagentDepth = 2`，防止无限递归。

#### 15.4.4 借鉴对象（仅模式，不引包）

| 借鉴点 | Reasonix 源码 | MAF 概念 |
|-------|--------------|---------|
| 真实隔离子会话 | `internal/agent/subagent_store.go` | `Agent` + 独立 message list |
| 双模型协调 | `internal/agent/coordinator.go` | Planner/Executor pattern |
| 编排图模式 | — | sequential / concurrent / handoff / group |
| Checkpoint/回滚 | `internal/checkpoint/` | `WorkflowState` checkpointing |

### 15.5 工作量与风险

| 项 | 工作量 | 风险 |
|---|:---:|---|
| 接口 + Orchestrator 骨架 | M | 低（纯 C# 7.3，无外部依赖）|
| Planner+Executor 双模型 | M | 中（需第二模型配置，复用 B1 多 Provider）|
| Concurrent 只读派发 | S | 低（复用 `ExecuteBatchAsync` 并行分区）|
| 前端 Agent 区分渲染 | S | 低（`CopilotEvent` 加 `AgentName`）|
| 嵌套深度/循环防护 | S | 低（复用 Storm Breaker）|

**总风险低**：核心收益是复杂任务分解（如"整 Zone 管线重排"可拆为 查询/规划/执行/校验 4 个子 Agent），且完全在现有 `AgentLoop` 能力范围内，不触碰 .NET 版本红线。

### 15.6 不建议跟进项（多 Agent 专项）

| 方案 | 不建议原因 |
|------|-----------|
| 引入 MAF / AutoGen NuGet 包 | net48 不兼容，编译失败；升级 .NET 破坏 E3D 宿主 |
| A2A（Agent-to-Agent）跨进程协议 | E小智 是进程内单宿主，无跨进程/跨机需求，徒增复杂度 |
| 完整 MAF Workflow 图引擎 | 过度工程；E3D 任务以线性+少量并行为主，手写编排足够 |

---

## 十六、实施计划：从 todo_write 到真·子代理（最小可行 MVP）

> 目标：把现有 `todo_write`（便利贴式任务清单）升级为**真正的子代理**——能独立干活、有独立上下文、可并行。
> 原则：**复用现有 `AgentLoop` + `CopilotSession` + `ToolExecutor`，不引外部依赖，不碰 .NET 版本红线**。
> 代码事实核实（2026-07-13）：`AgentLoop` 为普通类可多实例（`AgentLoop.cs:59`）；`CopilotSession` 为普通类可独立 new（`CopilotSession.cs:10`）；`ToolExecutor` 为共享注册表（`ToolExecutor.cs:18`）；事件源在 `Events/CopilotEvent.cs`。

### 16.1 MVP 范围（只做最有价值的 1 件事）

**只实现"只读子代理并行派发"**，不做双模型/图编排（那是后续增强）。

典型收益场景：
- "同时查管道、设备、结构三类对象" → 主 Agent 一次派 3 个只读子代理，**并行**查，比现在串行快 3 倍。
- "校验整 Zone 的属性完整性" → 一个只读子代理专职遍历核对，主 Agent 同时干别的。

**不做的**（留后续）：写类子代理、Planner+Executor 双模型、跨 Agent 交接 handoff。

### 16.2 现状 → 目标 的落差

| 项 | 现状 | MVP 目标 |
|---|---|---|
| 子代理执行器 | 无（`task` 仅前端类型，C# 无对应 Handler）| 新增 `SubagentDispatchHandler`（`IToolHandler`）|
| 上下文 | 单 session 串行 | 每个子代理用独立 `CopilotSession` |
| 并行 | `todo_write` 记录后主 Agent 自己串行干 | 主 Agent `Task.WhenAll` 派多个只读子代理 |
| 前端区分 | 不区分来源 | 事件带 `AgentName`，前端按员工分组渲染 |

### 16.3 改动清单（文件级）

#### ① 新增 `src/E3DCopilot.Core/Agents/SubagentContext.cs`（net48 兼容）
```csharp
public class SubagentContext
{
    public string Name;                 // 子代理名，如 "inspector-pipe"
    public string SystemPrompt;         // 专长 system prompt
    public bool IsReadOnly = true;      // MVP 仅支持只读
    public CopilotSession Session = new CopilotSession();  // 独立上下文
}
```

#### ② 新增 `src/E3DCopilot.Core/Agents/SubagentRunner.cs`（核心）
复用现有 `AgentLoop` 作为"员工"，共享 `ToolExecutor`（按 `IsReadOnly` 收紧工具）：
```csharp
public class SubagentRunner
{
    private readonly ICopilotProvider _provider;
    private readonly IEventSink _sink;
    private readonly ToolExecutor _executor;
    // ... 构造注入

    public async Task<AgentResult> RunAsync(SubagentContext ctx, string task, CancellationToken ct)
    {
        // 收紧只读：过滤掉 IsReadOnly=false 的 handler
        var readOnlyExecutor = _executor.FilterReadOnly();   // 新增 ToolExecutor.FilterReadOnly()
        var loop = new AgentLoop(_provider, new TaggedSink(_sink, ctx.Name),
                                 readOnlyExecutor, _permission, _config, _controller);
        await loop.RunAsync(ctx.Session, task, images: null, ct);
        return new AgentResult { Output = ctx.Session.LastAssistantText(), Success = true };
    }
}
```
> 注：`TaggedSink` 包装现有 `_sink`，在每条 `CopilotEvent` 上写入 `AgentName = ctx.Name`，使前端能区分来源（见 ④）。

#### ③ 新增 `src/E3DCopilot.Core/Tools/Handlers/SubagentDispatchHandler.cs`（工具入口）
把"派活"暴露给主 Agent 作为工具（对齐 Reasonix `task` / `SubagentSpec`）：
```csharp
public class SubagentDispatchHandler : IToolHandler
{
    public string Name => "dispatch_subagent";
    public bool IsReadOnly => true;     // 派发本身是只读动作
    // 参数：name / system_prompt / task / mode(=readonly, MVP 仅此值)
    public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct)
    {
        var spec = Parse(args);
        var ctx = new SubagentContext { Name = spec.Name, SystemPrompt = spec.SystemPrompt };
        var result = await _runner.RunAsync(ctx, spec.Task, ct);
        return ToolResult.Ok(result.Output);   // 回传结果给主 Agent
    }
}
```

#### ④ 改 `src/E3DCopilot.Core/Events/CopilotEvent.cs`
`CopilotEvent` 增加可选字段 `AgentName`（默认 null = 主 Agent），`EventKind` 不变。新增工厂方法 `CopilotEvent.WithAgent(string name, ...)` 重载。

#### ⑤ 改 `src/E3DCopilot.Core/Tools/ToolExecutor.cs`
新增 `ToolExecutor FilterReadOnly()` —— 返回仅含 `IsReadOnly==true` handler 的浅副本（子代理工具集收紧，写操作天然不可见，无需额外审批逻辑）。

#### ⑥ 改 `CopilotController.CreateDefault`（`CopilotController.cs:224`）
构造 `SubagentRunner` 并注入 `SubagentDispatchHandler`，注册进 `ToolExecutor`（沿用 `ToolExecutor.CreateDefault` 注册机制）。

#### ⑦ 前端（可选，MVP 可先不做完整 UI）
- 事件含 `AgentName` 时，在消息流中以"子代理卡片"分组（参考现有 `SubagentStatusItem` 类型，`shared/ExtensionMessage.ts:334`）。
- 最低成本：仅在 `tool:dispatch` / `tool:result` 事件显示 `[子代理: name]` 前缀。

### 16.4 主 Agent 如何使用（SystemPrompt 增补）

在 `SystemPrompt.cs` 增加一句，让主 Agent 知道能派活：
```
复杂或并行的只读查询任务，用 dispatch_subagent 派发多个只读子代理并行处理，
每个子代理独立完成自己的查询后回传结果，你再汇总。
```

### 16.5 风险与防护（复用现有机制）

| 风险 | 防护（已有） |
|---|---|
| 子代理无限递归派子代理 | 加 `MaxSubagentDepth = 2` 常量；子代理 `dispatch_subagent` 调用被 `FilterReadOnly` 之外的白名单禁用（只读子代理本就看不到写工具，但派发工具须在子代理侧禁用）|
| 子代理死循环 | 复用 `AgentLoop` 的 `MaxSteps` + Storm Breaker |
| 子代理 E3D 调用跨线程 | 子代理内 `ExecuteAsync` 已走 `ThreadMarshaller.InvokeOnUIThread`（与现状一致）|
| 长任务上下文溢出 | 子代理独立 `CopilotSession` 走已有的 `MaybeCompactAsync` 压缩 |

> 关键约束：MVP 子代理**仅只读**，写操作（modify/execute_pml/cad_import 等）仍由主 Agent 在主审批流下执行，安全性不变。

### 16.6 工作量与里程碑

| 步骤 | 文件 | 工作量 |
|---|---|:---:|
| 1. `SubagentContext` + `AgentResult` 类型 | `Agents/` 新建 | S |
| 2. `ToolExecutor.FilterReadOnly()` | `ToolExecutor.cs` | S |
| 3. `SubagentRunner`（复用 `AgentLoop`）| `Agents/` 新建 | M |
| 4. `SubagentDispatchHandler` 工具 | `Handlers/` 新建 | S |
| 5. `CopilotEvent.AgentName` | `Events/CopilotEvent.cs` | S |
| 6. 注入 + SystemPrompt 增补 | `CopilotController.cs` / `SystemPrompt.cs` | S |
| 7. 前端分组渲染（最低成本前缀）| `e3d-ui` | S |
| 8. 单元测试（并行派发 / 只读收紧 / 深度限制）| `E3DCopilot.Tests/` | M |

**总工作量约 1 周（单人）**，全部为纯 C# 7.3 + React，无外部依赖、无 .NET 版本变更。

### 16.7 与第十五节的关系

- 第十五节是**架构层决策**（为什么不引框架、该借鉴什么模式）。
- 本节是**落地层 MVP**（最小改动把 todo_write 升级为真·子代理）。
- 后续增强（双模型 Planner/Executor、handoff 交接）在 MVP 跑通后，按第十五节 15.4.2 的模式扩展，无需重构地基。
