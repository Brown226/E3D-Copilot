/**
 * Provider / Model 管理 Slice
 * 职责：switchModel / loadProviders / setConfig / setBridgeConnected / setLastPingTime
 */

import type { ChatStore } from '../types';
import type { ProviderInfo } from '../../services/messageContracts';
import type { StateCreator } from 'zustand';
import { useToastStore } from '../useToastStore';

/**
 * 统一错误提示。
 * 此前 state.error 只有写入方、没有任何组件渲染，所有失败对用户都是静默的。
 * 这里直接弹 Toast，保证失败一定可见。
 */
function notifyError(message: string): void {
  try {
    useToastStore.getState().addToast('error', message);
  } catch {
    /* Toast store 不可用时忽略，不能因为提示失败影响主流程 */
  }
}

// ============================================
// Slice 定义
// ============================================

export interface ProviderSlice {
  currentProvider: string;
  currentModel: string;
  providers: ProviderInfo[];
  models: { ref: string; provider: string; model: string; current: boolean }[];
  isLoadingModels: boolean;
  bridgeConnected: boolean;
  lastPingTime: number | null;

  switchModel: (ref: string, bridgeSwitchModel: (ref: string) => Promise<unknown>) => Promise<void>;
  loadProviders: (bridgeListProviders: () => Promise<unknown>) => Promise<void>;
  setBridgeConnected: (connected: boolean) => void;
  setLastPingTime: (time: number) => void;
  setConfig: (config: { currentProvider: string; currentModel: string; providers: ProviderInfo[] }) => void;
}

export const createProviderSlice: StateCreator<ChatStore, [], [], ProviderSlice> = (set) => ({
  currentProvider: '',
  currentModel: '',
  providers: [],
  models: [],
  isLoadingModels: false,
  bridgeConnected: false,
  lastPingTime: null,

  switchModel: async (ref, bridgeSwitchModel) => {
    try {
      set({ isLoadingModels: true });
      const result = await bridgeSwitchModel(ref) as { success?: boolean; error?: string } | null;

      // 后端切换失败时返回 success:false（不抛异常）。
      // 原实现不看返回值，UI 会显示已切换，实际后端仍是旧模型。
      if (result && result.success === false) {
        const msg = result.error || '切换模型失败';
        set({ error: msg, isLoadingModels: false });
        notifyError(msg);
        return;
      }

      const [provider, ...modelParts] = ref.split('/');
      const model = modelParts.join('/');
      set({
        currentProvider: provider,
        currentModel: model,
        isLoadingModels: false,
      });
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Failed to switch model';
      // error 字段此前没有任何组件渲染，用户完全看不到失败。
      set({ error: msg, isLoadingModels: false });
      notifyError(msg);
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
      const msg = err instanceof Error ? err.message : 'Failed to load providers';
      set({ error: msg, isLoadingModels: false });
      notifyError(msg);
    }
  },

  setBridgeConnected: (connected) => set({ bridgeConnected: connected }),
  setLastPingTime: (time) => set({ lastPingTime: time }),
  setConfig: (config) => set({
    currentProvider: config.currentProvider,
    currentModel: config.currentModel,
    providers: config.providers,
  }),
})
