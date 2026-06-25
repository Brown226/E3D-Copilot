/**
 * ModelsSection — 模型管理（对齐 Reasonix 设计）
 *
 * Usage Tab:
 *   - 当前模型状态
 *   - 默认模型选择器（按 Provider 分组）
 *   - Temperature / MaxTokens 数字输入（非滑块，对齐 Reasonix）
 *
 * Access Tab:
 *   - Provider 卡片列表（可展开）
 *   - API Key 明文显示 + 切换
 *   - 上下文窗口大小
 *   - Vision 模型标记
 *   - 刷新模型 / 编辑 / 删除 / 设为默认
 *   - 添加 Provider 表单（含上下文窗口、Vision 模型字段）
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
  ChevronRight,
  Zap,
  Key,
  Globe,
  Box,
  Camera,
  Hash,
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
  modelList: string           // 逗号分隔的模型列表
  contextWindow: number
  visionModels: string[]      // 勾选的视觉模型名
}

const emptyForm: ProviderFormData = {
  name: '',
  kind: 'openai',
  baseUrl: '',
  apiKey: '',
  modelList: '',
  contextWindow: 0,
  visionModels: [],
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

  // 执行轮数上限（从 localStorage 初始化）
  const [maxSteps, setMaxSteps] = useState(() => {
    try { return parseInt(localStorage.getItem('e3d-setting-maxSteps') || '20') }
    catch { return 20 }
  })

  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const saveSetting = useCallback((key: string, value: string) => {
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

  const activeProvider = useMemo(
    () => providers.find((p) => p.name === currentProvider),
    [providers, currentProvider]
  )

  const allModels = useMemo(() => {
    const list: { ref: string; provider: string; model: string; isVision: boolean }[] = []
    for (const p of providers) {
      const visionSet = new Set(p.visionModels || [])
      for (const m of p.models) {
        list.push({ ref: `${p.name}/${m}`, provider: p.name, model: m, isVision: visionSet.has(m) })
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
      {/* 当前模型状态 */}
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
                      <div className="flex items-center gap-1.5">
                        <span className={`text-xs ${isActive ? 'font-medium text-blue-700 dark:text-blue-300' : 'text-slate-700 dark:text-slate-300'}`}>
                          {m.model}
                        </span>
                        {m.isVision && (
                          <Camera className="w-3 h-3 text-purple-400" title="支持图片输入" />
                        )}
                      </div>
                      {isActive && <Check className="w-3.5 h-3.5 text-blue-600 dark:text-blue-400" />}
                    </button>
                  )
                })}
              </div>
            ))
          )}
        </div>
      </div>

      {/* Agent 运行配置 */}
      <div className="p-3 rounded-lg bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700 space-y-3">
        <div>
          <h4 className="text-sm font-semibold text-slate-700 dark:text-slate-200">Agent 运行</h4>
          <p className="text-xs text-slate-400 dark:text-slate-500 mt-0.5">
            限制每次回复最多调用多少轮工具；复杂任务建议设高一些。
          </p>
        </div>

        {/* 执行轮数上限 */}
        <div>
          <div className="flex items-center justify-between mb-1.5">
            <label className="text-sm text-slate-600 dark:text-slate-300">执行轮数上限</label>
            <span className="text-xs font-mono text-slate-500 dark:text-slate-400 bg-slate-100 dark:bg-slate-700 px-2 py-0.5 rounded">
              {maxSteps === 0 ? '不限' : maxSteps}
            </span>
          </div>
          <div className="flex gap-1.5">
            {[10, 20, 50, 0].map((n) => (
              <button
                key={n}
                onClick={() => {
                  setMaxSteps(n)
                  saveSetting('maxSteps', String(n))
                }}
                className={`flex-1 px-2 py-1.5 text-xs rounded-lg border transition-colors ${
                  maxSteps === n
                    ? 'bg-blue-100 dark:bg-blue-900/40 border-blue-300 dark:border-blue-700 text-blue-700 dark:text-blue-300 font-medium'
                    : 'border-slate-200 dark:border-slate-600 text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-700'
                }`}
              >
                {n === 0 ? '不限' : n}
              </button>
            ))}
          </div>
        </div>
      </div>
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
  const [testing, setTesting] = useState(false)
  const [testResult, setTestResult] = useState<{ success: boolean; models: string[]; error?: string } | null>(null)
  const [formShowApiKey, setFormShowApiKey] = useState(false)
  const [refreshingProvider, setRefreshingProvider] = useState<string | null>(null)
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null)
  const [showApiKey, setShowApiKey] = useState<Record<string, boolean>>({})
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

  const handleSetDefault = async (providerName: string, models: string[]) => {
    if (models.length === 0) return
    const ref = `${providerName}/${models[0]}`
    const { default: bridge } = await import('@/services/bridgeService')
    switchModel(ref, bridge.switchModel.bind(bridge))
  }

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

  const handleAdd = () => {
    setEditingProvider(null)
    setForm(emptyForm)
    setFormErrors({})
    setTestResult(null)
    setFormShowApiKey(false)
    setShowForm(true)
  }

  const handleEdit = (provider: ProviderInfo) => {
    setEditingProvider(provider)
    setForm({
      name: provider.name,
      kind: provider.kind,
      baseUrl: provider.baseUrl,
      apiKey: '',
      modelList: (provider.models || []).join(', '),
      contextWindow: provider.contextWindow || 0,
      visionModels: provider.visionModels || [],
    })
    setFormErrors({})
    setTestResult(null)
    setFormShowApiKey(false)
    setShowForm(true)
  }

  const handleTestAndFetch = async () => {
    if (!form.baseUrl.trim()) {
      setFormErrors({ baseUrl: '请先填写 API 地址' })
      return
    }
    setTesting(true)
    setTestResult(null)
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      // 先保存 provider 以便 fetch 能找到
      if (!editingProvider) {
        await bridge.saveProvider({
          name: form.name.trim() || 'temp',
          kind: form.kind,
          baseUrl: form.baseUrl.trim(),
          apiKey: form.apiKey,
          models: [],
        } as any)
      }
      const result = await bridge.fetchProviderModels(
        editingProvider?.name || form.name.trim() || 'temp'
      ) as { success?: boolean; models?: string[]; error?: string } | null
      if (result) {
        setTestResult({
          success: result.success ?? false,
          models: result.models ?? [],
          error: result.error,
        })
        if (result.success && result.models && result.models.length > 0) {
          setForm((f) => ({
            ...f,
            modelList: result.models!.join(', '),
          }))
        }
      }
    } catch (err) {
      setTestResult({
        success: false,
        models: [],
        error: err instanceof Error ? err.message : '未知错误',
      })
    } finally {
      setTesting(false)
    }
  }

  const handleSave = async () => {
    const errors: Record<string, string> = {}
    if (!form.name.trim()) errors.name = '名称不能为空'
    if (!form.baseUrl.trim()) errors.baseUrl = 'API 地址不能为空'
    if (Object.keys(errors).length > 0) { setFormErrors(errors); return }

    setSaving(true)
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      // 从模型列表 textarea 解析模型名
      const models = (testResult?.success ? testResult.models : [])
        .concat(
          form.modelList
            .split(',')
            .map((s) => s.trim())
            .filter(Boolean)
        )
      // 去重
      const uniqueModels = [...new Set(models)]

      await bridge.saveProvider({
        name: form.name.trim(),
        kind: form.kind,
        baseUrl: form.baseUrl.trim(),
        apiKey: form.apiKey,
        models: uniqueModels.length > 0 ? uniqueModels : undefined,
        default: uniqueModels.length > 0 ? uniqueModels[0] : undefined,
        contextWindow: form.contextWindow,
        visionModels: form.visionModels,
      } as any)
      setShowForm(false)
      setEditingProvider(null)
      reloadProviders()
    } catch (err) {
      setFormErrors({ submit: `保存失败: ${err instanceof Error ? err.message : '未知错误'}` })
    } finally {
      setSaving(false)
    }
  }

  const toggleApiKeyVisibility = (name: string) => {
    setShowApiKey((prev) => ({ ...prev, [name]: !prev[name] }))
  }

  const formatContextWindow = (n: number) => {
    if (!n || n === 0) return '默认'
    if (n >= 1000000) return `${(n / 1000000).toFixed(1)}M`
    if (n >= 1000) return `${(n / 1000).toFixed(0)}K`
    return String(n)
  }

  return (
    <div className="space-y-3">
      {/* 添加按钮 */}
      <div className="flex items-center justify-between">
        <p className="text-xs text-slate-500 dark:text-slate-400">
          {providers.length} 个 Provider
        </p>
        <button
          onClick={handleAdd}
          className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium rounded-lg bg-blue-600 text-white hover:bg-blue-700 transition-colors"
        >
          <Plus className="w-3.5 h-3.5" />
          添加 Provider
        </button>
      </div>

      {/* 错误提示 */}
      {fetchError && (
        <div className="flex items-center gap-2 p-2.5 rounded-lg bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 text-xs text-red-600 dark:text-red-400">
          <span className="flex-1">{fetchError}</span>
          <button onClick={() => setFetchError(null)} className="shrink-0">
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}

      {/* Provider 卡片列表 */}
      {providers.map((provider) => {
        const isExpanded = expandedProvider === provider.name
        const isCurrent = provider.name === currentProvider
        const isRefreshing = refreshingProvider === provider.name
        const isDeleting = deleteConfirm === provider.name
        const visionSet = new Set(provider.visionModels || [])

        return (
          <div
            key={provider.name}
            className={`rounded-xl border transition-colors ${
              isCurrent
                ? 'border-blue-200 dark:border-blue-800 bg-white dark:bg-slate-800'
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
                  <div className="flex items-center gap-3 text-xs text-slate-400 dark:text-slate-500 mt-0.5">
                    <span>{provider.models.length} 个模型</span>
                    <span>·</span>
                    <span>上下文 {formatContextWindow(provider.contextWindow)}</span>
                    {(provider.visionModels?.length ?? 0) > 0 && (
                      <>
                        <span>·</span>
                        <span className="flex items-center gap-0.5">
                          <Camera className="w-3 h-3 text-purple-400" />
                          {provider.visionModels!.length} 个视觉模型
                        </span>
                      </>
                    )}
                  </div>
                </div>
              </div>
              <ChevronRight className={`w-4 h-4 text-slate-400 transition-transform ${isExpanded ? 'rotate-90' : ''}`} />
            </button>

            {/* 展开内容 */}
            {isExpanded && (
              <div className="px-4 pb-4 space-y-3 border-t border-slate-100 dark:border-slate-700">
                {/* Base URL */}
                <div className="pt-3">
                  <p className="text-xs text-slate-400 dark:text-slate-500 mb-1">API 地址</p>
                  <p className="text-sm text-slate-700 dark:text-slate-300 font-mono truncate">
                    {provider.baseUrl || '（未设置）'}
                  </p>
                </div>

                {/* API Key — 明文显示 */}
                <div>
                  <div className="flex items-center justify-between mb-1">
                    <p className="text-xs text-slate-400 dark:text-slate-500">API Key</p>
                    <button
                      onClick={() => toggleApiKeyVisibility(provider.name)}
                      className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors"
                    >
                      {showApiKey[provider.name] ? <EyeOff className="w-3 h-3" /> : <Eye className="w-3 h-3" />}
                    </button>
                  </div>
                  <p className="text-sm text-slate-700 dark:text-slate-300 font-mono break-all">
                    {provider.keySet
                      ? (showApiKey[provider.name] ? provider.apiKey : '••••••••')
                      : '未设置'}
                  </p>
                </div>

                {/* 上下文窗口 */}
                {provider.contextWindow > 0 && (
                  <div>
                    <p className="text-xs text-slate-400 dark:text-slate-500 mb-1">上下文窗口</p>
                    <p className="text-sm text-slate-700 dark:text-slate-300">
                      {provider.contextWindow.toLocaleString()} tokens
                    </p>
                  </div>
                )}

                {/* 模型列表（含 Vision 标记） */}
                <div>
                  <p className="text-xs text-slate-400 dark:text-slate-500 mb-1.5">
                    已启用模型 ({provider.models.length})
                  </p>
                  <div className="flex flex-wrap gap-1.5">
                    {provider.models.map((model) => (
                      <span
                        key={model}
                        className={`inline-flex items-center gap-1 text-xs px-2 py-1 rounded-md ${
                          model === provider.default
                            ? 'bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 font-medium'
                            : 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-400'
                        }`}
                      >
                        {model}
                        {model === provider.default && ' ★'}
                        {visionSet.has(model) && <Camera className="w-3 h-3 text-purple-400" />}
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
                    {isDeleting ? (
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
          <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-full max-w-md max-h-[85vh] overflow-y-auto bg-white dark:bg-slate-800 rounded-2xl shadow-2xl p-6">
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
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">供应商名称</label>
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

              {/* 接入协议 */}
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">接入协议</label>
                <select
                  value={form.kind}
                  onChange={(e) => setForm((f) => ({ ...f, kind: e.target.value }))}
                  className="w-full px-3 py-2 text-sm rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-800 dark:text-slate-100 outline-none focus:border-blue-500 transition-colors"
                >
                  <option value="openai">OpenAI-compatible</option>
                </select>
              </div>

              {/* API 地址 */}
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">API 地址</label>
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

              {/* 密钥 — 带眼睛切换 */}
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">密钥</label>
                <div className="relative">
                  <input
                    type={formShowApiKey ? 'text' : 'password'}
                    value={form.apiKey}
                    onChange={(e) => setForm((f) => ({ ...f, apiKey: e.target.value }))}
                    placeholder={editingProvider ? '留空则不更新' : '输入 API Key'}
                    className="w-full px-3 py-2 pr-10 text-sm rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-800 dark:text-slate-100 placeholder-slate-400 outline-none focus:border-blue-500 transition-colors"
                  />
                  <button
                    type="button"
                    onClick={() => setFormShowApiKey((v) => !v)}
                    className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-slate-400 hover:text-slate-600"
                  >
                    {formShowApiKey ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
              </div>

              {/* 测试并获取模型 */}
              <div>
                <div className="flex items-center gap-3">
                  <button
                    type="button"
                    onClick={handleTestAndFetch}
                    disabled={testing}
                    className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-slate-300 dark:border-slate-600 text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors disabled:opacity-50"
                  >
                    {testing ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Zap className="w-3.5 h-3.5" />}
                    测试并获取模型
                  </button>
                  <span className="text-xs text-slate-400 dark:text-slate-500">
                    会使用上方 API 地址与密钥确认连接，并用返回结果填充模型列表。
                  </span>
                </div>
                {testResult && !testResult.success && testResult.error && (
                  <p className="text-xs text-red-500 mt-1.5">连接失败: {testResult.error}</p>
                )}
              </div>

              {/* 可用模型（测试成功后展示标签） */}
              {testResult && testResult.success && testResult.models.length > 0 && (
                <div>
                  <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1.5">可用模型</label>
                  <div className="flex flex-wrap gap-1.5">
                    {testResult.models.map((m) => (
                      <span
                        key={m}
                        className="text-xs px-2 py-1 rounded-md bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 border border-blue-200 dark:border-blue-800"
                      >
                        {m}
                      </span>
                    ))}
                  </div>
                </div>
              )}

              {/* 模型列表（逗号分隔文本框 + 手动填写说明） */}
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">模型列表</label>
                <textarea
                  value={form.modelList}
                  onChange={(e) => setForm((f) => ({ ...f, modelList: e.target.value }))}
                  placeholder="deepseek-v4-flash, deepseek-v4-pro, glm-5（逗号分隔）"
                  rows={3}
                  className="w-full px-3 py-2 text-sm rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-800 dark:text-slate-100 placeholder-slate-400 outline-none focus:border-blue-500 transition-colors resize-none"
                />
                <p className="text-xs text-slate-400 dark:text-slate-500 mt-1">
                  接口不支持模型发现时，可手动填写多个模型，用英文逗号分隔。
                </p>
              </div>

              {/* 上下文窗口 */}
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">上下文窗口</label>
                <input
                  type="number"
                  min="0"
                  step="1024"
                  value={form.contextWindow || ''}
                  onChange={(e) => setForm((f) => ({ ...f, contextWindow: parseInt(e.target.value) || 0 }))}
                  placeholder="0 = 模型默认值"
                  className="w-full px-3 py-2 text-sm rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-800 dark:text-slate-100 placeholder-slate-400 outline-none focus:border-blue-500 transition-colors"
                />
                <p className="text-xs text-slate-400 dark:text-slate-500 mt-1">
                  token 数（0 = 模型服务默认值）
                </p>
              </div>

              {/* 支持图片输入的模型 — 勾选框形式 */}
              {(() => {
                // 从 textarea 或测试结果中解析模型列表
                const modelNames = form.modelList
                  .split(',')
                  .map((s) => s.trim())
                  .filter(Boolean)
                if (modelNames.length === 0) return null
                return (
                  <div>
                    <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
                      支持图片输入的模型
                    </label>
                    <p className="text-xs text-slate-400 dark:text-slate-500 mb-2">
                      勾选该 Provider 中支持接收图片输入的模型。
                    </p>
                    <div className="flex flex-wrap gap-1.5">
                      {modelNames.map((m) => {
                        const checked = form.visionModels.includes(m)
                        return (
                          <label
                            key={m}
                            className={`inline-flex items-center gap-1.5 text-xs px-2.5 py-1.5 rounded-lg border cursor-pointer transition-all select-none ${
                              checked
                                ? 'bg-purple-50 dark:bg-purple-900/30 border-purple-300 dark:border-purple-700 text-purple-700 dark:text-purple-300'
                                : 'bg-white dark:bg-slate-700 border-slate-200 dark:border-slate-600 text-slate-600 dark:text-slate-400 hover:border-slate-300 dark:hover:border-slate-500'
                            }`}
                          >
                            <input
                              type="checkbox"
                              className="sr-only"
                              checked={checked}
                              onChange={() => {
                                setForm((f) => ({
                                  ...f,
                                  visionModels: checked
                                    ? f.visionModels.filter((v) => v !== m)
                                    : [...f.visionModels, m],
                                }))
                              }}
                            />
                            <span className={`w-3.5 h-3.5 rounded border flex items-center justify-center shrink-0 ${
                              checked
                                ? 'bg-purple-600 border-purple-600'
                                : 'border-slate-300 dark:border-slate-500'
                            }`}>
                              {checked && (
                                <svg className="w-2.5 h-2.5 text-white" viewBox="0 0 12 12" fill="none">
                                  <path d="M2 6l3 3 5-5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                                </svg>
                              )}
                            </span>
                            {m}
                          </label>
                        )
                      })}
                    </div>
                  </div>
                )
              })()}
            </div>

            {/* 表单错误 */}
            {formErrors.submit && (
              <div className="mt-3 p-2.5 rounded-lg bg-red-50 dark:bg-red-900/20 text-xs text-red-600 dark:text-red-400">
                {formErrors.submit}
              </div>
            )}

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
