/**
 * InputBar — Reasonix 风格 Composer
 *
 * 布局结构：
 * 1. 附件/粘贴块区（context cards）
 * 2. 流式状态栏（运行中显示：旋转词 + 时长 + token + 停止按钮）
 * 3. 主卡片：textarea + 发送按钮
 * 4. 底部工具栏：附件 | 模式切换(询问/自动/Yolo) | 模型选择器 | Plan/Act | 连接状态
 * 5. Slash 命令菜单（浮层）
 */

import { useCallback, useRef, useEffect, useState, type KeyboardEvent, type ClipboardEvent, type DragEvent, type PointerEvent as ReactPointerEvent, type CSSProperties } from 'react'
import { Paperclip, ArrowUp, Square, Zap, List, Shield, ShieldCheck, ShieldAlert, GripHorizontal } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'
import type { ToolApprovalMode } from '@/store/useChatStore'
import { ModelSwitcher } from '@/components/chat/ModelSwitcher'
import { SlashMenu } from '@/components/chat/SlashMenu'

// ── 常量 ──
const LONG_PASTE_MIN_CHARS = 2000
const LONG_PASTE_MIN_LINES = 20
const COMPOSER_MIN_HEIGHT = 56
const COMPOSER_MAX_HEIGHT = 280
const COMPOSER_MAX_VIEWPORT_RATIO = 0.35
const MAX_HISTORY = 100
const IME_CONFIRM_GRACE_MS = 100
const COMPOSER_HEIGHT_KEY = 'e3d-composer-height'

// ── 旋转词（流式时显示） ──
const SPINNER_WORDS = ['嘎吱运算', '飞速思考', '搜索中', '分析中', '推理中', '生成中']

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
  if (typeof window === 'undefined') return COMPOSER_MAX_HEIGHT
  return Math.max(COMPOSER_MIN_HEIGHT, Math.min(COMPOSER_MAX_HEIGHT, Math.floor(window.innerHeight * COMPOSER_MAX_VIEWPORT_RATIO)))
}

function clampComposerHeight(h: number): number {
  return Math.min(Math.max(Math.round(h), COMPOSER_MIN_HEIGHT), composerMaxHeight())
}

function loadComposerHeight(): number | null {
  try {
    const v = localStorage.getItem(COMPOSER_HEIGHT_KEY)
    if (!v) return null
    return clampComposerHeight(parseInt(v, 10))
  } catch { return null }
}

function saveComposerHeight(h: number): void {
  try { localStorage.setItem(COMPOSER_HEIGHT_KEY, String(h)) } catch { /* ignore */ }
}

function clearComposerHeight(): void {
  try { localStorage.removeItem(COMPOSER_HEIGHT_KEY) } catch { /* ignore */ }
}

/** 格式化 token 数量 */
function fmtTokens(n: number): string {
  if (n >= 1000) return (n / 1000).toFixed(1).replace(/\.0$/, '') + 'k'
  return String(n)
}

/** 格式化运行时长 */
function fmtElapsed(ms: number): string {
  const s = Math.floor(ms / 1000)
  if (s < 60) return `${s}s`
  return `${Math.floor(s / 60)}m ${s % 60}s`
}

