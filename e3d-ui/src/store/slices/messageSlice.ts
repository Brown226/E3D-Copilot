/**
 * 消息操作 Slice
 * 职责：appendMessage / startStreaming / appendAssistantDelta / finalize / thinking / tool / approval / question
 */

import type { ChatStore, PendingApproval, PendingQuestion } from '../types';
import type { Message, MessageRole } from '../../types';
import { generateMessageId } from '../../types';
import { updateTab } from './tabSlice';
import type { StateCreator } from 'zustand';

// ============================================
// Slice 定义
// ============================================

export interface MessageSlice {
  /** 当前正在接收后端事件的 Tab ID（锁定机制） */
  streamingTabId: string | null;

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
}

export const createMessageSlice: StateCreator<ChatStore, [], [], MessageSlice> = (set, get) => ({
  streamingTabId: null,

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
    set((s) => ({
      streamingTabId: targetId,  // 锁定：后续事件路由到此 Tab，即使用户切换 Tab
      tabs: updateTab(s.tabs, targetId, () => ({
        isStreaming: true,
        currentAssistantMsgId: existingTab?.currentAssistantMsgId ?? null,
        currentThinkingMsgId: null,
      })),
    }))
  },

  appendAssistantDelta: (delta, tabId) => {
    const targetId = tabId || get().activeTabId

    // 过滤空 delta，避免无意义的状态更新
    if (!delta || delta === '') return

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

  handleToolResult: (toolId, result, error, tabId, durationMs, meta) => {
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
                      toolMeta: meta ?? m.toolMeta,
                      finalized: true,
                    }
                  : m
              ),
            }
          : t
      ),
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

  stopStreaming: (tabId) => {
    const targetId = tabId || get().activeTabId
    set((s) => ({
      streamingTabId: null,  // 解锁：流式结束，清除锁定
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

  setMessageAgentName: (toolId, agentName, tabId) => {
    const targetId = tabId || get().activeTabId
    set((s) => ({
      tabs: s.tabs.map((t) =>
        t.id === targetId
          ? {
              ...t,
              messages: t.messages.map((m) =>
                m.toolId === toolId ? { ...m, agentName } : m
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
})
