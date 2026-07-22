/**
 * Provider / Model 管理 Slice
 * 职责：switchModel / loadProviders / setConfig / setBridgeConnected / setLastPingTime
 */

import type { ChatStore } from '../types';
import type { ProviderInfo } from '../../services/messageContracts';
import type { StateCreator } from 'zustand';

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
})
