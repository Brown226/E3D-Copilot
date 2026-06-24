/**
 * HistoryPanel — 对话历史管理（同款设计）
 *
 * 功能：
 * 1. 会话列表 — 按日期分组（今天/昨天/更早）
 * 2. 搜索过滤 — 按标题/内容搜索
 * 3. 会话操作 — 继续对话/删除/重命名
 * 4. 空状态 — 引导用户开始新对话
 */

import { useState, useMemo, useCallback } from 'react'
import {
  Search,
  MessageSquare,
  Trash2,
  Clock,
  X,
  Plus,
  ArrowRight,
} from 'lucide-react'
import { useChatStore, type SessionMeta } from '@/store/useChatStore'

// ── 日期分组 ──
function dayLabel(ts: number): string {
  const now = new Date()
  const d = new Date(ts)
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()
  const yesterday = today - 86400000

  if (ts >= today) return '今天'
  if (ts >= yesterday) return '昨天'

  const month = d.getMonth() + 1
  const day = d.getDate()
  if (d.getFullYear() === now.getFullYear()) {
    return `${month}月${day}日`
  }
  return `${d.getFullYear()}年${month}月${day}日`
}

function formatTime(ts: number): string {
  const d = new Date(ts)
  const hh = String(d.getHours()).padStart(2, '0')
  const mm = String(d.getMinutes()).padStart(2, '0')
  return `${hh}:${mm}`
}

// ═══════════════════════════════════════════
// 主组件
// ═══════════════════════════════════════════

