/**
 * SearchOverlay — 对话内搜索（Ctrl+F）
 * 固定在消息列表顶部，实时搜索当前 Tab 消息内容
 */

import { useState, useMemo, useRef, useEffect, useCallback, type KeyboardEvent } from 'react'
import { ChevronUp, ChevronDown, X, Search } from 'lucide-react'
import { useChatStore, useActiveTab } from '@/store/useChatStore'

export function SearchOverlay() {
  const showSearch = useChatStore((s) => s.showSearch)
  const toggleSearch = useChatStore((s) => s.toggleSearch)
  const { messages } = useActiveTab()

  const [query, setQuery] = useState('')
  const [currentIdx, setCurrentIdx] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)
  const prevHighlightRef = useRef<string | null>(null)

  // 打开时自动聚焦
  useEffect(() => {
    if (showSearch) {
      setTimeout(() => inputRef.current?.focus(), 50)
    } else {
      setQuery('')
      setCurrentIdx(0)
      clearHighlight()
    }
  }, [showSearch])

  // 计算匹配列表
  const matches = useMemo(() => {
    if (!query.trim()) return []
    const lowerQ = query.toLowerCase()
    const results: { msgId: string }[] = []
    for (const msg of messages) {
      if (msg.content && msg.content.toLowerCase().includes(lowerQ)) {
        results.push({ msgId: msg.id })
      }
    }
    return results
  }, [messages, query])

  // 匹配数变化时重置索引
  useEffect(() => {
    setCurrentIdx(0)
  }, [matches.length])

  const clearHighlight = useCallback(() => {
    if (prevHighlightRef.current) {
      const el = document.querySelector(`[data-msg-id="${prevHighlightRef.current}"]`)
      el?.classList.remove('search-highlight')
      prevHighlightRef.current = null
    }
  }, [])

  // 导航到指定匹配
  const navigateTo = useCallback((idx: number) => {
    if (matches.length === 0) return
    const clampedIdx = ((idx % matches.length) + matches.length) % matches.length
    setCurrentIdx(clampedIdx)

    // 清除上一个高亮
    clearHighlight()

    // 高亮并滚动到目标消息
    const target = matches[clampedIdx]
    if (target) {
      const el = document.querySelector(`[data-msg-id="${target.msgId}"]`)
      if (el) {
        el.classList.add('search-highlight')
        el.scrollIntoView({ behavior: 'smooth', block: 'center' })
        prevHighlightRef.current = target.msgId
      }
    }
  }, [matches, clearHighlight])

  const goNext = useCallback(() => navigateTo(currentIdx + 1), [currentIdx, navigateTo])
  const goPrev = useCallback(() => navigateTo(currentIdx - 1), [currentIdx, navigateTo])

  const handleClose = () => {
    toggleSearch()
  }

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault()
      if (e.shiftKey) goPrev()
      else goNext()
    } else if (e.key === 'Escape') {
      e.preventDefault()
      handleClose()
    }
  }

  if (!showSearch) return null

  return (
    <div className="search-overlay">
      <div className="search-overlay__inner">
        <Search size={14} className="search-overlay__icon" />
        <input
          ref={inputRef}
          className="search-overlay__input"
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="搜索消息内容..."
        />
        {query.trim() && (
          <span className="search-overlay__count">
            {matches.length > 0 ? `${currentIdx + 1}/${matches.length}` : '0/0'}
          </span>
        )}
        <button className="search-overlay__btn" onClick={goPrev} title="上一个 (Shift+Enter)" disabled={matches.length === 0}>
          <ChevronUp size={14} />
        </button>
        <button className="search-overlay__btn" onClick={goNext} title="下一个 (Enter)" disabled={matches.length === 0}>
          <ChevronDown size={14} />
        </button>
        <button className="search-overlay__btn" onClick={handleClose} title="关闭 (Escape)">
          <X size={14} />
        </button>
      </div>
    </div>
  )
}
