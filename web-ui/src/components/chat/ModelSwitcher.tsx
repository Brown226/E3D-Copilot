import { useEffect, useRef, useState } from "react"
import { Brain, Check, ChevronsUpDown, RefreshCw, Loader2 } from "lucide-react"
import { ModelsServiceClient, getCurrentModel, getProviders, subscribeProviders, type ModelInfo, type ProviderView } from "@/services/grpc-client"

/**
 * 模型切换器（参考 Reasonix ModelSwitcher）
 * - 显示当前激活的 model
 * - 点击弹出 popover 列出所有 provider/model
 * - 选择后通知后端切换
 */
export const ModelSwitcher: React.FC = () => {
  const [open, setOpen] = useState(false)
  const [closing, setClosing] = useState(false)
  const [models, setModels] = useState<ModelInfo[]>([])
  const [providers, setProviders] = useState<ProviderView[]>([])
  const [loading, setLoading] = useState(false)
  const [, setTick] = useState(0)
  const triggerRef = useRef<HTMLButtonElement>(null)

  // 订阅 providers 变化
  useEffect(() => {
    const unsub = subscribeProviders(() => setTick((t) => t + 1))
    return () => { unsub() }
  }, [])

  // 打开时拉取数据
  useEffect(() => {
    if (open) {
      setLoading(true)
      Promise.all([
        ModelsServiceClient.listModels({}).catch(() => ({ models: [] })),
        ModelsServiceClient.listProviders({}).catch(() => ({ providers: [] })),
      ]).then(([m, p]) => {
        setModels((m as any).models || [])
        setProviders((p as any).providers || [])
        setLoading(false)
      })
    }
  }, [open])

  const current = getCurrentModel()
  const currentDisplay = current.model || "选择模型"

  // 按 provider 分组
  const grouped = providers.map((p) => ({
    provider: p,
    models: models.filter((m) => m.provider === p.name),
  }))

  const triggerWidth = triggerRef.current?.getBoundingClientRect().width

  const handleSwitch = async (ref: string) => {
    if (ref === current.ref) {
      setOpen(false)
      return
    }
    setLoading(true)
    try {
      await ModelsServiceClient.switchModel({ ref })
      // 重新拉取确保同步
      await ModelsServiceClient.listModels({})
    } catch (e) {
      console.error("[ModelSwitcher] switch failed:", e)
    }
    setLoading(false)
    setOpen(false)
  }

  const handleRefresh = async () => {
    setLoading(true)
    try {
      await ModelsServiceClient.listModels({})
      await ModelsServiceClient.listProviders({})
    } catch {}
    setLoading(false)
  }

  return (
    <div className="relative">
      <button
        ref={triggerRef}
        type="button"
        onClick={() => (open ? (setOpen(false), setClosing(false)) : (setClosing(false), setOpen(true)))}
        className="flex items-center gap-1.5 px-2 py-1 text-xs rounded border border-[var(--vscode-input-border)] bg-[var(--vscode-input-background)] text-[var(--vscode-input-foreground)] hover:bg-[var(--vscode-list-hoverBackground)] transition-colors"
        style={{ minWidth: "160px" }}
        aria-expanded={open}
        title={`当前模型: ${currentDisplay}`}
      >
        <Brain size={12} className="opacity-70" />
        <span className="truncate flex-1 text-left">{currentDisplay}</span>
        {loading ? <Loader2 size={11} className="animate-spin opacity-50" /> : <ChevronsUpDown size={11} className="opacity-50" />}
      </button>

      {open && (
        <>
          <div
            className="fixed inset-0 z-40"
            onClick={() => { setClosing(true); setTimeout(() => { setOpen(false); setClosing(false) }, 120) }}
          />
          <div
            className={`absolute bottom-full left-0 mb-1 z-50 bg-[var(--vscode-menu-background)] border border-[var(--vscode-menu-border)] rounded-md shadow-lg overflow-hidden transition-opacity ${closing ? "opacity-0" : "opacity-100"}`}
            style={{ minWidth: triggerWidth ? Math.max(triggerWidth, 240) : 280, maxWidth: 400 }}
          >
            <div className="flex items-center justify-between px-3 py-1.5 border-b border-[var(--vscode-menu-border)] bg-[var(--vscode-menu-selectionBackground)] text-xs">
              <span className="font-medium text-[var(--vscode-menu-foreground)]">切换模型</span>
              <button
                type="button"
                onClick={(e) => { e.stopPropagation(); handleRefresh() }}
                className="flex items-center gap-1 text-[var(--vscode-textLink-foreground)] hover:underline"
                title="刷新模型列表"
              >
                <RefreshCw size={11} />
                刷新
              </button>
            </div>

            <div className="max-h-80 overflow-y-auto py-1">
              {loading && grouped.length === 0 && (
                <div className="px-3 py-2 text-xs opacity-60 text-center">加载中...</div>
              )}
              {!loading && grouped.length === 0 && (
                <div className="px-3 py-2 text-xs opacity-60 text-center">未配置模型，请到设置中添加</div>
              )}
              {grouped.map(({ provider, models: pmodels }) => (
                <div key={provider.name} className="py-0.5">
                  <div className="px-3 py-1 text-[10px] uppercase tracking-wider opacity-50 font-semibold">
                    {provider.name} {provider.keySet ? "" : "(无 Key)"}
                  </div>
                  {pmodels.length === 0 ? (
                    <div className="px-3 py-1 text-xs opacity-40 italic">无模型（需在设置中拉取）</div>
                  ) : (
                    pmodels.map((m) => {
                      const isCurrent = m.ref === current.ref
                      return (
                        <button
                          key={m.ref}
                          type="button"
                          onClick={() => handleSwitch(m.ref)}
                          className={`w-full px-3 py-1.5 text-left text-xs flex items-center justify-between hover:bg-[var(--vscode-menu-selectionBackground)] transition-colors ${isCurrent ? "bg-[var(--vscode-menu-selectionBackground)]" : ""}`}
                        >
                          <span className="truncate flex-1" title={m.model}>{m.model}</span>
                          {isCurrent && <Check size={12} className="text-[var(--vscode-textLink-foreground)]" />}
                        </button>
                      )
                    })
                  )}
                </div>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  )
}
