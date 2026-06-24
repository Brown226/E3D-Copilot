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
  UserSetPlanMode: 'user:set_plan_mode',
  Ping: 'ping',
  ModelsList: 'models:list',
  ModelSwitch: 'model:switch',
  ProvidersList: 'providers:list',
  ProviderSave: 'provider:save',
  ProviderDelete: 'provider:delete',
  ProviderFetchModels: 'provider:fetch_models',
  ProviderSetKey: 'provider:set_key',

  // === Skills 管理 ===
  SkillsList: 'skills:list',
  SkillsToggle: 'skills:toggle',
  SkillsAddSource: 'skills:add_source',
  SkillsRemoveSource: 'skills:remove_source',
  SkillsRefresh: 'skills:refresh',

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
  TurnDone: 'turn:done',

  // === 新增事件类型（补全前后端事件覆盖） ===
  LlmTurnStarted: 'llm:turn_started',     // 对应前端 api_req_started
  LlmUsage: 'llm:usage',                  // Token 用量
  LlmRetry: 'llm:retry',                  // 重试事件
  ToolProgress: 'tool:progress',           // 工具执行进度
} as const;

export type MessageType = typeof MessageTypes[keyof typeof MessageTypes];

// ============================================
// 前端 → 后端消息载荷
// ============================================

export interface UserMessagePayload {
  text: string;
  images?: string[];
  files?: string[];
  tabId?: string;
}

export interface ApprovalPayload {
  id: string;
  allow: boolean;
}

export interface AskResponsePayload {
  questionId: string;
  answer: string;
}

export interface SetPlanModePayload {
  mode: 'plan' | 'act';
}

export interface PlanModeChangedPayload {
  enabled: boolean;
}

// ============================================
// 后端 → 前端消息载荷
// ============================================

export interface LlmStreamDeltaPayload {
  delta: string;
  tabId?: string;
}

export interface LlmStreamEndPayload {
  usage?: unknown;
  tabId?: string;
}

export interface ToolDispatchPayload {
  id: string;
  name: string;
  args?: unknown;
  tabId?: string;
}

export interface ToolResultPayload {
  id: string;
  result?: string;
  error?: string;
  tabId?: string;
  durationMs?: number;
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
  data?: unknown;
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

// [Phase 2 修复] 补全 ConfigSyncPayload 遗漏字段
// Phase 0 调研发现：原 web-ui 版本缺少 currentProvider / currentModel / providers 字段，
// 导致 config:sync 事件无法完整传递后端配置。此处已补全。
export interface ConfigSyncPayload {
  provider: string;
  model: string;
  baseUrl: string;
  apiKey: string;
  mode: string;
  /** 当前选中的 Provider 名称 */
  currentProvider: string;
  /** 当前选中的 Model 名称 */
  currentModel: string;
  /** 后端同步的 Provider 完整列表 */
  providers: ProviderInfo[];
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

// ============================================
// Skills 管理
// ============================================

export interface SkillInfo {
  name: string;
  description: string;
  scope: 'builtin' | 'project' | 'global' | 'custom';
  runAs: 'inline' | 'subagent';
  enabled: boolean;
  filePath?: string;
  tags?: string[];
}

export interface SkillSource {
  path: string;
  status: 'active' | 'missing' | 'error';
  skillCount: number;
  removable: boolean;
}

export interface SkillsListResultPayload {
  skills: SkillInfo[];
  sources: SkillSource[];
}

export interface SkillsTogglePayload {
  name: string;
  enabled: boolean;
}

// ============================================
// Memory 管理
// ============================================

export interface MemoryEntry {
  id: string;
  title: string;
  content: string;
  kind: string;
  tags: string[];
  score?: number;
  created_at?: string;
  updated_at?: string;
}

export interface MemoryListResultPayload {
  memories: MemoryEntry[];
}

// ============================================
// Sessions 管理
// ============================================

export interface SessionInfo {
  id: string;
  title: string;
  messageCount: number;
  createdAt: number;
  lastActivityAt: number;
}

export interface SessionsListResultPayload {
  sessions: SessionInfo[];
}
