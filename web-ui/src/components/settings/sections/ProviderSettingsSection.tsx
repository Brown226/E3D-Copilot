import { Plus, Trash2, RefreshCw, Eye, EyeOff, Star, Loader2, AlertCircle } from "lucide-react"
import { useCallback, useEffect, useState } from "react"
import { ModelsServiceClient, subscribeProviders, type ProviderView } from "@/services/grpc-client"
import Section from "../Section"

interface Props { renderSectionHeader?: (tabId: string) => JSX.Element | null }

const emptyForm = { name: "", kind: "openai", baseUrl: "", apiKey: "", defaultModel: "" }

const ProviderSettingsSection = ({ renderSectionHeader }: Props) => {
  const [providers, setProviders] = useState<ProviderView[]>([])
  const [currentProvider, setCurrentProvider] = useState("")
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [editingName, setEditingName] = useState<string | null>(null)
  const [form, setForm] = useState(emptyForm)
  const [formError, setFormError] = useState<string | null>(null)
  const [formSaving, setFormSaving] = useState(false)
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null)
  const [fetchingModels, setFetchingModels] = useState<string | null>(null)
  const [visibleKeys, setVisibleKeys] = useState<Set<string>>(new Set())

  const loadData = useCallback(async () => {
    setLoading(true); setError(null)
    try {
      const r: any = await ModelsServiceClient.listProviders({})
      setProviders(r?.providers || [])
      setCurrentProvider(r?.currentProvider || "")
    } catch (e: any) { setError(e?.message || "加载失败") }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { loadData() }, [loadData])
  useEffect(() => { const u = subscribeProviders(() => loadData()); return () => u() }, [loadData])

  const openAdd = () => { setEditingName(null); setForm(emptyForm); setFormError(null); setShowForm(true) }
  const openEdit = (p: ProviderView) => {
    setEditingName(p.name)
    setForm({ name: p.name, kind: p.kind || "openai", baseUrl: p.baseUrl || "", apiKey: "", defaultModel: p.default || "" })
    setFormError(null); setShowForm(true)
  }

  const handleSave = async () => {
    if (!form.name.trim()) { setFormError("名称不能为空"); return }
    if (!form.baseUrl.trim()) { setFormError("Base URL 不能为空"); return }
    setFormSaving(true); setFormError(null)
    try {
      await ModelsServiceClient.saveProvider({ name: form.name.trim(), kind: form.kind, baseUrl: form.baseUrl.trim(), apiKey: form.apiKey, models: editingName ? undefined : [], default: form.defaultModel.trim(), enabled: true, builtIn: false })
      setShowForm(false); await loadData()
    } catch (e: any) { setFormError(e?.message || "保存失败") }
    finally { setFormSaving(false) }
  }

  const handleDelete = async (name: string) => {
    try { await ModelsServiceClient.deleteProvider({ name }); setDeleteConfirm(null); await loadData() }
    catch (e: any) { setError(e?.message || "删除失败") }
  }

  const handleFetch = async (name: string) => {
    setFetchingModels(name)
    try {
      const r: any = await ModelsServiceClient.fetchProviderModels({ name })
      if (r?.success) await loadData()
      else setError("拉取模型失败: " + (r?.error || "未知错误"))
    } catch (e: any) { setError(e?.message || "拉取模型失败") }
    finally { setFetchingModels(null) }
  }

  const handleSetDefault = async (ref: string) => {
    try { await ModelsServiceClient.switchModel({ ref }); await loadData() }
    catch (e: any) { setError(e?.message || "切换失败") }
  }

  const toggleKey = (name: string) => setVisibleKeys(p => { const n = new Set(p); n.has(name) ? n.delete(name) : n.add(name); return n })

  return (
    <div>
      {renderSectionHeader?.("provider-management")}
      {error && (
        <div className="flex items-center gap-2 px-3 py-2 mb-3 text-sm text-[var(--vscode-errorForeground)] bg-[var(--vscode-inputValidation-errorBackground)] border border-[var(--vscode-inputValidation-errorBorder)] rounded">
          <AlertCircle size={14} /><span>{error}</span>
          <button type="button" className="ml-auto text-xs underline" onClick={() => setError(null)}>关闭</button>
        </div>
      )}
      <Section>
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-sm font-medium">Provider 列表</h3>
          <button type="button" onClick={openAdd} className="flex items-center gap-1 px-2 py-1 text-xs rounded border border-[var(--vscode-button-border)] bg-[var(--vscode-button-secondaryBackground)] text-[var(--vscode-button-secondaryForeground)] hover:bg-[var(--vscode-button-secondaryHoverBackground)]"><Plus size={12} />添加 Provider</button>
        </div>
        {loading ? (
          <div className="flex items-center gap-2 py-4 text-sm opacity-60"><Loader2 size={14} className="animate-spin" />加载中...</div>
        ) : providers.length === 0 ? (
          <div className="py-6 text-center text-sm opacity-50">暂无 Provider，点击「添加 Provider」开始配置</div>
        ) : (
          <div className="space-y-2">
            {providers.map(p => {
              const isDefault = p.name === currentProvider
              const mc = p.models?.length || 0
              const defRef = p.name + "/" + (p.default || p.models?.[0] || "")
              return (
                <div key={p.name} className={"border rounded-md p-3 " + (isDefault ? "border-[var(--vscode-textLink-foreground)] bg-[var(--vscode-textLink-foreground)]/5" : "border-[var(--vscode-panel-border)]")}>
                  <div className="flex items-center gap-2 mb-1.5">
                    <span className="font-semibold text-sm">{p.name}</span>
                    <span className="text-[10px] px-1.5 py-0.5 rounded bg-[var(--vscode-badge-background)] text-[var(--vscode-badge-foreground)] uppercase">{p.kind}</span>
                    {isDefault && <span className="text-[10px] px-1.5 py-0.5 rounded bg-[var(--vscode-textLink-foreground)]/15 text-[var(--vscode-textLink-foreground)]">默认</span>}
                    {p.builtIn && <span className="text-[10px] px-1.5 py-0.5 rounded bg-[var(--vscode-list-warningForeground)]/15 text-[var(--vscode-list-warningForeground)]">内置</span>}
                    {!p.keySet && <span className="text-[10px] px-1.5 py-0.5 rounded bg-[var(--vscode-inputValidation-errorBackground)] text-[var(--vscode-inputValidation-errorForeground)]">无 Key</span>}
                    <div className="ml-auto flex items-center gap-1">
                      {!isDefault && <button type="button" onClick={() => handleSetDefault(defRef)} className="p-1 rounded hover:bg-[var(--vscode-list-hoverBackground)]" title="设为默认"><Star size={13} className="opacity-50 hover:opacity-100" /></button>}
                      <button type="button" onClick={() => handleFetch(p.name)} disabled={fetchingModels === p.name} className="p-1 rounded hover:bg-[var(--vscode-list-hoverBackground)] disabled:opacity-50" title="拉取模型列表">{fetchingModels === p.name ? <Loader2 size={13} className="animate-spin" /> : <RefreshCw size={13} className="opacity-50 hover:opacity-100" />}</button>
                      <button type="button" onClick={() => openEdit(p)} className="text-[10px] px-1.5 py-0.5 rounded border border-[var(--vscode-panel-border)] hover:bg-[var(--vscode-list-hoverBackground)]" title="编辑">编辑</button>
                      {!p.builtIn && <button type="button" onClick={() => setDeleteConfirm(p.name)} className="p-1 rounded hover:bg-[var(--vscode-list-hoverBackground)] text-[var(--vscode-errorForeground)]" title="删除"><Trash2 size={13} /></button>}
                    </div>
                  </div>
                  <div className="text-xs opacity-60 space-y-0.5">
                    <div className="truncate" title={p.baseUrl}>{p.baseUrl || "(无 URL)"}</div>
                    <div>模型: {mc > 0 ? p.models?.slice(0, 5).join(", ") + (mc > 5 ? " ...共 " + mc + " 个" : "") : "无（点击刷新拉取）"}</div>
                  </div>
                  {deleteConfirm === p.name && (
                    <div className="mt-2 flex items-center gap-2 text-xs">
                      <span className="text-[var(--vscode-errorForeground)]">确认删除「{p.name}」？</span>
                      <button type="button" onClick={() => handleDelete(p.name)} className="px-2 py-0.5 rounded bg-[var(--vscode-errorForeground)] text-white">确认</button>
                      <button type="button" onClick={() => setDeleteConfirm(null)} className="px-2 py-0.5 rounded border border-[var(--vscode-panel-border)]">取消</button>
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        )}
      </Section>
      {showForm && (
        <div className="fixed inset-0 z-[1100] flex items-center justify-center bg-black/40" onClick={() => setShowForm(false)}>
          <div className="bg-[var(--vscode-menu-background)] border border-[var(--vscode-menu-border)] rounded-lg shadow-xl p-5 w-[420px] max-w-[95vw]" onClick={e => e.stopPropagation()}>
            <h3 className="text-sm font-semibold mb-4">{editingName ? "编辑 Provider: " + editingName : "添加 Provider"}</h3>
            {formError && <div className="mb-3 text-xs text-[var(--vscode-errorForeground)] bg-[var(--vscode-inputValidation-errorBackground)] px-2 py-1 rounded">{formError}</div>}
            <div className="space-y-3">
              <div><label className="block text-xs font-medium mb-1">名称 *</label><input type="text" value={form.name} onChange={e => setForm({...form, name: e.target.value})} disabled={!!editingName} className="w-full px-2 py-1 text-sm rounded border border-[var(--vscode-input-border)] bg-[var(--vscode-input-background)] text-[var(--vscode-input-foreground)] disabled:opacity-50" placeholder="例如: qwen37" /></div>
              <div><label className="block text-xs font-medium mb-1">类型</label><select value={form.kind} onChange={e => setForm({...form, kind: e.target.value})} className="w-full px-2 py-1 text-sm rounded border border-[var(--vscode-input-border)] bg-[var(--vscode-input-background)] text-[var(--vscode-input-foreground)]"><option value="openai">OpenAI 兼容</option><option value="anthropic">Anthropic</option></select></div>
              <div><label className="block text-xs font-medium mb-1">Base URL *</label><input type="text" value={form.baseUrl} onChange={e => setForm({...form, baseUrl: e.target.value})} className="w-full px-2 py-1 text-sm rounded border border-[var(--vscode-input-border)] bg-[var(--vscode-input-background)] text-[var(--vscode-input-foreground)]" placeholder="https://api.example.com/v1" /></div>
              <div><label className="block text-xs font-medium mb-1">API Key</label><div className="flex gap-1"><input type={visibleKeys.has(form.name || "__new__") ? "text" : "password"} value={form.apiKey} onChange={e => setForm({...form, apiKey: e.target.value})} className="flex-1 px-2 py-1 text-sm rounded border border-[var(--vscode-input-border)] bg-[var(--vscode-input-background)] text-[var(--vscode-input-foreground)]" placeholder={editingName ? "留空不修改" : "sk-..."} /><button type="button" onClick={() => toggleKey(form.name || "__new__")} className="px-2 rounded border border-[var(--vscode-input-border)] hover:bg-[var(--vscode-list-hoverBackground)]">{visibleKeys.has(form.name || "__new__") ? <EyeOff size={14} /> : <Eye size={14} />}</button></div></div>
              <div><label className="block text-xs font-medium mb-1">默认模型</label><input type="text" value={form.defaultModel} onChange={e => setForm({...form, defaultModel: e.target.value})} className="w-full px-2 py-1 text-sm rounded border border-[var(--vscode-input-border)] bg-[var(--vscode-input-background)] text-[var(--vscode-input-foreground)]" placeholder="例如: gpt-4o" /></div>
            </div>
            <div className="flex justify-end gap-2 mt-4">
              <button type="button" onClick={() => setShowForm(false)} className="px-3 py-1 text-xs rounded border border-[var(--vscode-panel-border)] hover:bg-[var(--vscode-list-hoverBackground)]">取消</button>
              <button type="button" onClick={handleSave} disabled={formSaving} className="px-3 py-1 text-xs rounded bg-[var(--vscode-button-background)] text-[var(--vscode-button-foreground)] hover:bg-[var(--vscode-button-hoverBackground)] disabled:opacity-50">{formSaving ? "保存中..." : "保存"}</button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

export default ProviderSettingsSection
