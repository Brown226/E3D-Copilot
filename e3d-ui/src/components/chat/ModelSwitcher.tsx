/**
 * T5.1 ModelSwitcher 组件
 * 点击弹出下拉面板，按 provider 分组显示模型列表
 */

import { useState, useRef, useEffect, useCallback } from 'react'
import { Brain, ChevronDown, Loader2, Settings, Check } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'

export function ModelSwitcher() {
  const providers = useChatStore((s) => s.providers)
  const currentProvider = useChatStore((s) => s.currentProvider)
  const currentModel = useChatStore((s) => s.currentModel)
  const isLoadingModels = useChatStore((s) => s.isLoadingModels)
  const switchModel = useChatStore((s) => s.switchModel)
  const loadProviders = useChatStore((s) => s.loadProviders)
  const toggleSettings = useChatStore((s) => s.toggleSettings)

  const [open, setOpen] = useState(false)
  const panelRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)

  // 打开时自动加载 providers（如果尚未加载）
  const ensureProvidersLoaded = useCallback(() => {
    if (providers.length === 0) {
      import('@/services/bridgeService').then(({ default: bridge }) => {
        loadProviders(bridge.listProviders.bind(bridge))
      })
    }
  }, [providers.length, loadProviders])

  // 点击外部关闭
  useEffect(() => {
    if (!open) return
    const handler = (e: MouseEvent) => {
      if (
        panelRef.current && !panelRef.current.contains(e.target as Node) &&
        buttonRef.current && !buttonRef.current.contains(e.target as Node)
      ) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [open])

  // ESC 关闭
  useEffect(() => {
    if (!open) return
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false)
    }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [open])

  const handleToggle = () => {
    if (!open) {
      ensureProvidersLoaded()
    }
    setOpen((prev) => !prev)
  }

  const handleSelectModel = (ref: string) => {
    import('@/services/bridgeService').then(({ default: bridge }) => {
      switchModel(ref, bridge.switchModel.bind(bridge))
    })
    setOpen(false)
  }

  // 按 provider 分组模型
  const groupedModels = providers.map((p) => ({
    provider: p,
    modelEntries: p.models.map((m) => ({
      ref: `${p.name}/${m}`,
      model: m,
      isActive: p.name === currentProvider && m === currentModel,
    })),
  }))

  // 仅显示有模型的 provider
  const visibleGroups = groupedModels.filter((g) => g.modelEntries.length > 0)

  const displayLabel = currentModel
    ? `${currentProvider}/${currentModel}`
    : '选择模型'

  return (
    <div className="relative">
      <button
        ref={buttonRef}
        onClick={handleToggle}
        className="flex items-center gap-2 px-3 py-2 text-sm rounded-xl border border-slate-200 bg-white/80 hover:bg-white transition-colors dark:bg-slate-800/80 dark:border-slate-600 dark:hover:bg-slate-700 dark:text-slate-200"
        title="切换模型"
      >
        {isLoadingModels ? (
          <Loader2 className="w-4 h-4 animate-spin text-blue-500" />
        ) : (
          <Brain className="w-4 h-4 text-blue-500" />
        )}
        <span className="max-w-[160px] truncate text-slate-700 dark:text-slate-200">
          {displayLabel}
        </span>
        <ChevronDown
          className={`w-3.5 h-3.5 text-slate-400 transition-transform ${open ? 'rotate-180' : ''}`}
        />
      </button>

      {open && (
        <div
          ref={panelRef}
          className="absolute left-0 bottom-full mb-2 w-80 max-h-96 overflow-y-auto bg-white rounded-2xl shadow-2xl shadow-slate-200/60 border border-slate-200 z-50 dark:bg-slate-800 dark:border-slate-600 dark:shadow-slate-900/60"
        >
          {isLoadingModels ? (
            <div className="flex items-center justify-center py-8">
              <Loader2 className="w-6 h-6 animate-spin text-blue-500" />
              <span className="ml-2 text-sm text-slate-500 dark:text-slate-400">加载中...</span>
            </div>
          ) : visibleGroups.length === 0 ? (
            <div className="p-6 text-center">
              <Brain className="w-10 h-10 text-slate-300 dark:text-slate-600 mx-auto mb-3" />
              <p className="text-sm text-slate-500 dark:text-slate-400">未配置模型</p>
              <p className="text-xs text-slate-400 dark:text-slate-500 mt-1">请到设置中添加 Provider</p>
            </div>
          ) : (
            <>
              {visibleGroups.map(({ provider, modelEntries }) => (
                <div key={provider.name}>
                  {/* Provider 分组头 */}
                  <div className="px-4 pt-3 pb-1 flex items-center gap-2">
                    <span className="text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">
                      {provider.name}
                    </span>
                    {provider.name === currentProvider && (
                      <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-blue-100 text-blue-600 font-medium dark:bg-blue-900/40 dark:text-blue-400">
                        当前
                      </span>
                    )}
                  </div>

                  {/* 模型列表 */}
                  {modelEntries.map(({ ref, model, isActive }) => (
                    <button
                      key={ref}
                      onClick={() => handleSelectModel(ref)}
                      className={`w-full text-left px-4 py-2.5 text-sm flex items-center justify-between transition-colors
                        ${isActive
                          ? 'bg-blue-50 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300'
                          : 'text-slate-700 hover:bg-slate-50 dark:text-slate-200 dark:hover:bg-slate-700/50'
                        }`}
                    >
                      <span className="truncate">{model}</span>
                      {isActive && <Check className="w-4 h-4 text-blue-500 shrink-0 ml-2" />}
                    </button>
                  ))}
                </div>
              ))}

              {/* 底部：管理 Provider 链接 */}
              <div className="border-t border-slate-100 dark:border-slate-700 mt-1">
                <button
                  onClick={() => { toggleSettings(); setOpen(false) }}
                  className="w-full text-left px-4 py-3 text-sm text-slate-500 hover:text-blue-600 hover:bg-slate-50 flex items-center gap-2 transition-colors dark:text-slate-400 dark:hover:text-blue-400 dark:hover:bg-slate-700/50"
                >
                  <Settings className="w-4 h-4" />
                  <span>管理 Provider →</span>
                </button>
              </div>
            </>
          )}
        </div>
      )}
    </div>
  )
}
