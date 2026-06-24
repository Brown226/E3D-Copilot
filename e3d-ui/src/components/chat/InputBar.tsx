/**
 * InputBar — 同款 Reasonix Composer 双行布局
 *
 * 布局结构：
 * 1. 附件/粘贴块区（context cards）
 * 2. 主卡片：textarea + 发送按钮
 * 3. 底部工具栏：附件按钮 | Plan/Act | 模型选择器 | 连接状态
 * 4. Slash 命令菜单（浮层）
 */

import { useCallback, useRef, useEffect, useState, type KeyboardEvent, type ClipboardEvent, type DragEvent } from 'react'
import { Paperclip, ArrowUp, Square, Zap, List } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'
import { ModelSwitcher } from '@/components/chat/ModelSwitcher'
import { SlashMenu } from '@/components/chat/SlashMenu'

// ── 常量 ──
const LONG_PASTE_MIN_CHARS = 2000
const LONG_PASTE_MIN_LINES = 20
const COMPOSER_MIN_HEIGHT = 56
const COMPOSER_MAX_VIEWPORT_RATIO = 0.35
const MAX_HISTORY = 100

// ── 工具函数 ──
function fileToBase64(file: File): Promise<string> {
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

function shouldFoldPaste(s: string): boolean {
  return s.length >= LONG_PASTE_MIN_CHARS || lineCount(s) >= LONG_PASTE_MIN_LINES
}

function composerMaxHeight(): number {
  if (typeof window === 'undefined') return 240
  return Math.max(COMPOSER_MIN_HEIGHT, Math.floor(window.innerHeight * COMPOSER_MAX_VIEWPORT_RATIO))
}

// ── 附件类型 ──
interface Attachment {
  id: string
  name: string
  type: string
  size: number
  previewUrl?: string
  raw?: File
}

// ── 粘贴块类型 ──
interface PastedBlock {
  label: string
  text: string
}

export function InputBar() {
  const inputValue = useChatStore((s) => s.inputValue)
  const isStreaming = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.isStreaming ?? false)
  const bridgeConnected = useChatStore((s) => s.bridgeConnected)
  const currentModel = useChatStore((s) => s.currentModel)
  const isPlanMode = useChatStore((s) => s.isPlanMode)
  const togglePlanMode = useChatStore((s) => s.togglePlanMode)
  const setInputValue = useChatStore((s) => s.setInputValue)
  const sendMessage = useChatStore((s) => s.sendMessage)

  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const [attachments, setAttachments] = useState<Attachment[]>([])
  const [pastedBlocks, setPastedBlocks] = useState<PastedBlock[]>([])
  const [dragOver, setDragOver] = useState(false)
  const [showSlashMenu, setShowSlashMenu] = useState(false)
  const [slashQuery, setSlashQuery] = useState('')

  // IME 兼容
  const composingRef = useRef(false)

  // 输入历史
  const historyIndexRef = useRef(-1)
  const savedTextRef = useRef('')
  const [historyEntries, setHistoryEntries] = useState<string[]>(() => {
    try {
      const saved = localStorage.getItem('e3d-input-history')
      return saved ? JSON.parse(saved) : []
    } catch { return [] }
  })

  useEffect(() => {
    localStorage.setItem('e3d-input-history', JSON.stringify(historyEntries.slice(0, MAX_HISTORY)))
  }, [historyEntries])

  // 自动伸缩 textarea
  useEffect(() => {
    const el = textareaRef.current
    if (!el) return
    el.style.height = 'auto'
    const maxH = composerMaxHeight()
    el.style.height = `${Math.min(el.scrollHeight, maxH)}px`
    el.style.overflowY = el.scrollHeight > maxH ? 'auto' : 'hidden'
  }, [inputValue])

  // ── 发送消息 ──
  const handleSend = useCallback(() => {
    const text = inputValue.trim()
    if (!text && attachments.length === 0) return
    if (isStreaming || !bridgeConnected) return

    if (text) {
      setHistoryEntries((prev) => {
        const filtered = prev.filter((h) => h !== text)
        return [text, ...filtered].slice(0, MAX_HISTORY)
      })
    }

    historyIndexRef.current = -1
    savedTextRef.current = ''

    let fullText = text
    if (pastedBlocks.length > 0) {
      const blocks = pastedBlocks.map((b) => `${b.label}\n\n--- Begin ${b.label} ---\n${b.text}\n--- End ${b.label} ---`).join('\n\n')
      fullText = fullText ? `${fullText}\n\n${blocks}` : blocks
    }

    const convertAttachments = async (): Promise<import('@/types').Attachment[]> => {
      const result: import('@/types').Attachment[] = []
      for (const att of attachments) {
        const baseAtt: import('@/types').Attachment = {
          id: att.id, name: att.name, type: att.type, size: att.size, previewUrl: att.previewUrl,
        }
        if (att.raw) baseAtt.data = await fileToBase64(att.raw)
        result.push(baseAtt)
      }
      return result
    }

    convertAttachments().then((globalAttachments) => {
      import('@/services/bridgeService').then(({ default: bridge }) => {
        sendMessage(bridge.sendUserMessage.bind(bridge), globalAttachments, fullText)
      })
    })

    setInputValue('')
    setAttachments([])
    setPastedBlocks([])
  }, [inputValue, isStreaming, bridgeConnected, attachments, pastedBlocks, sendMessage, setInputValue])

  // ── 取消生成 ──
  const handleCancel = useCallback(() => {
    import('@/services/bridgeService').then(({ default: bridge }) => {
      bridge.cancel()
      useChatStore.getState().stopStreaming()
    })
  }, [])

  // ── 键盘事件 ──
  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (composingRef.current) return

    if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
      e.preventDefault()
      handleSend()
      return
    }

    const ta = textareaRef.current
    if (ta && e.key === 'ArrowUp' && !e.shiftKey && ta.selectionStart === 0) {
      e.preventDefault()
      navigateHistory(-1)
      return
    }
    if (ta && e.key === 'ArrowDown' && !e.shiftKey && ta.selectionStart === ta.value.length) {
      e.preventDefault()
      navigateHistory(1)
      return
    }

    if (e.key === 'Escape') {
      e.preventDefault()
      setInputValue('')
      setPastedBlocks([])
      setAttachments([])
      setShowSlashMenu(false)
      historyIndexRef.current = -1
    }
  }

  const navigateHistory = (direction: -1 | 1) => {
    const ta = textareaRef.current
    if (!ta || historyEntries.length === 0) return
    const newIndex = historyIndexRef.current + direction
    if (newIndex < -1 || newIndex >= historyEntries.length) return
    if (historyIndexRef.current === -1 && direction === -1) savedTextRef.current = ta.value
    historyIndexRef.current = newIndex
    setInputValue(newIndex === -1 ? savedTextRef.current : historyEntries[newIndex])
    requestAnimationFrame(() => { ta.selectionStart = ta.selectionEnd = ta.value.length })
  }

  const handleCompositionStart = () => { composingRef.current = true }
  const handleCompositionEnd = () => { composingRef.current = false }

  // ── 粘贴处理 ──
  const handlePaste = (e: ClipboardEvent<HTMLTextAreaElement>) => {
    const text = e.clipboardData.getData('text')
    if (text && shouldFoldPaste(text)) {
      e.preventDefault()
      const blockNum = pastedBlocks.length + 1
      const label = `Pasted block ${blockNum}`
      setPastedBlocks((prev) => [...prev, { label, text }])
      const ta = textareaRef.current
      if (ta) {
        const start = ta.selectionStart
        const before = inputValue.slice(0, start)
        const after = inputValue.slice(ta.selectionEnd)
        setInputValue(`${before}[${label}]${after}`)
      }
    }
    const items = Array.from(e.clipboardData.items)
    for (const item of items) {
      if (item.type.startsWith('image/')) {
        e.preventDefault()
        const file = item.getAsFile()
        if (file) addAttachment(file)
        break
      }
    }
  }

  // ── 拖拽处理 ──
  const handleDragOver = (e: DragEvent) => { e.preventDefault(); setDragOver(true) }
  const handleDragLeave = () => setDragOver(false)
  const handleDrop = (e: DragEvent) => {
    e.preventDefault(); setDragOver(false)
    const items = Array.from(e.dataTransfer.items)
    for (const item of items) {
      if (item.kind === 'file') { const file = item.getAsFile(); if (file) addAttachment(file) }
    }
  }

  // ── 附件管理 ──
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

  const fileInputRef = useRef<HTMLInputElement>(null)
  const handleFileSelect = () => fileInputRef.current?.click()
  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files
    if (!files) return
    Array.from(files).forEach(addAttachment)
    e.target.value = ''
  }

  const canSend = (inputValue.trim() || attachments.length > 0) && !isStreaming && bridgeConnected

  const formatSize = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  }

  return (
    <footer
      className={`border-t transition-colors duration-200 relative ${
        dragOver
          ? 'border-blue-400 bg-blue-50/50 dark:bg-blue-900/20'
          : 'border-slate-200 bg-white/80 dark:bg-slate-900/80 dark:border-slate-700'
      } backdrop-blur-sm px-2.5 py-2 sm:px-3`}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
    >
      <div className="space-y-1.5">
        {/* ═══════ 区域 1：附件/粘贴块 ═══════ */}
        {(attachments.length > 0 || pastedBlocks.length > 0) && (
          <div className="flex flex-wrap gap-2">
            {attachments.map((att) => (
              <div key={att.id} className="relative group flex items-center gap-2 px-2.5 py-1.5 bg-slate-100 dark:bg-slate-800 rounded-lg border border-slate-200 dark:border-slate-600 text-xs">
                {att.previewUrl ? (
                  <img src={att.previewUrl} alt="" className="w-8 h-8 rounded object-cover" />
                ) : (
                  <Paperclip className="w-3.5 h-3.5 text-slate-400" />
                )}
                <div className="flex flex-col min-w-0">
                  <span className="text-slate-700 dark:text-slate-300 truncate max-w-[120px]">{att.name}</span>
                  <span className="text-slate-400 dark:text-slate-500">{formatSize(att.size)}</span>
                </div>
                <button
                  onClick={() => removeAttachment(att.id)}
                  className="absolute -top-1.5 -right-1.5 w-4 h-4 rounded-full bg-red-500 text-white text-[10px] flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity"
                >×</button>
              </div>
            ))}
            {pastedBlocks.map((block) => (
              <div key={block.label} className="relative group flex items-center gap-1.5 px-2.5 py-1.5 bg-amber-50 dark:bg-amber-900/20 rounded-lg border border-amber-200 dark:border-amber-700 text-xs">
                <span className="text-amber-700 dark:text-amber-300">📋 {block.label} ({block.text.length} 字符)</span>
                <button
                  onClick={() => removePastedBlock(block.label)}
                  className="absolute -top-1.5 -right-1.5 w-4 h-4 rounded-full bg-red-500 text-white text-[10px] flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity"
                >×</button>
              </div>
            ))}
          </div>
        )}

        {/* ═══════ 区域 2：主输入卡片 ═══════ */}
        <div className={`relative bg-white dark:bg-slate-800 rounded-2xl shadow-lg shadow-slate-200/50 dark:shadow-slate-900/50 border transition-colors overflow-hidden ${
          dragOver ? 'border-blue-400 dark:border-blue-500' : 'border-slate-200 dark:border-slate-600'
        }`}>
          <div className="flex items-end">
            {/* textarea */}
            <textarea
              ref={textareaRef}
              value={inputValue}
              onChange={(e) => {
                const val = e.target.value
                setInputValue(val)
                if (val.startsWith('/') && !val.includes('\n')) {
                  setShowSlashMenu(true)
                  setSlashQuery(val)
                } else {
                  setShowSlashMenu(false)
                }
              }}
              onKeyDown={handleKeyDown}
              onCompositionStart={handleCompositionStart}
              onCompositionEnd={handleCompositionEnd}
              onPaste={handlePaste}
              placeholder={
                dragOver
                  ? '拖放文件到此处...'
                  : bridgeConnected
                  ? isStreaming ? 'AI 正在回复...' : '输入你的问题... (↑↓ 历史 · Shift+Enter 换行)'
                  : '等待连接...'
              }
              disabled={!bridgeConnected}
              rows={1}
              className="flex-1 px-3 py-2 bg-transparent outline-none text-slate-800 dark:text-slate-100 placeholder-slate-400 dark:placeholder-slate-500 text-sm resize-none leading-5 disabled:opacity-50 min-h-[40px]"
              style={{ maxHeight: `${composerMaxHeight()}px` }}
            />

            {/* 发送/取消按钮 */}
            {isStreaming ? (
              <button
                onClick={handleCancel}
                className="m-2 w-8 h-8 rounded-xl flex items-center justify-center bg-red-500 hover:bg-red-600 text-white transition-colors shrink-0"
                title="停止生成"
              >
                <Square className="w-4 h-4" />
              </button>
            ) : (
              <button
                onClick={handleSend}
                disabled={!canSend}
                className="m-2 w-8 h-8 rounded-xl flex items-center justify-center text-white transition-all shrink-0 bg-slate-800 dark:bg-slate-200 hover:bg-slate-700 dark:hover:bg-slate-300 disabled:opacity-30 disabled:cursor-not-allowed"
                title="发送 (Enter)"
              >
                <ArrowUp className="w-4 h-4" strokeWidth={2.5} />
              </button>
            )}
          </div>

          {/* ═══════ 区域 3：底部工具栏 ═══════ */}
          <div className="flex items-center gap-0.5 px-1.5 py-1 border-t border-slate-100 dark:border-slate-700/50">
            {/* 附件按钮 */}
            <button
              onClick={handleFileSelect}
              className="w-7 h-7 rounded-lg flex items-center justify-center text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-700 transition-colors"
              title="添加附件"
            >
              <Paperclip className="w-4 h-4" />
            </button>
            <input
              ref={fileInputRef}
              type="file"
              multiple
              accept="image/*,.pdf,.txt,.json,.csv,.xml,.yaml,.yml,.md"
              className="hidden"
              onChange={handleFileChange}
            />

            {/* 分隔线 */}
            <div className="w-px h-4 bg-slate-200 dark:bg-slate-700 mx-0.5" />

            {/* Plan/Act 模式切换 */}
            <button
              onClick={togglePlanMode}
              className={`flex items-center gap-1 px-2 py-1 text-xs rounded-lg font-medium transition-all ${
                isPlanMode
                  ? 'bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400'
                  : 'text-slate-500 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-700'
              }`}
              title={isPlanMode ? '规划模式：AI 只分析不执行' : '执行模式：AI 可直接操作'}
            >
              {isPlanMode ? <List className="w-3.5 h-3.5" /> : <Zap className="w-3.5 h-3.5" />}
              <span className="hidden min-[400px]:inline">{isPlanMode ? '规划' : '执行'}</span>
            </button>

            {/* 分隔线 */}
            <div className="w-px h-4 bg-slate-200 dark:bg-slate-700 mx-0.5" />

            {/* 模型选择器 */}
            <ModelSwitcher />

            {/* 右侧：连接状态 */}
            <div className="ml-auto flex items-center gap-1.5 text-xs text-slate-400 dark:text-slate-500">
              <span className={`w-1.5 h-1.5 rounded-full ${bridgeConnected ? 'bg-emerald-500' : 'bg-red-500'}`} />
              <span className="hidden sm:inline">
                {bridgeConnected ? (currentModel || '已连接') : '未连接'}
              </span>
              {historyEntries.length > 0 && (
                <span className="hidden sm:inline text-slate-300 dark:text-slate-600">
                  · ↑↓ {historyEntries.length}
                </span>
              )}
            </div>
          </div>
        </div>

        {/* ═══════ Slash 命令菜单 ═══════ */}
        {showSlashMenu && (
          <SlashMenu
            query={slashQuery}
            onClose={() => setShowSlashMenu(false)}
            onSelect={(cmd) => {
              if (cmd.name === 'new') {
                import('@/services/bridgeService').then(({ default: bridge }) => {
                  useChatStore.getState().newSession(bridge.newSession.bind(bridge))
                })
              } else if (cmd.name === 'plan') {
                useChatStore.getState().togglePlanMode()
              } else {
                setInputValue(cmd.template)
                textareaRef.current?.focus()
              }
              setShowSlashMenu(false)
            }}
          />
        )}
      </div>
    </footer>
  )
}
