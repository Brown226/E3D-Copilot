/**
 * E小智前后端消息契约 TypeScript 类型定义
 * 与 C# E3DCopilot.Core.Messaging.MessageContracts 对应
 * 
 * 使用方式：
 * import type { UserMessagePayload, MessageTypes } from './messageContracts';
 */

// ============================================
// 消息类型常量（与 C# MessageTypes 对应）
// ============================================

export const MessageTypes = {
  // === 前端 → 后端 ===
  UserMessage: 'user:message',
  UserCancel: 'user:cancel',
  UserNewSession: 'user:new_session',
  UserApprove: 'user:approve',
  UserAskResponse: 'user:ask_response',
  Ping: 'ping',
  ModelsList: 'models:list',
  ModelSwitch: 'model:switch',
  ProvidersList: 'providers:list',
  ProviderSave: 'provider:save',
  ProviderDelete: 'provider:delete',
  ProviderFetchModels: 'provider:fetch_models',
  ProviderSetKey: 'provider:set_key',

  // === 后端 → 前端 ===
  Pong: 'pong',
  LlmStreamDelta: 'llm:stream:delta',
  LlmStreamEnd: 'llm:stream:end',
  LlmThinking: 'llm:thinking',
  ToolDispatch: 'tool:dispatch',
  ToolResult: 'tool:result',
  ToolError: 'tool:error',
  ToolApproval: 'tool:approval',
  AskUser: 'ask_user',
  Notice: 'notice',
  Error: 'error',
  HostReady: 'host:ready',
  ConfigSync: 'config:sync',
  ModelsListResult: 'models:list:result',
  ProvidersListResult: 'providers:list:result',
  ProviderFetchResult: 'provider:fetch_models:result',
} as const;

export type MessageType = typeof MessageTypes[keyof typeof MessageTypes];

// ============================================
// 前端 → 后端消息载荷
// ============================================

export interface UserMessagePayload {
  text: string;
  images?: string[];
  files?: string[];
}

export interface ApprovalPayload {
  id: string;
  allow: boolean;
}

export interface AskResponsePayload {
  questionId: string;
  answer: string;
}

// ============================================
// 后端 → 前端消息载荷
// ============================================

export interface LlmStreamDeltaPayload {
  delta: string;
}

export interface LlmStreamEndPayload {
  usage?: any;
}

export interface ToolDispatchPayload {
  id: string;
  name: string;
  args?: any;
}

export interface ToolResultPayload {
  id: string;
  result?: string;
  error?: string;
}

export interface ApprovalRequestPayload {
  id: string;
  name: string;
  args?: string;
  description: string;
}

export interface AskUserPayload {
  questionId: string;
  question: string;
  data?: any;
}

export interface NoticePayload {
  text: string;
}

export interface ErrorPayload {
  message: string;
}

export interface HostReadyPayload {
  version: string;
  platform: string;
  timestamp: number;
}

export interface ConfigSyncPayload {
  provider: string;
  model: string;
  baseUrl: string;
  apiKey: string;
  mode: string;
}

// ============================================
// 消息信封
// ============================================

export interface CopilotMessage<T> {
  type: string;
  payload: T;
}

// ============================================
// 类型守卫函数
// ============================================

export function isMessageType(type: string): type is MessageType {
  return Object.values(MessageTypes).includes(type as MessageType);
}

// ============================================
// 消息创建辅助函数
// ============================================

export function createMessage<T>(type: MessageType, payload: T): CopilotMessage<T> {
  return { type, payload };
}

export function createUserMessage(text: string, images?: string[], files?: string[]): CopilotMessage<UserMessagePayload> {
  return createMessage(MessageTypes.UserMessage, { text, images, files });
}

export function createApproval(id: string, allow: boolean): CopilotMessage<ApprovalPayload> {
  return createMessage(MessageTypes.UserApprove, { id, allow });
}

export function createAskResponse(questionId: string, answer: string): CopilotMessage<AskResponsePayload> {
  return createMessage(MessageTypes.UserAskResponse, { questionId, answer });
}

// ============================================
// Provider / Model 管理（参考 Reasonix）
// ============================================

export interface ModelInfo {
  ref: string;        // "provider/model"
  provider: string;
  model: string;
  current: boolean;
}

export interface ProviderInfo {
  name: string;
  kind: string;
  baseUrl: string;
  apiKey: string;
  keySet: boolean;
  models: string[];
  default: string;
  enabled: boolean;
  builtIn: boolean;
}

export interface ModelsListResultPayload {
  models: ModelInfo[];
  currentProvider: string;
  currentModel: string;
}

export interface ProvidersListResultPayload {
  providers: ProviderInfo[];
  currentProvider: string;
  currentModel: string;
}

export interface ProviderFetchResultPayload {
  providerName: string;
  success: boolean;
  models: string[];
  error?: string;
}

export interface ModelSwitchPayload {
  ref: string; // "provider/model"
}

export interface ProviderSavePayload {
  name: string;
  kind: string;
  baseUrl: string;
  apiKey?: string;
  models: string[];
  default: string;
  enabled: boolean;
  builtIn: boolean;
}

export interface ProviderDeletePayload {
  name: string;
}

export interface ProviderSetKeyPayload {
  name: string;
  apiKey: string;
}
