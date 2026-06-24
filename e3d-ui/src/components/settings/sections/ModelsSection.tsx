/**
 * ModelsSection — 模型管理（同款双子 Tab 设计）
 *
 * Usage Tab:
 *   - 默认模型选择器
 *   - 温度/MaxTokens 参数
 *   - 当前模型健康检查
 *
 * Access Tab:
 *   - Provider 卡片列表
 *   - API Key 管理（掩码+编辑）
 *   - 模型拉取 + 勾选启用
 *   - 添加/编辑/删除 Provider
 */

import { useState, useEffect, useCallback, useMemo, useRef } from 'react'
import {
  RefreshCw,
  Pencil,
  Trash2,
  Star,
  Loader2,
  Eye,
  EyeOff,
  X,
  Plus,
  Check,
  AlertTriangle,
  ChevronRight,
  Zap,
  Key,
  Globe,
  Box,
} from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'
import type { ProviderInfo } from '@/services/messageContracts'

// ── Types ──
type SubTab = 'usage' | 'access'

interface ProviderFormData {
  name: string
  kind: string
  baseUrl: string
  apiKey: string
  defaultModel: string
}

const emptyForm: ProviderFormData = {
  name: '',
  kind: 'openai',
  baseUrl: '',
  apiKey: '',
  defaultModel: '',
}

// ── Helpers ──
function maskApiKey(key: string, keySet: boolean): string {
  if (!keySet || !key) return '未设置'
  if (key.length <= 8) return key
  return `${key.slice(0, 4)}****${key.slice(-4)}`
}

// ═══════════════════════════════════════════
// 主组件
// ═══════════════════════════════════════════

export default function ModelsSection() {
  const [subtab, setSubtab] = useState<SubTab>('usage')

  return (
    <div className="space-y-4">
      {/* 子 Tab 切换 */}
      <div className="flex gap-1 p-0.5 bg-slate-100 dark:bg-slate-800 rounded-lg">
        {([
          { id: 'usage' as SubTab, label: '模型使用', icon: <Zap className="w-3 h-3" /> },
          { id: 'access' as SubTab, label: '接入管理', icon: <Key className="w-3 h-3" /> },
        ]).map((tab) => (
          <button
            key={tab.id}
            onClick={() => setSubtab(tab.id)}
            className={`flex-1 flex items-center justify-center gap-1 px-2 py-1.5 text-xs font-medium rounded-md transition-all ${
              subtab === tab.id
                ? 'bg-white dark:bg-slate-700 text-blue-600 dark:text-blue-400 shadow-sm'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-800 dark:hover:text-slate-200'
            }`}
          >
            {tab.icon}
            {tab.label}
          </button>
        ))}
      </div>

      {/* 内容 */}
      {subtab === 'usage' ? <UsageTab /> : <AccessTab />}
    </div>
  )
}

// ═══════════════════════════════════════════
// Usage Tab — 模型使用配置
// ═══════════════════════════════════════════

