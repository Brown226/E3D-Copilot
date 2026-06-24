/**
 * CommandPalette — Cmd+K 命令面板
 * 快速执行常用操作：新会话、设置、历史、切换模型、切换主题
 */

import { useState, useEffect, useRef, useMemo } from 'react'
import { Search, MessageSquarePlus, Settings, History, Bot, Moon, Sun } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'

interface CommandItem {
  id: string
  label: string
  description?: string
  icon: React.ReactNode
  action: () => void
  keywords: string[]
}

export function CommandPalette() {
  const show = useChatStore((s) => s.showCommandPalette)
  const toggle = useChatStore((s) => s.toggleCommandPalette)
  const [query, setQuery] = useState('')
  const inputRef = useRef<HTMLInputElement>(null)
  const [selectedIdx, setSelectedIdx] = useState(0)

  const commands = useMemo<CommandItem[]>(() => [
    {
      id: 'new-session',
      label: '新会话',
      description: '开始一段新的对话',
      icon: <MessageSquarePlus className="w-4 h-4" />,
      action: () => {
        import('@/services/bridgeService').then(({ default: bridge }) => {
          useChatStore.getState().newSession(bridge.newSession.bind(bridge))
        })
        toggle()
      },
      keywords: ['new', 'session', '对话', '新建'],
    },
    {
      id: 'settings',
      label: '设置',
      description: '打开设置面板',
      icon: <Settings className="w-4 h-4" />,
      action: () => { useChatStore.getState().toggleSettings(); toggle() },
      keywords: ['settings', '设置', '配置'],
    },
    {
      id: 'history',
      label: '历史记录',
      description: '查看会话历史',
      icon: <History className="w-4 h-4" />,
      action: () => { useChatStore.getState().toggleHistory(); toggle() },
      keywords: ['history', '历史', '记录'],
    },
    {
      id: 'switch-model',
      label: '切换模型',
      description: '选择不同的 AI 模型',
      icon: <Bot className="w-4 h-4" />,
      action: () => { useChatStore.getState().toggleSettings(); toggle() },
      keywords: ['model', '模型', '切换', 'switch'],
    },
    {
      id: 'toggle-theme',
      label: '切换主题',
      description: '在亮色/暗色主题间切换',
      icon: document.documentElement.classList.contains('dark')
        ? <Sun className="w-4 h-4" />
        : <Moon className="w-4 h-4" />,
      action: () => {
        document.documentElement.classList.toggle('dark')
        localStorage.setItem('e3d-theme', document.documentElement.classList.contains('dark') ? 'dark' : 'light')
        toggle()
      },
      keywords: ['theme', '主题', '暗色', '亮色', 'dark', 'light'],
    },
  ], [toggle])

  const filtered = useMemo(() => {
    if (!query.trim()) return commands
    const q = query.toLowerCase()
    return commands.filter((c) =>
      c.label.toLowerCase().includes(q) ||
      c.description?.toLowerCase().includes(q) ||
      c.keywords.some((k) => k.includes(q))
    )
  }, [query, commands])

  useEffect(() => {
    if (show) {
      setQuery('')
      setSelectedIdx(0)
      setTimeout(() => inputRef.current?.focus(), 50)
    }
  }, [show])

  useEffect(() => { setSelectedIdx(0) }, [query])

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setSelectedIdx((i) => Math.min(i + 1, filtered.length - 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setSelectedIdx((i) => Math.max(i - 1, 0))
    } else if (e.key === 'Enter' && filtered[selectedIdx]) {
      filtered[selectedIdx].action()
    }
  }

  if (!show) return null

  return (
    <div className="fixed inset-0 z-[100] flex items-start justify-center pt-[20vh]">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={toggle} />
      <div className="relative w-full max-w-lg bg-white dark:bg-slate-800 rounded-2xl shadow-2xl border border-slate-200 dark:border-slate-700 overflow-hidden animate-in zoom-in-95 fade-in duration-150">
        {/* 搜索框 */}
        <div className="flex items-center gap-3 px-4 py-3 border-b border-slate-200 dark:border-slate-700">
          <Search className="w-5 h-5 text-slate-400" />
          <input
            ref={inputRef}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="搜索命令..."
            className="flex-1 bg-transparent outline-none text-slate-800 dark:text-slate-100 placeholder-slate-400 text-sm"
          />
          <kbd className="text-xs text-slate-400 bg-slate-100 dark:bg-slate-700 px-1.5 py-0.5 rounded">ESC</kbd>
        </div>

        {/* 命令列表 */}
        <div className="max-h-[300px] overflow-y-auto py-1">
          {filtered.length === 0 ? (
            <div className="px-4 py-8 text-center text-sm text-slate-400">无匹配命令</div>
          ) : (
            filtered.map((cmd, i) => (
              <button
                key={cmd.id}
                onClick={cmd.action}
                className={`w-full flex items-center gap-3 px-4 py-2.5 text-left transition-colors ${
                  i === selectedIdx
                    ? 'bg-blue-50 dark:bg-blue-900/30'
                    : 'hover:bg-slate-50 dark:hover:bg-slate-700/50'
                }`}
              >
                <span className="text-slate-500 dark:text-slate-400">{cmd.icon}</span>
                <div className="flex-1 min-w-0">
                  <div className="text-sm font-medium text-slate-800 dark:text-slate-200">{cmd.label}</div>
                  {cmd.description && (
                    <div className="text-xs text-slate-400 dark:text-slate-500 truncate">{cmd.description}</div>
                  )}
                </div>
              </button>
            ))
          )}
        </div>
      </div>
    </div>
  )
}
