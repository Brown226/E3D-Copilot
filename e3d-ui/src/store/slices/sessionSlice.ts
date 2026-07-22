/**
 * 会话管理 Slice
 * 职责：localStorage 持久化 + saveSession / loadSession / deleteSession / loadSessionList / setSessions / newSession
 */

import type { ChatStore, SessionMeta } from '../types';
import type { Message } from '../../types';
import { updateTab } from './tabSlice';
import type { StateCreator } from 'zustand';

// ============================================
// localStorage 辅助
// ============================================

const STORAGE_KEY = 'e3d-chat-sessions'
const MESSAGES_KEY_PREFIX = 'e3d-session-msgs-'
const MAX_SESSIONS = 100

export function generateSessionId(): string {
  return `session_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`
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
// Slice 定义
// ============================================

export interface SessionSlice {
  sessionId: string;
  sessions: SessionMeta[];
  saveSession: () => void;
  loadSession: (sessionId: string) => void;
  deleteSession: (sessionId: string) => void;
  loadSessionList: () => void;
  setSessions: (sessions: SessionMeta[]) => void;
  newSession: (bridgeNewSession: () => void) => void;
}

export const createSessionSlice: StateCreator<ChatStore, [], [], SessionSlice> = (set, get) => ({
  sessionId: generateSessionId(),
  sessions: loadSessionsFromStorage(),

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

  setSessions: (sessions) => {
    saveSessionsToStorage(sessions)
    set({ sessions })
  },

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
})
