/**
 * InputBar — Composer 输入组件（编排层）
 *
 * 布局结构：
 * 1. 流式状态栏（StreamingStatusBar）
 * 2. 附件/粘贴块区（AttachmentArea）
 * 3. 主卡片：textarea + 发送按钮
 * 4. 底部工具栏：附件 | 规划模式 | CAD导入 | 自动执行 | 模型选择器
 * 5. Slash 命令菜单（浮层）
 */

import { useCallback, useRef, useEffect, useState, type KeyboardEvent, type ClipboardEvent, type DragEvent, type CSSProperties } from 'react'
import { Paperclip, ArrowUp, Square, Shield, ShieldCheck, List } from 'lucide-react'
import { useChatStore, useActiveTab } from '@/store/useChatStore'
import { ModelSwitcher } from '@/components/chat/ModelSwitcher'
import { SlashMenu } from '@/components/chat/SlashMenu'
import { CadImportButton } from '@/components/chat/CadImportButton'
import { useComposerResize, composerMaxHeight } from './composer/useComposerResize'
import { useInputHistory } from './composer/useInputHistory'
import { useAttachments, shouldFoldPaste, fileToBase64 } from './composer/useAttachments'
import { StreamingStatusBar, fmtTokens } from './composer/StreamingStatusBar'
import { AttachmentArea } from './composer/AttachmentArea'

// ── 常量 ──
const IME_CONFIRM_GRACE_MS = 100

