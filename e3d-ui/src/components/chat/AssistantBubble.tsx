/**
 * AssistantBubble 组件
 * 左对齐，白色背景，带边框
 * 未完成时末尾显示闪烁光标
 * 增加：复制按钮 + 时间戳 + 操作栏
 */

import MarkdownBlock from '@/components/common/MarkdownBlock'
import { TurnActions } from './TurnActions'
import { useChatStore } from '@/store/useChatStore'
import type { Message } from '@/types'

interface AssistantBubbleProps {
  msg: Message
}

function formatTime(ts: number): string {
  const d = new Date(ts)
  const hh = String(d.getHours()).padStart(2, '0')
  const mm = String(d.getMinutes()).padStart(2, '0')
  return `${hh}:${mm}`
}

export function AssistantBubble({ msg }: AssistantBubbleProps) {
  const rerollLastMessage = useChatStore((s) => s.rerollLastMessage)

  const handleReroll = () => {
    import('@/services/bridgeService').then(({ default: bridge }) => {
      rerollLastMessage(bridge.sendUserMessage.bind(bridge))
    })
  }

  return (
    <div className="flex justify-start group">
      <div className="max-w-[85%]">
        {/* 标题行 */}
        <div className="flex items-center gap-2 mb-1 ml-1">
          <span className="text-xs text-slate-400 dark:text-slate-500">小智</span>
          <span className="text-xs text-slate-300 dark:text-slate-600">
            {formatTime(msg.timestamp)}
          </span>
        </div>

        {/* 消息气泡 */}
        <div className="rounded-2xl px-4 py-3 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-600 text-slate-800 dark:text-slate-100 text-base relative">
          <MarkdownBlock markdown={msg.content} showCursor={!msg.finalized} />
        </div>

        {/* 操作栏（完成后显示） */}
        {msg.finalized && msg.content && (
          <TurnActions content={msg.content} onReroll={handleReroll} />
        )}
      </div>
    </div>
  )
}
