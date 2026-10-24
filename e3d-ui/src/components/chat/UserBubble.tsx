/**
 * UserBubble — Reasonix 对标
 * 右对齐 + 边框气泡 + 时间/复制/编辑 meta
 */

import { useState, memo, useRef, useLayoutEffect } from 'react'
import { Copy, Check, Pencil } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'
import type { Message } from '@/types'

interface UserBubbleProps {
  msg: Message
}

function formatTime(ts: number): string {
  const d = new Date(ts)
  const hh = String(d.getHours()).padStart(2, '0')
  const mm = String(d.getMinutes()).padStart(2, '0')
  return `${hh}:${mm}`
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export const UserBubble = memo(function UserBubble({ msg }: UserBubbleProps) {
  const [copied, setCopied] = useState(false)
  const [editing, setEditing] = useState(false)
  const [draftText, setDraftText] = useState(msg.content)
  const editUserMessage = useChatStore((s) => s.editUserMessage)
  const inputRef = useRef<HTMLTextAreaElement>(null)

  // 高度随内容自然增长（上限 8 行后滚动），避免固定 rows 造成的大片空白或挤压
  useLayoutEffect(() => {
    const el = inputRef.current
    if (!el || !editing) return
    el.style.height = 'auto'
    el.style.height = `${Math.min(el.scrollHeight, 220)}px`
  }, [draftText, editing])

  const handleCopy = () => {
    navigator.clipboard.writeText(msg.content).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    })
  }

  const handleEdit = () => {
    setDraftText(msg.content)
    setEditing(true)
  }

  const handleCancelEdit = () => setEditing(false)

  const handleSubmitEdit = () => {
    if (!draftText.trim()) return
    // 按钮文案是「发送」，就必须真的发出去：截断旧历史后以新内容重新发起本轮
    import('@/services/bridgeService').then(({ default: bridge }) => {
      editUserMessage(msg.id, draftText, (text, images, files, tabId) =>
        bridge.sendUserMessage(text, images, files, tabId)
      )
    })
    setEditing(false)
  }

  const hasAttachments = msg.attachments && msg.attachments.length > 0

  return (
    <div className={`msg msg--user${editing ? ' msg--editing' : ''}`} data-turn={msg.id}>
      <div className={`msg__body${editing ? ' msg__body--editing' : ''}`}>
        {editing ? (
          <form className="msg-edit" onSubmit={(e) => { e.preventDefault(); handleSubmitEdit() }}>
            <textarea
              ref={inputRef}
              autoFocus
              value={draftText}
              onChange={(e) => setDraftText(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Escape') { e.preventDefault(); handleCancelEdit() }
                if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) { e.preventDefault(); handleSubmitEdit() }
              }}
              className="msg-edit__input"
              aria-label="编辑消息"
            />
            <div className="msg-edit__actions">
              <span className="msg-edit__hint">Ctrl/⌘ + Enter 发送 · Esc 取消</span>
              <button className="msg-edit__btn" type="button" onClick={handleCancelEdit}>取消</button>
              <button className="msg-edit__btn msg-edit__btn--primary" type="submit" disabled={!draftText.trim()}>发送</button>
            </div>
          </form>
        ) : (
          <>
            {/* 附件 */}
            {hasAttachments && (
              <div className="msg-attachments" aria-label="附件">
                {msg.attachments!.map((att) => (
                  <div className="msg-attachment" key={att.id} title={att.name}>
                    <span className="msg-attachment__icon">
                      {att.previewUrl || att.data ? (
                        <img src={att.previewUrl || `data:${att.type};base64,${att.data}`} alt="" draggable={false} />
                      ) : null}
                    </span>
                    <span className="msg-attachment__main">
                      <span className="msg-attachment__name">{att.name}</span>
                      <span className="msg-attachment__meta">{formatSize(att.size)}</span>
                    </span>
                  </div>
                ))}
              </div>
            )}

            {/* 消息文本 */}
            {msg.content && <div className="msg__text">{msg.content}</div>}
          </>
        )}
      </div>

      {!editing && (
        <div className="msg-meta" role="group" aria-label="操作">
          <time className="msg-meta__time" dateTime={new Date(msg.timestamp).toISOString()} title={new Date(msg.timestamp).toLocaleString()}>
            {formatTime(msg.timestamp)}
          </time>
          <button
            className="msg-meta__btn"
            onClick={handleCopy}
            title="复制"
          >
            {copied ? <Check className="w-3.5 h-3.5" /> : <Copy className="w-3.5 h-3.5" />}
          </button>
          <button
            className="msg-meta__btn"
            onClick={handleEdit}
            title="编辑"
          >
            <Pencil className="w-3.5 h-3.5" />
          </button>
        </div>
      )}
    </div>
  )
})
