/**
 * ThinkingBlock 组件
 * 思考过程折叠块
 * 核心改进：
 * 1. 流式中展开，完成后自动折叠
 * 2. 用户手动切换后不再自动操作
 * 3. 折叠时显示简短预览
 */

import { useState, useEffect, useRef } from 'react'
import { ChevronRight, Brain } from 'lucide-react'
import type { Message } from '@/types'

interface ThinkingBlockProps {
  msg: Message
}

export function ThinkingBlock({ msg }: ThinkingBlockProps) {
  const [expanded, setExpanded] = useState(!msg.finalized)
  const userOverridden = useRef(false)
  const prevFinalized = useRef(msg.finalized)

  useEffect(() => {
    const wasFinalized = prevFinalized.current
    const nowFinalized = msg.finalized
    prevFinalized.current = nowFinalized

    if (!wasFinalized && nowFinalized) {
      if (!userOverridden.current) {
        setExpanded(false)
      }
    } else if (!nowFinalized) {
      if (!userOverridden.current) {
        setExpanded(true)
      }
    }
  }, [msg.finalized])

  const handleToggle = () => {
    userOverridden.current = true
    setExpanded((v) => !v)
  }

  const preview = msg.content.length > 60
    ? msg.content.slice(0, 60) + '...'
    : msg.content

  return (
    <div className="flex justify-start my-1">
      <div className="max-w-[85%] w-full">
        <button
          onClick={handleToggle}
          className="flex items-center gap-1.5 text-xs text-amber-600 dark:text-amber-400 mb-1 ml-1 hover:text-amber-700 dark:hover:text-amber-300 transition-colors"
        >
          <Brain className={`w-3.5 h-3.5 ${!msg.finalized ? 'animate-pulse' : ''}`} />
          <span className="font-medium">思考过程</span>
          {!msg.finalized && (
            <span className="text-amber-400 dark:text-amber-500 animate-pulse">●</span>
          )}
          <ChevronRight
            className={`w-3.5 h-3.5 transition-transform ${expanded ? 'rotate-90' : ''}`}
          />
        </button>

        {expanded && (
          <div className="border-l-2 border-amber-300 dark:border-amber-600 bg-amber-50/50 dark:bg-amber-900/10 rounded-r-lg px-3 py-2 max-h-[300px] overflow-y-auto">
            <p className="text-xs text-amber-700 dark:text-amber-300 italic whitespace-pre-wrap break-words leading-relaxed">
              {msg.content}
            </p>
          </div>
        )}

        {!expanded && preview && (
          <p className="text-xs text-amber-500 dark:text-amber-400/60 italic ml-5 truncate max-w-[400px]">
            {preview}
          </p>
        )}
      </div>
    </div>
  )
}
