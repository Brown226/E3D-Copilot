/**
 * 附件管理 Hook
 * 职责：Attachment/PastedBlock 类型 + 添加/删除 + 文件选择 + 粘贴/拖拽辅助
 */

import { useState, useRef } from 'react'

// ── 类型 ──
export interface Attachment {
  id: string
  name: string
  type: string
  size: number
  previewUrl?: string
  raw?: File
}

export interface PastedBlock {
  label: string
  text: string
}

// ── 常量 ──
const LONG_PASTE_MIN_CHARS = 2000
const LONG_PASTE_MIN_LINES = 20

// ── 工具函数 ──
export function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result as string)
    reader.onerror = reject
    reader.readAsDataURL(file)
  })
}

function lineCount(s: string): number {
  if (s === '') return 0
  return s.split(/\r\n|\r|\n/).length
}

export function shouldFoldPaste(s: string): boolean {
  return s.length >= LONG_PASTE_MIN_CHARS || lineCount(s) >= LONG_PASTE_MIN_LINES
}

export function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

// ── Hook ──
export function useAttachments(inputValue: string, setInputValue: (v: string) => void) {
  const [attachments, setAttachments] = useState<Attachment[]>([])
  const [pastedBlocks, setPastedBlocks] = useState<PastedBlock[]>([])
  const fileInputRef = useRef<HTMLInputElement>(null)

  const addAttachment = (file: File) => {
    const id = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
    const attachment: Attachment = { id, name: file.name, type: file.type, size: file.size, raw: file }
    if (file.type.startsWith('image/')) {
      const reader = new FileReader()
      reader.onload = () => {
        setAttachments((prev) => prev.map((a) => (a.id === id ? { ...a, previewUrl: reader.result as string } : a)))
      }
      reader.readAsDataURL(file)
    }
    setAttachments((prev) => [...prev, attachment])
  }

  const removeAttachment = (id: string) => setAttachments((prev) => prev.filter((a) => a.id !== id))

  const removePastedBlock = (label: string) => {
    setPastedBlocks((prev) => prev.filter((b) => b.label !== label))
    const regex = new RegExp(`\\[${label.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\]`, 'g')
    setInputValue(inputValue.replace(regex, ''))
  }

  const addPastedBlock = (text: string) => {
    const blockNum = pastedBlocks.length + 1
    const label = `Pasted block ${blockNum}`
    setPastedBlocks((prev) => [...prev, { label, text }])
    return label
  }

  const handleFileSelect = () => fileInputRef.current?.click()

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files
    if (!files) return
    Array.from(files).forEach(addAttachment)
    e.target.value = ''
  }

  const clearAttachments = () => {
    setAttachments([])
    setPastedBlocks([])
  }

  return {
    attachments, pastedBlocks, fileInputRef,
    addAttachment, removeAttachment, removePastedBlock, addPastedBlock,
    handleFileSelect, handleFileChange, clearAttachments,
  }
}
