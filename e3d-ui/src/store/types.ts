/**
 * E小智 ChatStore 类型定义
 * 从 useChatStore.ts 提取，供各 slice 和外部消费方使用
 */

import type { Message, MessageRole, Attachment } from '../types';
import type { ProviderInfo } from '../services/messageContracts';

// ============================================
// 会话管理
// ============================================

export interface SessionMeta {
  id: string
  title: string
  preview: string
  messageCount: number
  createdAt: number
  lastActivityAt: number
  /** 后端 tab 会话才有；由 localStorage 持久化的本地会话没有这个字段 */
  tabId?: string
  /** 后端标记：该条目是否为当前活跃 tab 的会话 */
  isActive?: boolean
}

// ============================================
// Tab 类型
// ============================================

export interface Tab {
  id: string
  title: string
  messages: Message[]
  isStreaming: boolean
  currentAssistantMsgId: string | null
  currentThinkingMsgId: string | null
  pendingApproval: PendingApproval | null
  pendingQuestion: PendingQuestion | null
  /**
   * 该 Tab 对应的会话 id。
   *
   * 原实现全局只有一个 sessionId，新建 Tab 不生成新的，于是两个 Tab
   * 往同一个 localStorage 键和同一条 meta 里写：后结束的那轮覆盖前一个，
   * 历史面板只出现一条，另一个 Tab 的消息直接丢失。
   */
  sessionId?: string
}

// ============================================
// 审批 / 提问
// ============================================

export interface PendingApproval {
  toolId: string;
  toolName: string;
  args?: unknown;
  description?: string;
  /** 来源 Agent 名称 */
  agentName?: string;
}

/** AI 主动提问（对齐 Reasonix AskRequest） */
export interface PendingQuestion {
  questionId: string;
  question: string;
  options?: string[];
  multiSelect?: boolean;
  /** 新版：完整提问数据（原始 WireAsk，供 AskUserCard 渲染多问题 Tab） */
  askData?: {
    askId: string;
    questions: Array<{
      id: string;
      header?: string;
      prompt: string;
      options: Array<{ label: string; description?: string }>;
      multi?: boolean;
    }>;
  };
}

/** 工具审批模式（四档，对齐 计划模式 / 变更前确认 / 自主模式 / 完全访问） */
export type ToolApprovalMode = 'plan' | 'ask' | 'auto' | 'yolo';

// ============================================
// Store 接口
// ============================================

export interface ChatStore {
  // === 多 Tab ===
  tabs: Tab[];
  activeTabId: string;

  // === 全局状态 ===
  inputValue: string;
  currentProvider: string;
  currentModel: string;
  isPlanMode: boolean;
  /** 工具审批模式：ask=每次询问 / auto=自动执行 / yolo=全自动 */
  toolApprovalMode: ToolApprovalMode;
  providers: ProviderInfo[];
  models: { ref: string; provider: string; model: string; current: boolean }[];
  showSettings: boolean;
  settingsTab: 'general' | 'models' | 'skills' | 'mcp' | 'appearance' | 'memory' | 'about' | null;
  showHistory: boolean;
  showCommandPalette: boolean;
  showSearch: boolean;
  isLoadingModels: boolean;
  error: string | null;
  bridgeConnected: boolean;
  lastPingTime: number | null;
  sessionId: string;
  sessions: SessionMeta[];

  // === 流式状态追踪 ===
  /** 当前正在接收后端事件的 Tab ID（锁定机制，防止切换 Tab 后事件写入错误 Tab） */
  streamingTabId: string | null;
  /** 当前轮次开始时间戳（用于计算运行时长） */
  turnStartAt: number | null;
  /** 当前轮次 token 用量 */
  turnTokens: number;
  /** 会话累计 token 用量 */
  sessionTokens: number;
  /** 最近一次 LLM 请求的真实 prompt tokens */
  lastPromptTokens: number;
  /** 当前模型上下文窗口（0 = 未知） */
  contextWindow: number;
  setContextUsage: (promptTokens: number, contextWindow?: number) => void;

  // === Tab 操作 ===
  createTab: (title?: string) => string;
  closeTab: (tabId: string) => void;
  setActiveTab: (tabId: string) => void;
  updateTabTitle: (tabId: string, title: string) => void;

