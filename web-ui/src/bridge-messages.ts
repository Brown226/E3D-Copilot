/**
 * E小智 WebView2 Bridge 消息协议定义
 */

// web-ui → C# 后端
export type WebviewMessage =
  | { type: "chat"; text: string; images?: string[] }
  | { type: "cancelTask" }
  | { type: "getState" }
  | { type: "getHistory" }
  | { type: "deleteHistory"; taskId: string }
  | { type: "loadHistoryTask"; taskId: string }
  | { type: "saveSettings"; settings: Record<string, unknown> }
  | { type: "loadSettings" }
  | { type: "getModels"; provider: string }
  | { type: "readFile"; path: string }
  | { type: "writeFile"; path: string; content: string }
  | { type: "openFile"; path: string }
  | { type: "webviewReady" }
  | { type: "showMessage"; message: string; level: "info" | "warning" | "error" }

// C# 后端 → web-ui
export type ExtensionMessage =
  | { type: "state"; state: Partial<ExtensionState> }
  | { type: "chatChunk"; text: string; taskId: string }
  | { type: "chatComplete"; taskId: string }
  | { type: "chatError"; taskId: string; error: string }
  | { type: "toolUse"; toolName: string; toolInput: Record<string, unknown>; taskId: string }
  | { type: "toolResult"; toolName: string; result: string; taskId: string; isError?: boolean }
  | { type: "settingsLoaded"; settings: Record<string, unknown> }
  | { type: "modelsLoaded"; models: Array<{ id: string; name: string }> }
  | { type: "historyLoaded"; tasks: Array<{ id: string; title: string; timestamp: number }> }
  | { type: "historyTaskLoaded"; taskId: string; messages: unknown[] }
  | { type: "error"; message: string }
  | { type: "log"; message: string; level: "info" | "warning" | "error" }

// 全局状态
export interface ExtensionState {
  isConnected: boolean
  currentTaskId: string | null
  isStreaming: boolean
  messages: ChatMessage[]
  settings: Record<string, unknown>
  availableModels: Array<{ id: string; name: string }>
  showSettings: boolean
  showHistory: boolean
  didHydrateState: boolean
  showWelcome: boolean
}

export interface ChatMessage {
  id: string
  role: "user" | "assistant" | "tool"
  content: string
  toolUse?: { name: string; input: Record<string, unknown> }
  toolResult?: { name: string; result: string; isError?: boolean }
  timestamp: number
}

export type WebviewMessagePayload<T extends WebviewMessage["type"]> = Extract<WebviewMessage, { type: T }>
export type ExtensionMessagePayload<T extends ExtensionMessage["type"]> = Extract<ExtensionMessage, { type: T }>

export function isMessageType<T extends ExtensionMessage["type"]>(
  msg: ExtensionMessage, type: T
): msg is Extract<ExtensionMessage, { type: T }> {
  return msg.type === type
}
