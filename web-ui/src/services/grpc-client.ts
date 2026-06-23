/**
 * E小智 gRPC Client — Bridge 适配层（改进版）
 *
 * 改进点：
 * 1. 使用 MessageTypes 常量替代硬编码字符串
 * 2. 使用类型安全的消息处理
 * 3. 更好的错误处理
 */

import bridge, { MessageTypes } from "../bridge"

type Cb = { onResponse?: (...a: any[]) => void; onError?: (e: any) => void; onComplete?: () => void }

// ── Provider / Model 类型定义（参考 Reasonix ModelInfo/ProviderView） ──
export interface ModelInfo {
  ref: string          // "provider/model"
  provider: string
  model: string
  current: boolean
}

export interface ProviderView {
  name: string
  kind: "openai" | "anthropic" | string
  baseUrl: string
  apiKey: string
  keySet: boolean
  models: string[]
  default: string
  enabled: boolean
  builtIn: boolean
}

// 当前激活的 provider/model
let _currentProviderName = "openai"
let _currentModelName = "qwen3.7-plus"

let _providers: ProviderView[] = []
const _providerListeners: Set<() => void> = new Set()

export function getProviders(): ProviderView[] {
  return _providers
}

export function getCurrentModel(): { provider: string; model: string; ref: string } {
  return { provider: _currentProviderName, model: _currentModelName, ref: `${_currentProviderName}/${_currentModelName}` }
}

export function subscribeProviders(cb: () => void): () => void {
  _providerListeners.add(cb)
  return () => { _providerListeners.delete(cb) }
}

function notifyProvidersChanged() {
  _providerListeners.forEach((cb) => cb())
}

// ── 初始状态 ──
const INITIAL_STATE = {
  version: 1,
  welcomeViewCompleted: true,
  apiConfiguration: {
    provider: "openai",
    planModeApiProvider: "openai",
    actModeApiProvider: "openai",
    apiModelId: "qwen3.7-plus",
    openAiBaseUrl: "",
    openAiApiKey: ""
  },
  autoApprovalSettings: { enabled: false, actions: [], notifications: true },
  focusChainSettings: { enabled: false, modifiedMessages: [], checklists: [] },
  mcpServers: [],
  mcpMarketplaceCatalog: { categories: [], items: [] },
  clineMessages: [],
  taskHistory: [],
  platform: "win32",
  provider: "openai",
  uriScheme: "file",
  workspaceFolderUri: "",
  mode: "plan",
  telemetrySetting: { enabled: false },
  soundEnabled: false,
  soundName: "",
  userInfo: null,
  currentFocusChainChecklist: null,
  hooksEnabled: false,
  // === E3D 任务状态机 ===
  // isTaskRunning: true 表示 LLM 正在处理或工具正在执行
  // false 时前端 UI 解锁（sendingDisabled=false, enableButtons=true）
  isTaskRunning: false,
  // Plan Mode 状态（由后端 UserSetPlanMode 事件驱动）
  isPlanMode: false,
}

// ── Bridge 事件 → 状态更新 ──
// 存储当前消息列表，随 bridge 事件更新
let _clineMessages: any[] = []
let _stateListeners: Set<Cb> = new Set()
let _taskListeners: Set<Cb> = new Set()
let _uiListeners: Set<Cb> = new Set()

// 当前待审批的工具调用 id（用于 askResponse yes/no 时转发为 user:approve）
let _currentApprovalId: string | null = null
// 当前 ask_user 的 questionId（用于 askResponse messageResponse 时转发为 user:ask_response）
let _currentAskQuestionId: string | null = null
// 任务是否正在运行（驱动前端 UI 状态机）
let _isTaskRunning = false
// Plan Mode 状态
let _isPlanMode = false

