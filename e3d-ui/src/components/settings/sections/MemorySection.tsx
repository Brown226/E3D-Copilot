/**
 * MemorySection — 记忆管理
 * 显示/搜索/删除 AI 记忆条目
 */

import { useState, useEffect, useCallback } from 'react'
import { Search, Trash2, RefreshCw, Brain, Clock, Tag, Save } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'
import { useToastStore } from '@/store/useToastStore'
import type { MemoryEntry } from '@/services/messageContracts'

export default function MemorySection() {
  const bridgeConnected = useChatStore((s) => s.bridgeConnected)
  const addToast = useToastStore((s) => s.addToast)
  const [memories, setMemories] = useState<MemoryEntry[]>([])
  const [loading, setLoading] = useState(false)
  const [searchQuery, setSearchQuery] = useState('')
  const [filterKind, setFilterKind] = useState<string>('all')
  const [expandedId, setExpandedId] = useState<string | null>(null)
  const [showAddForm, setShowAddForm] = useState(false)
  const [newTitle, setNewTitle] = useState('')
  const [newContent, setNewContent] = useState('')
  const [newKind, setNewKind] = useState('project_context')
  const [saving, setSaving] = useState(false)

  // 保存新记忆
  const handleSave = async () => {
    if (!newTitle.trim() || !newContent.trim()) return
    setSaving(true)
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      const result = await bridge.saveMemory({
        title: newTitle,
        content: newContent,
        kind: newKind as any
      }) as { success?: boolean } | null
      if (result?.success) {
        addToast('success', '记忆已保存')
        setNewTitle('')
        setNewContent('')
        setShowAddForm(false)
        loadMemories()
      }
    } catch {
      addToast('error', '保存失败')
    } finally {
      setSaving(false)
    }
  }

  // 加载记忆列表
  const loadMemories = useCallback(async () => {
    setLoading(true)
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      const result = await bridge.listMemories() as { memories?: MemoryEntry[] } | null
      if (result?.memories) {
        setMemories(result.memories)
      }
    } catch {
      // bridge 不可用时降级到 localStorage
      try {
        const raw = localStorage.getItem('e3d-memories')
        if (raw) setMemories(JSON.parse(raw))
      } catch {}
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (bridgeConnected) loadMemories()
  }, [bridgeConnected, loadMemories])

  // 删除记忆
  const handleDelete = async (id: string) => {
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      const result = await bridge.deleteMemory(id) as { deleted?: boolean } | null
      if (result?.deleted) {
        setMemories((prev) => prev.filter((m) => m.id !== id))
        addToast('success', '记忆已删除')
      }
    } catch {
      // 降级到 localStorage
      setMemories((prev) => {
        const next = prev.filter((m) => m.id !== id)
        localStorage.setItem('e3d-memories', JSON.stringify(next))
        return next
      })
      addToast('success', '记忆已删除')
    }
  }

  // 搜索过滤
  const filteredMemories = memories.filter((m) => {
    const matchesSearch = !searchQuery ||
      m.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
      m.content.toLowerCase().includes(searchQuery.toLowerCase())
    const matchesKind = filterKind === 'all' || m.kind === filterKind
    return matchesSearch && matchesKind
  })

  // Kind 标签颜色
  const kindColor = (kind: string) => {
    const colors: Record<string, string> = {
      debug_context: 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400',
      architectural_decision: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400',
      known_issue: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400',
      convention: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400',
      project_context: 'bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-400',
    }
    return colors[kind] || 'bg-slate-100 text-slate-700 dark:bg-slate-700 dark:text-slate-300'
  }

  const kindLabel = (kind: string) => {
    const labels: Record<string, string> = {
      debug_context: '调试',
      architectural_decision: '架构决策',
      known_issue: '已知问题',
      convention: '约定',
      project_context: '项目上下文',
    }
    return labels[kind] || kind
  }

  return (
    <div className="space-y-4">
      {/* 搜索栏 */}
      <div className="flex gap-2">
        <div className="flex-1 relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="搜索记忆..."
            className="w-full pl-10 pr-3 py-2 text-sm rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-800 dark:text-slate-100 placeholder-slate-400 outline-none focus:border-blue-500 transition-colors"
          />
        </div>
        <button
          onClick={loadMemories}
          disabled={loading}
          className="px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors disabled:opacity-50"
          title="刷新"
        >
          <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
        </button>
      </div>

      {/* Kind 过滤器 */}
      <div className="flex gap-1.5 flex-wrap">
        {['all', 'debug_context', 'architectural_decision', 'known_issue', 'convention', 'project_context'].map((kind) => (
          <button
            key={kind}
            onClick={() => setFilterKind(kind)}
            className={`px-2.5 py-1 text-xs rounded-full transition-colors ${
              filterKind === kind
                ? 'bg-blue-600 text-white'
                : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 hover:bg-slate-200 dark:hover:bg-slate-700'
            }`}
          >
            {kind === 'all' ? '全部' : kindLabel(kind)}
          </button>
        ))}
      </div>

      {/* 新增记忆表单 */}
      {showAddForm && (
        <div className="p-4 rounded-xl border border-blue-200 dark:border-blue-800 bg-blue-50/50 dark:bg-blue-900/10 space-y-3">
          <input
            type="text"
            value={newTitle}
            onChange={(e) => setNewTitle(e.target.value)}
            placeholder="标题"
            className="w-full px-3 py-2 text-sm rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-800 dark:text-slate-100 placeholder-slate-400 outline-none focus:border-blue-500"
          />
          <select
            value={newKind}
            onChange={(e) => setNewKind(e.target.value)}
            className="w-full px-3 py-2 text-sm rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-800 dark:text-slate-100 outline-none focus:border-blue-500"
          >
            <option value="project_context">项目上下文</option>
            <option value="architectural_decision">架构决策</option>
            <option value="convention">约定</option>
            <option value="known_issue">已知问题</option>
            <option value="debug_context">调试</option>
          </select>
          <textarea
            value={newContent}
            onChange={(e) => setNewContent(e.target.value)}
            placeholder="记忆内容..."
            rows={3}
            className="w-full px-3 py-2 text-sm rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-800 dark:text-slate-100 placeholder-slate-400 outline-none focus:border-blue-500 resize-none"
          />
          <div className="flex justify-end gap-2">
            <button
              onClick={() => setShowAddForm(false)}
              className="px-3 py-1.5 text-sm rounded-lg bg-slate-200 dark:bg-slate-700 text-slate-600 dark:text-slate-300 hover:bg-slate-300 dark:hover:bg-slate-600 transition-colors"
            >
              取消
            </button>
            <button
              onClick={handleSave}
              disabled={saving || !newTitle.trim() || !newContent.trim()}
              className="px-3 py-1.5 text-sm rounded-lg bg-blue-600 text-white hover:bg-blue-700 transition-colors disabled:opacity-50 flex items-center gap-1"
            >
              <Save className="w-3.5 h-3.5" />
              {saving ? '保存中...' : '保存'}
            </button>
          </div>
        </div>
      )}

      {/* 记忆列表 */}
      {loading ? (
        <div className="flex items-center justify-center py-12">
          <div className="w-6 h-6 border-2 border-blue-500 border-t-transparent rounded-full animate-spin" />
        </div>
      ) : filteredMemories.length === 0 ? (
        <div className="text-center py-12">
          <Brain className="w-12 h-12 text-slate-300 dark:text-slate-600 mx-auto mb-3" />
          <p className="text-sm text-slate-500 dark:text-slate-400">
            {searchQuery ? '没有找到匹配的记忆' : '暂无记忆条目'}
          </p>
          <p className="text-xs text-slate-400 dark:text-slate-500 mt-1">
            AI 会在对话中自动保存重要信息
          </p>
        </div>
      ) : (
        <div className="space-y-2">
          {filteredMemories.map((memory) => (
            <div
              key={memory.id}
              className="rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 overflow-hidden"
            >
              {/* 标题行 */}
              <button
                onClick={() => setExpandedId(expandedId === memory.id ? null : memory.id)}
                className="w-full flex items-center justify-between px-4 py-3 text-left hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-colors"
              >
                <div className="flex items-center gap-2 min-w-0">
                  <span className={`px-2 py-0.5 text-xs rounded-full ${kindColor(memory.kind)}`}>
                    {kindLabel(memory.kind)}
                  </span>
                  <span className="text-sm font-medium text-slate-800 dark:text-slate-100 truncate">
                    {memory.title}
                  </span>
                </div>
                <div className="flex items-center gap-2 shrink-0">
                  {memory.tags?.slice(0, 2).map((tag) => (
                    <span key={tag} className="flex items-center gap-0.5 text-xs text-slate-400">
                      <Tag className="w-3 h-3" />
                      {tag}
                    </span>
                  ))}
                  <button
                    onClick={(e) => { e.stopPropagation(); handleDelete(memory.id) }}
                    className="p-1 text-slate-400 hover:text-red-500 transition-colors"
                    title="删除"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                </div>
              </button>

              {/* 展开内容 */}
              {expandedId === memory.id && (
                <div className="px-4 pb-3 border-t border-slate-100 dark:border-slate-700">
                  <p className="text-sm text-slate-600 dark:text-slate-300 mt-3 whitespace-pre-wrap">
                    {memory.content}
                  </p>
                  {memory.created_at && (
                    <p className="flex items-center gap-1 text-xs text-slate-400 mt-2">
                      <Clock className="w-3 h-3" />
                      {memory.created_at}
                    </p>
                  )}
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {/* 底部统计 */}
      <p className="text-xs text-slate-400 dark:text-slate-500 text-center">
        共 {memories.length} 条记忆
        {filteredMemories.length !== memories.length && ` · 显示 ${filteredMemories.length} 条`}
      </p>
    </div>
  )
}
