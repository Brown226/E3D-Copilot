/**
 * E小智 v2.0 聊天状态管理（Zustand）— 多 Tab 版本
 *
 * 组合入口：将各 slice 合并为单一 store，外部 API 不变。
 * 各 slice 位于 ./slices/ 目录：
 *   - tabSlice: Tab 增删切换
 *   - messageSlice: 消息追加/流式/工具/审批
 *   - sessionSlice: 会话持久化
 *   - providerSlice: Provider/Model 管理
 *   - uiSlice: 输入/面板/模式/流式状态
 */

import { create } from 'zustand';
import type { ChatStore } from './types';
import { createTabSlice } from './slices/tabSlice';
import { createMessageSlice } from './slices/messageSlice';
import { createSessionSlice } from './slices/sessionSlice';
import { createProviderSlice } from './slices/providerSlice';
import { createUISlice } from './slices/uiSlice';

// ============================================
// Store 组合
// ============================================

export const useChatStore = create<ChatStore>()((set, get, api) => ({
  ...createTabSlice(set, get, api),
  ...createMessageSlice(set, get, api),
  ...createSessionSlice(set, get, api),
  ...createProviderSlice(set, get, api),
  ...createUISlice(set, get, api),
}));

// ============================================
// 派生 hooks
// ============================================

/**
 * useActiveTab — 单个选择器获取当前 activeTab 的关键字段
 * 避免多个组件各自重复 `s.tabs.find(t => t.id === s.activeTabId)?.xxx`
 */
export function useActiveTab() {
  return useChatStore((s) => {
    const tab = s.tabs.find((t) => t.id === s.activeTabId)
    return {
      messages: tab?.messages ?? [],
      isStreaming: tab?.isStreaming ?? false,
      pendingApproval: tab?.pendingApproval ?? null,
      pendingQuestion: tab?.pendingQuestion ?? null,
    }
  })
}

// ============================================
// Re-export 类型（保持外部 import 兼容）
// ============================================

export type { ChatStore, Tab, SessionMeta, PendingApproval, PendingQuestion, ToolApprovalMode } from './types';
