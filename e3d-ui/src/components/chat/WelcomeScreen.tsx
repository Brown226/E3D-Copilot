/**
 * T3.2 WelcomeScreen 组件
 * 空消息时显示品牌欢迎页 + QuickAction 按钮
 */

import { Bot } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'

const QUICK_ACTIONS = [
  { icon: '📋', text: '查询所有设备' },
  { icon: '🔧', text: '创建一根管道' },
  { icon: '📐', text: '帮我设计一个泵房' },
]

export function WelcomeScreen() {
  const messages = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.messages ?? [])
  const isStreaming = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.isStreaming ?? false)
  const setInputValue = useChatStore((s) => s.setInputValue)

  if (messages.length > 0 || isStreaming) return null

  const handleQuickAction = (text: string) => {
    setInputValue(text)
    import('@/services/bridgeService').then(({ default: bridge }) => {
      useChatStore.getState().sendMessage(bridge.sendUserMessage.bind(bridge))
    })
  }

  return (
    <div className="flex flex-col items-center justify-center py-20 space-y-6">
      <div className="w-16 h-16 rounded-2xl bg-blue-100 dark:bg-blue-900/40 flex items-center justify-center">
        <Bot className="w-8 h-8 text-blue-600 dark:text-blue-400" />
      </div>
      <div className="text-center space-y-2">
        <h2 className="text-2xl font-bold text-slate-800 dark:text-slate-100">你好，我是 E小智</h2>
        <p className="text-slate-500 dark:text-slate-400">E3D 工厂设计智能助手，有什么可以帮你的？</p>
      </div>
      <div className="flex flex-wrap justify-center gap-3 mt-4">
        {QUICK_ACTIONS.map((action) => (
          <button
            key={action.text}
            onClick={() => handleQuickAction(action.text)}
            className="flex items-center gap-2 px-4 py-2.5 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-600 rounded-xl text-sm text-slate-700 dark:text-slate-300 hover:border-blue-300 dark:hover:border-blue-500 hover:bg-blue-50 dark:hover:bg-slate-700 transition-colors shadow-sm"
          >
            <span>{action.icon}</span>
            <span>{action.text}</span>
          </button>
        ))}
      </div>
    </div>
  )
}
