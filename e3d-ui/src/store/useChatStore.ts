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
import type { Message } from '../types';
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

// 稳定空数组引用：当 tab 不存在时返回此常量，避免每次创建新数组触发 React error #185
const EMPTY_MESSAGES: Message[] = [];

/**
 * useActiveTab — 获取当前 activeTab 的关键字段
 *
 * Zustand v5 + React 18 注意：选择器不能返回新对象字面量，
 * 否则 useSyncExternalStore 检测到 snapshot 不稳定 → error #185。
 * 这里拆分为多个独立选择器，每个返回基本类型或稳定引用。
 */
export function useActiveTab() {
  const messages = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.messages ?? EMPTY_MESSAGES);
  const isStreaming = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.isStreaming ?? false);
  const pendingApproval = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.pendingApproval ?? null);
  const pendingQuestion = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.pendingQuestion ?? null);
  return { messages, isStreaming, pendingApproval, pendingQuestion };
}

// ============================================
// Re-export 类型（保持外部 import 兼容）
// ============================================

export type { ChatStore, Tab, SessionMeta, PendingApproval, PendingQuestion, ToolApprovalMode } from './types';
