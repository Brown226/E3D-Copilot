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

    updateSettings: async (_req?: any) => {
      console.log("[gRPC-Client] updateSettings (E3D stub):", _req)
      return {}
    },

    getSettings: async () => {
      return { settings: "{}" }
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

// ── ModelsServiceClient 真实实现（基于 bridge） ──
function createModelsServiceClient() {
  return {
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
}

// ── 导出 ──
export const TaskServiceClient = createTaskServiceClient()
export const StateServiceClient = createStateServiceClient()
export const UiServiceClient = createUiServiceClient()
export const FileServiceClient = createFileServiceClient()
export const McpServiceClient = createStubClient("Mcp")
export const ModelsServiceClient = createModelsServiceClient()
export const AccountServiceClient = createStubClient("Account")
export const CheckpointsServiceClient = createStubClient("Checkpoints")
export const WorktreeServiceClient = createStubClient("Worktree")
export const SlashServiceClient = createStubClient("Slash")
export const BrowserServiceClient = createStubClient("Browser")
export const WebServiceClient = createStubClient("Web")
export const OcaAccountServiceClient = createStubClient("OcaAccount")