// 监听 C# 后端推送的事件（使用 MessageTypes 常量）
bridge.on((msg: { type: string; payload: any }) => {
  switch (msg.type) {
    // ── 前端就绪确认 ──
    case MessageTypes.HostReady:
      console.log("[gRPC-Client] Host ready:", msg.payload)
      break

    // ── 配置同步 ──
    case MessageTypes.ConfigSync:
      console.log("[gRPC-Client] Config sync:", msg.payload)
      if (msg.payload) {
        // 多 provider 模式（新）
        if (msg.payload.providers && Array.isArray(msg.payload.providers)) {
          _providers = msg.payload.providers
        } else if (msg.payload.baseUrl) {
          // 兼容旧单 provider 模式
          _providers = [{
            name: msg.payload.provider || "openai",
            kind: "openai",
            baseUrl: msg.payload.baseUrl || "",
            apiKey: msg.payload.apiKey || "",
            keySet: !!(msg.payload.apiKey),
            models: msg.payload.model ? [msg.payload.model] : [],
            default: msg.payload.model || "",
            enabled: true,
            builtIn: false,
          }]
        }
        // 更新当前激活
        if (msg.payload.currentProvider) _currentProviderName = msg.payload.currentProvider
        if (msg.payload.currentModel) _currentModelName = msg.payload.currentModel
        else if (msg.payload.model) _currentModelName = msg.payload.model
        // 兼容：填充 apiConfiguration（cline 框架依赖）
        const active = _providers.find((p) => p.name === _currentProviderName) || _providers[0]
        if (active) {
          INITIAL_STATE.apiConfiguration = {
            provider: "openai",
            planModeApiProvider: "openai",
            actModeApiProvider: "openai",
            apiModelId: _currentModelName,
            openAiBaseUrl: active.baseUrl,
            openAiApiKey: active.apiKey,
          }
        }
        notifyProvidersChanged()
        notifyStateChange()
      }
      break

    // ── LLM 流式文本 ──
    case MessageTypes.LlmTurnStarted:
      handleTurnStarted(msg.payload)
      break

    case MessageTypes.LlmStreamDelta:
      handleStreamDelta(msg.payload)
      break

    case MessageTypes.LlmStreamEnd:
      handleStreamEnd(msg.payload)
      break

    case MessageTypes.TurnDone:
      // 整个轮次结束：LLM 完成 + 所有工具执行完毕
      // 重置 UI 状态机，让输入框重新可用
      handleTurnDone()
      break

    case MessageTypes.LlmUsage:
      // Token 用量统计（后端 LLM 调用完成后推送）
      // 加入消息列表作为 tool_result 类型的用量行
      addMessage({
        type: "say",
        say: "tool_result",
        text: JSON.stringify({ usage: msg.payload }),
        ts: Date.now(),
      })
      break

    case MessageTypes.LlmThinking:
      handleThinking(msg.payload)
      break

    // ── 工具事件 ──
    case MessageTypes.ToolDispatch:
      handleToolDispatch(msg.payload)
      break

    case MessageTypes.ToolResult:
      handleToolResult(msg.payload)
      break

    case MessageTypes.ToolApproval:
      handleToolApproval(msg.payload)
      break

    // ── 用户交互 ──
    case MessageTypes.AskUser:
      handleAskUser(msg.payload)
      break

    // ── Plan/Act 模式切换通知（后端 → 前端） ──
    case MessageTypes.UserSetPlanMode:
      if (msg.payload) {
        _isPlanMode = !!msg.payload.enabled
        INITIAL_STATE.isPlanMode = _isPlanMode
        INITIAL_STATE.mode = _isPlanMode ? "plan" : "act"
        notifyStateChange()
      }
      break

    // ── 通用事件 ──
    case MessageTypes.Notice:
      // 修复：Notice 不再加入 _clineMessages（避免污染消息流，破坏 messages.length===0 判断）
      // 只在控制台打印，作为诊断信息
      console.log("[Bridge Notice]", msg.payload?.text || "")
      break

    case MessageTypes.Error:
      // 错误也结束任务运行状态
      handleTurnDone()
      addMessage({ type: "say", say: "error", text: msg.payload?.message || "Unknown error" })
      break

    // ── 响应消息（保持兼容） ──
    case "chatResponse":
      // C# 后端返回的聊天响应
      if (msg.payload?.messages) {
        _clineMessages = msg.payload.messages
        notifyStateChange()
      }
      break

    case "taskStarted":
      notifyTaskEvent("started", msg.payload)
      break

    case "taskCompleted":
      notifyTaskEvent("completed", msg.payload)
      break

    case "taskError":
      notifyTaskEvent("error", msg.payload)
      break

    default:
      // 调试：未知消息类型
      console.log("[gRPC-Client] Unknown event:", msg.type, msg.payload)
  }
})

// ── 流式消息管理 ──
let _currentStreamingMessage: any = null

