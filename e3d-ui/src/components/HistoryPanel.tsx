/**
 * HistoryPanel — 对话历史管理（v2 重设计）
 *
 * 改进：
 * 1. 卡片化会话项 + 左侧彩色竖条（按日期分组着色）
 * 2. 当前活跃会话显示蓝色圆点 + "当前" 标签
 * 3. 消息数/时间戳用 pill 徽章样式
 * 4. 删除操作简化为单击确认
 * 5. 搜索栏增加快捷键提示
 * 6. 面板宽度收窄为 max-w-sm
 */

import { useState, useMemo, useCallback, useEffect } from 'react'
import {
  Search,
  MessageSquare,
  Trash2,
  Clock,
  X,
  Plus,
  Loader2,
  AlertCircle,
} from 'lucide-react'
import { useChatStore, type SessionMeta } from '@/store/useChatStore'
import bridge from '@/services/bridgeService'

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

// 按日期分组着色
const GROUP_COLORS: Record<string, string> = {
  '今天': 'bg-blue-500',
  '昨天': 'bg-cyan-500',
}
function groupColor(label: string): string {
  return GROUP_COLORS[label] || 'bg-slate-400 dark:bg-slate-500'
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
  const sessionId = useChatStore((s) => s.sessionId)

  const [query, setQuery] = useState('')
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // 从后端加载会话列表（合并去重，后端为权威数据源）
  const loadSessionsFromBackend = useCallback(async () => {
    if (!bridge.isAvailable()) return
    setLoading(true)
    setError(null)
    try {
      const result = await bridge.listSessions() as { sessions?: SessionMeta[] } | null
      if (result?.sessions) {
        const localSessions = useChatStore.getState().sessions
        const backendIds = new Set(result.sessions.map(s => s.id))
        // 后端会话优先，保留仅存在于本地的会话（取并集，后端数据覆盖本地）
        const localOnly = localSessions.filter(s => !backendIds.has(s.id))
        const merged = [...result.sessions, ...localOnly]
        useChatStore.getState().setSessions(merged)
      }
    } catch (err) {
      // 后端不可用时降级到 localStorage，不阻断 UI
      console.warn('[HistoryPanel] 后端会话列表获取失败，使用本地缓存', err)
      setError('无法同步后端数据，显示本地缓存')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (showHistory) {
      loadSessionsFromBackend()
    }
  }, [showHistory, loadSessionsFromBackend])

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

  // 删除
  const handleDelete = useCallback(async (sessionId: string) => {
    // 先同步删除本地（立即响应 UI）
    deleteSession(sessionId)
    setDeleteConfirm(null)
    // 异步通知后端删除
    if (bridge.isAvailable()) {
      try {
        await bridge.deleteSession(sessionId)
      } catch (err) {
        console.warn('[HistoryPanel] 后端删除会话失败', err)
      }
    }
  }, [deleteSession])

  // 新建对话（纯前端，不发 new_session）
  const handleNewSession = useCallback(() => {
    useChatStore.getState().createTab()
    toggleHistory()
  }, [toggleHistory])

  if (!showHistory) return null

  return (
    <div className="fixed inset-0 z-[var(--z-modal)]">
      {/* 遮罩 */}
      <div
        className="absolute inset-0 bg-black/40 backdrop-blur-sm animate-in fade-in"
        onClick={toggleHistory}
      />

      {/* 面板 — 收窄为 max-w-sm */}
      <div className="absolute inset-y-0 left-0 w-full max-w-sm bg-white dark:bg-slate-900 shadow-2xl flex flex-col animate-in slide-in-from-left duration-300">
        {/* 标题栏 */}
        <div className="flex items-center justify-between px-4 py-3 border-b border-slate-200 dark:border-slate-700">
          <div className="flex items-center gap-2">
            <Clock className="w-4 h-4 text-blue-500" />
            <h2 className="text-sm font-semibold text-slate-800 dark:text-slate-100">对话历史</h2>
            <span className="px-1.5 py-0.5 text-[10px] font-medium rounded-full bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400">
              {sessions.length}
            </span>
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

        {/* 搜索栏 + 快捷键提示 */}
        <div className="px-3 py-2 border-b border-slate-100 dark:border-slate-800">
          <div className="relative">
            <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-slate-400" />
            <input
              type="text"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="搜索对话..."
              className="w-full pl-8 pr-12 py-2 text-sm rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-800 dark:text-slate-100 placeholder-slate-400 outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/20 transition-all"
            />
            <kbd className="absolute right-2.5 top-1/2 -translate-y-1/2 px-1.5 py-0.5 text-[10px] font-mono text-slate-400 bg-slate-100 dark:bg-slate-700 rounded border border-slate-200 dark:border-slate-600">
              Ctrl+K
            </kbd>
          </div>
        </div>

        {/* 同步状态提示 */}
        {error && (
          <div className="px-3 py-2 bg-amber-50 dark:bg-amber-900/20 border-b border-amber-200 dark:border-amber-800 flex items-center gap-2">
            <AlertCircle className="w-3.5 h-3.5 text-amber-500 shrink-0" />
            <span className="text-xs text-amber-600 dark:text-amber-400">{error}</span>
          </div>
        )}

        {/* 会话列表 */}
        <div className="flex-1 overflow-y-auto">
          {loading ? (
            <div className="flex flex-col items-center justify-center h-48 px-6">
              <Loader2 className="w-6 h-6 text-blue-500 animate-spin mb-3" />
              <p className="text-sm text-slate-500 dark:text-slate-400">正在同步会话列表...</p>
            </div>
          ) : groups.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-full px-6">
              <div className="w-16 h-16 rounded-2xl bg-slate-100 dark:bg-slate-800 flex items-center justify-center mb-4">
                <MessageSquare className="w-7 h-7 text-slate-300 dark:text-slate-600" />
              </div>
              <p className="text-sm font-medium text-slate-600 dark:text-slate-300 mb-1">
                {query ? '没有找到匹配的对话' : '还没有对话历史'}
              </p>
              <p className="text-xs text-slate-400 dark:text-slate-500 text-center mb-4">
                {query ? '试试其他关键词' : '开始一段新对话，历史记录将自动保存'}
              </p>
              {!query && (
                <button
                  onClick={handleNewSession}
                  className="flex items-center gap-1.5 px-4 py-2 text-sm font-medium rounded-lg bg-blue-600 text-white hover:bg-blue-700 transition-colors"
                >
                  <Plus className="w-4 h-4" />
                  开始新对话
                </button>
              )}
            </div>
          ) : (
            <div className="py-2 px-2 space-y-3">
              {groups.map((group) => (
                <div key={group.label}>
                  {/* 日期标题 */}
                  <div className="px-2 py-1">
                    <span className="text-[11px] font-semibold uppercase tracking-wide text-slate-400 dark:text-slate-500">
                      {group.label}
                    </span>
                  </div>

                  {/* 会话卡片 */}
                  <div className="space-y-1">
                    {group.items.map((session) => {
                      const isConfirming = deleteConfirm === session.id
                      const isCurrent = session.id === sessionId
                      return (
                        <div
                          key={session.id}
                          className={`group relative flex items-stretch rounded-lg overflow-hidden transition-all cursor-pointer ${
                            isCurrent
                              ? 'bg-blue-50 dark:bg-blue-900/20 ring-1 ring-blue-200 dark:ring-blue-800'
                              : 'hover:bg-slate-50 dark:hover:bg-slate-800/60'
                          }`}
                          onClick={() => handleResume(session)}
                        >
                          {/* 左侧彩色竖条 */}
                          <div className={`w-1 shrink-0 ${groupColor(group.label)} ${isCurrent ? 'opacity-100' : 'opacity-60 group-hover:opacity-100'} transition-opacity`} />

                          {/* 内容区 */}
                          <div className="flex-1 min-w-0 px-3 py-2.5">
                            <div className="flex items-center gap-1.5">
                              {isCurrent && (
                                <span className="w-1.5 h-1.5 rounded-full bg-blue-500 shrink-0 animate-pulse" />
                              )}
                              <p className={`text-[13px] font-medium truncate ${
                                isCurrent ? 'text-blue-700 dark:text-blue-300' : 'text-slate-800 dark:text-slate-100'
                              }`}>
                                {session.title}
                              </p>
                              {isCurrent && (
                                <span className="shrink-0 px-1.5 py-0.5 text-[9px] font-semibold rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-600 dark:text-blue-400">
                                  当前
                                </span>
                              )}
                            </div>
                            {session.preview && (
                              <p className="text-xs text-slate-400 dark:text-slate-500 truncate mt-0.5 leading-relaxed">
                                {session.preview}
                              </p>
                            )}
                            {/* Pill 徽章：时间 + 消息数 */}
                            <div className="flex items-center gap-1.5 mt-1.5">
                              <span className="inline-flex items-center px-1.5 py-0.5 text-[10px] font-medium rounded-md bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400">
                                {formatTime(session.lastActivityAt)}
                              </span>
                              <span className="inline-flex items-center px-1.5 py-0.5 text-[10px] font-medium rounded-md bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400">
                                {session.messageCount} 条
                              </span>
                            </div>
                          </div>

                          {/* 删除按钮 */}
                          <div className="flex items-center pr-2 shrink-0">
                            {isConfirming ? (
                              <div className="flex items-center gap-1">
                                <button
                                  onClick={(e) => { e.stopPropagation(); handleDelete(session.id) }}
                                  className="px-2 py-1 text-[10px] font-semibold rounded-md bg-red-500 text-white hover:bg-red-600 transition-colors"
                                >
                                  删除
                                </button>
                                <button
                                  onClick={(e) => { e.stopPropagation(); setDeleteConfirm(null) }}
                                  className="px-2 py-1 text-[10px] font-medium rounded-md bg-slate-200 dark:bg-slate-600 text-slate-600 dark:text-slate-300 transition-colors"
                                >
                                  取消
                                </button>
                              </div>
                            ) : (
                              <button
                                onClick={(e) => { e.stopPropagation(); setDeleteConfirm(session.id) }}
                                className="p-1.5 text-slate-300 dark:text-slate-600 hover:text-red-500 dark:hover:text-red-400 rounded-md opacity-0 group-hover:opacity-100 transition-all"
                                title="删除"
                              >
                                <Trash2 className="w-3.5 h-3.5" />
                              </button>
                            )}
                          </div>
                        </div>
                      )
                    })}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* 底部统计 */}
        {sessions.length > 0 && (
          <div className="px-4 py-2 border-t border-slate-100 dark:border-slate-800">
            <p className="text-[11px] text-slate-400 dark:text-slate-500 text-center">
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
