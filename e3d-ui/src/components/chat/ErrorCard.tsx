/**
 * ErrorCard 组件
 * 错误消息卡片：红色边框 + 淡红背景，可复制 + 时间戳
 */

import { useState } from 'react'
import { XCircle, Copy, Check } from 'lucide-react'
import type { Message } from '@/types'

interface ErrorCardProps {
  msg: Message
}

function formatTime(ts: number): string {
  const d = new Date(ts)
  const hh = String(d.getHours()).padStart(2, '0')
  const mm = String(d.getMinutes()).padStart(2, '0')
  return `${hh}:${mm}`
}

export function ErrorCard({ msg }: ErrorCardProps) {
  const [copied, setCopied] = useState(false)

  const handleCopy = () => {
    navigator.clipboard.writeText(msg.content).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    })
  }

  return (
    <div className="flex justify-start my-1">
      <div className="max-w-[85%] w-full border border-red-300 dark:border-red-700 bg-red-50 dark:bg-red-900/20 rounded-xl px-4 py-3">
        <div className="flex items-start gap-2">
          <XCircle className="w-4 h-4 text-red-500 shrink-0 mt-0.5" />
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 mb-1">
              <span className="text-xs font-medium text-red-600 dark:text-red-400">错误</span>
              <span className="text-xs text-red-400 dark:text-red-500">{formatTime(msg.timestamp)}</span>
            </div>
            <p className="text-sm text-red-700 dark:text-red-300 whitespace-pre-wrap break-words">{msg.content}</p>
          </div>
          <button
            onClick={handleCopy}
            className="flex items-center gap-1 text-xs text-red-400 hover:text-red-600 dark:hover:text-red-300 transition-colors shrink-0"
            title="复制错误信息"
          >
            {copied ? <Check className="w-3 h-3" /> : <Copy className="w-3 h-3" />}
          </button>
        </div>
      </div>
    </div>
  )
}