export function InputBar() {
  const inputValue = useChatStore((s) => s.inputValue)
  const { isStreaming } = useActiveTab()
  const bridgeConnected = useChatStore((s) => s.bridgeConnected)
  const toolApprovalMode = useChatStore((s) => s.toolApprovalMode)
  const setToolApprovalMode = useChatStore((s) => s.setToolApprovalMode)
  const isPlanMode = useChatStore((s) => s.isPlanMode)
  const togglePlanMode = useChatStore((s) => s.togglePlanMode)
  const turnStartAt = useChatStore((s) => s.turnStartAt)
  const turnTokens = useChatStore((s) => s.turnTokens)
  const setInputValue = useChatStore((s) => s.setInputValue)
  const sendMessage = useChatStore((s) => s.sendMessage)
  const sessionTokens = useChatStore((s) => s.sessionTokens)

  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const composerCardRef = useRef<HTMLDivElement>(null)
  const [dragOver, setDragOver] = useState(false)
  const [showSlashMenu, setShowSlashMenu] = useState(false)
  const [slashQuery, setSlashQuery] = useState('')

  // IME 兼容
  const composingRef = useRef(false)
  const lastCompositionEndAt = useRef(0)

  // ── Hooks ──
  const { composerHeight, composerResizing, onComposerResizeStart, resetComposerHeight } = useComposerResize(composerCardRef)
  const { historyEntries, pushHistory, navigateHistory, resetHistoryNavigation, historyIndexRef } = useInputHistory(setInputValue)
  const {
    attachments, pastedBlocks, fileInputRef,
    addAttachment, removeAttachment, removePastedBlock, addPastedBlock,
    handleFileSelect, handleFileChange, clearAttachments,
  } = useAttachments(inputValue, setInputValue)

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

  // ── 发送消息 ──
  const handleSend = useCallback(() => {
    const text = inputValue.trim()
    if (!text && attachments.length === 0) return
    if (isStreaming || !bridgeConnected) return

    if (text) pushHistory(text)
    resetHistoryNavigation()

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
    clearAttachments()
  }, [inputValue, isStreaming, bridgeConnected, attachments, pastedBlocks, sendMessage, setInputValue, pushHistory, resetHistoryNavigation, clearAttachments])

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
        navigateHistory(-1, textareaRef)
        return
      }
    }
    // ↓ 历史导航：光标在行尾时触发
    if (ta && e.key === 'ArrowDown' && !e.shiftKey && !e.ctrlKey && !e.metaKey) {
      if (ta.selectionStart === ta.value.length && ta.selectionEnd === ta.value.length) {
        e.preventDefault()
        navigateHistory(1, textareaRef)
        return
      }
    }

    // Escape 清空
    if (e.key === 'Escape') {
      e.preventDefault()
      setInputValue('')
      clearAttachments()
      setShowSlashMenu(false)
      resetHistoryNavigation()
      return
    }

    // 其他按键重置历史导航
    if (historyIndexRef.current !== -1 && e.key.length === 1) {
      resetHistoryNavigation()
    }
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
      const label = addPastedBlock(text)
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

  const canSend = (inputValue.trim() || attachments.length > 0) && !isStreaming && bridgeConnected

  // ── 自动执行开关状态 ──
  const isAutoMode = toolApprovalMode !== 'ask'

  return (
    <footer
      className={`composer-footer${dragOver ? ' composer-footer--dragover' : ''}`}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
    >
      {/* ═══════ 流式状态栏 ═══════ */}
      <StreamingStatusBar
        isStreaming={isStreaming}
        turnStartAt={turnStartAt}
        turnTokens={turnTokens}
        onCancel={handleCancel}
      />

      {/* ═══════ 附件 / 粘贴块 ═══════ */}
      <AttachmentArea
        attachments={attachments}
        pastedBlocks={pastedBlocks}
        onRemoveAttachment={removeAttachment}
        onRemovePastedBlock={removePastedBlock}
      />

      <div className="composer-wrap">
        {/* ═══════ 主输入卡片 ═══════ */}
        <div
          ref={composerCardRef}
          className={`composer-card${composerHeight !== null ? ' composer-card--resized' : ''}${composerResizing ? ' composer-card--resizing' : ''}${isStreaming ? ' composer-card--running' : ''}`}
          style={composerHeight !== null ? ({ '--composer-height': `${composerHeight}px` } as CSSProperties) : undefined}
        >
          <button
            className="composer-resize-handle"
            type="button"
            aria-label="拖拽调整高度"
            title="拖拽调整高度（双击重置）"
            onPointerDown={onComposerResizeStart}
            onDoubleClick={resetComposerHeight}
          />

          {/* textarea + 发送按钮 */}
          <div
            className={`composer${dragOver ? ' composer--dragover' : ''}${!bridgeConnected ? ' composer--disabled' : ''}`}
          >
            <span className="composer__caret">›</span>
            <textarea
              ref={textareaRef}
              className="composer__input"
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
                  ? isStreaming ? 'AI 正在回复...' : '给 E小智 发消息...'
                  : '等待连接...'
              }
              disabled={!bridgeConnected}
              style={{ maxHeight: `${composerMaxHeight()}px` }}
            />

            {/* 发送 / 停止按钮 */}
            {isStreaming ? (
              <button className="composer__btn composer__btn--stop" onClick={handleCancel} title="停止生成">
                <Square size={14} fill="currentColor" strokeWidth={0} />
              </button>
            ) : (
              <button
                className="composer__btn composer__btn--send"
                onClick={handleSend}
                disabled={!canSend}
                title="发送 (Enter)"
              >
                <ArrowUp size={16} />
              </button>
            )}
          </div>

          {/* ═══════ 底部工具栏 ═══════ */}
          <div className="composer-meta">
            <div className="composer-meta__params">
              {/* 附件按钮 */}
              <div className="composer-meta__control">
                <button
                  className="composer-action-trigger"
                  onClick={handleFileSelect}
                  title="添加附件"
                >
                  <Paperclip size={15} />
                </button>
                <input
                  ref={fileInputRef}
                  type="file"
                  multiple
                  accept="image/*,.pdf,.txt,.json,.csv,.xml,.yaml,.yml,.md"
                  className="hidden"
                  onChange={handleFileChange}
                />
              </div>

              {/* 规划模式开关 */}
              <div className="composer-meta__control">
                <button
                  type="button"
                  className={`composer-auto-toggle${isPlanMode ? ' composer-auto-toggle--plan' : ''}`}
                  onClick={togglePlanMode}
                  disabled={!bridgeConnected}
                  title={isPlanMode ? '当前：先规划再执行，点击切换为直接执行' : '当前：直接执行，点击开启规划模式'}
                >
                  <List size={15} />
                  <span>规划</span>
                </button>
              </div>

              {/* CAD 导入按钮 */}
              <div className="composer-meta__control relative">
                <CadImportButton />
              </div>

              {/* 自动执行开关 */}
              <div className="composer-meta__control">
                <button
                  type="button"
                  className={`composer-auto-toggle${isAutoMode ? ' composer-auto-toggle--on' : ''}`}
                  onClick={() => setToolApprovalMode(isAutoMode ? 'ask' : 'auto')}
                  disabled={!bridgeConnected}
                  title={isAutoMode ? '当前：只读操作自动执行，点击切换为每次询问' : '当前：每次工具调用前询问确认，点击开启自动执行'}
                >
                  {isAutoMode ? <ShieldCheck size={15} /> : <Shield size={15} />}
                  <span>{isAutoMode ? '自动' : '询问'}</span>
                </button>
              </div>
            </div>

            {/* 右侧：连接状态 + 模型选择器 */}
            <div className="composer-status">
              {sessionTokens > 0 && (
                <span className="hidden sm:inline" style={{ fontVariantNumeric: 'tabular-nums' }}>
                  Σ {fmtTokens(sessionTokens)}
                </span>
              )}
              <ModelSwitcher />
              {historyEntries.length > 0 && (
                <span className="hidden sm:inline" style={{ color: 'var(--fg-faint)' }}>· ↑↓ {historyEntries.length}</span>
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
              } else if (cmd.name === 'cad') {
                // 结构化执行：直接打开文件对话框，获取路径后自动发送
                import('@/services/bridgeService').then(({ default: bridge }) => {
                  bridge.sendAndWait('dialog:open_file', {
                    title: '选择 CAD 文件（DWG/DXF）',
                    filter: 'CAD 文件|*.dwg;*.dxf|所有文件|*.*'
                  }).then((result: any) => {
                    if (result?.path) {
                      sendMessage(bridge.sendUserMessage.bind(bridge), undefined, `/cad 导入文件: ${result.path}`)
                    }
                  })
                })
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