// 当前 api_req_started 消息的 ts（用于在 LlmStreamEnd 时回填 cost）
let _currentApiReqTs: number | null = null

function handleTurnStarted(payload: any) {
  if (!_isTaskRunning) {
    _isTaskRunning = true
    INITIAL_STATE.isTaskRunning = true
  }
  // 插入 api_req_started 消息（前端依赖此消息做工具分组、活动指示器等）
  _currentApiReqTs = Date.now()
  const apiReqMsg = {
    type: "say",
    say: "api_req_started",
    text: JSON.stringify({ request: payload?.request || "" }),
    ts: _currentApiReqTs,
  }
  _clineMessages.push(apiReqMsg)
  notifyStateChange()
}

function getOrCreateStreamingMessage() {
  if (!_currentStreamingMessage || _currentStreamingMessage.partial === false) {
    _currentStreamingMessage = {
      type: "say",
      say: "text",
      text: "",
      partial: true,
      ts: Date.now(),
    }
    _clineMessages.push(_currentStreamingMessage)
  }
  return _currentStreamingMessage
}

function handleStreamDelta(payload: any) {
  // 第一个 delta 到达 → 标记任务运行中
  if (!_isTaskRunning) {
    _isTaskRunning = true
    INITIAL_STATE.isTaskRunning = true
  }
  const msg = getOrCreateStreamingMessage()
  msg.text += payload?.delta || ""
  notifyStateChange()
}

function handleStreamEnd(payload: any) {
  if (_currentStreamingMessage) {
    _currentStreamingMessage.partial = false
    _currentStreamingMessage = null
  }
  // 回填 api_req_started 的 cost/usage 信息
  if (_currentApiReqTs) {
    const apiReqMsg = _clineMessages.find((m: any) => m.say === "api_req_started" && m.ts === _currentApiReqTs)
    if (apiReqMsg) {
      try {
        const info = JSON.parse(apiReqMsg.text || "{}")
        info.cost = payload?.usage?.cost ?? payload?.usage?.totalCost
        info.tokensIn = payload?.usage?.promptTokens
        info.tokensOut = payload?.usage?.completionTokens
        apiReqMsg.text = JSON.stringify(info)
      } catch {}
    }
    _currentApiReqTs = null
  }
  // 将最后一条 reasoning 消息标记为 partial=false
  for (let i = _clineMessages.length - 1; i >= 0; i--) {
    if (_clineMessages[i].say === "reasoning" && _clineMessages[i].partial === true) {
      _clineMessages[i].partial = false
      break
    }
  }
  notifyStateChange()
}

/**
 * 整个轮次结束：LLM 完成 + 所有工具执行完毕
 * 修复"输入框永久禁用"问题：重置 UI 状态机
 */
function handleTurnDone() {
  _isTaskRunning = false
  INITIAL_STATE.isTaskRunning = false
  // 清理当前审批/提问上下文
  _currentApprovalId = null
  _currentAskQuestionId = null
  // 推送状态变更，让 React 重新渲染并解锁输入框
  notifyStateChange()
  // 兜底：直接通知 UI 监听器（ExtensionStateContext 可监听此事件）
  _uiListeners.forEach(cb => {
    try { cb.onResponse?.({ event: "turn_done" }) } catch (e) { cb.onError?.(e) }
  })
}

function handleThinking(payload: any) {
  if (!_isTaskRunning) {
    _isTaskRunning = true
    INITIAL_STATE.isTaskRunning = true
  }
  const msg = {
    type: "say",
    say: "reasoning",
    text: payload?.text || "",
    partial: true,
    ts: Date.now(),
  }
  _clineMessages.push(msg)
  notifyStateChange()
}

function handleToolDispatch(payload: any) {
  if (!_isTaskRunning) {
    _isTaskRunning = true
    INITIAL_STATE.isTaskRunning = true
  }
  // coreToolName: 原始核心工具名（路由前），供 ChatRow 渲染分组用
  const coreToolName = payload?.coreToolName || payload?.name
  const msg = {
    type: "say",
    say: "tool",
    text: JSON.stringify({ tool: payload?.name, coreTool: coreToolName, args: payload?.args }),
    ts: Date.now(),
    _toolId: payload?.id,  // 保存 toolId 供 handleToolResult 回填
  }
  _clineMessages.push(msg)
  notifyStateChange()
}

