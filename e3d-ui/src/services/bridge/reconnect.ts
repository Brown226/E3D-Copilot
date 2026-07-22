/**
 * 断连重连逻辑
 * 职责：host:ready 超时检测 + 自动 ping 重连 + 连接成功后清理定时器
 */

import { useChatStore } from '../../store/useChatStore';
import type { Bridge } from './BridgeCore';

let hostReadyTimer: ReturnType<typeof setTimeout> | null = null;
let autoReconnectTimer: ReturnType<typeof setInterval> | null = null;

function startAutoReconnect(bridge: Bridge) {
  if (autoReconnectTimer) return
  autoReconnectTimer = setInterval(async () => {
    const state = useChatStore.getState()
    if (state.bridgeConnected) {
      // 已连接，停止重试
      if (autoReconnectTimer) { clearInterval(autoReconnectTimer); autoReconnectTimer = null }
      return
    }
    try {
      await bridge.ping()
      // ping 成功，标记为已连接
      useChatStore.getState().setBridgeConnected(true)
      if (autoReconnectTimer) { clearInterval(autoReconnectTimer); autoReconnectTimer = null }
    } catch {
      // ping 失败，继续重试
    }
  }, 3000)  // 每 3 秒重试一次
}

/**
 * 初始化重连机制
 * - WebView2 模式：等待 host:ready，超时后自动 ping 重连
 * - Standalone 模式：由外部调用 startMock
 */
export function initReconnect(bridge: Bridge): void {
  if (bridge.isAvailable()) {
    // WebView2 模式：等待 C# 发送 host:ready
    hostReadyTimer = setTimeout(() => {
      const state = useChatStore.getState()
      if (!state.bridgeConnected) {
        console.warn('[Bridge] host:ready 未在 5 秒内收到，启动自动重连')
        startAutoReconnect(bridge)
      }
    }, 5000)
  }

  // 监听 host:ready 后取消超时定时器和自动重连
  bridge.onHostReady(() => {
    if (hostReadyTimer) {
      clearTimeout(hostReadyTimer)
      hostReadyTimer = null
    }
    if (autoReconnectTimer) {
      clearInterval(autoReconnectTimer)
      autoReconnectTimer = null
    }
  })
}