function UsageTab() {
  const providers = useChatStore((s) => s.providers)
  const currentProvider = useChatStore((s) => s.currentProvider)
  const currentModel = useChatStore((s) => s.currentModel)
  const switchModel = useChatStore((s) => s.switchModel)

  // 从 localStorage 初始化（后端 config:sync 时也可同步）
  const [temperature, setTemperature] = useState(() => {
    try { return parseFloat(localStorage.getItem('e3d-setting-temperature') || '0.7') }
    catch { return 0.7 }
  })
  const [maxTokens, setMaxTokens] = useState(() => {
    try { return parseInt(localStorage.getItem('e3d-setting-maxTokens') || '4096') }
    catch { return 4096 }
  })

  // debounced 保存 ref
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  // 持久化 Temperature / MaxTokens 到后端 + localStorage（带 300ms debounce）
  const saveParam = useCallback((key: string, value: string) => {
    localStorage.setItem(`e3d-setting-${key}`, value)
    if (debounceRef.current) clearTimeout(debounceRef.current)
    debounceRef.current = setTimeout(() => {
      import('@/services/bridgeService').then(({ default: bridge }) => {
        if (bridge.isAvailable()) {
          bridge.saveSetting(key, value)
        }
      })
    }, 300)
  }, [])

  const handleTemperatureChange = useCallback((val: number) => {
    setTemperature(val)
    saveParam('temperature', String(val))
  }, [saveParam])

  const handleMaxTokensChange = useCallback((val: number) => {
    setMaxTokens(val)
    saveParam('maxTokens', String(val))
  }, [saveParam])

  // 当前激活的 Provider
  const activeProvider = useMemo(
    () => providers.find((p) => p.name === currentProvider),
    [providers, currentProvider]
  )

  // 所有可用模型（扁平列表）
  const allModels = useMemo(() => {
    const list: { ref: string; provider: string; model: string }[] = []
    for (const p of providers) {
      for (const m of p.models) {
        list.push({ ref: `${p.name}/${m}`, provider: p.name, model: m })
      }
    }
    return list
  }, [providers])

  const handleSwitchModel = async (ref: string) => {
    const { default: bridge } = await import('@/services/bridgeService')
    switchModel(ref, bridge.switchModel.bind(bridge))
  }

  return (
    <div className="space-y-4">
      {/* 当前模型状态（紧凑） */}
      <div className="flex items-center gap-2.5 p-2.5 rounded-lg bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800">
        <Box className="w-4 h-4 text-blue-600 dark:text-blue-400 shrink-0" />
        <div className="flex-1 min-w-0">
          <p className="text-xs text-slate-500 dark:text-slate-400">当前模型</p>
          <p className="text-sm font-semibold text-blue-700 dark:text-blue-300 truncate">
            {currentModel || '未选择'}
            {activeProvider && <span className="text-xs font-normal text-slate-400 ml-1.5">({activeProvider.name})</span>}
          </p>
        </div>
      </div>
      {/* 默认模型选择器 */}
      <div>
        <label className="block text-sm font-semibold text-slate-700 dark:text-slate-200 mb-2">
          默认模型
        </label>
        <p className="text-xs text-slate-500 dark:text-slate-400 mb-3">
          选择 AI 对话时使用的模型。不同模型在能力和速度上有所差异。
        </p>
        <div className="space-y-1 max-h-[200px] overflow-y-auto">
          {allModels.length === 0 ? (
            <div className="text-center py-6 text-sm text-slate-400">
              暂无可用模型，请先在"接入管理"中配置 Provider
            </div>
          ) : (
            // 按 Provider 分组
            Object.entries(
              allModels.reduce((acc, m) => {
                if (!acc[m.provider]) acc[m.provider] = []
                acc[m.provider].push(m)
                return acc
              }, {} as Record<string, typeof allModels>)
            ).map(([provider, models]) => (
              <div key={provider} className="mb-2">
                <p className="text-xs font-medium text-slate-400 dark:text-slate-500 px-2 py-1">
                  {provider}
                </p>
                {models.map((m) => {
                  const isActive = m.ref === `${currentProvider}/${currentModel}`
                  return (
                    <button
                      key={m.ref}
                      onClick={() => handleSwitchModel(m.ref)}
                      className={`w-full flex items-center justify-between px-2.5 py-1.5 rounded-md text-left transition-all ${
                        isActive
                          ? 'bg-blue-100 dark:bg-blue-900/40 border border-blue-300 dark:border-blue-700'
                          : 'hover:bg-slate-100 dark:hover:bg-slate-800 border border-transparent'
                      }`}
                    >
                      <span className={`text-xs ${isActive ? 'font-medium text-blue-700 dark:text-blue-300' : 'text-slate-700 dark:text-slate-300'}`}>
                        {m.model}
                      </span>
                      {isActive && <Check className="w-3.5 h-3.5 text-blue-600 dark:text-blue-400" />}
                    </button>
                  )
                })}
              </div>
            ))
          )}
        </div>
      </div>

      {/* 模型参数 */}
      <div className="space-y-4">
        <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200">模型参数</h3>

        {/* Temperature */}
        <div>
          <div className="flex items-center justify-between mb-1">
            <label className="text-sm text-slate-600 dark:text-slate-300">Temperature</label>
            <span className="text-xs font-mono text-slate-500 dark:text-slate-400 bg-slate-100 dark:bg-slate-800 px-2 py-0.5 rounded">
              {temperature.toFixed(1)}
            </span>
          </div>
          <input
            type="range"
            min="0"
            max="2"
            step="0.1"
            value={temperature}
            onChange={(e) => handleTemperatureChange(parseFloat(e.target.value))}
            className="w-full h-2 bg-slate-200 dark:bg-slate-700 rounded-lg appearance-none cursor-pointer accent-blue-600"
          />
          <div className="flex justify-between text-xs text-slate-400 mt-1">
            <span>精确 (0)</span>
            <span>均衡 (1)</span>
            <span>创意 (2)</span>
          </div>
        </div>

        {/* Max Tokens */}
        <div>
          <div className="flex items-center justify-between mb-1">
            <label className="text-sm text-slate-600 dark:text-slate-300">最大输出长度</label>
            <span className="text-xs font-mono text-slate-500 dark:text-slate-400 bg-slate-100 dark:bg-slate-800 px-2 py-0.5 rounded">
              {maxTokens.toLocaleString()}
            </span>
          </div>
          <input
            type="range"
            min="256"
            max="16384"
            step="256"
            value={maxTokens}
            onChange={(e) => handleMaxTokensChange(parseInt(e.target.value))}
            className="w-full h-2 bg-slate-200 dark:bg-slate-700 rounded-lg appearance-none cursor-pointer accent-blue-600"
          />
          <div className="flex justify-between text-xs text-slate-400 mt-1">
            <span>256</span>
            <span>4096</span>
            <span>16384</span>
          </div>
        </div>
      </div>

      {/* 健康检查 */}
      {providers.length > 0 && (
        <div className="p-3 rounded-lg bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700">
          <div className="flex items-center gap-2 text-xs text-slate-500 dark:text-slate-400">
            <Globe className="w-3.5 h-3.5" />
            <span>
              {providers.length} 个 Provider 已配置 ·{' '}
              {providers.filter((p) => p.keySet).length} 个已设置 API Key
            </span>
          </div>
        </div>
      )}
    </div>
  )
}

