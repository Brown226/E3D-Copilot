/**
 * AssistantBubble — Reasonix 风格
 * 纯文本 + 内联 reasoning 折叠 + 复制/重试
 */

import { useState, useRef, useEffect } from 'react'
import { ChevronRight, Copy, Check } from 'lucide-react'
import MarkdownBlock from '@/components/common/MarkdownBlock'
import { TurnActions } from './TurnActions'
import { useChatStore } from '@/store/useChatStore'
import type { Message } from '@/types'

interface AssistantBubbleProps {
  msg: Message
  /** 紧接其前的 thinking 消息（由 MessageRow 传入） */
  thinkingMsg?: Message
}

/** 内联 reasoning 折叠块 — 简洁行内风格 */
function ReasoningBlock({ msg }: { msg: Message }) {
  const [open, setOpen] = useState(!msg.finalized) // 流式时默认展开，完成后折叠
  const bodyRef = useRef<HTMLDivElement>(null)

  // 流式时自动滚动 reasoning 到底
  useEffect(() => {
    if (open && !msg.finalized && bodyRef.current) {
      bodyRef.current.scrollTop = bodyRef.current.scrollHeight
    }
  }, [msg.content, open, msg.finalized])

  return (
    <div className="reasoning-block">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        className="reasoning-block__head"
        data-running={!msg.finalized ? '' : undefined}
      >
        {/* 思考状态文字 */}
        <span className="reasoning-block__label">
          {!msg.finalized ? '正在思考…' : '思考过程'}
        </span>
        {/* 右侧 meta + 箭头 */}
        <span className="reasoning-block__meta">
          {!msg.finalized ? '运行中' : '已完成'}
        </span>
        <ChevronRight
          className={`reasoning-block__chevron ${open ? 'reasoning-block__chevron--open' : ''}`}
        />
      </button>
      {open && (
        <div ref={bodyRef} className="reasoning-block__body">
          <p className="text-sm text-slate-500 dark:text-slate-400 whitespace-pre-wrap break-words leading-relaxed">
            {msg.content}
          </p>
        </div>
      )}
    </div>
  )
}

export function AssistantBubble({ msg, thinkingMsg }: AssistantBubbleProps) {
  const [copied, setCopied] = useState(false)
  const rerollLastMessage = useChatStore((s) => s.rerollLastMessage)

  const handleReroll = () => {
    import('@/services/bridgeService').then(({ default: bridge }) => {
      rerollLastMessage(bridge.sendUserMessage.bind(bridge))
    })
  }

  const handleCopy = () => {
    navigator.clipboard.writeText(msg.content).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    })
  }

  const hasContent = msg.content.trim() !== ''

  return (
    <div className="msg-assistant group" data-entrance={msg.id}>
      {/* 内联 reasoning 折叠块 */}
      {thinkingMsg && <ReasoningBlock msg={thinkingMsg} />}

      {/* 消息正文 */}
      {hasContent && (
        <div className="msg-assistant__body relative">
          <MarkdownBlock markdown={msg.content} showCursor={!msg.finalized} />

          {/* 复制按钮（hover 显示） */}
          {msg.finalized && (
            <button
              onClick={handleCopy}
              className="absolute -top-1 -right-1 p-1 rounded text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 opacity-0 group-hover:opacity-100 transition-opacity"
              title="复制"
            >
              {copied ? <Check className="w-3.5 h-3.5" /> : <Copy className="w-3.5 h-3.5" />}
            </button>
          )}
        </div>
      )}

      {/* 操作栏（完成后显示） */}
      {msg.finalized && hasContent && (
        <TurnActions content={msg.content} onReroll={handleReroll} />
      )}

      {/* 分隔线 */}
      <div className="mt-3 border-t border-slate-100 dark:border-slate-800" />
    </div>
  )
}
