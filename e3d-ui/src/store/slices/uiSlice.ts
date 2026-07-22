/**
 * UI / 全局动作 Slice
 * 职责：inputValue / 面板开关 / Plan模式 / 审批模式 / sendMessage / clearSession / reroll / edit / rollback / 流式状态
 */

import type { ChatStore, ToolApprovalMode } from '../types';
import type { Attachment } from '../../types';
import { generateMessageId } from '../../types';
import { MessageTypes } from '../../services/messageContracts';
import { updateTab } from './tabSlice';
import type { StateCreator } from 'zustand';

// ============================================
// Slice 定义
// ============================================

export interface UISlice {
  inputValue: string;
  isPlanMode: boolean;
  toolApprovalMode: ToolApprovalMode;
  showSettings: boolean;
  showHistory: boolean;
  showCommandPalette: boolean;
  showSearch: boolean;
  error: string | null;
  isRetrying: boolean;

  // 流式状态
  turnStartAt: number | null;
  turnTokens: number;
  sessionTokens: number;

  // 动作
  setInputValue: (v: string) => void;
  sendMessage: (bridgeSend: (text: string, images?: string[], files?: string[], tabId?: string) => void, attachments?: Attachment[], overrideText?: string) => void;
  clearSession: (bridgeNewSession: () => void) => void;
  toggleSettings: () => void;
  toggleHistory: () => void;
  toggleCommandPalette: () => void;
  toggleSearch: () => void;
  setPlanMode: (enabled: boolean) => void;
  togglePlanMode: () => void;
  setToolApprovalMode: (mode: ToolApprovalMode) => void;
  rerollLastMessage: (bridgeSend: (text: string, images?: string[]) => void) => void;
  editUserMessage: (messageId: string, newText?: string) => void;
  rollbackToMessage: (messageId: string) => void;
  setRetrying: (retrying: boolean) => void;
  setTurnStart: (timestamp: number | null) => void;
  addTurnTokens: (tokens: number) => void;
  resetTurnStats: () => void;
}

export const createUISlice: StateCreator<ChatStore, [], [], UISlice> = (set, get) => ({
  inputValue: '',
  isPlanMode: false,
  toolApprovalMode: 'ask' as ToolApprovalMode,
  showSettings: false,
  showHistory: false,
  showCommandPalette: false,
  showSearch: false,
  error: null,
  isRetrying: false,
  turnStartAt: null,
  turnTokens: 0,
  sessionTokens: 0,

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

    const userMsg: import('../../types').Message = {
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
  toggleSearch: () => set((s) => ({ showSearch: !s.showSearch })),

  setPlanMode: (enabled) => set({ isPlanMode: enabled }),
  togglePlanMode: () => {
    const newMode = !get().isPlanMode
    set({ isPlanMode: newMode })
    // 通知后端
    import('@/services/bridgeService').then(({ default: bridge }) => {
      bridge.send(MessageTypes.UserSetPlanMode, { mode: newMode ? 'plan' : 'act' })
    })
  },

  setToolApprovalMode: (mode) => {
    set({ toolApprovalMode: mode })
    // 通知后端
    import('@/services/bridgeService').then(({ default: bridge }) => {
      bridge.send(MessageTypes.UserSetApprovalMode, { mode })
    })
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

  rollbackToMessage: (messageId) => {
    const { activeTabId } = get()
    set((s) => ({
      tabs: updateTab(s.tabs, activeTabId, () => ({
        messages: s.tabs.find((t) => t.id === activeTabId)?.messages
          .slice(0, s.tabs.find((t) => t.id === activeTabId)?.messages.findIndex((m) => m.id === messageId)) ?? [],
        isStreaming: false,
        currentAssistantMsgId: null,
        currentThinkingMsgId: null,
      })),
    }))
  },

  setRetrying: (retrying) => set({ isRetrying: retrying }),

  // === 流式状态 ===
  setTurnStart: (timestamp) => set({ turnStartAt: timestamp }),
  addTurnTokens: (tokens) => set((s) => ({
    turnTokens: s.turnTokens + tokens,
    sessionTokens: s.sessionTokens + tokens,
  })),
  resetTurnStats: () => set({ turnStartAt: null, turnTokens: 0 }),
})
