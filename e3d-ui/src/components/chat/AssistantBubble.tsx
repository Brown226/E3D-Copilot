/**
 * AssistantBubble — Reasonix 对标
 * 纯文本 + 内联 reasoning 折叠 + 复制/重试
 */

import { useState, useRef, useEffect } from 'react'
import { ChevronRight, Copy, Check, RotateCcw } from 'lucide-react'
import MarkdownBlock from '@/components/common/MarkdownBlock'
import { useChatStore } from '@/store/useChatStore'
import type { Message } from '@/types'

interface AssistantBubbleProps {
  msg: Message
  /** true = turn 结束后的最终回复（显示操作栏），false = 中间步骤（隐藏操作栏） */
  isFinal?: boolean
}

/** 内联 reasoning 折叠块 — Reasonix 风格 */
export function ReasoningBlock({ msg }: { msg: Message }) {
  const [open, setOpen] = useState(!msg.finalized) // 流式时默认展开，完成后折叠
  const bodyRef = useRef<HTMLDivElement>(null)

  // 流式时自动滚动 reasoning 到底
  useEffect(() => {
    if (open && !msg.finalized && bodyRef.current) {
      bodyRef.current.scrollTop = bodyRef.current.scrollHeight
    }
  }, [msg.content, open, msg.finalized])

  return (
    <div className="reasoning">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        className="reasoning__head"
        data-running={!msg.finalized ? '' : undefined}
        aria-expanded={open}
      >
        {/* 思考图标 */}
        <svg className="w-3 h-3" viewBox="0 0 24 24" fill="currentColor">
          <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z"/>
        </svg>
        <span>{!msg.finalized ? '正在思考…' : '思考过程'}</span>
        <span className="reasoning__meta">{!msg.finalized ? '运行中' : '已完成'}</span>
        <ChevronRight
          size={12}
          className={`reasoning__chevron${open ? ' reasoning__chevron--open' : ''}`}
        />
      </button>
      {open && (
        <div ref={bodyRef} className="reasoning__body">
          {msg.content}
        </div>
      )}
    </div>
  )
}

export function AssistantBubble({ msg, isFinal = true }: AssistantBubbleProps) {
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
  const hasError = !!msg.errorMessage || !!msg.toolError
  const errorText = msg.errorMessage || msg.toolError

  return (
    <div className="msg msg--assistant" data-entrance={msg.id}>
      {/* 消息正文 */}
      <div className="msg__body">
        {hasContent ? (
          <MarkdownBlock markdown={msg.content} showCursor={!msg.finalized} />
        ) : hasError ? (
          /* 有真实错误信息时显示 */
          <div style={{ color: 'var(--error)', fontSize: '14px' }}>
            <strong>错误：</strong>{errorText}
          </div>
        ) : (
          /* 无内容且无错误时的通用提示 */
          <div style={{ color: 'var(--fg-dim)', fontStyle: 'italic' }}>
            {!msg.finalized ? '正在生成回复…' : '抱歉，未能生成有效回复'}
          </div>
        )}
      </div>

      {/* 操作栏（仅最终回复显示，中间步骤不显示） */}
      {msg.finalized && isFinal && (
        <div className="turn-actions">
          {hasContent && (
            <button className="turn-actions__btn" onClick={handleCopy} title="复制">
              {copied ? <Check size={13} /> : <Copy size={13} />}
              <span>{copied ? '已复制' : '复制'}</span>
            </button>
          )}
          <button className="turn-actions__btn" onClick={handleReroll} title="重新生成">
            <RotateCcw size={13} />
            <span>重新生成</span>
          </button>
        </div>
      )}
    </div>
  )
}