export function HistoryPanel() {
  const sessions = useChatStore((s) => s.sessions)
  const showHistory = useChatStore((s) => s.showHistory)
  const toggleHistory = useChatStore((s) => s.toggleHistory)
  const loadSession = useChatStore((s) => s.loadSession)
  const deleteSession = useChatStore((s) => s.deleteSession)
  const newSession = useChatStore((s) => s.newSession)

  const [query, setQuery] = useState('')
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null)

  // 搜索过滤
  const filteredSessions = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return sessions
    return sessions.filter((s) =>
      [s.title, s.preview].some((part) => (part ?? '').toLowerCase().includes(q))
    )
  }, [sessions, query])

  // 按日期分组
  const groups = useMemo(() => {
    const result: { label: string; items: SessionMeta[] }[] = []
    for (const s of filteredSessions) {
      const label = dayLabel(s.lastActivityAt)
      const last = result[result.length - 1]
      if (last && last.label === label) {
        last.items.push(s)
      } else {
        result.push({ label, items: [s] })
      }
    }
    return result
  }, [filteredSessions])

  // 继续对话
  const handleResume = useCallback((session: SessionMeta) => {
    loadSession(session.id)
  }, [loadSession])

  // 删除确认
  const handleDelete = useCallback((sessionId: string) => {
    deleteSession(sessionId)
    setDeleteConfirm(null)
  }, [deleteSession])

  // 新建对话
  const handleNewSession = useCallback(() => {
    import('@/services/bridgeService').then(({ default: bridge }) => {
      newSession(bridge.newSession.bind(bridge))
    })
  }, [newSession])

  if (!showHistory) return null

  return (
    <div className="fixed inset-0 z-50">
      {/* 遮罩 */}
      <div
        className="absolute inset-0 bg-black/40 backdrop-blur-sm animate-in fade-in"
        onClick={toggleHistory}
      />

      {/* 面板 */}
      <div className="absolute inset-y-0 left-0 w-full max-w-md bg-white dark:bg-slate-900 shadow-2xl flex flex-col animate-in slide-in-from-left duration-300">
        {/* 标题栏 */}
        <div className="flex items-center justify-between px-4 py-3 border-b border-slate-200 dark:border-slate-700">
          <div className="flex items-center gap-2">
            <Clock className="w-4 h-4 text-slate-500" />
            <h2 className="text-base font-semibold text-slate-800 dark:text-slate-100">对话历史</h2>
            <span className="text-xs text-slate-400">({sessions.length})</span>
          </div>
          <div className="flex items-center gap-1">
            <button
              onClick={handleNewSession}
              className="flex items-center gap-1 px-2.5 py-1.5 text-xs font-medium rounded-lg bg-blue-600 text-white hover:bg-blue-700 transition-colors"
            >
              <Plus className="w-3.5 h-3.5" />
              新建
            </button>
            <button
              onClick={toggleHistory}
              className="p-1.5 text-slate-400 hover:text-slate-600 rounded-lg hover:bg-slate-100 transition-colors dark:hover:text-slate-200 dark:hover:bg-slate-700"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* 搜索栏 */}
        <div className="px-4 py-2 border-b border-slate-100 dark:border-slate-800">
          <div className="relative">
            <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-slate-400" />
            <input
              type="text"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="搜索对话..."
              className="w-full pl-8 pr-3 py-2 text-sm rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-800 dark:text-slate-100 placeholder-slate-400 outline-none focus:border-blue-500 transition-colors"
            />
          </div>
        </div>

        {/* 会话列表 */}
        <div className="flex-1 overflow-y-auto">
          {groups.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-full px-4">
              <MessageSquare className="w-12 h-12 text-slate-300 dark:text-slate-600 mb-3" />
              <p className="text-sm text-slate-500 dark:text-slate-400 text-center mb-4">
                {query ? '没有找到匹配的对话' : '还没有对话历史'}
              </p>
              <button
                onClick={handleNewSession}
                className="flex items-center gap-1.5 px-4 py-2 text-sm font-medium rounded-lg bg-blue-600 text-white hover:bg-blue-700 transition-colors"
              >
                <Plus className="w-4 h-4" />
                开始新对话
              </button>
            </div>
          ) : (
            <div className="py-2">
              {groups.map((group) => (
                <div key={group.label}>
                  {/* 日期标题 */}
                  <div className="px-4 py-1.5">
                    <span className="text-xs font-medium text-slate-400 dark:text-slate-500">
                      {group.label}
                    </span>
                  </div>

                  {/* 会话项 */}
                  {group.items.map((session) => {
                    const isConfirming = deleteConfirm === session.id
                    return (
                      <div
                        key={session.id}
                        className="group mx-2 px-3 py-2.5 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
                        onClick={() => handleResume(session)}
                      >
                        <div className="flex items-start justify-between gap-2">
                          <div className="flex-1 min-w-0">
                            <p className="text-sm font-medium text-slate-800 dark:text-slate-100 truncate">
                              {session.title}
                            </p>
                            {session.preview && (
                              <p className="text-xs text-slate-400 dark:text-slate-500 truncate mt-0.5">
                                {session.preview}
                              </p>
                            )}
                            <div className="flex items-center gap-2 mt-1">
                              <span className="text-xs text-slate-400 dark:text-slate-500">
                                {formatTime(session.lastActivityAt)}
                              </span>
                              <span className="text-xs text-slate-300 dark:text-slate-600">
                                {session.messageCount} 条消息
                              </span>
                            </div>
                          </div>

                          {/* 操作按钮 */}
                          <div className="flex items-center gap-1 shrink-0 opacity-0 group-hover:opacity-100 transition-opacity">
                            <button
                              onClick={(e) => { e.stopPropagation(); handleResume(session) }}
                              className="p-1.5 text-blue-500 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors"
                              title="继续对话"
                            >
                              <ArrowRight className="w-3.5 h-3.5" />
                            </button>
                            {isConfirming ? (
                              <div className="flex items-center gap-0.5">
                                <button
                                  onClick={(e) => { e.stopPropagation(); handleDelete(session.id) }}
                                  className="px-1.5 py-0.5 text-xs rounded bg-red-500 text-white hover:bg-red-600"
                                >
                                  删除
                                </button>
                                <button
                                  onClick={(e) => { e.stopPropagation(); setDeleteConfirm(null) }}
                                  className="px-1.5 py-0.5 text-xs rounded bg-slate-200 dark:bg-slate-600 text-slate-600 dark:text-slate-300"
                                >
                                  取消
                                </button>
                              </div>
                            ) : (
                              <button
                                onClick={(e) => { e.stopPropagation(); setDeleteConfirm(session.id) }}
                                className="p-1.5 text-slate-400 hover:text-red-500 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors"
                                title="删除"
                              >
                                <Trash2 className="w-3.5 h-3.5" />
                              </button>
                            )}
                          </div>
                        </div>
                      </div>
                    )
                  })}
                </div>
              ))}
            </div>
          )}
        </div>

        {/* 底部统计 */}
        {sessions.length > 0 && (
          <div className="px-4 py-2 border-t border-slate-100 dark:border-slate-800">
            <p className="text-xs text-slate-400 dark:text-slate-500 text-center">
              共 {sessions.length} 个对话
              {query && filteredSessions.length !== sessions.length &&
                ` · 显示 ${filteredSessions.length} 个`}
            </p>
          </div>
        )}
      </div>
    </div>
  )
}
