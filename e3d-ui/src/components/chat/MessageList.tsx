/**
 * MessageList 组件
 * 可滚动消息容器 + ToolGroup 分组 + 自动滚动
 */

import { useRef, useEffect, useCallback, useState, useMemo } from 'react'
import { ChevronDown } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'
import { MessageRow } from './MessageRow'
import { ToolGroup, groupConsecutiveTools } from './ToolGroup'
import type { Message } from '@/types'

export function MessageList() {
  const messages = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.messages ?? [])
  const parentRef = useRef<HTMLDivElement>(null)
  const autoScrollRef = useRef(true)
  const [showScrollBtn, setShowScrollBtn] = useState(false)

  // 计算子调用关系
  const subcallMap = useMemo(() => {
    const map = new Map<string, Message[]>()
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

  // 过滤掉已被归为子调用的消息 + thinking 消息（内联到 assistant 中）
  const { topLevelMessages, thinkingMap } = useMemo(() => {
    const childIds = new Set<string>()
    for (const children of subcallMap.values()) {
      for (const child of children) childIds.add(child.id)
    }

    // 建立 thinking → assistant 的映射
    const tMap = new Map<string, Message>()
    for (let i = 0; i < messages.length; i++) {
      const m = messages[i]
      if (m.role === 'thinking' && i + 1 < messages.length) {
        const next = messages[i + 1]
        if (next.role === 'assistant') {
          tMap.set(next.id, m)
        }
      }
    }

    // 过滤掉 thinking 和 child 消息
    const filtered = messages.filter(
      (msg) => !childIds.has(msg.id) && msg.role !== 'thinking'
    )

    return { topLevelMessages: filtered, thinkingMap: tMap }
  }, [messages, subcallMap])

  // ToolGroup 分组
  const displayItems = useMemo(() => groupConsecutiveTools(topLevelMessages), [topLevelMessages])

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
    if (autoScrollRef.current && displayItems.length > 0) {
      parentRef.current?.scrollTo({ top: parentRef.current.scrollHeight, behavior: 'smooth' })
    }
  }, [displayItems.length])

  useEffect(() => {
    if (displayItems.length === 0) {
      autoScrollRef.current = true
      setShowScrollBtn(false)
    }
  }, [displayItems.length])

  const scrollToBottom = () => {
    parentRef.current?.scrollTo({ top: parentRef.current.scrollHeight, behavior: 'smooth' })
    autoScrollRef.current = true
    setShowScrollBtn(false)
  }

  return (
    <div className="flex-1 overflow-hidden relative">
      <div
        ref={parentRef}
        onScroll={handleScroll}
        className="h-full overflow-y-auto px-3 py-2"
      >
        <div className="space-y-0.5">
          {displayItems.map((item, index) => {
            if (item.kind === 'group') {
              return (
                <ToolGroup
                  key={`group-${index}`}
                  kind={item.groupKind}
                  messages={item.messages}
                  subcalls={subcallMap}
                  allMessages={messages}
                />
              )
            }

            const msg = item.msg
            const toolId = msg.toolId || msg.id
            const subcalls = subcallMap.get(toolId)

            return (
              <MessageRow
                key={msg.id}
                msg={msg}
                subcalls={subcalls}
                allMessages={messages}
                thinkingMsg={thinkingMap.get(msg.id)}
              />
            )
          })}
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
