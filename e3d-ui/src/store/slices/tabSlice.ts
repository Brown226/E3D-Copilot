/**
 * Tab 管理 Slice
 * 职责：createTab / closeTab / setActiveTab / updateTabTitle
 */

import type { ChatStore, Tab } from '../types';
import type { StateCreator } from 'zustand';

// ============================================
// 辅助函数
// ============================================

export function generateTabId(): string {
  return `tab_${Date.now()}_${Math.random().toString(36).slice(2, 6)}`
}

export function createTab(title = '新对话'): Tab {
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

/** 更新指定 tab 的状态（不可变） */
export function updateTab(tabs: Tab[], tabId: string, updater: (tab: Tab) => Partial<Tab>): Tab[] {
  return tabs.map((t) => t.id === tabId ? { ...t, ...updater(t) } : t)
}

// ============================================
// Slice 定义
// ============================================

export interface TabSlice {
  tabs: Tab[];
  activeTabId: string;
  createTab: (title?: string) => string;
  closeTab: (tabId: string) => void;
  setActiveTab: (tabId: string) => void;
  updateTabTitle: (tabId: string, title: string) => void;
}

const initialTab = createTab();

export const createTabSlice: StateCreator<ChatStore, [], [], TabSlice> = (set) => ({
  tabs: [initialTab],
  activeTabId: initialTab.id,

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
})
