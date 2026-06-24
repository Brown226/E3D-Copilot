/**
 * DisconnectScreen — 内联断连提示条
 * 不全屏遮挡，改为顶部红色条幅
 */

import { useState, useCallback } from 'react'
import { WifiOff, RefreshCw } from 'lucide-react'
import { useChatStore } from '../store/useChatStore'

export function DisconnectScreen() {
  const bridgeConnected = useChatStore((s) => s.bridgeConnected)
  const [isReconnecting, setIsReconnecting] = useState(false)

  const handleReconnect = useCallback(async () => {
    setIsReconnecting(true)
    try {
      const { default: bridge } = await import('../services/bridgeService')
      await bridge.ping()
      useChatStore.getState().setBridgeConnected(true)
    } catch {
      useChatStore.getState().setBridgeConnected(false)
    } finally {
      setIsReconnecting(false)
    }
  }, [])

  // 连接正常时不渲染
  if (bridgeConnected) return null

  return (
    <div className="bg-amber-500 dark:bg-amber-600 text-white px-3 py-1.5 flex items-center gap-2 text-xs shrink-0">
      <WifiOff className="w-3.5 h-3.5 shrink-0" />
      <span className="flex-1">E3D 未连接</span>
      <button
        onClick={handleReconnect}
        disabled={isReconnecting}
        className="flex items-center gap-1 px-2 py-0.5 bg-white/20 hover:bg-white/30 rounded text-[11px] font-medium transition-colors disabled:opacity-50"
      >
        <RefreshCw className={`w-3 h-3 ${isReconnecting ? 'animate-spin' : ''}`} />
        重连
      </button>
    </div>
  )
}
