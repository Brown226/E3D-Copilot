/**
 * TodoPanel — 实时任务列表面板
 * 从消息流中提取 todo_write 工具调用，渲染为可折叠的任务清单
 * 显示在消息列表上方（流式期间）或作为浮动面板
 */

import { memo, useMemo, useState } from 'react'
import { ChevronDown, ChevronRight, CheckCircle2, Circle, Loader2, ListTodo } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'
import type { Message } from '@/types'

interface TodoItem {
  id: string
  content: string
  status: 'pending' | 'in_progress' | 'completed'
  activeForm?: string
}

function extractTodos(messages: Message[]): TodoItem[] {
  // 从后往前找最后一个 todo_write 工具调用的参数
  for (let i = messages.length - 1; i >= 0; i--) {
    const msg = messages[i]
    if (msg.role === 'tool_call' && msg.toolName === 'todo_write' && msg.toolArgs) {
      try {
        const args = msg.toolArgs as { todos?: TodoItem[] }
        if (Array.isArray(args.todos)) return args.todos
      } catch { /* ignore */ }
    }
  }
  return []
}

export const TodoPanel = memo(function TodoPanel() {
  const messages = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.messages ?? [])
  // const isStreaming = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.isStreaming ?? false)
  const [collapsed, setCollapsed] = useState(false)

  const todos = useMemo(() => extractTodos(messages), [messages])

  if (todos.length === 0) return null

  const completedCount = todos.filter((t) => t.status === 'completed').length
  // const inProgressCount = todos.filter((t) => t.status === 'in_progress').length
  const progress = Math.round((completedCount / todos.length) * 100)

  return (
    <div className="mx-1 mb-1 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800/80 overflow-hidden shadow-sm">
      {/* 头部 */}
      <button
        onClick={() => setCollapsed((v) => !v)}
        className="w-full flex items-center gap-2 px-3 py-2 hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors"
      >
        {collapsed ? <ChevronRight className="w-3.5 h-3.5 text-slate-400" /> : <ChevronDown className="w-3.5 h-3.5 text-slate-400" />}
        <ListTodo className="w-4 h-4 text-slate-500 dark:text-slate-400" />
        <span className="text-xs font-semibold text-slate-600 dark:text-slate-300 flex-1 text-left">
          任务清单
        </span>
        <span className="text-[11px] text-slate-400 tabular-nums">
          {completedCount}/{todos.length}
        </span>
        {/* 进度条 */}
        <div className="w-16 h-1.5 rounded-full bg-slate-200 dark:bg-slate-700 overflow-hidden">
          <div
            className="h-full rounded-full bg-emerald-500 transition-all duration-300"
            style={{ width: `${progress}%` }}
          />
        </div>
      </button>

      {/* 任务列表 */}
      {!collapsed && (
        <div className="px-3 pb-2 space-y-0.5">
          {todos.map((todo) => (
            <div
              key={todo.id}
              className={`flex items-start gap-2 py-1 px-1.5 rounded text-xs ${
                todo.status === 'in_progress'
                  ? 'bg-blue-50 dark:bg-blue-900/20'
                  : ''
              }`}
            >
              <span className="shrink-0 mt-0.5">
                {todo.status === 'completed' ? (
                  <CheckCircle2 className="w-3.5 h-3.5 text-emerald-500" />
                ) : todo.status === 'in_progress' ? (
                  <Loader2 className="w-3.5 h-3.5 text-blue-500 animate-spin" />
                ) : (
                  <Circle className="w-3.5 h-3.5 text-slate-300 dark:text-slate-600" />
                )}
              </span>
              <span
                className={`flex-1 ${
                  todo.status === 'completed'
                    ? 'text-slate-400 dark:text-slate-500 line-through'
                    : todo.status === 'in_progress'
                    ? 'text-slate-700 dark:text-slate-200 font-medium'
                    : 'text-slate-500 dark:text-slate-400'
                }`}
              >
                {todo.activeForm && todo.status === 'in_progress' ? todo.activeForm : todo.content}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  )
})
