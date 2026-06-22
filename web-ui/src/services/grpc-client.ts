// Stub — gRPC clients removed. Use bridge.ts instead.
// Provides minimal initial state so the React app boots and renders.
type Cb = { onResponse?: (...a: any[]) => void; onError?: (e: any) => void; onComplete?: () => void }

const INITIAL_STATE_JSON = JSON.stringify({
  version: 1,
  welcomeViewCompleted: true,
  apiConfiguration: { provider: "openai", planModeApiProvider: "openai", actModeApiProvider: "openai", apiModelId: "qwen2.5-coder-14b", openAiBaseUrl: "http://localhost:8000/v1" },
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
}) as const

function createClient(_n: string) {
  return new Proxy({}, { get: (_t, p: string | symbol) => {
    if (typeof p !== "string") return undefined
    if (p === "subscribeToState") return (_r?: any, c?: Cb) => {
      // Fire initial state on next tick so React has mounted
      setTimeout(() => c?.onResponse?.({ stateJson: INITIAL_STATE_JSON }), 0)
      return () => {} // unsubscribe
    }
    if (p.startsWith("subscribeTo")) return (_r?: any, _c?: Cb) => () => {}
    return async (..._a: any[]) => ({})
  }})
}
export const StateServiceClient = createClient("S")
export const UiServiceClient = createClient("U")
export const FileServiceClient = createClient("F")
export const McpServiceClient = createClient("M")
export const ModelsServiceClient = createClient("M")
export const AccountServiceClient = createClient("A")
export const TaskServiceClient = createClient("T")
export const CheckpointsServiceClient = createClient("C")
export const WorktreeServiceClient = createClient("W")
export const SlashServiceClient = createClient("S")
export const BrowserServiceClient = createClient("B")
export const WebServiceClient = createClient("W")
export const OcaAccountServiceClient = createClient("O")
