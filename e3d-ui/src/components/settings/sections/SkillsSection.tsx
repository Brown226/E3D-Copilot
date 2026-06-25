/**
 * SkillsSection — 技能管理（完整实现）
 *
 * 功能：
 * 1. 从后端 Bridge 获取真实技能列表
 * 2. 技能来源管理（添加/移除/刷新）
 * 3. 技能启用/禁用（持久化到后端）
 * 4. 搜索/过滤/展开详情
 * 5. 内置技能由后端动态生成（ToolExecutor 注册的工具自动同步）
 */

import { useState, useMemo, useCallback, useEffect } from 'react'
import {
  Search,
  RefreshCw,
  FolderOpen,
  Plus,
  Trash2,
  Zap,
  ChevronRight,
  ToggleLeft,
  ToggleRight,
  Wrench,
  FileText,
  Globe,
  Lock,
  AlertCircle,
  Loader2,
} from 'lucide-react'
import type { SkillInfo, SkillSource } from '@/services/messageContracts'
import { useToastStore } from '@/store/useToastStore'

// ── 内置技能由后端动态生成，不再硬编码 ──

// ── Helpers ──
function summarizeDescription(desc: string, maxLen = 120): string {
  if (desc.length <= maxLen) return desc
  return desc.slice(0, maxLen).trim() + '…'
}

function scopeLabel(scope: string): string {
  const labels: Record<string, string> = { builtin: '内置', project: '项目', global: '全局', custom: '自定义' }
  return labels[scope] || scope
}

function scopeColor(scope: string): string {
  const colors: Record<string, string> = {
    builtin: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400',
    project: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400',
    global: 'bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-400',
    custom: 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400',
  }
  return colors[scope] || 'bg-slate-100 text-slate-700'
}

function scopeIcon(scope: string) {
  switch (scope) {
    case 'builtin': return <Lock className="w-3 h-3" />
    case 'project': return <FolderOpen className="w-3 h-3" />
    case 'global': return <Globe className="w-3 h-3" />
    default: return <FileText className="w-3 h-3" />
  }
}

// ═══════════════════════════════════════════
// 主组件
// ═══════════════════════════════════════════