function handleToolResult(payload: any) {
  // 修复：不再创建独立的 tool_result 消息（ClineSay 枚举中没有 tool_result）
  // 而是将结果回填到对应的 say:"tool" 消息中
  const toolId = payload?.id
  // meta: 结构化元数据（最小安全方案：由 Handler 的 ToolResult.Data 传过来）
  const meta = payload?.meta
  if (toolId) {
    const toolMsg = _clineMessages.find((m: any) => m.say === "tool" && m._toolId === toolId)
    if (toolMsg) {
      try {
        const toolData = JSON.parse(toolMsg.text || "{}")
        toolData.result = payload?.result || payload?.error
        // 回填 meta 信息（summary 供 ChatRow 标题渲染）
        if (meta) {
          toolData.summary = meta.summary
          // 如果 meta 中有 coreTool，且 tool 消息中还没有（兜底），补上
          if (meta.coreTool && !toolData.coreTool) {
            toolData.coreTool = meta.coreTool
          }
          // PML 脚本特殊处理：meta 中有 pmlScript
          if (meta.pmlScript) {
            toolData.pmlScript = meta.pmlScript
          }
        }
        toolMsg.text = JSON.stringify(toolData)
      } catch {
        toolMsg.text += "\nResult: " + (payload?.result || payload?.error)
      }
    } else {
      // 找不到对应的 tool 消息，创建一个 say:"tool" 消息兜底
      const msg = {
        type: "say",
        say: "tool",
        text: JSON.stringify({ tool: meta?.tool || "e3d_generic", coreTool: meta?.coreTool || meta?.tool || "e3d_generic", result: payload?.result || payload?.error, summary: meta?.summary }),
        ts: Date.now(),
      }
      _clineMessages.push(msg)
    }
  }
  notifyStateChange()
}

/**
 * 工具审批请求
 * 修复：不再自动批准，而是推 ask:"tool" 消息让 Cline 原生审批 UI 渲染
 * 用户点 Approve/Reject 后 useMessageHandlers.executeButtonAction 会调 askResponse
 * askResponse 再把响应转发为 bridge.sendApproval(id, allow)
 */
function handleToolApproval(payload: any) {
  _currentApprovalId = payload?.id || null
  const msg = {
    type: "ask",
    ask: "tool",
    text: payload?.name || "",
    ts: Date.now(),
    // 额外字段：approve/reject 时回传给后端用
    _approvalId: payload?.id,
    _toolName: payload?.name,
    _args: payload?.args,
  }
  _clineMessages.push(msg)
  notifyStateChange()
}

function handleAskUser(payload: any) {
  _currentAskQuestionId = payload?.questionId || null
  const msg = {
    type: "ask",
    ask: "followup",
    text: JSON.stringify({ question: payload?.question || payload?.data }),
    ts: Date.now(),
    _questionId: payload?.questionId,
  }
  _clineMessages.push(msg)
  notifyStateChange()
}

function addMessage(msg: any) {
  _clineMessages.push(msg)
  notifyStateChange()
}

function notifyStateChange() {
  const state = { ...INITIAL_STATE, clineMessages: [..._clineMessages] }
  const stateJson = JSON.stringify(state)
  _stateListeners.forEach(cb => {
    try { cb.onResponse?.({ stateJson }) } catch (e) { cb.onError?.(e) }
  })
}

function notifyTaskEvent(event: string, payload: any) {
  _taskListeners.forEach(cb => {
    try { cb.onResponse?.({ event, ...payload }) } catch (e) { cb.onError?.(e) }
  })
}

// ── Service Client 创建器 ──

