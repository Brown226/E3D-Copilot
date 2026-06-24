/**
 * UserBubble — 聊天式右对齐 + 蓝色气泡
 */

import { useState } from 'react'
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
    editUserMessage(msg.id, draftText)
    setEditing(false)
  }

  const hasAttachments = msg.attachments && msg.attachments.length > 0

  return (
    <div className="flex justify-end group px-4 py-1">
      <div className="max-w-[80%] flex flex-col items-end gap-1">
        {/* 附件 */}
        {hasAttachments && (
          <div className="flex flex-wrap gap-1.5 justify-end">
            {msg.attachments!.map((att) => (
              <span key={att.id} className="inline-flex items-center gap-1 px-2 py-0.5 bg-blue-500/20 rounded text-xs text-blue-100">
                {att.previewUrl || att.data ? (
                  <img src={att.previewUrl || `data:${att.type};base64,${att.data}`} alt="" className="w-4 h-4 rounded object-cover" />
                ) : null}
                <span className="truncate max-w-[100px]">{att.name}</span>
                <span className="text-blue-200/70">{formatSize(att.size)}</span>
              </span>
            ))}
          </div>
        )}

        {/* 消息气泡 */}
        {editing ? (
          <div className="space-y-2 w-full">
            <textarea
              autoFocus
              value={draftText}
              onChange={(e) => setDraftText(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Escape') { e.preventDefault(); handleCancelEdit() }
                if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) { e.preventDefault(); handleSubmitEdit() }
              }}
              rows={Math.max(2, Math.min(8, draftText.split('\n').length))}
              className="w-full px-3 py-2 bg-white dark:bg-slate-700 border border-slate-300 dark:border-slate-500 rounded-lg outline-none text-sm text-slate-800 dark:text-slate-200 resize-none focus:border-blue-400"
            />
            <div className="flex items-center gap-2 justify-end">
              <button onClick={handleCancelEdit} className="px-3 py-1 text-xs text-slate-500 hover:text-slate-700 dark:hover:text-slate-300 transition-colors">取消</button>
              <button onClick={handleSubmitEdit} disabled={!draftText.trim()} className="px-3 py-1 text-xs bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-40 transition-colors">发送</button>
            </div>
          </div>
        ) : msg.content ? (
          <div className="rounded-2xl px-4 py-2.5 bg-blue-600 text-white text-sm whitespace-pre-wrap break-words leading-relaxed">
            {msg.content}
          </div>
        ) : null}

        {/* 底部信息 */}
        <div className="flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
          <span className="text-[10px] text-slate-400 dark:text-slate-500">{formatTime(msg.timestamp)}</span>
          {!editing && (
            <>
              <button onClick={handleEdit} className="text-slate-400 hover:text-blue-500 dark:hover:text-blue-400 transition-colors" title="编辑">
                <Pencil className="w-3 h-3" />
              </button>
              <button onClick={handleCopy} className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors" title="复制">
                {copied ? <Check className="w-3 h-3" /> : <Copy className="w-3 h-3" />}
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