export default function SkillsSection() {
  const [skills, setSkills] = useState<SkillInfo[]>([])
  const [sources, setSources] = useState<SkillSource[]>([
    { path: '（从后端加载）', status: 'active', skillCount: 0, removable: false },
  ])
  const [searchQuery, setSearchQuery] = useState('')
  const [expandedSkills, setExpandedSkills] = useState<Set<string>>(new Set())
  const [expandedSources, setExpandedSources] = useState(false)
  const [loading, setLoading] = useState(false)
  const [newSourcePath, setNewSourcePath] = useState('')
  const [showAddInput, setShowAddInput] = useState(false)
  const addToast = useToastStore((s) => s.addToast)

  // ── 从后端加载技能 ──
  const loadSkills = useCallback(async () => {
    setLoading(true)
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      if (!bridge.isAvailable()) {
        // standalone 模式，使用静态数据
        return
      }
      const result = await bridge.listSkills() as { skills?: SkillInfo[]; sources?: SkillSource[] } | null
      if (result) {
        if (result.skills && result.skills.length > 0) setSkills(result.skills)
        if (result.sources) setSources(result.sources)
      }
    } catch {
      // bridge 不可用时保持空列表
    } finally {
      setLoading(false)
    }
  }, [])

  // ── 初始化加载 ──
  useEffect(() => {
    loadSkills()
  }, [loadSkills])

  // 搜索过滤
  const filteredSkills = useMemo(() => {
    const q = searchQuery.trim().toLowerCase()
    if (!q) return skills
    return skills.filter((sk) => {
      const text = [sk.name, sk.description, sk.scope, ...(sk.tags || [])].join(' ').toLowerCase()
      return text.includes(q)
    })
  }, [skills, searchQuery])

  // 统计
  const stats = useMemo(() => ({
    total: skills.length,
    enabled: skills.filter((s) => s.enabled).length,
    builtin: skills.filter((s) => s.scope === 'builtin').length,
    subagent: skills.filter((s) => s.runAs === 'subagent').length,
  }), [skills])

  // 切换展开
  const toggleExpand = useCallback((name: string) => {
    setExpandedSkills((prev) => {
      const next = new Set(prev)
      if (next.has(name)) next.delete(name)
      else next.add(name)
      return next
    })
  }, [])

  // ── 启用/禁用技能（发送到后端） ──
  const toggleEnabled = useCallback(async (name: string) => {
    // 乐观更新
    setSkills((prev) => prev.map((sk) => sk.name === name ? { ...sk, enabled: !sk.enabled } : sk))

    try {
      const { default: bridge } = await import('@/services/bridgeService')
      if (bridge.isAvailable()) {
        const result = await bridge.toggleSkill(name) as { name: string; enabled: boolean } | null
        if (result) {
          // 后端确认，同步状态
          setSkills((prev) => prev.map((sk) => sk.name === result.name ? { ...sk, enabled: result.enabled } : sk))
          addToast('success', `技能 /${result.name} 已${result.enabled ? '启用' : '禁用'}`)
        }
      }
    } catch {
      addToast('error', `切换技能失败`)
    }
  }, [addToast])

  // ── 刷新 ──
  const handleRefresh = useCallback(async () => {
    setLoading(true)
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      if (!bridge.isAvailable()) return
      const result = await bridge.refreshSkills() as { skills?: SkillInfo[]; sources?: SkillSource[] } | null
      if (result) {
        if (result.skills) setSkills(result.skills)
        if (result.sources) setSources(result.sources)
        addToast('success', `已刷新，发现 ${result.skills?.length ?? 0} 个技能`)
      }
    } catch {
      addToast('error', '刷新技能失败')
    } finally {
      setLoading(false)
    }
  }, [addToast])

  // ── 添加技能来源 ──
  const handleAddSource = useCallback(async () => {
    if (!newSourcePath.trim()) return
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      if (!bridge.isAvailable()) {
        addToast('warning', '未连接到后端，无法添加来源')
        return
      }
      const result = await bridge.addSkillSource(newSourcePath.trim()) as { added: boolean; sources?: SkillSource[] } | null
      if (result?.added) {
        setSources(result.sources || [])
        setNewSourcePath('')
        setShowAddInput(false)
        addToast('success', `已添加技能来源: ${newSourcePath}`)
        // 自动刷新技能列表
        handleRefresh()
      } else {
        addToast('warning', '来源已存在或添加失败')
      }
    } catch {
      addToast('error', '添加来源失败')
    }
  }, [newSourcePath, addToast, handleRefresh])

  // ── 移除技能来源 ──
  const handleRemoveSource = useCallback(async (path: string) => {
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      if (!bridge.isAvailable()) return
      const result = await bridge.removeSkillSource(path) as { removed: boolean; sources?: SkillSource[] } | null
      if (result?.removed) {
        setSources(result.sources || [])
        addToast('success', `已移除来源: ${path}`)
        handleRefresh()
      }
    } catch {
      addToast('error', '移除来源失败')
    }
  }, [addToast, handleRefresh])

  return (
    <div className="space-y-4">
      {/* 搜索栏 */}
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
        <input
          type="text"
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          placeholder="搜索技能... (名称/描述/标签)"
          className="w-full pl-10 pr-3 py-2.5 text-sm rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-800 dark:text-slate-100 placeholder-slate-400 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 transition-all"
        />
      </div>

      {/* 统计概览 */}
      <div className="grid grid-cols-4 gap-2">
        {[
          { label: '总计', value: stats.total, color: 'text-slate-700 dark:text-slate-200' },
          { label: '已启用', value: stats.enabled, color: 'text-emerald-600 dark:text-emerald-400' },
          { label: '内置', value: stats.builtin, color: 'text-blue-600 dark:text-blue-400' },
          { label: '子代理', value: stats.subagent, color: 'text-purple-600 dark:text-purple-400' },
        ].map((item) => (
          <div key={item.label} className="text-center p-2 rounded-lg bg-slate-50 dark:bg-slate-800/50">
            <p className={`text-lg font-bold ${item.color}`}>{item.value}</p>
            <p className="text-xs text-slate-500 dark:text-slate-400">{item.label}</p>
          </div>
        ))}
      </div>

      {/* 技能来源 */}
      <div className="rounded-xl border border-slate-200 dark:border-slate-700 overflow-hidden">
        <button
          onClick={() => setExpandedSources(!expandedSources)}
          className="w-full flex items-center justify-between px-4 py-3 text-left hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-colors"
        >
          <div className="flex items-center gap-2">
            <FolderOpen className="w-4 h-4 text-slate-500" />
            <span className="text-sm font-medium text-slate-700 dark:text-slate-200">
              技能来源 ({sources.length})
            </span>
            <span className="text-xs text-slate-400">
              {sources.filter((s) => s.status === 'active').length} 个活跃
            </span>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={(e) => { e.stopPropagation(); handleRefresh() }}
              disabled={loading}
              className="p-1 text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 rounded transition-colors"
              title="刷新"
            >
              {loading ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <RefreshCw className="w-3.5 h-3.5" />}
            </button>
            <ChevronRight className={`w-4 h-4 text-slate-400 transition-transform ${expandedSources ? 'rotate-90' : ''}`} />
          </div>
        </button>

        {expandedSources && (
          <div className="px-4 pb-3 space-y-2 border-t border-slate-100 dark:border-slate-700">
            {sources.map((source, i) => (
              <div key={i} className="flex items-center justify-between py-2">
                <div className="flex items-center gap-2 min-w-0">
                  <div className={`w-2 h-2 rounded-full ${
                    source.status === 'active' ? 'bg-emerald-500' :
                    source.status === 'missing' ? 'bg-amber-500' : 'bg-red-500'
                  }`} />
                  <span className="text-xs text-slate-600 dark:text-slate-400 truncate font-mono">
                    {source.path}
                  </span>
                  <span className="text-xs text-slate-400">
                    {source.skillCount} 个技能
                  </span>
                  {source.status === 'missing' && (
                    <AlertCircle className="w-3 h-3 text-amber-500" />
                  )}
                </div>
                {source.removable && (
                  <button
                    onClick={() => handleRemoveSource(source.path)}
                    className="p-1 text-slate-400 hover:text-red-500 transition-colors"
                    title="移除"
                  >
                    <Trash2 className="w-3 h-3" />
                  </button>
                )}
              </div>
            ))}

            {/* 添加来源 */}
            {showAddInput ? (
              <div className="flex items-center gap-2 pt-1">
                <input
                  type="text"
                  value={newSourcePath}
                  onChange={(e) => setNewSourcePath(e.target.value)}
                  onKeyDown={(e) => e.key === 'Enter' && handleAddSource()}
                  placeholder="输入技能文件夹路径..."
                  className="flex-1 px-3 py-1.5 text-xs rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-800 dark:text-slate-100 placeholder-slate-400 outline-none focus:border-blue-500"
                  autoFocus
                />
                <button
                  onClick={handleAddSource}
                  className="px-3 py-1.5 text-xs bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
                >
                  添加
                </button>
                <button
                  onClick={() => { setShowAddInput(false); setNewSourcePath('') }}
                  className="px-3 py-1.5 text-xs text-slate-500 hover:text-slate-700 dark:hover:text-slate-300 transition-colors"
                >
                  取消
                </button>
              </div>
            ) : (
              <button
                onClick={() => setShowAddInput(true)}
                className="w-full flex items-center justify-center gap-1.5 py-2 text-xs text-blue-600 dark:text-blue-400 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded-lg transition-colors"
              >
                <Plus className="w-3.5 h-3.5" />
                添加技能文件夹
              </button>
            )}
          </div>
        )}
      </div>

      {/* 技能列表 */}
      <div>
        <div className="flex items-center justify-between mb-2">
          <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200">
            技能列表
          </h3>
          <span className="text-xs text-slate-400">
            {filteredSkills.length === skills.length
              ? `${stats.total} 个`
              : `${filteredSkills.length} / ${stats.total} 个`}
          </span>
        </div>

        {filteredSkills.length === 0 ? (
          <div className="text-center py-8">
            <Zap className="w-10 h-10 text-slate-300 dark:text-slate-600 mx-auto mb-2" />
            <p className="text-sm text-slate-500 dark:text-slate-400">
              {searchQuery ? '没有找到匹配的技能' : '暂无可用技能'}
            </p>
          </div>
        ) : (
          <div className="space-y-1.5">
            {filteredSkills.map((skill) => {
              const isExpanded = expandedSkills.has(skill.name)
              const description = skill.description
              const canExpand = description.length > 120

              return (
                <div
                  key={skill.name}
                  className={`rounded-xl border overflow-hidden transition-all ${
                    skill.enabled
                      ? 'border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800'
                      : 'border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/30 opacity-60'
                  }`}
                >
                  {/* 标题行 */}
                  <div className="flex items-center gap-3 px-3 py-2.5">
                    <button
                      onClick={() => canExpand && toggleExpand(skill.name)}
                      className="shrink-0"
                      disabled={!canExpand}
                    >
                      {canExpand ? (
                        <ChevronRight className={`w-4 h-4 text-slate-400 transition-transform ${isExpanded ? 'rotate-90' : ''}`} />
                      ) : (
                        <div className="w-4" />
                      )}
                    </button>

                    <div className="flex items-center gap-2 min-w-0 flex-1">
                      <Wrench className="w-4 h-4 text-slate-400 shrink-0" />
                      <span className="text-sm font-medium text-slate-800 dark:text-slate-100 font-mono">
                        /{skill.name}
                      </span>
                      <span className={`inline-flex items-center gap-1 px-1.5 py-0.5 text-xs rounded ${scopeColor(skill.scope)}`}>
                        {scopeIcon(skill.scope)}
                        {scopeLabel(skill.scope)}
                      </span>
                      {skill.runAs === 'subagent' && (
                        <span className="text-xs px-1.5 py-0.5 rounded bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-400">
                          子代理
                        </span>
                      )}
                      {skill.tags && skill.tags.length > 0 && (
                        <div className="hidden sm:flex items-center gap-1">
                          {skill.tags.slice(0, 2).map((tag) => (
                            <span key={tag} className="text-xs px-1 py-0.5 rounded bg-slate-100 dark:bg-slate-700 text-slate-500 dark:text-slate-400">
                              {tag}
                            </span>
                          ))}
                        </div>
                      )}
                    </div>

                    <button
                      onClick={() => toggleEnabled(skill.name)}
                      className={`shrink-0 p-0.5 rounded-full transition-colors ${
                        skill.enabled
                          ? 'text-blue-600 dark:text-blue-400 hover:text-blue-700'
                          : 'text-slate-400 hover:text-slate-500'
                      }`}
                      title={skill.enabled ? '点击禁用' : '点击启用'}
                    >
                      {skill.enabled ? (
                        <ToggleRight className="w-6 h-6" />
                      ) : (
                        <ToggleLeft className="w-6 h-6" />
                      )}
                    </button>
                  </div>

                  {/* 描述 */}
                  <div className="px-3 pb-2.5 pl-10">
                    <p className="text-xs text-slate-500 dark:text-slate-400 leading-relaxed">
                      {isExpanded ? description : summarizeDescription(description)}
                    </p>
                    {canExpand && (
                      <button
                        onClick={() => toggleExpand(skill.name)}
                        className="text-xs text-blue-500 dark:text-blue-400 hover:text-blue-600 mt-1 transition-colors"
                      >
                        {isExpanded ? '收起' : '展开详情'}
                      </button>
                    )}
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </div>
    </div>
  )
}
