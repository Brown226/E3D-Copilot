/**
 * Header — 紧凑型插件顶栏
 * 高度压缩到 py-1.5，Logo 缩小，模型名和连接状态合并一行
 * 所有按钮 icon-only
 */

import { useState, useEffect, useCallback } from 'react'
import { Bot, Settings, Clock, Plus, Sun, Moon } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'

type Theme = 'light' | 'dark' | 'system'

function getStoredTheme(): Theme {
  try { return (localStorage.getItem('e3d-theme') as Theme) || 'dark' } catch { return 'dark' }
}

function isDarkActive(): boolean {
  const t = getStoredTheme()
  return t === 'dark' || (t === 'system' && window.matchMedia('(prefers-color-scheme:dark)').matches)
}

function applyTheme(theme: Theme) {
  const root = document.documentElement
  const dark = theme === 'dark' || (theme === 'system' && window.matchMedia('(prefers-color-scheme:dark)').matches)
  root.classList.toggle('dark', dark)
  root.classList.toggle('light', !dark)
  localStorage.setItem('e3d-theme', theme)
}

export function Header() {
  const currentModel = useChatStore((s) => s.currentModel)
  const bridgeConnected = useChatStore((s) => s.bridgeConnected)
  const toggleSettings = useChatStore((s) => s.toggleSettings)
  const toggleHistory = useChatStore((s) => s.toggleHistory)
  const newSession = useChatStore((s) => s.newSession)
  const sessions = useChatStore((s) => s.sessions)

  const [dark, setDark] = useState(isDarkActive)

  // 监听系统主题变化（当用户选了 system 时）
  useEffect(() => {
    const mq = window.matchMedia('(prefers-color-scheme: dark)')
    const handler = () => { if (getStoredTheme() === 'system') setDark(isDarkActive()) }
    mq.addEventListener('change', handler)
    return () => mq.removeEventListener('change', handler)
  }, [])

  // 监听其他地方的主题变更（如设置面板、命令面板）
  useEffect(() => {
    const handler = () => setDark(isDarkActive())
    window.addEventListener('theme-changed', handler)
    return () => window.removeEventListener('theme-changed', handler)
  }, [])

  const handleToggleTheme = useCallback(() => {
    const next: Theme = dark ? 'light' : 'dark'
    applyTheme(next)
    setDark(!dark)
    window.dispatchEvent(new Event('theme-changed'))
  }, [dark])

  const handleNewSession = () => {
    import('@/services/bridgeService').then(({ default: bridge }) => {
      useChatStore.getState().saveSession()
      newSession(bridge.newSession.bind(bridge))
    })
  }

  return (
    <header className="bg-white/80 backdrop-blur-sm border-b border-slate-200 px-2.5 py-1.5 dark:bg-slate-900/80 dark:border-slate-700 shrink-0">
      <div className="flex items-center justify-between">
        {/* 左侧：Logo + 标题 + 状态 */}
        <div className="flex items-center gap-2 min-w-0">
          <div className="w-6 h-6 rounded-lg bg-blue-600 flex items-center justify-center shrink-0">
            <Bot className="w-3.5 h-3.5 text-white" />
          </div>
          <div className="min-w-0">
            <h1 className="text-sm font-semibold text-slate-800 dark:text-slate-100 leading-tight">E小智</h1>
            <div className="flex items-center gap-1 text-[11px] text-slate-400 dark:text-slate-500 leading-tight">
              <span className={`w-1.5 h-1.5 rounded-full shrink-0 ${bridgeConnected ? 'bg-emerald-500' : 'bg-red-500'}`} />
              <span className="truncate">{currentModel || (bridgeConnected ? '已连接' : '未连接')}</span>
            </div>
          </div>
        </div>

        {/* 右侧：icon-only 按钮 */}
        <div className="flex items-center gap-0.5 shrink-0">
          <button
            onClick={handleNewSession}
            className="p-1.5 text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-700 rounded-md transition-colors"
            title="新建对话"
          >
            <Plus className="w-4 h-4" />
          </button>

          <button
            onClick={toggleHistory}
            className="relative p-1.5 text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-700 rounded-md transition-colors"
            title="对话历史"
          >
            <Clock className="w-4 h-4" />
            {sessions.length > 0 && (
              <span className="absolute -top-0.5 -right-0.5 min-w-[14px] h-[14px] rounded-full bg-blue-500 text-white text-[9px] flex items-center justify-center px-0.5">
                {sessions.length > 99 ? '99+' : sessions.length}
              </span>
            )}
          </button>

          <button
            onClick={handleToggleTheme}
            className="p-1.5 text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-700 rounded-md transition-colors"
            title={dark ? '切换到亮色' : '切换到暗色'}
          >
            {dark ? <Sun className="w-4 h-4" /> : <Moon className="w-4 h-4" />}
          </button>

          <button
            onClick={toggleSettings}
            className="p-1.5 text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-700 rounded-md transition-colors"
            title="设置"
          >
            <Settings className="w-4 h-4" />
          </button>
        </div>
      </div>
    </header>
  )
}
