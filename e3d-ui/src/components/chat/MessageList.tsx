/**
 * MessageList 组件
 * 可滚动消息容器，虚拟滚动 + 自动滚动 + 滚动到底部按钮
 * 增加：计算子调用关系，传递给 ToolCard
 */

import { useRef, useEffect, useCallback, useState, useMemo } from 'react'
import { ChevronDown } from 'lucide-react'
import { useVirtualizer } from '@tanstack/react-virtual'
import { useChatStore } from '@/store/useChatStore'
import { MessageRow } from './MessageRow'

export function MessageList() {
  const messages = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.messages ?? [])
  const parentRef = useRef<HTMLDivElement>(null)
  const autoScrollRef = useRef(true)
  const [showScrollBtn, setShowScrollBtn] = useState(false)

  // 计算子调用关系
  const subcallMap = useMemo(() => {
    const map = new Map<string, typeof messages>()
    let currentParent: string | null = null
    const SUBAGENT_TOOLS = new Set(['task', 'explore', 'research', 'review'])

    for (const msg of messages) {
      if (msg.role === 'tool_call') {
        if (SUBAGENT_TOOLS.has(msg.toolName || '')) {
          currentParent = msg.toolId || msg.id
          if (!map.has(currentParent)) map.set(currentParent, [])
        } else if (currentParent) {
          map.get(currentParent)!.push(msg)
        }
      } else if (msg.role === 'tool_result' && currentParent) {
        map.get(currentParent)!.push(msg)
      } else if (msg.role === 'assistant' || msg.role === 'user') {
        currentParent = null
      }
    }
    return map
  }, [messages])

  // 过滤掉已被归为子调用的消息
  const topLevelMessages = useMemo(() => {
    const childIds = new Set<string>()
    for (const children of subcallMap.values()) {
      for (const child of children) childIds.add(child.id)
    }
    return messages.filter((msg) => !childIds.has(msg.id))
  }, [messages, subcallMap])

  // 虚拟滚动
  const virtualizer = useVirtualizer({
    count: topLevelMessages.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => 80, // 估计每条消息高度
    overscan: 5,
  })

  // 检测用户是否手动上滚
  const handleScroll = useCallback(() => {
    const el = parentRef.current
    if (!el) return
    const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 50
    autoScrollRef.current = atBottom
    setShowScrollBtn(!atBottom)
  }, [])

  // 新消息到达时自动滚动到底部
  useEffect(() => {
    if (autoScrollRef.current && topLevelMessages.length > 0) {
      virtualizer.scrollToIndex(topLevelMessages.length - 1, { align: 'end' })
    }
  }, [topLevelMessages.length, virtualizer])

  useEffect(() => {
    if (topLevelMessages.length === 0) {
      autoScrollRef.current = true
      setShowScrollBtn(false)
    }
  }, [topLevelMessages.length])

  const scrollToBottom = () => {
    if (topLevelMessages.length > 0) {
      virtualizer.scrollToIndex(topLevelMessages.length - 1, { align: 'end' })
    }
    autoScrollRef.current = true
    setShowScrollBtn(false)
  }

  return (
    <div className="flex-1 overflow-hidden relative">
      <div
        ref={parentRef}
        onScroll={handleScroll}
        className="h-full overflow-y-auto px-6 py-4"
      >
        {/* 虚拟滚动容器 */}
        <div
          style={{ height: `${virtualizer.getTotalSize()}px`, width: '100%', position: 'relative' }}
        >
          <div className="max-w-4xl mx-auto">
            {virtualizer.getVirtualItems().map((virtualRow) => {
              const msg = topLevelMessages[virtualRow.index]
              const toolId = msg.toolId || msg.id
              const subcalls = subcallMap.get(toolId)
              return (
                <div
                  key={msg.id}
                  data-index={virtualRow.index}
                  ref={virtualizer.measureElement}
                  style={{
                    position: 'absolute',
                    top: 0,
                    left: 0,
                    width: '100%',
                    transform: `translateY(${virtualRow.start}px)`,
                  }}
                >
                  <div className="pb-4">
                    <MessageRow
                      msg={msg}
                      subcalls={subcalls}
                      allMessages={messages}
                    />
                  </div>
                </div>
              )
            })}
          </div>
        </div>
      </div>

      {showScrollBtn && (
        <button
          onClick={scrollToBottom}
          className="fixed bottom-28 right-8 w-10 h-10 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-600 rounded-full shadow-lg flex items-center justify-center text-slate-500 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors z-10"
          title="滚动到底部"
        >
          <ChevronDown className="w-5 h-5" />
        </button>
      )}
    </div>
  )
}
