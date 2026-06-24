/**
 * TurnActions — 消息操作栏（Re-roll / Copy / Edit）
 * 显示在 assistant 消息下方
 */

import { useState } from 'react'
import { RotateCcw, Copy, Check } from 'lucide-react'

interface TurnActionsProps {
  content: string
  onReroll?: () => void
  isStreaming?: boolean
}

export function TurnActions({ content, onReroll, isStreaming }: TurnActionsProps) {
  const [copied, setCopied] = useState(false)

  if (isStreaming) return null

  const handleCopy = () => {
    navigator.clipboard.writeText(content).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    })
  }

  return (
    <div className="flex items-center gap-1 mt-1 opacity-0 group-hover:opacity-100 transition-opacity">
      {onReroll && (
        <button
          onClick={onReroll}
          className="flex items-center gap-1 px-2 py-1 text-xs text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-700 rounded-lg transition-colors"
          title="重新生成"
        >
          <RotateCcw className="w-3 h-3" />
          重新生成
        </button>
      )}
      <button
        onClick={handleCopy}
        className="flex items-center gap-1 px-2 py-1 text-xs text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-700 rounded-lg transition-colors"
        title="复制"
      >
        {copied ? <Check className="w-3 h-3" /> : <Copy className="w-3 h-3" />}
        {copied ? '已复制' : '复制'}
      </button>
    </div>
  )
}
