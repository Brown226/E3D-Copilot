/**
 * UserBubble — Reasonix 风格
 * 纯文本左对齐，无背景气泡，底部细线分隔
 */

import { useState } from 'react'
import { Copy, Check, Pencil } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'
import type { Message, Attachment } from '@/types'

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

function AttachmentChip({ attachment }: { attachment: Attachment }) {
  return (
    <span className="inline-flex items-center gap-1 px-2 py-0.5 bg-slate-100 dark:bg-slate-700 rounded text-xs text-slate-500 dark:text-slate-400">
      {attachment.previewUrl || attachment.data ? (
        <img
          src={attachment.previewUrl || `data:${attachment.type};base64,${attachment.data}`}
          alt=""
          className="w-4 h-4 rounded object-cover"
        />
      ) : null}
      <span className="truncate max-w-[100px]">{attachment.name}</span>
      <span className="text-slate-400 dark:text-slate-500">{formatSize(attachment.size)}</span>
    </span>
  )
}

export function UserBubble({ msg }: UserBubbleProps) {
  const [copied, setCopied] = useState(false)
  const [editing, setEditing] = useState(false)
  const [draftText, setDraftText] = useState(msg.content)
  const editUserMessage = useChatStore((s) => s.editUserMessage)

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
    editUserMessage(msg.id)
    setEditing(false)
  }

  const hasAttachments = msg.attachments && msg.attachments.length > 0

  return (
    <div className="group" data-entrance={msg.id}>
      {/* 用户消息正文 */}
      <div className={`msg-user ${editing ? 'msg-user--editing' : ''}`}>
        {editing ? (
          /* ── 编辑模式 ── */
          <div className="space-y-2">
            <textarea
              autoFocus
              value={draftText}
              onChange={(e) => setDraftText(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Escape') { e.preventDefault(); handleCancelEdit() }
                if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) { e.preventDefault(); handleSubmitEdit() }
              }}
              rows={Math.max(2, Math.min(8, draftText.split('\n').length))}
              className="w-full px-3 py-2 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-600 rounded-lg outline-none text-base text-slate-800 dark:text-slate-200 resize-none focus:border-blue-400 dark:focus:border-blue-500"
            />
            <div className="flex items-center gap-2">
              <button
                onClick={handleCancelEdit}
                className="px-3 py-1 text-xs text-slate-500 hover:text-slate-700 dark:hover:text-slate-300 transition-colors"
              >
                取消
              </button>
              <button
                onClick={handleSubmitEdit}
                disabled={!draftText.trim()}
                className="px-3 py-1 text-xs bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-40 transition-colors"
              >
                发送
              </button>
            </div>
          </div>
        ) : (
          /* ── 展示模式 ── */
          <>
            {/* 附件行 */}
            {hasAttachments && (
              <div className="flex flex-wrap gap-1.5 mb-1.5">
                {msg.attachments!.map((att) => (
                  <AttachmentChip key={att.id} attachment={att} />
                ))}
              </div>
            )}

            {/* 文本内容 */}
            {msg.content && (
              <p className="text-base text-slate-800 dark:text-slate-200 whitespace-pre-wrap break-words leading-relaxed">
                {msg.content}
              </p>
            )}
          </>
        )}
      </div>

      {/* 底部：时间 + 操作按钮（hover 显示） */}
      <div className="flex items-center gap-2 mt-1 opacity-0 group-hover:opacity-100 transition-opacity">
        <span className="text-xs text-slate-400 dark:text-slate-500">
          {formatTime(msg.timestamp)}
        </span>
        {!editing && (
          <>
            <button
              onClick={handleEdit}
              className="text-xs text-slate-400 hover:text-blue-500 dark:hover:text-blue-400 transition-colors"
              title="编辑此消息"
            >
              <Pencil className="w-3 h-3" />
            </button>
            <button
              onClick={handleCopy}
              className="text-xs text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors"
              title="复制"
            >
              {copied ? <Check className="w-3 h-3" /> : <Copy className="w-3 h-3" />}
            </button>
          </>
        )}
      </div>

      {/* 分隔线 */}
      <div className="mt-3 border-t border-slate-100 dark:border-slate-800" />
    </div>
  )
}
