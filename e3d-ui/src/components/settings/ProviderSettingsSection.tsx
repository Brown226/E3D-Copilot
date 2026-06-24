/**
 * T5.3 / T5.4 / T5.5 / T5.6 ProviderSettingsSection 组件
 * Provider 卡片列表 + 编辑/添加表单 + 刷新模型 + API Key 掩码
 */

import { useState, useEffect, useCallback } from 'react'
import {
  Plus,
  RefreshCw,
  Pencil,
  Trash2,
  Star,
  Loader2,
  Eye,
  EyeOff,
  X,
} from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'
import type { ProviderInfo } from '@/services/messageContracts'

// ============================================
// API Key 掩码工具 (T5.6)
// ============================================

function maskApiKey(key: string, keySet: boolean): string {
  if (!keySet) return '（未设置）'
  if (!key) return '（未设置）'
  if (key.length <= 7) return key
  return `${key.substring(0, 3)}****${key.substring(key.length - 4)}`
}

// ============================================
// 表单数据类型 (T5.4)
// ============================================

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

// ============================================
// 组件
// ============================================

export function ProviderSettingsSection() {
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

  const reloadProviders = useCallback(() => {
    import('@/services/bridgeService').then(({ default: bridge }) => {
      loadProviders(bridge.listProviders.bind(bridge))
    })
  }, [loadProviders])

  // ESC 关闭表单
  useEffect(() => {
    if (!showForm) return
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { setShowForm(false); setEditingProvider(null) }
    }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [showForm])

  // ============================================
  // 刷新模型 (T5.5)
  // ============================================

  const handleRefreshModels = async (providerName: string) => {
    setRefreshingProvider(providerName)
    setFetchError(null)
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      const result = await bridge.fetchProviderModels(providerName) as {
        success?: boolean;
        models?: string[];
        error?: string;
      } | null
      if (result && !result.success && result.error) {
        setFetchError(`刷新 ${providerName} 失败: ${result.error}`)
      }
      // 重新加载列表以更新 UI
      reloadProviders()
    } catch (err) {
      setFetchError(`刷新 ${providerName} 失败: ${err instanceof Error ? err.message : '未知错误'}`)
    } finally {
      setRefreshingProvider(null)
    }
  }

  // ============================================
  // 设为默认
  // ============================================

  const handleSetDefault = async (providerName: string, models: string[]) => {
    if (models.length === 0) return
    const defaultModel = models[0]
    const ref = `${providerName}/${defaultModel}`
    const { default: bridge } = await import('@/services/bridgeService')
    switchModel(ref, bridge.switchModel.bind(bridge))
  }

  // ============================================
  // 删除 Provider
  // ============================================

  const handleDelete = async (providerName: string) => {
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      await bridge.deleteProvider(providerName)
      setDeleteConfirm(null)
      reloadProviders()
    } catch (err) {
      setFetchError(`删除 ${providerName} 失败: ${err instanceof Error ? err.message : '未知错误'}`)
    }
  }

  // ============================================
  // 编辑 Provider
  // ============================================

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

  // ============================================
  // 添加 Provider
  // ============================================

  const handleAdd = () => {
    setEditingProvider(null)
    setForm(emptyForm)
    setFormErrors({})
    setShowApiKey(false)
    setShowForm(true)
  }

  // ============================================
  // 表单验证 + 保存 (T5.4)
  // ============================================

  const validateForm = (): boolean => {
    const errors: Record<string, string> = {}
    if (!editingProvider && !form.name.trim()) {
      errors.name = '名称不能为空'
    }
    if (!form.baseUrl.trim()) {
      errors.baseUrl = 'Base URL 不能为空'
    }
    setFormErrors(errors)
    return Object.keys(errors).length === 0
  }

  const handleSave = async () => {
    if (!validateForm()) return
    setSaving(true)
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      // 保存 provider 配置
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

      // 保存 API Key（如果有输入）
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

  // ============================================
  // 渲染
  // ============================================

  return (
    <div className="space-y-4">
      {/* 页面标题 */}
      <div className="mb-6">
        <h3 className="text-base font-semibold text-slate-800 dark:text-slate-100 mb-1">Provider 管理</h3>
        <p className="text-sm text-slate-500 dark:text-slate-400">管理 AI 模型提供商和 API 密钥</p>
      </div>

      {/* 错误信息 */}
      {fetchError && (
        <div className="p-3 rounded-lg bg-red-50 border border-red-200 text-sm text-red-700 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400 flex items-center justify-between">
          <span>{fetchError}</span>
          <button onClick={() => setFetchError(null)} className="ml-2 text-red-400 hover:text-red-600">
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Provider 卡片列表 (T5.3) */}
      {providers.map((provider) => {
        const isCurrent = provider.name === currentProvider
        const isRefreshing = refreshingProvider === provider.name
        const isDeleting = deleteConfirm === provider.name

        return (
          <div
            key={provider.name}
            className={`rounded-xl border p-4 transition-all ${
              isCurrent
                ? 'border-blue-300 bg-blue-50/50 dark:border-blue-700 dark:bg-blue-900/20'
                : 'border-slate-200 bg-white dark:border-slate-700 dark:bg-slate-800'
            }`}
          >
            {/* 标题行 */}
            <div className="flex items-center justify-between mb-2">
              <div className="flex items-center gap-2">
                <span className="font-bold text-slate-800 dark:text-slate-100">
                  {provider.name}
                </span>
                <span className="text-xs px-2 py-0.5 rounded-full bg-slate-100 text-slate-600 dark:bg-slate-700 dark:text-slate-300">
                  {provider.kind}
                </span>
                {isCurrent && (
                  <span className="text-xs px-2 py-0.5 rounded-full bg-blue-100 text-blue-700 font-medium dark:bg-blue-900/40 dark:text-blue-300">
                    默认
                  </span>
                )}
                {provider.builtIn && (
                  <span className="text-xs px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400">
                    内置
                  </span>
                )}
              </div>
            </div>

            {/* Base URL */}
            <p className="text-sm text-slate-500 dark:text-slate-400 truncate mb-2" title={provider.baseUrl}>
              {provider.baseUrl || '（未设置 URL）'}
            </p>

            {/* API Key 掩码 (T5.6) */}
            <p className="text-xs text-slate-400 dark:text-slate-500 mb-2">
              API Key: <span className="font-mono">{maskApiKey(provider.apiKey, provider.keySet)}</span>
            </p>

            {/* 模型列表 */}
            <p className="text-sm text-slate-600 dark:text-slate-300 mb-3">
              {provider.models.length > 0 ? (
                <>
                  模型:{' '}
                  <span className="text-slate-700 dark:text-slate-200">
                    {provider.models.slice(0, 5).join(', ')}
                  </span>
                  {provider.models.length > 5 && (
                    <span className="text-slate-400"> 共 {provider.models.length} 个</span>
                  )}
                  {provider.models.length <= 5 && (
                    <span className="text-slate-400"> 共 {provider.models.length} 个</span>
                  )}
                </>
              ) : (
                <span className="text-slate-400 dark:text-slate-500">暂无模型</span>
              )}
            </p>

            {/* 操作按钮 */}
            <div className="flex items-center gap-2 flex-wrap">
              {!isCurrent && provider.models.length > 0 && (
                <button
                  onClick={() => handleSetDefault(provider.name, provider.models)}
                  className="flex items-center gap-1 px-3 py-1.5 text-xs rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors dark:border-slate-600 dark:text-slate-300 dark:hover:bg-slate-700"
                >
                  <Star className="w-3.5 h-3.5" />
                  设为默认
                </button>
              )}
              <button
                onClick={() => handleRefreshModels(provider.name)}
                disabled={isRefreshing}
                className="flex items-center gap-1 px-3 py-1.5 text-xs rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors dark:border-slate-600 dark:text-slate-300 dark:hover:bg-slate-700 disabled:opacity-50"
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
                className="flex items-center gap-1 px-3 py-1.5 text-xs rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors dark:border-slate-600 dark:text-slate-300 dark:hover:bg-slate-700"
              >
                <Pencil className="w-3.5 h-3.5" />
                编辑
              </button>
              {!provider.builtIn && (
                <>
                  {isDeleting ? (
                    <div className="flex items-center gap-1">
                      <span className="text-xs text-red-600 dark:text-red-400">确认删除？</span>
                      <button
                        onClick={() => handleDelete(provider.name)}
                        className="px-2 py-1 text-xs rounded bg-red-500 text-white hover:bg-red-600 transition-colors"
                      >
                        删除
                      </button>
                      <button
                        onClick={() => setDeleteConfirm(null)}
                        className="px-2 py-1 text-xs rounded bg-slate-200 text-slate-600 hover:bg-slate-300 transition-colors dark:bg-slate-600 dark:text-slate-200 dark:hover:bg-slate-500"
                      >
                        取消
                      </button>
                    </div>
                  ) : (
                    <button
                      onClick={() => setDeleteConfirm(provider.name)}
                      className="flex items-center gap-1 px-3 py-1.5 text-xs rounded-lg border border-red-200 text-red-500 hover:bg-red-50 transition-colors dark:border-red-800 dark:text-red-400 dark:hover:bg-red-900/20"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                      删除
                    </button>
                  )}
                </>
              )}
            </div>
          </div>
        )
      })}

      {/* 添加 Provider 按钮 */}
      <button
        onClick={handleAdd}
        className="w-full p-4 rounded-xl border-2 border-dashed border-slate-300 text-slate-500 hover:border-blue-400 hover:text-blue-500 hover:bg-blue-50/50 transition-all dark:border-slate-600 dark:text-slate-400 dark:hover:border-blue-500 dark:hover:text-blue-400 dark:hover:bg-blue-900/10 flex items-center justify-center gap-2"
      >
        <Plus className="w-5 h-5" />
        <span className="text-sm font-medium">添加 Provider</span>
      </button>

      {/* 编辑/添加表单 Modal (T5.4) */}
      {showForm && (
        <div className="fixed inset-0 z-[60]">
          {/* 遮罩 */}
          <div
            className="absolute inset-0 bg-black/40 backdrop-blur-sm"
            onClick={() => { setShowForm(false); setEditingProvider(null) }}
          />
          {/* 表单卡片 */}
          <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-full max-w-md bg-white dark:bg-slate-800 rounded-2xl shadow-2xl p-6">
            <div className="flex items-center justify-between mb-6">
              <h3 className="text-lg font-bold text-slate-800 dark:text-slate-100">
                {editingProvider ? '编辑 Provider' : '添加 Provider'}
              </h3>
              <button
                onClick={() => { setShowForm(false); setEditingProvider(null) }}
                className="p-1 text-slate-400 hover:text-slate-600 rounded-lg hover:bg-slate-100 transition-colors dark:hover:text-slate-200 dark:hover:bg-slate-700"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="space-y-4">
              {/* 名称 */}
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
                  名称
                </label>
                <input
                  type="text"
                  value={form.name}
                  onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                  disabled={!!editingProvider}
                  placeholder="例如: openai, anthropic"
                  className={`w-full px-3 py-2 text-sm rounded-lg border bg-white dark:bg-slate-700 text-slate-800 dark:text-slate-100 placeholder-slate-400 dark:placeholder-slate-500 outline-none transition-colors
                    ${formErrors.name ? 'border-red-400' : 'border-slate-300 dark:border-slate-600'}
                    focus:border-blue-500 dark:focus:border-blue-400
                    disabled:opacity-50 disabled:cursor-not-allowed`}
                />
                {formErrors.name && (
                  <p className="text-xs text-red-500 mt-1">{formErrors.name}</p>
                )}
              </div>

              {/* 类型 */}
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
                  类型
                </label>
                <select
                  value={form.kind}
                  onChange={(e) => setForm((f) => ({ ...f, kind: e.target.value }))}
                  className="w-full px-3 py-2 text-sm rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-800 dark:text-slate-100 outline-none focus:border-blue-500 dark:focus:border-blue-400 transition-colors"
                >
                  <option value="openai">OpenAI</option>
                  <option value="anthropic">Anthropic</option>
                </select>
              </div>

              {/* Base URL */}
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
                  Base URL
                </label>
                <input
                  type="text"
                  value={form.baseUrl}
                  onChange={(e) => setForm((f) => ({ ...f, baseUrl: e.target.value }))}
                  placeholder="https://api.openai.com/v1"
                  className={`w-full px-3 py-2 text-sm rounded-lg border bg-white dark:bg-slate-700 text-slate-800 dark:text-slate-100 placeholder-slate-400 dark:placeholder-slate-500 outline-none transition-colors
                    ${formErrors.baseUrl ? 'border-red-400' : 'border-slate-300 dark:border-slate-600'}
                    focus:border-blue-500 dark:focus:border-blue-400`}
                />
                {formErrors.baseUrl && (
                  <p className="text-xs text-red-500 mt-1">{formErrors.baseUrl}</p>
                )}
              </div>

              {/* API Key */}
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
                  API Key
                </label>
                <div className="relative">
                  <input
                    type={showApiKey ? 'text' : 'password'}
                    value={form.apiKey}
                    onChange={(e) => setForm((f) => ({ ...f, apiKey: e.target.value }))}
                    placeholder={editingProvider ? '留空则不更新' : '输入 API Key'}
                    className="w-full px-3 py-2 pr-10 text-sm rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-800 dark:text-slate-100 placeholder-slate-400 dark:placeholder-slate-500 outline-none focus:border-blue-500 dark:focus:border-blue-400 transition-colors"
                  />
                  <button
                    type="button"
                    onClick={() => setShowApiKey((v) => !v)}
                    className="absolute right-2 top-1/2 -translate-y-1/2 p-1 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
                  >
                    {showApiKey ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
              </div>

              {/* 默认模型 */}
              <div>
                <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
                  默认模型
                </label>
                <input
                  type="text"
                  value={form.defaultModel}
                  onChange={(e) => setForm((f) => ({ ...f, defaultModel: e.target.value }))}
                  placeholder="例如: gpt-4o"
                  className="w-full px-3 py-2 text-sm rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-800 dark:text-slate-100 placeholder-slate-400 dark:placeholder-slate-500 outline-none focus:border-blue-500 dark:focus:border-blue-400 transition-colors"
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
