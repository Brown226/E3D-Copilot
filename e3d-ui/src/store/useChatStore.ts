/**
 * E小智 v2.0 聊天状态管理（Zustand）— 多 Tab 版本
 *
 * 每个 Tab 独立维护：messages, isStreaming, currentAssistantMsgId, currentThinkingMsgId, pendingApproval
 * 全局共享：inputValue, providers, settings, bridge 状态
 */

import { create } from 'zustand';
import type { Message, MessageRole } from '../types';
import { generateMessageId } from '../types';
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
}

const STORAGE_KEY = 'e3d-chat-sessions'
const MESSAGES_KEY_PREFIX = 'e3d-session-msgs-'
const MAX_SESSIONS = 100

function generateSessionId(): string {
  return `session_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`
}

function generateTabId(): string {
  return `tab_${Date.now()}_${Math.random().toString(36).slice(2, 6)}`
}

function loadSessionsFromStorage(): SessionMeta[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? JSON.parse(raw) : []
  } catch {
    return []
  }
}

function saveSessionsToStorage(sessions: SessionMeta[]) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(sessions.slice(0, MAX_SESSIONS)))
}

function saveMessagesToStorage(sessionId: string, messages: Message[]) {
  try {
    // 限制存储大小：每个会话最多保存 500 条消息
    const trimmed = messages.slice(-500)
    localStorage.setItem(MESSAGES_KEY_PREFIX + sessionId, JSON.stringify(trimmed))
  } catch {
    // localStorage 满时静默失败
  }
}

function loadMessagesFromStorage(sessionId: string): Message[] {
  try {
    const raw = localStorage.getItem(MESSAGES_KEY_PREFIX + sessionId)
    return raw ? JSON.parse(raw) : []
  } catch {
    return []
  }
}

function deleteMessagesFromStorage(sessionId: string) {
  localStorage.removeItem(MESSAGES_KEY_PREFIX + sessionId)
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
}

function createTab(title = '新对话'): Tab {
  return {
    id: generateTabId(),
    title,
    messages: [],
    isStreaming: false,
    currentAssistantMsgId: null,
    currentThinkingMsgId: null,
    pendingApproval: null,
    pendingQuestion: null,
  }
}

// ============================================
// Store 接口
// ============================================

export interface PendingApproval {
  toolId: string;
  toolName: string;
  args?: unknown;
  description?: string;
}

/** AI 主动提问 */
export interface PendingQuestion {
  questionId: string;
  question: string;
  options?: string[];
  multiSelect?: boolean;
}

/** 工具审批模式 */
export type ToolApprovalMode = 'ask' | 'auto' | 'yolo';

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
  showHistory: boolean;
  showCommandPalette: boolean;
  isLoadingModels: boolean;
  error: string | null;
  bridgeConnected: boolean;
  lastPingTime: number | null;
  sessionId: string;
  sessions: SessionMeta[];

  // === 流式状态追踪 ===
  /** 当前轮次开始时间戳（用于计算运行时长） */
  turnStartAt: number | null;
  /** 当前轮次 token 用量 */
  turnTokens: number;
  /** 会话累计 token 用量 */
  sessionTokens: number;

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
  handleToolResult: (toolId: string, result?: string, error?: string, tabId?: string, durationMs?: number) => void;
  handleToolProgress: (toolId: string, text: string, progress: unknown, tabId?: string) => void;
  finalizeThinkingMessage: (tabId?: string) => void;
  stopStreaming: (tabId?: string) => void;
  setPendingApproval: (approval: PendingApproval | null, tabId?: string) => void;
  setPendingQuestion: (question: PendingQuestion | null, tabId?: string) => void;

  // === 全局动作 ===
  setInputValue: (v: string) => void;
  sendMessage: (bridgeSend: (text: string, images?: string[], files?: string[], tabId?: string) => void, attachments?: import('../types').Attachment[], overrideText?: string) => void;
  clearSession: (bridgeNewSession: () => void) => void;
  toggleSettings: () => void;
  toggleHistory: () => void;
  toggleCommandPalette: () => void;
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
  loadSessionList: () => void;
  setSessions: (sessions: SessionMeta[]) => void;
  newSession: (bridgeNewSession: () => void) => void;
  rerollLastMessage: (bridgeSend: (text: string, images?: string[]) => void) => void;
  editUserMessage: (messageId: string, newText?: string) => void;
  /** 重试状态 */
  isRetrying: boolean;
  setRetrying: (retrying: boolean) => void;

  // === 流式状态 ===
  setTurnStart: (timestamp: number | null) => void;
  addTurnTokens: (tokens: number) => void;
  resetTurnStats: () => void;
}

