/**
 * ApprovalCard — 内联工具审批卡片
 * 当 pendingApproval 存在时显示在消息流底部（非全屏模态）
 * 支持键盘 1(允许) 2(本次允许) 3(拒绝) 快捷操作
 */

import { useEffect, useRef, useState } from 'react'
import { Shield, ShieldCheck, X, AlertTriangle } from 'lucide-react'
import type { PendingApproval } from '@/store/useChatStore'

interface ApprovalCardProps {
  approval: PendingApproval
  onAnswer: (allow: boolean, session: boolean) => void
}

export function ApprovalCard({ approval, onAnswer }: ApprovalCardProps) {
  const [selectedIndex, setSelectedIndex] = useState(0)
  const cardRef = useRef<HTMLDivElement>(null)

  // 键盘快捷键 1/2/3
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      const target = e.target as Element | null
      const tag = target?.tagName.toLowerCase()
      if (tag === 'input' || tag === 'textarea' || tag === 'select') return

      if (e.key === '1') { e.preventDefault(); handleAllow(false) }
      else if (e.key === '2') { e.preventDefault(); handleAllow(true) }
      else if (e.key === '3') { e.preventDefault(); handleDeny() }
      else if (e.key === 'ArrowLeft') { e.preventDefault(); setSelectedIndex((i) => (i - 1 + 3) % 3) }
      else if (e.key === 'ArrowRight') { e.preventDefault(); setSelectedIndex((i) => (i + 1) % 3) }
      else if (e.key === 'Enter') {
        e.preventDefault()
        if (selectedIndex === 0) handleAllow(false)
        else if (selectedIndex === 1) handleAllow(true)
        else handleDeny()
      }
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [selectedIndex])

  const handleAllow = (session: boolean) => onAnswer(true, session)
  const handleDeny = () => onAnswer(false, false)

  const actions = [
    { key: '1', label: '允许', onClick: () => handleAllow(false), icon: Shield, tone: 'allow' },
    { key: '2', label: '本次允许', onClick: () => handleAllow(true), icon: ShieldCheck, tone: 'session' },
    { key: '3', label: '拒绝', onClick: handleDeny, icon: X, tone: 'deny' },
  ]

  return (
    <div
      ref={cardRef}
      className="mx-1 my-2 rounded-xl border border-amber-300 dark:border-amber-700 bg-amber-50 dark:bg-amber-900/20 overflow-hidden shadow-sm"
      role="dialog"
      aria-label="工具审批"
    >
      {/* 头部 */}
      <div className="flex items-center gap-2 px-3 py-2 border-b border-amber-200 dark:border-amber-800">
        <AlertTriangle className="w-4 h-4 text-amber-600 dark:text-amber-400 shrink-0" />
        <div className="flex-1 min-w-0">
          <span className="text-sm font-semibold text-amber-800 dark:text-amber-200">工具待审批</span>
          <span className="ml-2 text-xs font-mono text-amber-600 dark:text-amber-400 truncate">
            {approval.toolName}
          </span>
        </div>
      </div>

      {/* 描述 / 参数预览 */}
      {approval.description && (
        <div className="px-3 py-1.5 text-xs text-amber-700 dark:text-amber-300 border-b border-amber-100 dark:border-amber-800/50">
          {approval.description}
        </div>
      )}
      {approval.args && (
        <pre className="px-3 py-1.5 text-[11px] font-mono text-amber-600/80 dark:text-amber-400/70 bg-amber-50/50 dark:bg-amber-900/10 overflow-x-auto max-h-24 border-b border-amber-100 dark:border-amber-800/50">
          {typeof approval.args === 'string' ? approval.args : JSON.stringify(approval.args, null, 2)}
        </pre>
      )}

      {/* 操作按钮 */}
      <div className="flex gap-1 p-2">
        {actions.map((action, i) => {
          const Icon = action.icon
          const isSelected = i === selectedIndex
          return (
            <button
              key={action.key}
              onClick={action.onClick}
              className={`flex-1 flex items-center justify-center gap-1.5 h-9 rounded-lg text-xs font-semibold border transition-all ${
                isSelected ? 'ring-2 ring-amber-400 dark:ring-amber-600' : ''
              } ${
                action.tone === 'allow'
                  ? 'border-emerald-300 dark:border-emerald-700 bg-emerald-50 dark:bg-emerald-900/20 text-emerald-700 dark:text-emerald-300 hover:bg-emerald-100 dark:hover:bg-emerald-900/30'
                  : action.tone === 'session'
                  ? 'border-blue-300 dark:border-blue-700 bg-blue-50 dark:bg-blue-900/20 text-blue-700 dark:text-blue-300 hover:bg-blue-100 dark:hover:bg-blue-900/30'
                  : 'border-red-300 dark:border-red-700 bg-red-50 dark:bg-red-900/20 text-red-700 dark:text-red-300 hover:bg-red-100 dark:hover:bg-red-900/30'
              }`}
            >
              <Icon className="w-3.5 h-3.5" />
              <span>{action.label}</span>
              <kbd className="ml-0.5 px-1 py-0.5 rounded text-[10px] bg-black/10 dark:bg-white/10 font-mono">{action.key}</kbd>
            </button>
          )
        })}
      </div>
    </div>
  )
}