function createTaskServiceClient() {
  return {
    subscribeToState: (_req?: any, cb?: Cb) => {
      // 初始状态推送（带完整字段）
      const fullState = {
        ...INITIAL_STATE,
        didHydrateState: true,
        clineMessages: [..._clineMessages],
      }
      setTimeout(() => cb?.onResponse?.({ stateJson: JSON.stringify(fullState) }), 0)
      _stateListeners.add(cb)
      return () => { _stateListeners.delete(cb) }
    },

    newTask: async (req: any) => {
      const text = req?.text || ""
      const images = req?.images || []
      const files = req?.files || []
      console.log("[gRPC-Client] newTask:", text.substring(0, 50))

      // 修复：保留用户消息作为 task，不要清空 _clineMessages 后只放 AI 回复。
      // 否则 messages.at(0) 会错误地变成 AI 回复，导致 TaskHeader 显示错乱。
      _clineMessages = []
      _currentStreamingMessage = null
      _isTaskRunning = true

      // 先把用户任务消息加入列表，作为 Cline 的 task 根消息
      const userMessage: any = {
        type: "ask",
        ask: "task",
        text: text,
        images: images.length > 0 ? images : undefined,
        files: files.length > 0 ? files : undefined,
        ts: Date.now(),
      }
      _clineMessages.push(userMessage)

      // 使用类型安全的方法
      bridge.sendUserMessage(text, images, files)
      notifyStateChange()
      return {}
    },

    clearTask: async () => {
      bridge.newSession()
      _clineMessages = []
      _currentStreamingMessage = null
      notifyStateChange()
      return {}
    },

    stopTask: async () => {
      bridge.cancel()
      return {}
    },

    askResponse: async (req: any) => {
      const text = req?.text || ""
      const responseType = req?.responseType || "messageResponse"
      console.log("[gRPC-Client] askResponse:", responseType, text.substring(0, 50))

      // 修复：根据 responseType 转发到对应的后端通道
      // - yesButtonClicked / noButtonClicked → 工具审批（user:approve）
      // - messageResponse → ask_user 回答（user:ask_response）
      if (responseType === "yesButtonClicked") {
        if (_currentApprovalId) {
          bridge.sendApproval(_currentApprovalId, true)
          _currentApprovalId = null
        }
      } else if (responseType === "noButtonClicked") {
        if (_currentApprovalId) {
          bridge.sendApproval(_currentApprovalId, false)
          _currentApprovalId = null
        }
      } else if (responseType === "messageResponse") {
        // ask_user 的回答，或者任务运行中的反馈
        if (_currentAskQuestionId) {
          bridge.sendAskResponse(_currentAskQuestionId, text)
          _currentAskQuestionId = null
        } else {
          // 没有待回答的 ask_user，作为反馈消息发给后端
          bridge.sendUserMessage(text, [], [])
        }
      }
      return {}
    },

    getTaskHistory: async () => {
      return { history: [] }
    },

    deleteTask: async () => {
      return {}
    },
  }
}

function createStateServiceClient() {
  return {
    subscribeToState: (_req?: any, cb?: Cb) => {
      setTimeout(() => cb?.onResponse?.({ stateJson: JSON.stringify(INITIAL_STATE) }), 0)
      return () => {}
    },

    getWelcomeViewCompleted: async () => {
      return { value: true }
    },

    setWelcomeViewCompleted: async () => {
      return {}
    },

    updateInfoBannerVersion: async () => {
      return {}
    },

    updateModelBannerVersion: async () => {
      return {}
    },

    updateCliBannerVersion: async () => {
      return {}
    },

    dismissBanner: async () => {
      return {}
    },

    installClineCli: async () => {
      return {}
    },

    setTerminalExecutionMode: async () => {
      return {}
    },

    setPlanActMode: async (req: any) => {
      const mode = req?.mode || "act"
      // 修复：使用专门的 UserSetPlanMode 类型，不再借用 UserMessage
      bridge.send(MessageTypes.UserSetPlanMode, { mode })
      return {}
    },

    togglePlanActModeProto: async (req: any) => {
      // 修复：切换到相反模式
      const newMode = _isPlanMode ? "act" : "plan"
      bridge.send(MessageTypes.UserSetPlanMode, { mode: newMode })
      return { value: true }
    },

    getAvailableTerminalProfiles: async () => {
      return { profiles: [] }
    },

    updateSettings: async (_req?: any) => {
      console.log("[gRPC-Client] updateSettings (E3D stub):", _req)
      return {}
    },

    getSettings: async () => {
      return { settings: "{}" }
    },

    updateAutoApprovalSettings: async (_req?: any) => {
      console.log("[StateServiceClient] updateAutoApprovalSettings stub:", _req)
      return {}
    },
  }
}

function createUiServiceClient() {
  return {
    subscribeToShowWebview: (_req?: any, cb?: Cb) => {
      return () => {}
    },

    subscribeToAddToInput: (_req?: any, cb?: Cb) => {
      return () => {}
    },

    openUrl: async (req: any) => {
      if (req?.value) {
        window.open(req.value, "_blank")
      }
      return {}
    },
  }
}