// ============================================
// 辅助：更新指定 tab 的状态
// ============================================

function updateTab(tabs: Tab[], tabId: string, updater: (tab: Tab) => Partial<Tab>): Tab[] {
  return tabs.map((t) => t.id === tabId ? { ...t, ...updater(t) } : t)
}

// ============================================
// Store 实现
// ============================================

const initialTab = createTab();

export const useChatStore = create<ChatStore>((set, get) => ({
  // === 多 Tab 初始状态 ===
  tabs: [initialTab],
  activeTabId: initialTab.id,

  // === 全局初始状态 ===
  inputValue: '',
  currentProvider: '',
  currentModel: '',
  isPlanMode: false,
  toolApprovalMode: 'ask' as ToolApprovalMode,
  providers: [],
  models: [],
  showSettings: false,
  showHistory: false,
  showCommandPalette: false,
  isLoadingModels: false,
  error: null,
  bridgeConnected: false,
  lastPingTime: null,
  sessionId: generateSessionId(),
  sessions: loadSessionsFromStorage(),
  turnStartAt: null,
  turnTokens: 0,
  sessionTokens: 0,
  isRetrying: false,

  // ============================================
  // Tab 操作
  // ============================================

  createTab: (title) => {
    const tab = createTab(title)
    set((s) => ({ tabs: [...s.tabs, tab], activeTabId: tab.id }))
    return tab.id
  },

  closeTab: (tabId) => {
    set((s) => {
      if (s.tabs.length <= 1) return s
      const idx = s.tabs.findIndex((t) => t.id === tabId)
      const nextTabs = s.tabs.filter((t) => t.id !== tabId)
      const nextActive = s.activeTabId === tabId
        ? nextTabs[Math.min(idx, nextTabs.length - 1)].id
        : s.activeTabId
      return { tabs: nextTabs, activeTabId: nextActive }
    })
  },

  setActiveTab: (tabId) => set({ activeTabId: tabId }),

  updateTabTitle: (tabId, title) => {
    set((s) => ({ tabs: updateTab(s.tabs, tabId, () => ({ title })) }))
  },

  // ============================================
  // 消息操作（自动路由到 tabId 或 activeTab）
  // ============================================

  appendMessage: (msg, tabId) => {
    const targetId = tabId || get().activeTabId
    const newMsg: Message = {
      id: generateMessageId(),
      timestamp: Date.now(),
      role: msg.role,
      content: msg.content ?? '',
      toolId: msg.toolId,
      toolName: msg.toolName,
      toolArgs: msg.toolArgs,
      toolError: msg.toolError,
      finalized: msg.finalized,
    }
    set((s) => ({
      tabs: updateTab(s.tabs, targetId, (tab) => ({
        messages: [...tab.messages, newMsg],
      })),
    }))
  },

  startStreaming: (tabId) => {
    const targetId = tabId || get().activeTabId
    const existingTab = get().tabs.find((t) => t.id === targetId)
    // 幂等：如果已在流式中且有 assistant 消息，不覆盖（AgentLoop 循环内多次 TurnStarted）
    if (existingTab?.isStreaming && existingTab?.currentAssistantMsgId) {
      set((s) => ({
        tabs: updateTab(s.tabs, targetId, () => ({
          currentThinkingMsgId: null,  // 只重置 thinking（新一轮可能有新 thinking）
        })),
      }))
      return
    }
    // 首次进入流式：不创建 assistant 消息，等第一个 text delta 到达时由 appendAssistantDelta 创建
    // 这确保 thinking/tool 消息排在 assistant 消息前面，UI 顺序正确
    set((s) => ({
      tabs: updateTab(s.tabs, targetId, () => ({
        isStreaming: true,
        currentAssistantMsgId: existingTab?.currentAssistantMsgId ?? null,
        currentThinkingMsgId: null,
      })),
    }))
  },

  appendAssistantDelta: (delta, tabId) => {
    const targetId = tabId || get().activeTabId
    set((s) => {
      const tab = s.tabs.find((t) => t.id === targetId)
      if (!tab) return s

      // 如果还没有 assistant 消息，现在创建（排在 thinking/tool 消息之后）
      if (!tab.currentAssistantMsgId) {
        const assistantId = generateMessageId()
        const assistantMsg: Message = {
          id: assistantId,
          role: 'assistant',
          content: delta,
          timestamp: Date.now(),
        }
        return {
          tabs: s.tabs.map((t) =>
            t.id === targetId
              ? {
                  ...t,
                  messages: [...t.messages, assistantMsg],
                  currentAssistantMsgId: assistantId,
                }
              : t
          ),
        }
      }

      // 已有 assistant 消息，追加 delta
      return {
        tabs: s.tabs.map((t) => {
          if (t.id !== targetId || !t.currentAssistantMsgId) return t
          return {
            ...t,
            messages: t.messages.map((m) =>
              m.id === t.currentAssistantMsgId ? { ...m, content: m.content + delta } : m
            ),
          }
        }),
      }
    })
  },

  finalizeAssistantMessage: (id, tabId) => {
    const targetId = tabId || get().activeTabId
    set((s) => ({
      tabs: updateTab(s.tabs, targetId, () => ({
        messages: (s.tabs.find((t) => t.id === targetId)?.messages ?? []).map((m) =>
          m.id === id ? { ...m, finalized: true } : m
        ),
        currentAssistantMsgId: null,
      })),
    }))
  },

  setAssistantErrorMessage: (id, errorMessage, tabId) => {
    const targetId = tabId || get().activeTabId
    set((s) => ({
      tabs: updateTab(s.tabs, targetId, () => ({
        messages: (s.tabs.find((t) => t.id === targetId)?.messages ?? []).map((m) =>
          m.id === id ? { ...m, errorMessage } : m
        ),
      })),
    }))
  },

  handleThinkingDelta: (text, tabId) => {
    const targetId = tabId || get().activeTabId
    set((s) => {
      const tab = s.tabs.find((t) => t.id === targetId)
      if (!tab) return s

      if (tab.currentThinkingMsgId) {
        return {
          tabs: s.tabs.map((t) =>
            t.id === targetId
              ? {
                  ...t,
                  messages: t.messages.map((m) =>
                    m.id === t.currentThinkingMsgId ? { ...m, content: m.content + text } : m
                  ),
                }
              : t
          ),
        }
      }

      const thinkingId = generateMessageId()
      const thinkingMsg: Message = {
        id: thinkingId,
        role: 'thinking',
        content: text,
        timestamp: Date.now(),
      }
      return {
        tabs: updateTab(s.tabs, targetId, () => ({
          messages: [...tab.messages, thinkingMsg],
          currentThinkingMsgId: thinkingId,
        })),
      }
    })
  },

  finalizeThinkingMessage: (tabId) => {
    const targetId = tabId || get().activeTabId
    set((s) => ({
      tabs: s.tabs.map((t) =>
        t.id === targetId
          ? {
              ...t,
              messages: t.messages.map((m) =>
                m.id === t.currentThinkingMsgId ? { ...m, finalized: true } : m
              ),
              currentThinkingMsgId: null,
            }
          : t
      ),
    }))
  },

  handleToolResult: (toolId, result, error, tabId, durationMs) => {
    const targetId = tabId || get().activeTabId
    set((s) => ({
      tabs: s.tabs.map((t) =>
        t.id === targetId
          ? {
              ...t,
              messages: t.messages.map((m) =>
                m.toolId === toolId
                  ? {
                      ...m,
                      role: 'tool_result' as MessageRole,
                      content: error ? `Error: ${error}` : (result || 'Done'),
                      toolError: error,
                      durationMs: durationMs ?? m.durationMs,
                      finalized: true,
                    }
                  : m
              ),
            }
          : t
      ),
    }))
  },

  stopStreaming: (tabId) => {
    const targetId = tabId || get().activeTabId
    set((s) => ({
      tabs: s.tabs.map((t) => {
        if (t.id !== targetId) return t
        // Finalize assistant message and clear ID
        const updatedMessages = t.currentAssistantMsgId
          ? t.messages.map((m) =>
              m.id === t.currentAssistantMsgId ? { ...m, finalized: true } : m
            )
          : t.messages
        return {
          ...t,
          isStreaming: false,
          messages: updatedMessages,
          currentAssistantMsgId: null,
          currentThinkingMsgId: null,
        }
      }),
    }))
  },

  handleToolProgress: (toolId, text, _progress, tabId) => {
    const targetId = tabId || get().activeTabId
    set((s) => ({
      tabs: s.tabs.map((t) =>
        t.id === targetId
          ? {
              ...t,
              messages: t.messages.map((m) =>
                m.toolId === toolId && m.role === 'tool_call'
                  ? { ...m, content: text || m.content }
                  : m
              ),
            }
          : t
      ),
    }))
  },

  setPendingApproval: (approval, tabId) => {
    const targetId = tabId || get().activeTabId
    set((s) => ({
      tabs: updateTab(s.tabs, targetId, () => ({ pendingApproval: approval })),
    }))
  },

  setPendingQuestion: (question, tabId) => {
    const targetId = tabId || get().activeTabId
    set((s) => ({
      tabs: updateTab(s.tabs, targetId, () => ({ pendingQuestion: question })),
    }))
  },

  // ============================================
  // 全局动作
  // ============================================

  setInputValue: (v: string) => set({ inputValue: v }),

  sendMessage: (bridgeSend, attachments, overrideText) => {
    const { inputValue, activeTabId } = get()
    const text = overrideText ?? inputValue;
    const trimmed = text.trim();
    if (!trimmed && (!attachments || attachments.length === 0)) return;

    const images = attachments
      ?.filter((a) => a.type.startsWith('image/') && (a.data || a.previewUrl))
      .map((a) => a.data || a.previewUrl || '')
      .filter(Boolean);

    const userMsg: import('../types').Message = {
      id: generateMessageId(),
      role: 'user',
      content: trimmed,
      timestamp: Date.now(),
      attachments: attachments && attachments.length > 0 ? attachments : undefined,
    }

    set((s) => ({
      tabs: s.tabs.map((t) =>
        t.id === activeTabId
          ? {
              ...t,
              messages: [...t.messages, userMsg],
              title: t.messages.length === 0 ? trimmed.slice(0, 30) || '新对话' : t.title,
            }
          : t
      ),
      inputValue: '',
    }))

    bridgeSend(trimmed, images, undefined, activeTabId);
  },

  clearSession: (bridgeNewSession) => {
    const { activeTabId } = get()
    set((s) => ({
      tabs: updateTab(s.tabs, activeTabId, () => ({
        messages: [],
        isStreaming: false,
        currentAssistantMsgId: null,
        currentThinkingMsgId: null,
        pendingApproval: null,
        pendingQuestion: null,
      })),
      error: null,
    }))
    bridgeNewSession();
  },

  toggleSettings: () => set((s) => ({ showSettings: !s.showSettings })),
  toggleHistory: () => set((s) => ({ showHistory: !s.showHistory })),
  toggleCommandPalette: () => set((s) => ({ showCommandPalette: !s.showCommandPalette })),

  switchModel: async (ref, bridgeSwitchModel) => {
    try {
      set({ isLoadingModels: true });
      await bridgeSwitchModel(ref);
      const [provider, ...modelParts] = ref.split('/');
      const model = modelParts.join('/');
      set({
        currentProvider: provider,
        currentModel: model,
        isLoadingModels: false,
      });
    } catch (err) {
      set({
        error: err instanceof Error ? err.message : 'Failed to switch model',
        isLoadingModels: false,
      });
    }
  },

  loadProviders: async (bridgeListProviders) => {
    try {
      set({ isLoadingModels: true });
      const result = await bridgeListProviders() as {
        providers?: ProviderInfo[];
        currentProvider?: string;
        currentModel?: string;
      } | null;
      if (result) {
        set({
          providers: result.providers || [],
          currentProvider: result.currentProvider || '',
          currentModel: result.currentModel || '',
          isLoadingModels: false,
        });
      }
    } catch (err) {
      set({
        error: err instanceof Error ? err.message : 'Failed to load providers',
        isLoadingModels: false,
      });
    }
  },

  setBridgeConnected: (connected) => set({ bridgeConnected: connected }),
  setLastPingTime: (time) => set({ lastPingTime: time }),
  setConfig: (config) => set({
    currentProvider: config.currentProvider,
    currentModel: config.currentModel,
    providers: config.providers,
  }),

  setPlanMode: (enabled) => set({ isPlanMode: enabled }),
  togglePlanMode: () => {
    const newMode = !get().isPlanMode
    set({ isPlanMode: newMode })
    // 通知后端
    import('@/services/bridgeService').then(({ default: bridge }) => {
      bridge.send('user:set_plan_mode', { mode: newMode ? 'plan' : 'act' })
    })
  },

  setToolApprovalMode: (mode) => {
    set({ toolApprovalMode: mode })
    // 通知后端
    import('@/services/bridgeService').then(({ default: bridge }) => {
      bridge.send('user:set_approval_mode', { mode })
    })
  },

  // === 流式状态 ===
  setTurnStart: (timestamp) => set({ turnStartAt: timestamp }),
  addTurnTokens: (tokens) => set((s) => ({
    turnTokens: s.turnTokens + tokens,
    sessionTokens: s.sessionTokens + tokens,
  })),
  resetTurnStats: () => set({ turnStartAt: null, turnTokens: 0 }),

  setRetrying: (retrying) => set({ isRetrying: retrying }),

  // ============================================
  // 会话管理
  // ============================================

  saveSession: () => {
    const { sessionId, tabs, activeTabId, sessions } = get()
    const activeTab = tabs.find((t) => t.id === activeTabId)
    if (!activeTab || activeTab.messages.length === 0) return

    const firstUserMsg = activeTab.messages.find((m) => m.role === 'user')
    const title = firstUserMsg?.content.slice(0, 50) || '新对话'
    const preview = firstUserMsg?.content.slice(0, 100) || ''

    const meta: SessionMeta = {
      id: sessionId,
      title,
      preview,
      messageCount: activeTab.messages.length,
      createdAt: sessions.find((s) => s.id === sessionId)?.createdAt || Date.now(),
      lastActivityAt: Date.now(),
    }

    // 持久化消息内容
    saveMessagesToStorage(sessionId, activeTab.messages)

    const exists = sessions.findIndex((s) => s.id === sessionId)
    const nextSessions = exists >= 0
      ? sessions.map((s, i) => (i === exists ? meta : s))
      : [meta, ...sessions]

    saveSessionsToStorage(nextSessions)
    set({ sessions: nextSessions })
  },

  loadSession: (sessionId) => {
    const { sessions, tabs, activeTabId } = get()
    const session = sessions.find((s) => s.id === sessionId)
    if (!session) return

    // 先保存当前会话的消息
    const currentTab = tabs.find((t) => t.id === activeTabId)
    const { sessionId: currentSessionId } = get()
    if (currentTab && currentTab.messages.length > 0) {
      saveMessagesToStorage(currentSessionId, currentTab.messages)
    }

    // 恢复目标会话的消息
    const savedMessages = loadMessagesFromStorage(sessionId)
    set((s) => ({
      sessionId,
      showHistory: false,
      tabs: updateTab(s.tabs, s.activeTabId, () => ({
        messages: savedMessages,
        isStreaming: false,
        currentAssistantMsgId: null,
        currentThinkingMsgId: null,
        pendingApproval: null,
        pendingQuestion: null,
        title: session.title,
      })),
    }))
  },

  deleteSession: (sessionId) => {
    const { sessions } = get()
    const nextSessions = sessions.filter((s) => s.id !== sessionId)
    saveSessionsToStorage(nextSessions)
    deleteMessagesFromStorage(sessionId)
    set({ sessions: nextSessions })
  },

  loadSessionList: () => {
    set({ sessions: loadSessionsFromStorage() })
  },

  setSessions: (sessions) => set({ sessions }),

  newSession: (bridgeNewSession) => {
    get().saveSession()
    const { activeTabId } = get()
    set((s) => ({
      sessionId: generateSessionId(),
      tabs: updateTab(s.tabs, activeTabId, () => ({
        messages: [],
        isStreaming: false,
        currentAssistantMsgId: null,
        currentThinkingMsgId: null,
        pendingApproval: null,
        pendingQuestion: null,
        title: '新对话',
      })),
      error: null,
    }))
    bridgeNewSession()
  },

  rerollLastMessage: (bridgeSend) => {
    const { activeTabId } = get()
    const tab = get().tabs.find((t) => t.id === activeTabId)
    if (!tab) return

    // 找到最后一条 user 消息
    const lastUserIdx = [...tab.messages].reverse().findIndex((m) => m.role === 'user')
    if (lastUserIdx < 0) return

    const lastUserMsg = [...tab.messages].reverse()[lastUserIdx]
    const actualIdx = tab.messages.length - 1 - lastUserIdx

    // 删除该 user 消息及其之后的所有消息
    const newMessages = tab.messages.slice(0, actualIdx)
    set((s) => ({
      tabs: updateTab(s.tabs, activeTabId, () => ({
        messages: newMessages,
        isStreaming: false,
        currentAssistantMsgId: null,
        currentThinkingMsgId: null,
      })),
    }))

    // 重新发送
    const text = lastUserMsg.content
    const images = lastUserMsg.attachments
      ?.filter((a) => a.type.startsWith('image/') && (a.data || a.previewUrl))
      .map((a) => a.data || a.previewUrl || '')
      .filter(Boolean)

    bridgeSend(text, images)
  },

  editUserMessage: (messageId, newText) => {
    const { activeTabId } = get()
    const tab = get().tabs.find((t) => t.id === activeTabId)
    if (!tab) return

    const msg = tab.messages.find((m) => m.id === messageId)
    if (!msg || msg.role !== 'user') return

    // 将编辑后的内容（或原始内容）放回输入框，删除该消息及其之后的所有消息
    const msgIdx = tab.messages.findIndex((m) => m.id === messageId)
    set((s) => ({
      inputValue: newText ?? msg.content,
      tabs: updateTab(s.tabs, activeTabId, () => ({
        messages: s.tabs.find((t) => t.id === activeTabId)?.messages.slice(0, msgIdx) ?? [],
        isStreaming: false,
        currentAssistantMsgId: null,
        currentThinkingMsgId: null,
      })),
    }))
  },
}));
