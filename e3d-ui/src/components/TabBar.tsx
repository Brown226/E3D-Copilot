/**
 * TabBar — 多会话标签栏
 * 仅在多于 1 个 tab 时显示
 * 关闭正在流式的 Tab 时需要确认（防止误操作终止任务）
 */

import { useState } from 'react'
import { Plus, X, AlertTriangle } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'

export function TabBar() {
  const tabs = useChatStore((s) => s.tabs)
  const activeTabId = useChatStore((s) => s.activeTabId)
  const setActiveTab = useChatStore((s) => s.setActiveTab)
  const createTab = useChatStore((s) => s.createTab)
  const closeTab = useChatStore((s) => s.closeTab)
  const [confirmCloseId, setConfirmCloseId] = useState<string | null>(null)

  if (tabs.length <= 1) return null

  const handleClose = (tabId: string, isStreaming: boolean) => {
    if (isStreaming) {
      // 流式 Tab 需要二次确认
      if (confirmCloseId === tabId) {
        // 第二次点击：确认关闭，取消后端任务
        import('@/services/bridgeService').then(({ default: bridge }) => {
          bridge.cancel()
          bridge.closeTab(tabId)
        })
        closeTab(tabId)
        setConfirmCloseId(null)
      } else {
        // 第一次点击：进入确认状态
        setConfirmCloseId(tabId)
        // 3 秒后自动取消确认状态
        setTimeout(() => setConfirmCloseId(null), 3000)
      }
    } else {
      import('@/services/bridgeService').then(({ default: bridge }) => {
        bridge.closeTab(tabId)
      })
      closeTab(tabId)
    }
  }

  return (
    <div className="flex items-center gap-0.5 px-1.5 py-0.5 bg-slate-100 dark:bg-slate-800 border-b border-slate-200 dark:border-slate-700 overflow-x-auto shrink-0">
      {tabs.map((tab) => (
        <div
          key={tab.id}
          className={`group flex items-center gap-1 px-2 py-1 rounded text-[11px] cursor-pointer transition-colors shrink-0 max-w-[120px] ${
            tab.id === activeTabId
              ? 'bg-white dark:bg-slate-700 shadow-sm text-slate-800 dark:text-slate-200'
              : 'text-slate-500 dark:text-slate-400 hover:bg-white/50 dark:hover:bg-slate-700/50'
          }`}
          onClick={() => setActiveTab(tab.id)}
        >
          <span className="truncate">{tab.title}</span>
          {tab.isStreaming && (
            <span className="w-1.5 h-1.5 rounded-full bg-blue-500 animate-pulse shrink-0" />
          )}
          {tabs.length > 1 && (
            <button
              onClick={(e) => {
                e.stopPropagation()
                handleClose(tab.id, tab.isStreaming)
              }}
              className={`shrink-0 rounded p-0.5 transition-all ${
                confirmCloseId === tab.id
                  ? 'opacity-100 bg-red-100 dark:bg-red-900/40 text-red-600 dark:text-red-400'
                  : 'opacity-0 group-hover:opacity-100 hover:bg-slate-200 dark:hover:bg-slate-600'
              }`}
              title={confirmCloseId === tab.id ? '再次点击确认关闭（任务将被终止）' : '关闭标签页'}
            >
              {confirmCloseId === tab.id ? <AlertTriangle className="w-3 h-3" /> : <X className="w-3 h-3" />}
            </button>
          )}
        </div>
      ))}
      <button
        onClick={() => createTab()}
        className="shrink-0 p-1 rounded text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 hover:bg-white/50 dark:hover:bg-slate-700/50 transition-colors"
        title="新建标签页"
      >
        <Plus className="w-3.5 h-3.5" />
      </button>
    </div>
  )
}
