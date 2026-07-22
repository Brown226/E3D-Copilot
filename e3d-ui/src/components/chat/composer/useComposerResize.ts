/**
 * Composer 高度拖拽调整 Hook
 * 职责：composerHeight 状态 + 拖拽逻辑 + localStorage 持久化
 */

import { useState, useCallback, type PointerEvent as ReactPointerEvent } from 'react'

// ── 常量 ──
const COMPOSER_MIN_HEIGHT = 56
const COMPOSER_MAX_HEIGHT = 280
const COMPOSER_MAX_VIEWPORT_RATIO = 0.35
const COMPOSER_HEIGHT_KEY = 'e3d-composer-height'

// ── 工具函数 ──
export function composerMaxHeight(): number {
  if (typeof window === 'undefined') return COMPOSER_MAX_HEIGHT
  return Math.max(COMPOSER_MIN_HEIGHT, Math.min(COMPOSER_MAX_HEIGHT, Math.floor(window.innerHeight * COMPOSER_MAX_VIEWPORT_RATIO)))
}

export function clampComposerHeight(h: number): number {
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

// ── Hook ──
export function useComposerResize(composerCardRef: React.RefObject<HTMLDivElement | null>) {
  const [composerHeight, setComposerHeight] = useState<number | null>(loadComposerHeight)
  const [composerResizing, setComposerResizing] = useState(false)

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
  }, [composerHeight, composerCardRef])

  const resetComposerHeight = useCallback(() => {
    setComposerHeight(null)
    clearComposerHeight()
  }, [])

  return { composerHeight, composerResizing, onComposerResizeStart, resetComposerHeight }
}
