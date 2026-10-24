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

/** 本地会话 id 前缀（由 generateSessionId 生成）。后端 tab 会话用 GUID，可据此区分。 */
const LOCAL_SESSION_PREFIX = 'session_'

/**
 * 只持久化「本地产生的」会话。
 *
 * 原实现把合并后的完整列表（含后端 GUID 条目）整份写入 localStorage，
 * 于是同一段对话在列表里出现两份：后端一份、本地缓存一份。
 * 用户删掉本地那份，后端那份仍在，重开面板又刷出来 —— 表现为「删不掉」。
 */
function saveSessionsToStorage(sessions: SessionMeta[]) {
  const localOnly = sessions.filter((s) => s.id.startsWith(LOCAL_SESSION_PREFIX))
  localStorage.setItem(STORAGE_KEY, JSON.stringify(localOnly.slice(0, MAX_SESSIONS)))
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

    // 用 Tab 自己的 sessionId，而不是全局那个：
    // 多个 Tab 共用全局 id 会写同一份存储，后结束的一轮覆盖前一个 Tab。
    const tabSessionId = activeTab.sessionId || sessionId

    const firstUserMsg = activeTab.messages.find((m) => m.role === 'user')
    const title = firstUserMsg?.content.slice(0, 50) || '新对话'
    const preview = firstUserMsg?.content.slice(0, 100) || ''

    const meta: SessionMeta = {
      id: tabSessionId,
      title,
      preview,
      messageCount: activeTab.messages.length,
      createdAt: sessions.find((s) => s.id === tabSessionId)?.createdAt || Date.now(),
      lastActivityAt: Date.now(),
    }

    // 持久化消息内容
    saveMessagesToStorage(tabSessionId, activeTab.messages)

    const exists = sessions.findIndex((s) => s.id === tabSessionId)
    const nextSessions = exists >= 0
      ? sessions.map((s, i) => (i === exists ? meta : s))
      : [meta, ...sessions]

    saveSessionsToStorage(nextSessions)
    // 同步全局 sessionId，保持 loadSession / deleteSession 的比较语义一致
    set({ sessions: nextSessions, sessionId: tabSessionId })
  },

  loadSession: (sessionId) => {
    const { sessions, tabs, activeTabId } = get()
    const session = sessions.find((s) => s.id === sessionId)
    if (!session) return

    // 先保存当前会话的消息（写回它自己的 id，不能写到全局 id 上）
    const currentTab = tabs.find((t) => t.id === activeTabId)
    if (currentTab && currentTab.messages.length > 0) {
      saveMessagesToStorage(currentTab.sessionId || get().sessionId, currentTab.messages)
    }

    // 恢复目标会话的消息
    const savedMessages = loadMessagesFromStorage(sessionId)
    set((s) => ({
      sessionId,
      showHistory: false,
      // 清空输入框与残留交互态，避免把上一个会话的草稿带进历史会话
      inputValue: '',
      tabs: updateTab(s.tabs, s.activeTabId, () => ({
        messages: savedMessages,
        sessionId,
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
    // 若删的是当前会话，清空 store 里的 sessionId，避免后续 saveSession()
    // 继续以已删除的 id 写入，把条目又「复活」出来。
    const patch: { sessions: SessionMeta[]; sessionId?: string } = { sessions: nextSessions }
    if (get().sessionId === sessionId) patch.sessionId = generateSessionId()
    set(patch)
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
    const nextSessionId = generateSessionId()
    set((s) => ({
      sessionId: nextSessionId,
      inputValue: '',
      tabs: updateTab(s.tabs, activeTabId, () => ({
        messages: [],
        sessionId: nextSessionId,
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
