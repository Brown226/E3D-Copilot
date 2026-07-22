/**
 * 输入历史导航 Hook
 * 职责：historyEntries 状态 + ↑↓ 导航 + localStorage 持久化
 */

import { useState, useRef, useEffect } from 'react'

const MAX_HISTORY = 100

export function useInputHistory(setInputValue: (v: string) => void) {
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

  /** 将文本加入历史（发送时调用） */
  const pushHistory = (text: string) => {
    if (!text) return
    setHistoryEntries((prev) => {
      const filtered = prev.filter((h) => h !== text)
      return [text, ...filtered].slice(0, MAX_HISTORY)
    })
    historyIndexRef.current = -1
    savedTextRef.current = ''
  }

  /** ↑↓ 导航历史 */
  const navigateHistory = (direction: -1 | 1, textareaRef: React.RefObject<HTMLTextAreaElement | null>) => {
    const ta = textareaRef.current
    if (!ta || historyEntries.length === 0) return
    const newIndex = historyIndexRef.current + direction
    if (newIndex < -1 || newIndex >= historyEntries.length) return
    if (historyIndexRef.current === -1 && direction === -1) savedTextRef.current = ta.value
    historyIndexRef.current = newIndex
    setInputValue(newIndex === -1 ? savedTextRef.current : historyEntries[newIndex])
    requestAnimationFrame(() => { ta.selectionStart = ta.selectionEnd = ta.value.length })
  }

  /** 重置历史导航状态（Escape 或其他按键时调用） */
  const resetHistoryNavigation = () => {
    historyIndexRef.current = -1
    savedTextRef.current = ''
  }

  return { historyEntries, pushHistory, navigateHistory, resetHistoryNavigation, historyIndexRef }
}