  // === 消息操作（路由到 tabId 或 activeTab） ===
  appendMessage: (msg: { role: MessageRole; content?: string } & Partial<Omit<Message, 'role' | 'content'>>, tabId?: string) => void;
  startStreaming: (tabId?: string) => void;
  appendAssistantDelta: (delta: string, tabId?: string) => void;
  finalizeAssistantMessage: (id: string, tabId?: string) => void;
  setAssistantErrorMessage: (id: string, errorMessage: string, tabId?: string) => void;
  handleThinkingDelta: (text: string, tabId?: string) => void;
  handleToolResult: (toolId: string, result?: string, error?: string, tabId?: string, durationMs?: number, meta?: unknown) => void;
  handleToolProgress: (toolId: string, text: string, progress: unknown, tabId?: string) => void;
  finalizeThinkingMessage: (tabId?: string) => void;
  stopStreaming: (tabId?: string) => void;
  setMessageAgentName: (toolId: string, agentName: string, tabId?: string) => void;
  setPendingApproval: (approval: PendingApproval | null, tabId?: string) => void;
  setPendingQuestion: (question: PendingQuestion | null, tabId?: string) => void;

  // === 全局动作 ===
  setInputValue: (v: string) => void;
  sendMessage: (
    bridgeSend: (text: string, images?: string[], files?: string[], tabId?: string) => void,
    attachments?: Attachment[],
    overrideText?: string,
    /** 锁定目标 Tab（异步发送期间用户可能切换 Tab） */
    targetTabId?: string,
  ) => void;
  clearSession: (bridgeNewSession: () => void) => void;
  toggleSettings: () => void;
  /** 打开设置并直达指定分页 */
  openSettings: (tab?: 'general' | 'models' | 'skills' | 'mcp' | 'appearance' | 'memory' | 'about') => void;
  toggleHistory: () => void;
  toggleCommandPalette: () => void;
  toggleSearch: () => void;
  switchModel: (ref: string, bridgeSwitchModel: (ref: string) => Promise<unknown>) => Promise<void>;
  loadProviders: (bridgeListProviders: () => Promise<unknown>) => Promise<void>;
  setBridgeConnected: (connected: boolean) => void;
  setLastPingTime: (time: number) => void;
  setConfig: (config: { currentProvider: string; currentModel: string; providers: ProviderInfo[] }) => void;
  setPlanMode: (enabled: boolean) => void;
  togglePlanMode: () => void;
  setToolApprovalMode: (mode: ToolApprovalMode) => void;
  saveSession: () => void;
  loadSession: (sessionId: string) => void;
  deleteSession: (sessionId: string) => void;
  /** 批量删除。删除后会给挂载这些会话的 Tab 换新 id，避免下一轮保存复活条目。 */
  /** 批量删除，返回实际删除的所有 id（含同名重复条目），供调用方同步后端 */
  deleteSessions: (sessionIds: string[]) => string[];
  loadSessionList: () => void;
  setSessions: (sessions: SessionMeta[]) => void;
  newSession: (bridgeNewSession: () => void) => void;
  rerollLastMessage: (bridgeSend: (text: string, images?: string[]) => void) => void;
  /** 编辑并「发送」：截断该消息之后的历史并以新内容重新发起本轮 */
  editUserMessage: (
    messageId: string,
    newText?: string,
    bridgeSend?: (text: string, images?: string[], files?: string[], tabId?: string, rewindTurns?: number) => void
  ) => void;
  /** 回退到指定消息位置（从这继续 — 删除该消息之后的所有消息） */
  rollbackToMessage: (messageId: string) => void;
  /** 重试状态 */
  isRetrying: boolean;
  setRetrying: (retrying: boolean) => void;

  // === 流式状态 ===
  setTurnStart: (timestamp: number | null) => void;
  addTurnTokens: (tokens: number) => void;
  resetTurnStats: () => void;
}

// ============================================
// Slice 类型辅助
// ============================================

import type { StateCreator } from 'zustand';

/** Slice 创建器类型：每个 slice 接收 set/get 返回部分 store */
export type SliceCreator<T> = StateCreator<ChatStore, [], [], T>;