function createFileServiceClient() {
  const methods = {
    copyToClipboard: async (req: any) => {
      if (req?.value) {
        try { await navigator.clipboard.writeText(req.value) } catch {}
      }
      return {}
    },

    selectFiles: async () => {
      return { values1: [], values2: [] }
    },

    searchFiles: async () => {
      return { results: [] }
    },

    searchCommits: async () => {
      return { commits: [] }
    },

    // ── Rules/Skills/Hooks 管理（桩实现） ──
    refreshRules: async (_req?: any) => {
      console.log("[FileServiceClient] refreshRules stub")
      return {
        globalClineRulesToggles: { toggles: {} },
        localClineRulesToggles: { toggles: {} },
        localCursorRulesToggles: { toggles: {} },
        localWindsurfRulesToggles: { toggles: {} },
        localAgentsRulesToggles: { toggles: {} },
        localWorkflowToggles: { toggles: {} },
        globalWorkflowToggles: { toggles: {} },
        globalSkillsToggles: { toggles: {} },
        localSkillsToggles: { toggles: {} },
        remoteRulesToggles: { toggles: {} },
        remoteWorkflowToggles: { toggles: {} },
      }
    },

    toggleClineRule: async (_req?: any) => {
      console.log("[FileServiceClient] toggleClineRule stub")
      return {}
    },

    toggleCursorRule: async (_req?: any) => {
      console.log("[FileServiceClient] toggleCursorRule stub")
      return {}
    },

    toggleWindsurfRule: async (_req?: any) => {
      console.log("[FileServiceClient] toggleWindsurfRule stub")
      return {}
    },

    toggleAgentsRule: async (_req?: any) => {
      console.log("[FileServiceClient] toggleAgentsRule stub")
      return {}
    },

    toggleWorkflow: async (_req?: any) => {
      console.log("[FileServiceClient] toggleWorkflow stub")
      return {}
    },

    toggleSkill: async (_req?: any) => {
      console.log("[FileServiceClient] toggleSkill stub")
      return {}
    },

    toggleHook: async (_req?: any) => {
      console.log("[FileServiceClient] toggleHook stub")
      return {}
    },

    refreshHooks: async (_req?: any) => {
      console.log("[FileServiceClient] refreshHooks stub")
      return { globalHooks: [], workspaceHooks: [] }
    },

    refreshSkills: async (_req?: any) => {
      console.log("[FileServiceClient] refreshSkills stub")
      return { globalSkills: [], localSkills: [] }
    },

    createRuleFile: async (_req?: any) => {
      console.log("[FileServiceClient] createRuleFile stub")
      return {}
    },

    createHook: async (_req?: any) => {
      console.log("[FileServiceClient] createHook stub")
      return {}
    },

    createSkillFile: async (_req?: any) => {
      console.log("[FileServiceClient] createSkillFile stub")
      return {}
    },

    deleteRuleFile: async (_req?: any) => {
      console.log("[FileServiceClient] deleteRuleFile stub")
      return {}
    },

    deleteHook: async (_req?: any) => {
      console.log("[FileServiceClient] deleteHook stub")
      return {}
    },

    deleteSkillFile: async (_req?: any) => {
      console.log("[FileServiceClient] deleteSkillFile stub")
      return {}
    },

    getRelativePaths: async (_req?: any) => {
      return { paths: [] }
    },

    openFile: async (_req?: any) => {
      console.log("[FileServiceClient] openFile stub")
      return {}
    },
  }

  // Proxy 包装：已知方法走真实实现，未知方法 fallback 到 stub
  return new Proxy(methods, {
    get: (target, p: string | symbol) => {
      if (typeof p !== "string") return undefined
      if (p in target) return (target as any)[p]
      console.log("[FileServiceClient] stub:", p)
      return async (..._a: any[]) => ({})
    },
  })
}

function createStubClient(name: string) {
  return new Proxy({}, { get: (_t, p: string | symbol) => {
    if (typeof p !== "string") return undefined
    if (p === "subscribeToState") return (_r?: any, cb?: Cb) => () => {}
    if (p.startsWith("subscribeTo")) return (_r?: any, _c?: Cb) => () => {}
    return async (..._a: any[]) => {
      console.log(`[gRPC-Client] Stub call: ${name}.${String(p)}()`)
      return {}
    }
  }})
}

