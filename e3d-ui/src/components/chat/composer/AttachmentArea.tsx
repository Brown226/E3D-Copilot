/**
 * 附件/粘贴块预览区组件
 * 职责：渲染附件列表和粘贴块列表
 */

import { Paperclip } from 'lucide-react'
import type { Attachment, PastedBlock } from './useAttachments'
import { formatSize } from './useAttachments'

interface AttachmentAreaProps {
  attachments: Attachment[]
  pastedBlocks: PastedBlock[]
  onRemoveAttachment: (id: string) => void
  onRemovePastedBlock: (label: string) => void
}

export function AttachmentArea({ attachments, pastedBlocks, onRemoveAttachment, onRemovePastedBlock }: AttachmentAreaProps) {
  if (attachments.length === 0 && pastedBlocks.length === 0) return null

  return (
    <div className="composer-context">
      {attachments.map((att) => (
        <div key={att.id} className="composer-context__item">
          <span className="composer-context__icon">
            {att.previewUrl ? (
              <img src={att.previewUrl} alt="" draggable={false} />
            ) : (
              <Paperclip className="w-4 h-4" />
            )}
          </span>
          <span className="composer-context__info">
            <span className="composer-context__name">{att.name}</span>
            <span className="composer-context__meta">{formatSize(att.size)}</span>
          </span>
          <button className="composer-context__remove" onClick={() => onRemoveAttachment(att.id)}>×</button>
        </div>
      ))}
      {pastedBlocks.map((block) => (
        <div key={block.label} className="composer__pasted-block">
          <span>📋 {block.label} ({block.text.length} 字符)</span>
          <button className="composer-context__remove" style={{ position: 'static', opacity: 1 }} onClick={() => onRemovePastedBlock(block.label)}>×</button>
        </div>
      ))}
    </div>
  )
}
