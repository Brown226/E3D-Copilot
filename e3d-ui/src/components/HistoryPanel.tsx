/**
 * HistoryPanel — 对话历史管理（v3 紧凑版）
 *
 * 设计约束（大王要求）：
 * 1. 每行只保留标题，不再显示预览 / 时间 / 消息数徽章 —— 原来的卡片太占空间
 * 2. 支持批量删除：勾选多条后一次删掉
 * 3. 删除必须真的生效（内存状态先改，持久化失败不阻断）
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
  CheckSquare,
  Square,
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
  const deleteSessions = useChatStore((s) => s.deleteSessions)
  const sessionId = useChatStore((s) => s.sessionId)

  const [query, setQuery] = useState('')
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  // 批量选择：进入多选后才显示勾选框
  const [selectMode, setSelectMode] = useState(false)
  const [selected, setSelected] = useState<Set<string>>(new Set())

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
        // 本地会话若后端已有同 title+messageCount 的条目，视为同一段对话，不重复列出。
        // 后端 tab 会话的 id 是 GUID、本地是 session_xxx，两边 id 永远不相等，
        // 只按 id 去重会让同一段对话出现两份，删掉一份另一份还在。
        const backendKeys = new Set(
          result.sessions.map(s => `${s.title}|${s.messageCount}`),
        )
        const localOnly = localSessions.filter(
          s => !backendIds.has(s.id) && !backendKeys.has(`${s.title}|${s.messageCount}`),
        )
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
      // 每次打开都重置搜索与删除确认态：组件常驻（只是渲染 null），
      // 否则再次打开时列表仍被上次的搜索词过滤，甚至停在某条的删除确认态。
      setQuery('')
      setDeleteConfirm(null)
      setError(null)
      setSelectMode(false)
      setSelected(new Set())
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

  /**
   * 删除（单条或批量）。
   *
   * 两个要点：
   * 1. 先删内存再删后端 —— 若只依赖后端，后端没这条 id 时前端毫无变化，看起来就是「删不掉」。
   * 2. 同名同条数的本地/后端重复条目一并清掉，否则下次打开面板又从后端刷回来。
   */
  const handleDeleteMany = useCallback(async (sidList: string[]) => {
    if (sidList.length === 0) return

    // 归一化（同标题同条数视为同一段对话）由 store 负责，它返回实际删掉的全部 id
    const removedIds = deleteSessions(sidList)
    setDeleteConfirm(null)
    setSelected(new Set())
    setSelectMode(false)

    if (!bridge.isAvailable()) return
    for (const id of removedIds) {
      try {
        await bridge.deleteSession(id)
      } catch {
        // 后端没有该 id 属于正常情况（纯本地会话），忽略
      }
    }
  }, [deleteSessions])

  const toggleSelect = useCallback((sid: string) => {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(sid)) next.delete(sid)
      else next.add(sid)
      return next
    })
  }, [])

  const allVisibleSelected = filteredSessions.length > 0
    && filteredSessions.every((s) => selected.has(s.id))

  const toggleSelectAll = useCallback(() => {
    setSelected((prev) => (
      filteredSessions.length > 0 && filteredSessions.every((s) => prev.has(s.id))
        ? new Set()
        : new Set(filteredSessions.map((s) => s.id))
    ))
  }, [filteredSessions])

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
              onClick={() => { setSelectMode((v) => !v); setSelected(new Set()); setDeleteConfirm(null) }}
              className={`flex items-center gap-1 px-2 py-1.5 text-xs font-medium rounded-lg transition-colors ${
                selectMode
                  ? 'bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-200'
                  : 'text-slate-500 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800'
              }`}
              title="批量选择"
            >
              <CheckSquare className="w-3.5 h-3.5" />
              {selectMode ? '取消' : '多选'}
            </button>
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
            <div className="py-1 px-2">
              {groups.map((group) => (
                <div key={group.label}>
                  {/* 日期标题 */}
                  <div className="px-2 pt-2 pb-1 flex items-center justify-between">
                    <span className="text-[11px] font-semibold text-slate-400 dark:text-slate-500">
                      {group.label}
                    </span>
                    {selectMode && (
                      <button
                        onClick={toggleSelectAll}
                        className="text-[11px] text-blue-500 hover:text-blue-600 transition-colors"
                      >
                        {allVisibleSelected ? '取消全选' : '全选'}
                      </button>
                    )}
                  </div>

                  {/* 会话行 — 只保留标题，不显示预览/时间/条数 */}
                  <div className="space-y-0.5">
                    {group.items.map((session) => {
                      const isConfirming = deleteConfirm === session.id
                      const isCurrent = session.id === sessionId
                      const isChecked = selected.has(session.id)
                      return (
                        <div
                          key={session.id}
                          className={`group flex items-center gap-2 h-9 px-2 rounded-md transition-colors cursor-pointer ${
                            isCurrent
                              ? 'bg-blue-50 dark:bg-blue-900/20'
                              : isChecked
                                ? 'bg-slate-100 dark:bg-slate-800'
                                : 'hover:bg-slate-50 dark:hover:bg-slate-800/60'
                          }`}
                          onClick={() => (selectMode ? toggleSelect(session.id) : handleResume(session))}
                          title={session.title}
                        >
                          {selectMode ? (
                            isChecked
                              ? <CheckSquare className="w-3.5 h-3.5 shrink-0 text-blue-500" />
                              : <Square className="w-3.5 h-3.5 shrink-0 text-slate-300 dark:text-slate-600" />
                          ) : (
                            <div className={`w-0.5 h-4 shrink-0 rounded-full ${groupColor(group.label)} ${isCurrent ? 'opacity-100' : 'opacity-50'}`} />
                          )}

                          <p className={`flex-1 min-w-0 text-[13px] truncate ${
                            isCurrent
                              ? 'font-medium text-blue-700 dark:text-blue-300'
                              : 'text-slate-700 dark:text-slate-200'
                          }`}>
                            {session.title}
                          </p>

                          {isCurrent && !selectMode && (
                            <span className="shrink-0 text-[10px] text-blue-500">当前</span>
                          )}

                          {/* 单条删除（多选模式下隐藏，用底部批量删除） */}
                          {!selectMode && (
                            <div className="shrink-0 flex items-center">
                              {isConfirming ? (
                                <div className="flex items-center gap-1">
                                  <button
                                    onClick={(e) => { e.stopPropagation(); handleDeleteMany([session.id]) }}
                                    className="px-2 py-0.5 text-[11px] font-semibold rounded bg-red-500 text-white hover:bg-red-600 transition-colors"
                                  >
                                    删除
                                  </button>
                                  <button
                                    onClick={(e) => { e.stopPropagation(); setDeleteConfirm(null) }}
                                    className="px-2 py-0.5 text-[11px] rounded bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-100 transition-colors"
                                  >
                                    取消
                                  </button>
                                </div>
                              ) : (
                                <button
                                  onClick={(e) => { e.stopPropagation(); setDeleteConfirm(session.id) }}
                                  className="p-1 text-slate-300 dark:text-slate-600 hover:text-red-500 dark:hover:text-red-400 rounded opacity-0 group-hover:opacity-100 transition-all"
                                  title="删除"
                                >
                                  <Trash2 className="w-3.5 h-3.5" />
                                </button>
                              )}
                            </div>
                          )}
                        </div>
                      )
                    })}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* 底部：多选模式下显示批量删除，否则显示统计 */}
        {sessions.length > 0 && (
          <div className="px-3 py-2 border-t border-slate-100 dark:border-slate-800">
            {selectMode ? (
              <div className="flex items-center justify-between gap-2">
                <span className="text-[11px] text-slate-500 dark:text-slate-400">
                  已选 {selected.size} 项
                </span>
                <button
                  onClick={() => handleDeleteMany([...selected])}
                  disabled={selected.size === 0}
                  className={`flex items-center gap-1 px-3 py-1.5 text-xs font-semibold rounded-lg transition-colors ${
                    selected.size === 0
                      ? 'bg-slate-100 dark:bg-slate-800 text-slate-400 dark:text-slate-600 cursor-not-allowed'
                      : 'bg-red-500 text-white hover:bg-red-600'
                  }`}
                >
                  <Trash2 className="w-3.5 h-3.5" />
                  删除选中
                </button>
              </div>
            ) : (
              <p className="text-[11px] text-slate-400 dark:text-slate-500 text-center">
                共 {sessions.length} 个对话
                {query && filteredSessions.length !== sessions.length &&
                  ` · 显示 ${filteredSessions.length} 个`}
              </p>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
