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
function saveSessionsToStorage(sessions: SessionMeta[]): boolean {
  const localOnly = sessions.filter((s) => s.id.startsWith(LOCAL_SESSION_PREFIX))
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(localOnly.slice(0, MAX_SESSIONS)))
    return true
  } catch (err) {
    // 配额写满时 setItem 会抛 QuotaExceededError。
    // 这里必须吞掉：调用方（deleteSession / setSessions）已经把内存状态改好了，
    // 让异常冒出去会让「删除」在更新 UI 之前中断，表现为点了删除毫无反应。
    console.warn('[session] 会话列表持久化失败（localStorage 可能已满）', err)
    return false
  }
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
  /** 批量删除。删除后会给挂载这些会话的 Tab 换新 id，避免下一轮保存复活条目。 */
  /** 批量删除，返回实际删除的所有 id（含同名重复条目），供调用方同步后端 */
  deleteSessions: (sessionIds: string[]) => string[];
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

    // 先更新内存再持久化：存储写满时不能让会话列表更新也一起失败
    set({ sessions: nextSessions, sessionId: tabSessionId })
    saveSessionsToStorage(nextSessions)
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
    get().deleteSessions([sessionId])
  },


  deleteSessions: (ids) => {
    const { sessions, tabs, activeTabId } = get()
    const drop = new Set(ids)
    if (drop.size === 0) return []

    // 同一段对话可能有两份：后端 tab 会话（GUID）与前端本地缓存（session_），
    // 两边 id 永不相等，只按 id 删会留下一份，重开面板又刷出来。
    //
    // 但只在「一个本地、一个后端」之间认定副本：两条都是本地会话时，
    // 即使标题和条数相同也是两段独立对话（例如两次都叫「工具测试。」），不能互相牵连误删。
    const isLocal = (id: string) => id.startsWith(LOCAL_SESSION_PREFIX)
    for (const t of sessions) {
      if (!drop.has(t.id)) continue
      for (const s of sessions) {
        if (isLocal(s.id) === isLocal(t.id)) continue
        if (s.title === t.title && s.messageCount === t.messageCount) drop.add(s.id)
      }
    }

    const nextSessions = sessions.filter((s) => !drop.has(s.id))
    // 先改内存状态，再做持久化：即使 localStorage 写失败，UI 也必须真的删掉。
    const patch: { sessions: SessionMeta[]; sessionId?: string; tabs?: typeof tabs } = {
      sessions: nextSessions,
    }

    // 被删的会话若仍挂在某个 Tab 上，必须给那个 Tab 换新 id。
    // 否则下一轮 TurnDone 的 saveSession() 会以旧 id 再写一条，条目「复活」。
    let nextTabs = tabs
    const activeTabBefore = tabs.find((t) => t.id === activeTabId)
    for (const t of tabs) {
      if (t.sessionId && drop.has(t.sessionId)) {
        nextTabs = nextTabs.map((x) => (x.id === t.id ? { ...x, sessionId: generateSessionId() } : x))
      }
    }
    if (get().sessionId && drop.has(get().sessionId)) {
      patch.sessionId = generateSessionId()
    }
    // 当前 Tab 换了 id 时，全局 sessionId 跟随它，保持 loadSession/deleteSession 的比较语义一致
    const activeTabAfter = nextTabs.find((t) => t.id === activeTabId)
    if (activeTabBefore && activeTabAfter && activeTabBefore.sessionId !== activeTabAfter.sessionId) {
      patch.sessionId = activeTabAfter.sessionId
    }
    if (nextTabs !== tabs) patch.tabs = nextTabs

    set(patch)

    // 持久化（失败不阻断删除）
    saveSessionsToStorage(nextSessions)
    for (const id of drop) deleteMessagesFromStorage(id)

    return [...drop]
  },

  loadSessionList: () => {
    set({ sessions: loadSessionsFromStorage() })
  },

  setSessions: (sessions) => {
    // 先改内存状态，再做持久化：存储写满时不能让列表更新也一起失败。
    set({ sessions })
    saveSessionsToStorage(sessions)
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
