/**
 * ApprovalModeSwitcher — 权限模式选择器（四档）
 *
 * 四档语义：
 *   plan  计划模式   — 只读自动，写操作一律拒绝，只出方案不落笔
 *   ask   变更前确认 — 每次工具调用前都问
 *   auto  自主模式   — 只读自动，写操作先问（默认）
 *   yolo  完全访问   — 全部自动，减少确认次数
 *
 * 浮层用 position:fixed + portal（与 ModelSwitcher 一致）：
 * 输入栏的 .composer-meta 带 overflow:hidden，普通 absolute 浮层会被裁掉，
 * 表现为「点了没反应」。
 */

import { useCallback, useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Lightbulb, Hand, Shield, ShieldCheck, Check } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'
import type { ToolApprovalMode } from '@/store/types'

interface ModeDef {
  key: ToolApprovalMode
  label: string
  hint: string
  icon: typeof Lightbulb
}

const MODES: ModeDef[] = [
  { key: 'plan', label: '计划模式', hint: '编辑前先出计划', icon: Lightbulb },
  { key: 'ask', label: '变更前确认', hint: '改文件前先问我', icon: Hand },
  { key: 'auto', label: '自主模式', hint: '在有风险时询问', icon: Shield },
  { key: 'yolo', label: '完全访问', hint: '减少确认次数', icon: ShieldCheck },
]

const PANEL_WIDTH = 224
const PANEL_GAP = 6

export function ApprovalModeSwitcher() {
  const toolApprovalMode = useChatStore((s) => s.toolApprovalMode)
  const setToolApprovalMode = useChatStore((s) => s.setToolApprovalMode)
  const bridgeConnected = useChatStore((s) => s.bridgeConnected)

  const [open, setOpen] = useState(false)
  const [panelStyle, setPanelStyle] = useState<React.CSSProperties>({})
  const buttonRef = useRef<HTMLButtonElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)

  // 定位：优先向下展开，底部空间不足时改为向上
  const updatePanelPosition = useCallback(() => {
    if (!open || !buttonRef.current) return
    const rect = buttonRef.current.getBoundingClientRect()
    const panelHeight = MODES.length * 52 + 12

    let left = rect.left
    if (left + PANEL_WIDTH > window.innerWidth - 8) {
      left = window.innerWidth - PANEL_WIDTH - 8
    }
    if (left < 8) left = 8

    const spaceBelow = window.innerHeight - rect.bottom
    const style: React.CSSProperties = {
      position: 'fixed',
      left: left + 'px',
      width: PANEL_WIDTH + 'px',
    }
    if (spaceBelow < panelHeight + PANEL_GAP + 8) {
      style.bottom = window.innerHeight - rect.top + PANEL_GAP + 'px'
    } else {
      style.top = rect.bottom + PANEL_GAP + 'px'
    }
    setPanelStyle(style)
  }, [open])

  useEffect(() => {
    if (!open) return
    updatePanelPosition()

    const onDown = (e: MouseEvent) => {
      if (panelRef.current?.contains(e.target as Node)) return
      if (buttonRef.current?.contains(e.target as Node)) return
      setOpen(false)
    }
    const onKey = (e: globalThis.KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false)
    }
    document.addEventListener('mousedown', onDown)
    document.addEventListener('keydown', onKey)
    window.addEventListener('resize', updatePanelPosition)
    window.addEventListener('scroll', updatePanelPosition, true)
    return () => {
      document.removeEventListener('mousedown', onDown)
      document.removeEventListener('keydown', onKey)
      window.removeEventListener('resize', updatePanelPosition)
      window.removeEventListener('scroll', updatePanelPosition, true)
    }
  }, [open, updatePanelPosition])

  const current = MODES.find((m) => m.key === toolApprovalMode) ?? MODES[2]
  const CurrentIcon = current.icon

  return (
    <div className="perm-switch">
      <button
        ref={buttonRef}
        type="button"
        className="perm-switch__trigger"
        onClick={() => setOpen((v) => !v)}
        disabled={!bridgeConnected}
        aria-haspopup="listbox"
        aria-expanded={open}
        title={`当前权限：${current.label} — ${current.hint}`}
      >
        <CurrentIcon size={14} className="shrink-0" />
        <span className="perm-switch__trigger-label">{current.label}</span>
      </button>

      {open && createPortal(
        <div className="perm-switch__menu" role="listbox" ref={panelRef} style={panelStyle}>
          {MODES.map((m) => {
            const Icon = m.icon
            const active = m.key === toolApprovalMode
            return (
              <button
                key={m.key}
                type="button"
                role="option"
                aria-selected={active}
                className={`perm-switch__item${active ? ' perm-switch__item--active' : ''}`}
                onClick={() => {
                  setToolApprovalMode(m.key)
                  setOpen(false)
                }}
              >
                <Icon size={16} className="perm-switch__icon" />
                <span className="perm-switch__text">
                  <span className="perm-switch__label">{m.label}</span>
                  <span className="perm-switch__hint">{m.hint}</span>
                </span>
                {active && <Check size={15} className="perm-switch__check" />}
              </button>
            )
          })}
        </div>,
        document.body
      )}
    </div>
  )
}