/** 每秒触发的 tick hook（用于实时更新运行时长） */
function useTick(on: boolean): number {
  const [, setN] = useState(0)
  useEffect(() => {
    if (!on) return
    const id = window.setInterval(() => setN((n) => n + 1), 1000)
    return () => window.clearInterval(id)
  }, [on])
  return Date.now()
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

// ── 审批模式配置 ──
const APPROVAL_MODES: { mode: ToolApprovalMode; icon: typeof Shield; label: string; title: string }[] = [
  { mode: 'ask', icon: Shield, label: '询问', title: '每次工具调用前询问确认' },
  { mode: 'auto', icon: ShieldCheck, label: '自动', title: '自动执行工具调用（只读操作）' },
  { mode: 'yolo', icon: ShieldAlert, label: 'Yolo', title: '全自动模式：所有操作无需确认' },
]

export function InputBar() {
  const inputValue = useChatStore((s) => s.inputValue)
  const isStreaming = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.isStreaming ?? false)
  const bridgeConnected = useChatStore((s) => s.bridgeConnected)
  const currentModel = useChatStore((s) => s.currentModel)
  const isPlanMode = useChatStore((s) => s.isPlanMode)
  const togglePlanMode = useChatStore((s) => s.togglePlanMode)
  const toolApprovalMode = useChatStore((s) => s.toolApprovalMode)
  const setToolApprovalMode = useChatStore((s) => s.setToolApprovalMode)
  const turnStartAt = useChatStore((s) => s.turnStartAt)
  const turnTokens = useChatStore((s) => s.turnTokens)
  const setInputValue = useChatStore((s) => s.setInputValue)
  const sendMessage = useChatStore((s) => s.sendMessage)

  // 每秒刷新（流式时）
  const now = useTick(isStreaming)

  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const [attachments, setAttachments] = useState<Attachment[]>([])
  const [pastedBlocks, setPastedBlocks] = useState<PastedBlock[]>([])
  const [dragOver, setDragOver] = useState(false)
  const [showSlashMenu, setShowSlashMenu] = useState(false)
  const [slashQuery, setSlashQuery] = useState('')

  // IME 兼容
  const composingRef = useRef(false)
  const lastCompositionEndAt = useRef(0)

  // Composer 高度拖拽
  const composerCardRef = useRef<HTMLDivElement>(null)
  const [composerHeight, setComposerHeight] = useState<number | null>(loadComposerHeight)
  const [composerResizing, setComposerResizing] = useState(false)

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

  // 自动伸缩 textarea（仅当未手动调整高度时）
  useEffect(() => {
    if (composerHeight !== null) return // 手动模式下不自动伸缩
    const el = textareaRef.current
    if (!el) return
    el.style.height = 'auto'
    const maxH = composerMaxHeight()
    el.style.height = `${Math.min(el.scrollHeight, maxH)}px`
    el.style.overflowY = el.scrollHeight > maxH ? 'auto' : 'hidden'
  }, [inputValue, composerHeight])

  // Composer 拖拽调整高度
  const onComposerResizeStart = useCallback((e: ReactPointerEvent<HTMLButtonElement>) => {
    if (e.button !== 0) return
    const card = composerCardRef.current
    if (!card) return
    e.preventDefault()
    const startY = e.clientY
    const startHeight = composerHeight ?? card.getBoundingClientRect().height
    let nextHeight = clampComposerHeight(startHeight)
    let moved = false
    setComposerResizing(true)
    document.body.classList.add('composer-resizing')
    const onMove = (ev: PointerEvent) => {
      moved = true
      nextHeight = clampComposerHeight(startHeight + startY - ev.clientY)
      setComposerHeight(nextHeight)
    }
    const onUp = () => {
      setComposerResizing(false)
      document.body.classList.remove('composer-resizing')
      if (moved) saveComposerHeight(nextHeight)
      document.removeEventListener('pointermove', onMove)
      document.removeEventListener('pointerup', onUp)
      document.removeEventListener('pointercancel', onUp)
    }
    document.addEventListener('pointermove', onMove)
    document.addEventListener('pointerup', onUp)
    document.addEventListener('pointercancel', onUp)
  }, [composerHeight])

  const resetComposerHeight = useCallback(() => {
    setComposerHeight(null)
    clearComposerHeight()
  }, [])

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
    // IME 组字期间禁止所有快捷键
    const isIme = composingRef.current
      || (e.nativeEvent as globalThis.KeyboardEvent & { isComposing?: boolean }).isComposing === true
      || Date.now() - lastCompositionEndAt.current < IME_CONFIRM_GRACE_MS
    if (isIme) return

    // Enter 发送（Shift+Enter 换行）
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
      return
    }

    const ta = textareaRef.current
    // ↑ 历史导航：光标在行首时触发
    if (ta && e.key === 'ArrowUp' && !e.shiftKey && !e.ctrlKey && !e.metaKey) {
      if (ta.selectionStart === 0 && ta.selectionEnd === 0) {
        e.preventDefault()
        navigateHistory(-1)
        return
      }
    }
    // ↓ 历史导航：光标在行尾时触发
    if (ta && e.key === 'ArrowDown' && !e.shiftKey && !e.ctrlKey && !e.metaKey) {
      if (ta.selectionStart === ta.value.length && ta.selectionEnd === ta.value.length) {
        e.preventDefault()
        navigateHistory(1)
        return
      }
    }

    // Escape 清空
    if (e.key === 'Escape') {
      e.preventDefault()
      setInputValue('')
      setPastedBlocks([])
      setAttachments([])
      setShowSlashMenu(false)
      historyIndexRef.current = -1
      return
    }

    // 其他按键重置历史导航
    if (historyIndexRef.current !== -1 && e.key.length === 1) {
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
  const handleCompositionEnd = () => {
    composingRef.current = false
    lastCompositionEndAt.current = Date.now()
  }

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

  // ── 计算流式状态文本 ──
  const runActivity = isStreaming && turnStartAt
    ? (() => {
        const elapsedMs = Math.max(0, now - turnStartAt)
        const word = SPINNER_WORDS[Math.floor(elapsedMs / 3000) % SPINNER_WORDS.length]
        const tok = turnTokens > 0 ? ` · ↓ ${fmtTokens(turnTokens)} tokens` : ''
        return `${word}… ${fmtElapsed(elapsedMs)}${tok}`
      })()
    : null

  // ── 审批模式切换器当前激活索引（用于滑块动画） ──
  const approvalIndex = APPROVAL_MODES.findIndex((m) => m.mode === toolApprovalMode)

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

        {/* ═══════ 区域 2：流式状态栏 ═══════ */}
        {runActivity && (
          <div className="flex items-center justify-end">
            <div
              className="inline-flex items-center gap-2 h-[34px] px-3 rounded-[10px] border text-xs font-medium"
              style={{
                borderColor: 'color-mix(in srgb, var(--e3d-primary) 38%, var(--border))',
                background: 'color-mix(in srgb, var(--e3d-primary) 9%, var(--bg-elev))',
                color: 'var(--e3d-primary)',
              }}
              role="status"
              aria-live="polite"
            >
              <span
                className="w-1.5 h-1.5 rounded-full bg-current animate-pulse"
                style={{ animation: 'pulse 1.2s ease-in-out infinite' }}
              />
              <span className="truncate tabular-nums">{runActivity}</span>
              <button
                onClick={handleCancel}
                className="inline-flex items-center gap-1 h-[26px] px-2.5 rounded-[7px] border text-xs font-semibold transition-colors"
                style={{
                  borderColor: 'color-mix(in srgb, var(--e3d-error) 42%, transparent)',
                  background: 'color-mix(in srgb, var(--e3d-error) 14%, var(--bg-elev-2))',
                  color: 'var(--e3d-error)',
                }}
                title="停止生成"
              >
                <Square className="w-2.5 h-2.5" fill="currentColor" />
                <span>停止</span>
              </button>
            </div>
          </div>
        )}

        {/* ═══════ 区域 3：主输入卡片 ═══════ */}
        <div
          ref={composerCardRef}
          className={`relative bg-white dark:bg-slate-800 rounded-2xl shadow-lg shadow-slate-200/50 dark:shadow-slate-900/50 border transition-colors overflow-hidden ${
            dragOver ? 'border-blue-400 dark:border-blue-500' : 'border-slate-200 dark:border-slate-600'
          }${composerResizing ? ' ring-2 ring-blue-300 dark:ring-blue-700' : ''}`}
          style={composerHeight !== null ? ({ '--composer-h': `${composerHeight}px` } as CSSProperties) : undefined}
        >
          {/* 拖拽调整高度手柄 */}
          <button
            type="button"
            className="absolute -top-0.5 left-1/2 -translate-x-1/2 z-20 h-2 w-16 flex items-center justify-center cursor-ns-resize group/resizer touch-none"
            onPointerDown={onComposerResizeStart}
            onDoubleClick={resetComposerHeight}
            title="拖拽调整高度（双击重置）"
            aria-label="拖拽调整输入框高度"
          >
            <GripHorizontal className="w-4 h-3 text-slate-300 dark:text-slate-600 group-hover/resizer:text-slate-400 dark:group-hover/resizer:text-slate-400 transition-colors" />
          </button>
          <div
            className="flex items-end"
            style={composerHeight !== null ? { height: `${composerHeight}px` } : undefined}
          >
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
                  ? isStreaming ? 'AI 正在回复...' : '给 E小智 发消息... (/ 命令 · @ 文件 · ! 终端)'
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

          {/* ═══════ 区域 4：底部工具栏 ═══════ */}
          <div className="flex items-center gap-1 px-1.5 py-1 border-t border-slate-100 dark:border-slate-700/50">
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

            {/* ══ 审批模式三段切换器（询问/自动/Yolo） ══ */}
            <div
              className="relative grid grid-cols-3 items-center h-8 p-0.5 rounded-full border border-slate-200 dark:border-slate-600 bg-slate-100/50 dark:bg-slate-900/50 overflow-hidden"
              data-mode={toolApprovalMode}
              style={{ minWidth: '180px' }}
            >
              {/* 滑块 */}
              <span
                className="absolute top-0.5 bottom-0.5 left-0.5 rounded-full border transition-all duration-200 pointer-events-none"
                style={{
                  width: 'calc((100% - 4px) / 3)',
                  transform: `translateX(${approvalIndex * 100}%)`,
                  ...(toolApprovalMode === 'ask'
                    ? { borderColor: 'var(--border)', background: 'var(--bg-elev)' }
                    : toolApprovalMode === 'auto'
                    ? { borderColor: 'color-mix(in srgb, var(--e3d-success) 76%, #0b3d24)', background: 'color-mix(in srgb, var(--e3d-success) 58%, #0b3d24)' }
                    : { borderColor: 'var(--e3d-error)', background: 'color-mix(in srgb, var(--e3d-error) 20%, var(--bg-elev-2))' }
                  ),
                }}
              />
              {APPROVAL_MODES.map(({ mode, icon: Icon, label, title }) => {
                const isActive = toolApprovalMode === mode
                return (
                  <button
                    key={mode}
                    onClick={() => setToolApprovalMode(mode)}
                    disabled={!bridgeConnected}
                    className={`relative z-10 inline-flex items-center justify-center gap-1 h-[26px] px-1.5 rounded-full border border-transparent text-xs font-semibold transition-colors disabled:opacity-50 disabled:cursor-not-allowed ${
                      isActive
                        ? mode === 'ask'
                          ? 'text-slate-700 dark:text-slate-200'
                          : mode === 'auto'
                          ? 'text-white'
                          : 'text-red-600 dark:text-red-400'
                        : 'text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-200'
                    }`}
                    title={title}
                    aria-pressed={isActive}
                  >
                    <Icon className="w-3.5 h-3.5" />
                    <span className="hidden min-[420px]:inline">{label}</span>
                  </button>
                )
              })}
            </div>

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

            {/* 右侧：连接状态 + token 统计 */}
            <div className="ml-auto flex items-center gap-1.5 text-xs text-slate-400 dark:text-slate-500">
              {/* 会话 token 统计 */}
              {useChatStore((s) => s.sessionTokens) > 0 && (
                <span className="hidden sm:inline tabular-nums">
                  Σ {fmtTokens(useChatStore((s) => s.sessionTokens))} tok
                </span>
              )}
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
