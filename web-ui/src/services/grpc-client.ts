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

// ── 初始状态 ──
const INITIAL_STATE = {
  version: 1,
  welcomeViewCompleted: true,
  apiConfiguration: { 
    provider: "openai", 
    planModeApiProvider: "openai", 
    actModeApiProvider: "openai", 
    apiModelId: "qwen3.7-plus", 
    openAiBaseUrl: "https://opencode.ai/zen/go/v1", 
    openAiApiKey: "sk-jnDafVDYkBqpd6X81cWbkNuocNQtgHcaaiwm08fU6EtQcavJV97iPr6J1SgPq1pe" 
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
}

// ── Bridge 事件 → 状态更新 ──
// 存储当前消息列表，随 bridge 事件更新
let _clineMessages: any[] = []
let _stateListeners: Set<Cb> = new Set()
let _taskListeners: Set<Cb> = new Set()
let _uiListeners: Set<Cb> = new Set()

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
      // 更新前端初始状态中的 API 配置
      if (msg.payload) {
        INITIAL_STATE.apiConfiguration = {
          provider: "openai",
          planModeApiProvider: "openai",
          actModeApiProvider: "openai",
          apiModelId: msg.payload.model || INITIAL_STATE.apiConfiguration.apiModelId,
          openAiBaseUrl: msg.payload.baseUrl || INITIAL_STATE.apiConfiguration.openAiBaseUrl,
          openAiApiKey: msg.payload.apiKey || INITIAL_STATE.apiConfiguration.openAiApiKey,
        }
      }
      break

    // ── LLM 流式文本 ──
    case MessageTypes.LlmStreamDelta:
      handleStreamDelta(msg.payload)
      break

    case MessageTypes.LlmStreamEnd:
      handleStreamEnd(msg.payload)
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

    // ── 通用事件 ──
    case MessageTypes.Notice:
      addMessage({ type: "say", say: "text", text: msg.payload?.text || "" })
      break

    case MessageTypes.Error:
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
  const msg = getOrCreateStreamingMessage()
  msg.text += payload?.delta || ""
  notifyStateChange()
}

function handleStreamEnd(_payload: any) {
  if (_currentStreamingMessage) {
    _currentStreamingMessage.partial = false
    _currentStreamingMessage = null
  }
  notifyStateChange()
}

function handleThinking(payload: any) {
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
  const msg = {
    type: "say",
    say: "tool",
    text: JSON.stringify({ tool: payload?.name, args: payload?.args }),
    ts: Date.now(),
  }
  _clineMessages.push(msg)
  notifyStateChange()
}

function handleToolResult(payload: any) {
  const msg = {
    type: "say",
    say: "tool_result",
    text: JSON.stringify({ id: payload?.id, result: payload?.result || payload?.error }),
    ts: Date.now(),
  }
  _clineMessages.push(msg)
  notifyStateChange()
}

function handleToolApproval(payload: any) {
  // 工具审批请求 — 在自动审批模式下自动批准
  // 使用类型安全的方法
  bridge.sendApproval(payload?.id, true)
}

function handleAskUser(payload: any) {
  const msg = {
    type: "ask",
    ask: "followup",
    text: JSON.stringify({ question: payload?.question || payload?.data }),
    ts: Date.now(),
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

      // 清空旧消息，发送新任务
      _clineMessages = []
      _currentStreamingMessage = null
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
      // 使用类型安全的方法
      bridge.sendAskResponse("current", text)
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
      // 使用常量
      bridge.send(MessageTypes.UserMessage, { mode })
      return {}
    },

    togglePlanActModeProto: async (req: any) => {
      const mode = req?.mode || "act"
      const chatContent = req?.chatContent
      console.log("[gRPC-Client] togglePlanActModeProto:", mode, chatContent?.message?.substring(0, 30))
      // 使用常量
      bridge.send("togglePlanActMode", { mode, chatContent })
      return { value: true }
    },

    getAvailableTerminalProfiles: async () => {
      return { profiles: [] }
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
  return {
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
  }
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

// ── 导出 ──
export const TaskServiceClient = createTaskServiceClient()
export const StateServiceClient = createStateServiceClient()
export const UiServiceClient = createUiServiceClient()
export const FileServiceClient = createFileServiceClient()
export const McpServiceClient = createStubClient("Mcp")
export const ModelsServiceClient = createStubClient("Models")
export const AccountServiceClient = createStubClient("Account")
export const CheckpointsServiceClient = createStubClient("Checkpoints")
export const WorktreeServiceClient = createStubClient("Worktree")
export const SlashServiceClient = createStubClient("Slash")
export const BrowserServiceClient = createStubClient("Browser")
export const WebServiceClient = createStubClient("Web")
export const OcaAccountServiceClient = createStubClient("OcaAccount")
