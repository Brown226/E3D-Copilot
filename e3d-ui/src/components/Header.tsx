/**
 * Header 组件
 * Logo + 标题 + 模型信息 + 连接状态 + 操作按钮（新会话/历史/设置）
 */

import { Bot, Settings, Clock, Plus } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'

export function Header() {
  const currentModel = useChatStore((s) => s.currentModel)
  const bridgeConnected = useChatStore((s) => s.bridgeConnected)
  const toggleSettings = useChatStore((s) => s.toggleSettings)
  const toggleHistory = useChatStore((s) => s.toggleHistory)
  const newSession = useChatStore((s) => s.newSession)
  const sessions = useChatStore((s) => s.sessions)

  const handleNewSession = () => {
    import('@/services/bridgeService').then(({ default: bridge }) => {
      // 保存当前会话
      useChatStore.getState().saveSession()
      // 新建会话
      newSession(bridge.newSession.bind(bridge))
    })
  }

  return (
    <header className="bg-white/80 backdrop-blur-sm border-b border-slate-200 px-4 py-3 dark:bg-slate-900/80 dark:border-slate-700">
      <div className="max-w-4xl mx-auto flex items-center justify-between">
        {/* 左侧：Logo + 标题 */}
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 rounded-xl bg-blue-600 flex items-center justify-center">
            <Bot className="w-5 h-5 text-white" />
          </div>
          <div>
            <h1 className="text-lg font-bold text-slate-800 dark:text-slate-100">E小智</h1>
            <div className="flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400">
              <span className={`w-1.5 h-1.5 rounded-full ${bridgeConnected ? 'bg-emerald-500' : 'bg-red-500'}`} />
              <span>
                {currentModel || 'E3D 智能助手'}
              </span>
            </div>
          </div>
        </div>

        {/* 右侧：操作按钮 */}
        <div className="flex items-center gap-1">
          {/* 新会话 */}
          <button
            onClick={handleNewSession}
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-700 rounded-lg transition-colors"
            title="新建对话"
          >
            <Plus className="w-4 h-4" />
            <span className="hidden sm:inline">新建</span>
          </button>

          {/* 历史 */}
          <button
            onClick={toggleHistory}
            className="relative flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-700 rounded-lg transition-colors"
            title="对话历史"
          >
            <Clock className="w-4 h-4" />
            <span className="hidden sm:inline">历史</span>
            {sessions.length > 0 && (
              <span className="absolute -top-1 -right-1 w-4 h-4 rounded-full bg-blue-500 text-white text-[10px] flex items-center justify-center">
                {sessions.length > 99 ? '99+' : sessions.length}
              </span>
            )}
          </button>

          {/* 设置 */}
          <button
            onClick={toggleSettings}
            className="p-2 text-slate-400 hover:text-slate-600 rounded-lg hover:bg-slate-100 transition-colors dark:hover:text-slate-200 dark:hover:bg-slate-700"
            title="设置"
          >
            <Settings className="w-4 h-4" />
          </button>
        </div>
      </div>
    </header>
  )
}
