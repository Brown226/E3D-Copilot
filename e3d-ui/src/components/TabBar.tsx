/**
 * TabBar — 多会话标签栏
 * 仅在多于 1 个 tab 时显示
 */

import { Plus, X } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'

export function TabBar() {
  const tabs = useChatStore((s) => s.tabs)
  const activeTabId = useChatStore((s) => s.activeTabId)
  const setActiveTab = useChatStore((s) => s.setActiveTab)
  const createTab = useChatStore((s) => s.createTab)
  const closeTab = useChatStore((s) => s.closeTab)

  if (tabs.length <= 1) return null

  return (
    <div className="flex items-center gap-1 px-2 py-1 bg-slate-100 dark:bg-slate-800 border-b border-slate-200 dark:border-slate-700 overflow-x-auto">
      {tabs.map((tab) => (
        <div
          key={tab.id}
          className={`group flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs cursor-pointer transition-colors shrink-0 max-w-[160px] ${
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
              onClick={(e) => { e.stopPropagation(); closeTab(tab.id) }}
              className="shrink-0 opacity-0 group-hover:opacity-100 hover:bg-slate-200 dark:hover:bg-slate-600 rounded p-0.5 transition-all"
            >
              <X className="w-3 h-3" />
            </button>
          )}
        </div>
      ))}
      <button
        onClick={() => createTab()}
        className="shrink-0 p-1.5 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 hover:bg-white/50 dark:hover:bg-slate-700/50 transition-colors"
        title="新建标签页"
      >
        <Plus className="w-4 h-4" />
      </button>
    </div>
  )
}