// ── ModelsServiceClient 真实实现（基于 bridge，兼容 cline 旧接口） ──
function createModelsServiceClient() {
  const methods = {
    // 列出所有可用模型（含当前激活标记）
    listModels: async (_req?: any) => {
      const result = await bridge.sendAndWait(MessageTypes.ModelsList, {}, 10000)
      if (result && (result as any).models) {
        _currentProviderName = (result as any).currentProvider || _currentProviderName
        _currentModelName = (result as any).currentModel || _currentModelName
        notifyProvidersChanged()
      }
      return result || { models: [] }
    },

    // 切换当前模型
    switchModel: async (req: { ref: string }) => {
      const result: any = await bridge.sendAndWait(MessageTypes.ModelSwitch, req, 5000)
      if (result?.success && req.ref) {
        const parts = req.ref.split("/")
        _currentProviderName = parts[0] || _currentProviderName
        _currentModelName = parts[1] || _currentModelName
        notifyProvidersChanged()
      }
      return result || { success: false }
    },

    // 列出所有 provider
    listProviders: async (_req?: any) => {
      const result: any = await bridge.sendAndWait(MessageTypes.ProvidersList, {}, 10000)
      if (result?.providers) {
        _providers = result.providers
        if (result.currentProvider) _currentProviderName = result.currentProvider
        if (result.currentModel) _currentModelName = result.currentModel
        notifyProvidersChanged()
      }
      return result || { providers: [] }
    },

    // 保存 provider
    saveProvider: async (req: any) => {
      return await bridge.sendAndWait(MessageTypes.ProviderSave, req, 10000)
    },

    // 删除 provider
    deleteProvider: async (req: { name: string }) => {
      return await bridge.sendAndWait(MessageTypes.ProviderDelete, req, 10000)
    },

    // 拉取 provider 的模型列表
    fetchProviderModels: async (req: { name: string }) => {
      return await bridge.sendAndWait(MessageTypes.ProviderFetchModels, req, 30000)
    },

    // 设置 API Key
    setProviderKey: async (req: { name: string; apiKey: string }) => {
      return await bridge.sendAndWait(MessageTypes.ProviderSetKey, req, 5000)
    },

    // 兼容 cline 旧接口
    refreshOpenAi: async () => {
      return await bridge.sendAndWait(MessageTypes.ProvidersList, {}, 10000)
    },
    refreshOpenAiModels: async () => {
      return await bridge.sendAndWait(MessageTypes.ModelsList, {}, 10000)
    },
    updateApiConfigurationProto: async (req: any) => {
      // 旧版 cline 改 apiConfiguration 的调用 → 路由到 saveProvider
      const provider = req?.apiProvider || req?.provider || "openai"
      return await bridge.sendAndWait(MessageTypes.ProviderSave, {
        name: provider,
        kind: "openai",
        baseUrl: req?.openAiBaseUrl || req?.baseUrl || "",
        apiKey: req?.openAiApiKey || req?.apiKey || "",
        models: req?.apiModelId ? [req.apiModelId] : [],
        default: req?.apiModelId || req?.model || "",
        enabled: true,
        builtIn: false,
      }, 10000)
    },
  }

  // Proxy 包装：已知方法走真实实现，未知方法 fallback 到 stub（兼容 cline 旧接口）
  return new Proxy(methods, {
    get: (target, p: string | symbol) => {
      if (typeof p !== "string") return undefined
      if (p in target) return (target as any)[p]
      console.log("[gRPC-Client] Models stub:", p)
      return async (..._a: any[]) => ({})
    },
  })
}

// ── 导出 ──
export const TaskServiceClient = createTaskServiceClient()
export const StateServiceClient = createStateServiceClient()
export const UiServiceClient = createUiServiceClient()
export const FileServiceClient = createFileServiceClient()
export const McpServiceClient = createStubClient("Mcp")
export const ModelsServiceClient = createModelsServiceClient()
export const CheckpointsServiceClient = createStubClient("Checkpoints")
export const SlashServiceClient = createStubClient("Slash")
export const BrowserServiceClient = createStubClient("Browser")
export const WebServiceClient = createStubClient("Web")
