/**
 * useKeyboardShortcuts — 全局键盘快捷键管理
 * Cmd/Ctrl+K: 命令面板
 * Escape: 关闭当前面板
 * Cmd/Ctrl+,: 设置
 * Cmd/Ctrl+Shift+N: 新会话
 */

import { useEffect } from 'react'
import { useChatStore } from '@/store/useChatStore'

export function useKeyboardShortcuts() {
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      const tag = (e.target as HTMLElement).tagName
      const isInput = tag === 'INPUT' || tag === 'TEXTAREA'

      // Escape 在输入框内也生效
      if (e.key === 'Escape') {
        const s = useChatStore.getState()
        if (s.showCommandPalette) { s.toggleCommandPalette(); return }
        if (s.showSettings) { s.toggleSettings(); return }
        if (s.showHistory) { s.toggleHistory(); return }
        return
      }

      // 输入框内不触发其他快捷键
      if (isInput) return

      const ctrlOrMeta = e.ctrlKey || e.metaKey

      // Cmd/Ctrl+K → 命令面板
      if (e.key === 'k' && ctrlOrMeta && !e.shiftKey) {
        e.preventDefault()
        useChatStore.getState().toggleCommandPalette()
        return
      }

      // Cmd/Ctrl+, → 设置
      if (e.key === ',' && ctrlOrMeta) {
        e.preventDefault()
        useChatStore.getState().toggleSettings()
        return
      }

      // Cmd/Ctrl+Shift+N → 新会话
      if (e.key === 'N' && ctrlOrMeta && e.shiftKey) {
        e.preventDefault()
        import('@/services/bridgeService').then(({ default: bridge }) => {
          useChatStore.getState().newSession(bridge.newSession.bind(bridge))
        })
        return
      }
    }

    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [])
}