// ═══════════════════════════════════════════
// Access Tab — Provider 接入管理
// ═══════════════════════════════════════════

function AccessTab() {
  const providers = useChatStore((s) => s.providers)
  const currentProvider = useChatStore((s) => s.currentProvider)
  const loadProviders = useChatStore((s) => s.loadProviders)
  const switchModel = useChatStore((s) => s.switchModel)

  const [showForm, setShowForm] = useState(false)
  const [editingProvider, setEditingProvider] = useState<ProviderInfo | null>(null)
  const [form, setForm] = useState<ProviderFormData>(emptyForm)
  const [formErrors, setFormErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)
  const [refreshingProvider, setRefreshingProvider] = useState<string | null>(null)
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null)
  const [showApiKey, setShowApiKey] = useState(false)
  const [fetchError, setFetchError] = useState<string | null>(null)
  const [expandedProvider, setExpandedProvider] = useState<string | null>(null)

  const reloadProviders = useCallback(() => {
    import('@/services/bridgeService').then(({ default: bridge }) => {
      loadProviders(bridge.listProviders.bind(bridge))
    })
  }, [loadProviders])

  useEffect(() => {
    if (!showForm) return
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { setShowForm(false); setEditingProvider(null) }
    }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [showForm])

  // 刷新模型
  const handleRefreshModels = async (providerName: string) => {
    setRefreshingProvider(providerName)
    setFetchError(null)
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      const result = await bridge.fetchProviderModels(providerName) as {
        success?: boolean; models?: string[]; error?: string;
      } | null
      if (result && !result.success && result.error) {
        setFetchError(`刷新 ${providerName} 失败: ${result.error}`)
      }
      reloadProviders()
    } catch (err) {
      setFetchError(`刷新 ${providerName} 失败: ${err instanceof Error ? err.message : '未知错误'}`)
    } finally {
      setRefreshingProvider(null)
    }
  }

  // 设为默认
  const handleSetDefault = async (providerName: string, models: string[]) => {
    if (models.length === 0) return
    const ref = `${providerName}/${models[0]}`
    const { default: bridge } = await import('@/services/bridgeService')
    switchModel(ref, bridge.switchModel.bind(bridge))
  }

  // 删除
  const handleDelete = async (providerName: string) => {
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      await bridge.deleteProvider(providerName)
      setDeleteConfirm(null)
      reloadProviders()
    } catch (err) {
      setFetchError(`删除失败: ${err instanceof Error ? err.message : '未知错误'}`)
    }
  }

  // 编辑
  const handleEdit = (provider: ProviderInfo) => {
    setEditingProvider(provider)
    setForm({
      name: provider.name,
      kind: provider.kind,
      baseUrl: provider.baseUrl,
      apiKey: '',
      defaultModel: provider.default,
    })
    setFormErrors({})
    setShowApiKey(false)
    setShowForm(true)
  }

  // 添加
  const handleAdd = () => {
    setEditingProvider(null)
    setForm(emptyForm)
    setFormErrors({})
    setShowApiKey(false)
    setShowForm(true)
  }

  // 验证 + 保存
  const validateForm = (): boolean => {
    const errors: Record<string, string> = {}
    if (!editingProvider && !form.name.trim()) errors.name = '名称不能为空'
    if (!form.baseUrl.trim()) errors.baseUrl = 'Base URL 不能为空'
    setFormErrors(errors)
    return Object.keys(errors).length === 0
  }

  const handleSave = async () => {
    if (!validateForm()) return
    setSaving(true)
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      const payload = {
        name: editingProvider ? editingProvider.name : form.name.trim(),
        kind: form.kind,
        baseUrl: form.baseUrl.trim(),
        models: editingProvider ? editingProvider.models : [],
        default: form.defaultModel.trim() || (editingProvider?.models[0] ?? ''),
        enabled: true,
        builtIn: editingProvider?.builtIn ?? false,
      }
      await bridge.saveProvider(payload)
      const providerName = editingProvider ? editingProvider.name : form.name.trim()
      if (form.apiKey) {
        await bridge.setProviderKey(providerName, form.apiKey)
      }
      setShowForm(false)
      setEditingProvider(null)
      reloadProviders()
    } catch (err) {
      setFetchError(`保存失败: ${err instanceof Error ? err.message : '未知错误'}`)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="space-y-4">
      {/* 页头 */}
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            {providers.length} 个 Provider · {providers.filter((p) => p.keySet).length} 个已配置
          </p>
        </div>
        <button
          onClick={handleAdd}
          className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg bg-blue-600 text-white hover:bg-blue-700 transition-colors"
        >
          <Plus className="w-4 h-4" />
          添加
        </button>
      </div>

      {/* 错误信息 */}
      {fetchError && (
        <div className="p-3 rounded-lg bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 text-sm text-red-700 dark:text-red-400 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <AlertTriangle className="w-4 h-4 shrink-0" />
            <span>{fetchError}</span>
          </div>
          <button onClick={() => setFetchError(null)} className="text-red-400 hover:text-red-600">
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Provider 卡片列表 */}
      {providers.map((provider) => {
        const isCurrent = provider.name === currentProvider
        const isRefreshing = refreshingProvider === provider.name
        const isDeleting = deleteConfirm === provider.name
        const isExpanded = expandedProvider === provider.name

        return (
          <div
            key={provider.name}
            className={`rounded-xl border overflow-hidden transition-all ${
              isCurrent
                ? 'border-blue-300 dark:border-blue-700 bg-blue-50/30 dark:bg-blue-900/10'
                : 'border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800'
            }`}
          >
            {/* 标题行（可点击展开） */}
            <button
              onClick={() => setExpandedProvider(isExpanded ? null : provider.name)}
              className="w-full flex items-center justify-between px-4 py-3 text-left hover:bg-slate-50 dark:hover:bg-slate-700/30 transition-colors"
            >
              <div className="flex items-center gap-3">
                <div className={`w-8 h-8 rounded-lg flex items-center justify-center text-xs font-bold ${
                  isCurrent
                    ? 'bg-blue-600 text-white'
                    : 'bg-slate-200 dark:bg-slate-700 text-slate-600 dark:text-slate-300'
                }`}>
                  {provider.name.charAt(0).toUpperCase()}
                </div>
                <div>
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-semibold text-slate-800 dark:text-slate-100">
                      {provider.name}
                    </span>
                    <span className="text-xs px-1.5 py-0.5 rounded bg-slate-100 dark:bg-slate-700 text-slate-500 dark:text-slate-400">
                      {provider.kind}
                    </span>
                    {isCurrent && (
                      <span className="text-xs px-1.5 py-0.5 rounded bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 font-medium">
                        当前
                      </span>
                    )}
                    {provider.builtIn && (
                      <span className="text-xs px-1.5 py-0.5 rounded bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400">
                        内置
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-slate-400 dark:text-slate-500 mt-0.5">
                    {provider.models.length} 个模型 · API Key {provider.keySet ? '✓' : '✗'}
                  </p>
                </div>
              </div>
              <ChevronRight className={`w-4 h-4 text-slate-400 transition-transform ${isExpanded ? 'rotate-90' : ''}`} />
            </button>

            {/* 展开内容 */}
            {isExpanded && (
              <div className="px-4 pb-4 space-y-3 border-t border-slate-100 dark:border-slate-700">
                {/* Base URL */}
                <div className="pt-3">
                  <p className="text-xs text-slate-400 dark:text-slate-500 mb-1">Base URL</p>
                  <p className="text-sm text-slate-700 dark:text-slate-300 font-mono truncate">
                    {provider.baseUrl || '（未设置）'}
                  </p>
                </div>

                {/* API Key */}
                <div>
                  <p className="text-xs text-slate-400 dark:text-slate-500 mb-1">API Key</p>
                  <p className="text-sm text-slate-700 dark:text-slate-300 font-mono">
                    {maskApiKey(provider.apiKey, provider.keySet)}
                  </p>
                </div>

                {/* 模型列表 */}
                <div>
                  <p className="text-xs text-slate-400 dark:text-slate-500 mb-1.5">
                    已启用模型 ({provider.models.length})
                  </p>
                  <div className="flex flex-wrap gap-1.5">
                    {provider.models.map((model) => (
                      <span
                        key={model}
                        className={`text-xs px-2 py-1 rounded-md ${
                          model === provider.default
                            ? 'bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 font-medium'
                            : 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-400'
                        }`}
                      >
                        {model}
                        {model === provider.default && ' ★'}
                      </span>
                    ))}
                    {provider.models.length === 0 && (
                      <span className="text-xs text-slate-400 italic">暂无模型，点击"刷新模型"获取</span>
                    )}
                  </div>
                </div>

                {/* 操作按钮 */}
                <div className="flex items-center gap-2 pt-2">
                  {!isCurrent && provider.models.length > 0 && (
                    <button
                      onClick={() => handleSetDefault(provider.name, provider.models)}
                      className="flex items-center gap-1 px-3 py-1.5 text-xs rounded-lg border border-slate-200 dark:border-slate-600 text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors"
                    >
                      <Star className="w-3.5 h-3.5" />
                      设为默认
                    </button>
                  )}
                  <button
                    onClick={() => handleRefreshModels(provider.name)}
                    disabled={isRefreshing}
                    className="flex items-center gap-1 px-3 py-1.5 text-xs rounded-lg border border-slate-200 dark:border-slate-600 text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors disabled:opacity-50"
                  >
                    {isRefreshing ? (
                      <Loader2 className="w-3.5 h-3.5 animate-spin" />
                    ) : (
                      <RefreshCw className="w-3.5 h-3.5" />
                    )}
                    刷新模型
                  </button>
                  <button
                    onClick={() => handleEdit(provider)}
                    className="flex items-center gap-1 px-3 py-1.5 text-xs rounded-lg border border-slate-200 dark:border-slate-600 text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors"
                  >
                    <Pencil className="w-3.5 h-3.5" />
                    编辑
                  </button>
                  {!provider.builtIn && (
                    isDeleting ? (
                      <div className="flex items-center gap-1 ml-auto">
                        <span className="text-xs text-red-600 dark:text-red-400">确认？</span>
                        <button
                          onClick={() => handleDelete(provider.name)}
                          className="px-2 py-1 text-xs rounded bg-red-500 text-white hover:bg-red-600"
                        >
                          删除
                        </button>
                        <button
                          onClick={() => setDeleteConfirm(null)}
                          className="px-2 py-1 text-xs rounded bg-slate-200 dark:bg-slate-600 text-slate-600 dark:text-slate-300"
                        >
                          取消
                        </button>
                      </div>
                    ) : (
                      <button
                        onClick={() => setDeleteConfirm(provider.name)}
                        className="flex items-center gap-1 px-3 py-1.5 text-xs rounded-lg border border-red-200 dark:border-red-800 text-red-500 hover:bg-red-50 dark:hover:bg-red-900/20 transition-colors ml-auto"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                        删除
                      </button>
                    )
                  )}
                </div>
              </div>
            )}
          </div>
        )
      })}

      {/* 空状态 */}
      {providers.length === 0 && !showForm && (
        <div className="text-center py-8">
          <Key className="w-10 h-10 text-slate-300 dark:text-slate-600 mx-auto mb-3" />
          <p className="text-sm text-slate-500 dark:text-slate-400 mb-3">还没有配置任何 Provider</p>
          <button
            onClick={handleAdd}
            className="inline-flex items-center gap-1.5 px-4 py-2 text-sm font-medium rounded-lg bg-blue-600 text-white hover:bg-blue-700 transition-colors"
          >
            <Plus className="w-4 h-4" />
            添加第一个 Provider
          </button>
        </div>
      )}

      {/* 编辑/添加表单 Modal */}
      {showForm && (
        <div className="fixed inset-0 z-[60]">
          <div
            className="absolute inset-0 bg-black/40 backdrop-blur-sm"
            onClick={() => { setShowForm(false); setEditingProvider(null) }}
          />
          <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-full max-w-md bg-white dark:bg-slate-800 rounded-2xl shadow-2xl p-6">
            <div className="flex items-center justify-between mb-5">
              <h3 className="text-lg font-bold text-slate-800 dark:text-slate-100">
                {editingProvider ? '编辑 Provider' : '添加 Provider'}
              </h3>
              <button
                onClick={() => { setShowForm(false); setEditingProvider(null) }}
                className="p-1 text-slate-400 hover:text-slate-600 rounded-lg hover:bg-slate-100 transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="space-y-4">
              {/* 名称 */}
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">名称</label>
                <input
                  type="text"
                  value={form.name}
                  onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                  disabled={!!editingProvider}
                  placeholder="例如: openai, deepseek"
                  className={`w-full px-3 py-2 text-sm rounded-lg border bg-white dark:bg-slate-700 text-slate-800 dark:text-slate-100 placeholder-slate-400 outline-none transition-colors
                    ${formErrors.name ? 'border-red-400' : 'border-slate-300 dark:border-slate-600'}
                    focus:border-blue-500 disabled:opacity-50 disabled:cursor-not-allowed`}
                />
                {formErrors.name && <p className="text-xs text-red-500 mt-1">{formErrors.name}</p>}
              </div>

              {/* 类型 */}
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">类型</label>
                <select
                  value={form.kind}
                  onChange={(e) => setForm((f) => ({ ...f, kind: e.target.value }))}
                  className="w-full px-3 py-2 text-sm rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-800 dark:text-slate-100 outline-none focus:border-blue-500 transition-colors"
                >
                  <option value="openai">OpenAI</option>
                  <option value="anthropic">Anthropic</option>
                </select>
              </div>

              {/* Base URL */}
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Base URL</label>
                <input
                  type="text"
                  value={form.baseUrl}
                  onChange={(e) => setForm((f) => ({ ...f, baseUrl: e.target.value }))}
                  placeholder="https://api.openai.com/v1"
                  className={`w-full px-3 py-2 text-sm rounded-lg border bg-white dark:bg-slate-700 text-slate-800 dark:text-slate-100 placeholder-slate-400 outline-none transition-colors
                    ${formErrors.baseUrl ? 'border-red-400' : 'border-slate-300 dark:border-slate-600'}
                    focus:border-blue-500`}
                />
                {formErrors.baseUrl && <p className="text-xs text-red-500 mt-1">{formErrors.baseUrl}</p>}
              </div>

              {/* API Key */}
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">API Key</label>
                <div className="relative">
                  <input
                    type={showApiKey ? 'text' : 'password'}
                    value={form.apiKey}
                    onChange={(e) => setForm((f) => ({ ...f, apiKey: e.target.value }))}
                    placeholder={editingProvider ? '留空则不更新' : '输入 API Key'}
                    className="w-full px-3 py-2 pr-10 text-sm rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-800 dark:text-slate-100 placeholder-slate-400 outline-none focus:border-blue-500 transition-colors"
                  />
                  <button
                    type="button"
                    onClick={() => setShowApiKey((v) => !v)}
                    className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-slate-400 hover:text-slate-600"
                  >
                    {showApiKey ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
              </div>

              {/* 默认模型 */}
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">默认模型</label>
                <input
                  type="text"
                  value={form.defaultModel}
                  onChange={(e) => setForm((f) => ({ ...f, defaultModel: e.target.value }))}
                  placeholder="例如: gpt-4o"
                  className="w-full px-3 py-2 text-sm rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-800 dark:text-slate-100 placeholder-slate-400 outline-none focus:border-blue-500 transition-colors"
                />
              </div>
            </div>

            {/* 按钮组 */}
            <div className="flex justify-end gap-3 mt-6">
              <button
                onClick={() => { setShowForm(false); setEditingProvider(null) }}
                className="px-4 py-2 text-sm rounded-lg border border-slate-300 dark:border-slate-600 text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors"
              >
                取消
              </button>
              <button
                onClick={handleSave}
                disabled={saving}
                className="px-4 py-2 text-sm rounded-lg bg-blue-600 text-white hover:bg-blue-700 transition-colors disabled:opacity-50 flex items-center gap-2"
              >
                {saving && <Loader2 className="w-4 h-4 animate-spin" />}
                保存
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
