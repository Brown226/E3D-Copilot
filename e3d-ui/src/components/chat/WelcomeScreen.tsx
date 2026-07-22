/**
 * WelcomeScreen — 空消息时显示品牌欢迎页
 * 紧凑布局 + QuickAction + 快捷键引导
 */

import { Bot, Clock } from 'lucide-react'
import { useChatStore, useActiveTab } from '@/store/useChatStore'

const QUICK_ACTIONS = [
  { icon: '📋', text: '查询所有设备' },
  { icon: '🔧', text: '创建一根管道' },
  { icon: '📐', text: '导入 CAD 图纸' },
  { icon: '🖼️', text: '生成 ISO 管道图' },
  { icon: '🏗️', text: '结构出图 DXF' },
  { icon: '📊', text: '查看当前元素属性' },
]

export function WelcomeScreen() {
  const { messages, isStreaming } = useActiveTab()
  const setInputValue = useChatStore((s) => s.setInputValue)
  const sessions = useChatStore((s) => s.sessions)
  const loadSession = useChatStore((s) => s.loadSession)

  if (messages.length > 0 || isStreaming) return null

  const handleQuickAction = (text: string) => {
    setInputValue(text)
    import('@/services/bridgeService').then(({ default: bridge }) => {
      useChatStore.getState().sendMessage(bridge.sendUserMessage.bind(bridge))
    })
  }

  return (
    <div className="flex flex-col items-center justify-center flex-1 px-4 py-8 space-y-5">
      {/* Logo — 渐变品牌色 */}
      <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-blue-600 to-cyan-500 flex items-center justify-center shadow-lg shadow-blue-500/20">
        <Bot className="w-6 h-6 text-white" />
      </div>

      {/* 标题 */}
      <div className="text-center space-y-1">
        <h2 className="text-lg font-bold text-slate-800 dark:text-slate-100">你好，我是 E小智</h2>
        <p className="text-sm text-slate-500 dark:text-slate-400">E3D 工厂设计智能助手</p>
      </div>

      {/* Quick Actions — 3列网格 */}
      <div className="grid grid-cols-3 gap-2 w-full max-w-sm">
        {QUICK_ACTIONS.map((action) => (
          <button
            key={action.text}
            onClick={() => handleQuickAction(action.text)}
            className="flex flex-col items-center gap-1.5 px-2 py-3 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-xs text-slate-700 dark:text-slate-300 hover:border-blue-300 dark:hover:border-blue-500 hover:bg-blue-50 dark:hover:bg-slate-700 hover:scale-[1.02] active:scale-[0.98] transition-all"
          >
            <span className="text-base">{action.icon}</span>
            <span className="truncate w-full text-center">{action.text}</span>
          </button>
        ))}
      </div>

      {/* 快捷键提示 */}
      <div className="flex flex-wrap justify-center gap-3 text-[11px] text-slate-400 dark:text-slate-500">
        <span className="flex items-center gap-1">
          <kbd className="px-1 py-0.5 bg-slate-100 dark:bg-slate-800 rounded text-[10px] font-mono">↑↓</kbd>
          历史
        </span>
        <span className="flex items-center gap-1">
          <kbd className="px-1 py-0.5 bg-slate-100 dark:bg-slate-800 rounded text-[10px] font-mono">/</kbd>
          命令
        </span>
        <span className="flex items-center gap-1">
          <kbd className="px-1 py-0.5 bg-slate-100 dark:bg-slate-800 rounded text-[10px] font-mono">Shift+Enter</kbd>
          换行
        </span>
      </div>

      {/* 最近会话 */}
      {sessions.length > 0 && (
        <div className="w-full max-w-xs space-y-1">
          <p className="text-[11px] text-slate-400 dark:text-slate-500 flex items-center gap-1">
            <Clock className="w-3 h-3" /> 最近对话
          </p>
          {sessions.slice(0, 3).map((session) => (
            <button
              key={session.id}
              onClick={() => loadSession(session.id)}
              className="w-full text-left px-2.5 py-1.5 text-xs text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-md transition-colors truncate"
            >
              {session.title || '新对话'}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
