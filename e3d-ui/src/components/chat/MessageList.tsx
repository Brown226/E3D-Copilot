/**
 * MessageList 组件
 * 可滚动消息容器 + 三层分层渲染 + Compact步骤折叠 + 审批卡片 + 自动滚动
 *
 * 分层策略（适配窄面板）：
 * - Hot zone：最近 N 轮对话全量渲染
 * - Warm zone：更早的对话折叠为摘要卡片，点击展开
 * - Cold zone：折叠的 Warm zone 超过阈值时显示"加载更多"
 *
 * Compact 步骤折叠：
 * - Hot zone 内连续的已完成工具调用折叠为"已处理 · N 步"
 * - 最终回复和正在进行的步骤始终全量渲染
 */

import { useRef, useEffect, useCallback, useState, useMemo } from 'react'
import { ChevronDown, ChevronRight } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'
import type { Message } from '@/types'
import { MessageRow } from './MessageRow'
import { ToolGroup, groupConsecutiveTools, type ToolGroupKind } from './ToolGroup'
import { ApprovalCard } from './ApprovalCard'
import { TodoPanel } from './TodoPanel'

// ── 分层常量 ──
const HOT_TURNS = 8
const WARM_PAGE_SIZE = 5

// ── 类型 ──
type DisplayItem =
  | { kind: 'message'; msg: Message }
  | { kind: 'group'; groupKind: ToolGroupKind; messages: Message[] }
  | { kind: 'folded'; messages: Message[]; toolCount: number; durationMs: number }

// ── Turn 分组：以 user 消息为界 ──
interface TurnGroup {
  startIdx: number
  endIdx: number
  userText: string
  toolCount: number
  assistantPreview: string
}

function buildTurnGroups(messages: Message[]): TurnGroup[] {
  const groups: TurnGroup[] = []
  let current: { startIdx: number; userText: string; toolCount: number } | null = null

  for (let i = 0; i < messages.length; i++) {
    const msg = messages[i]
    if (msg.role === 'user') {
      if (current) groups.push({ ...current, endIdx: i, assistantPreview: '' })
      current = { startIdx: i, userText: msg.content.slice(0, 60), toolCount: 0 }
    } else if (current) {
      if (msg.role === 'tool_call' || msg.role === 'tool_result') current.toolCount++
    }
  }
  if (current) groups.push({ ...current, endIdx: messages.length, assistantPreview: '' })

  // 填充 assistantPreview
  for (const g of groups) {
    for (let i = g.startIdx; i < g.endIdx; i++) {
      if (messages[i].role === 'assistant' && messages[i].content.trim()) {
        g.assistantPreview = messages[i].content.slice(0, 100)
        break
      }
    }
  }
  return groups
}

// ── 将消息转换为 DisplayItem，含 Compact 折叠 ──
function buildDisplayItems(
  messages: Message[],
  _subcallMap: Map<string, Message[]>,
  isStreaming: boolean,
): DisplayItem[] {
  // 先用 groupConsecutiveTools 分组
  const grouped = groupConsecutiveTools(messages)

  // 合并连续的已完成工具组/消息为 folded
  const result: DisplayItem[] = []
  let foldedMessages: Message[] = []
  let foldedToolCount = 0
  let foldedDuration = 0

  const flushFolded = () => {
    if (foldedToolCount > 0) {
      result.push({
        kind: 'folded',
        messages: foldedMessages,
        toolCount: foldedToolCount,
        durationMs: foldedDuration,
      })
    }
    foldedMessages = []
    foldedToolCount = 0
    foldedDuration = 0
  }

  for (const item of grouped) {
    if (item.kind === 'message') {
      const msg = item.msg

      // user 消息：始终全量渲染
      if (msg.role === 'user') {
        flushFolded()
        result.push(item)
        continue
      }

      // assistant 消息：有实质内容则全量渲染，空内容跳过
      if (msg.role === 'assistant') {
        if (msg.content.trim()) {
          flushFolded()
          result.push(item)
        }
        // 空的 assistant 占位消息不渲染也不折叠
        continue
      }

      // error 消息：全量渲染
      if (msg.role === 'error') {
        flushFolded()
        result.push(item)
        continue
      }

      // tool_call / tool_result：检查是否已完成
      const isComplete = msg.role === 'tool_result' || (msg.role === 'tool_call' && msg.finalized)
      if (isComplete && !isStreaming) {
        foldedMessages.push(msg)
        if (msg.role === 'tool_call') foldedToolCount++
        foldedDuration += msg.durationMs ?? 0
      } else {
        // 正在进行的工具调用：全量渲染
        flushFolded()
        result.push(item)
      }
    } else {
      // ToolGroup：检查组内工具是否全部完成
      const allComplete = item.messages.every(
        (m) => m.role === 'tool_result' || (m.role === 'tool_call' && m.finalized),
      )
      const toolCalls = item.messages.filter((m) => m.role === 'tool_call')

      if (allComplete && !isStreaming && toolCalls.length > 0) {
        foldedMessages.push(...item.messages)
        foldedToolCount += toolCalls.length
        foldedDuration += item.messages.reduce((ms, m) => ms + (m.durationMs ?? 0), 0)
      } else {
        flushFolded()
        result.push(item)
      }
    }
  }
  flushFolded()

  return result
}

export function MessageList() {
  const messages = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.messages ?? [])
  const pendingApproval = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.pendingApproval ?? null)
  const isStreaming = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.isStreaming ?? false)
  const parentRef = useRef<HTMLDivElement>(null)
  const autoScrollRef = useRef(true)
  const [showScrollBtn, setShowScrollBtn] = useState(false)

  // Warm zone 展开状态
  const [expandedWarmTurns, setExpandedWarmTurns] = useState<Set<number>>(new Set())
  const [coldPage, setColdPage] = useState(0)

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

  // thinking 消息映射（传递给 AssistantBubble）
  const thinkingMap = useMemo(() => {
    const tMap = new Map<string, Message>()
    for (let i = 0; i < messages.length; i++) {
      const m = messages[i]
      if (m.role === 'thinking' && i + 1 < messages.length) {
        const next = messages[i + 1]
        if (next.role === 'assistant') tMap.set(next.id, m)
      }
    }
    return tMap
  }, [messages])

  // 过滤掉已被归为子调用的消息 + thinking 消息
  const topLevelMessages = useMemo(() => {
    const childIds = new Set<string>()
    for (const children of subcallMap.values()) {
      for (const child of children) childIds.add(child.id)
    }
    return messages.filter((msg) => !childIds.has(msg.id) && msg.role !== 'thinking')
  }, [messages, subcallMap])

  // ── 三层分层计算 ──
  const turnGroups = useMemo(() => buildTurnGroups(messages), [messages])

  // hotStartIdx: Hot zone 起始消息索引
  const hotStartIdx = useMemo(() => {
    let userCount = 0
    for (let i = messages.length - 1; i >= 0; i--) {
      if (messages[i].role === 'user') {
        userCount++
        if (userCount >= HOT_TURNS) return i
      }
    }
    return 0
  }, [messages])

  // Warm/Cold turn 分割
  const hotTurnCount = Math.min(turnGroups.length, HOT_TURNS)
  const warmTurnCount = turnGroups.length - hotTurnCount
  const shownWarmCount = Math.min(warmTurnCount, coldPage * WARM_PAGE_SIZE + WARM_PAGE_SIZE)
  const coldTurnCount = warmTurnCount - shownWarmCount

  // Hot zone 顶层消息
  const hotMessages = useMemo(() => {
    const hotStartId = messages[hotStartIdx]?.id
    const startInTop = topLevelMessages.findIndex((m) => m.id === hotStartId)
    return startInTop >= 0 ? topLevelMessages.slice(startInTop) : topLevelMessages
  }, [messages, hotStartIdx, topLevelMessages])

  // Hot zone display items（含 Compact 折叠）
  const hotDisplayItems = useMemo(
    () => buildDisplayItems(hotMessages, subcallMap, isStreaming),
    [hotMessages, subcallMap, isStreaming],
  )

  // Warm zone turns（从后往前，跳过已展开的）
  const warmTurns = useMemo(() => {
    if (warmTurnCount === 0) return []
    const start = shownWarmCount < warmTurnCount ? warmTurnCount - shownWarmCount : 0
    return turnGroups.slice(start, warmTurnCount)
  }, [turnGroups, warmTurnCount, shownWarmCount])

  // 检测用户是否手动上滚
  const handleScroll = useCallback(() => {
    const el = parentRef.current
    if (!el) return
    const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 60
    autoScrollRef.current = atBottom
    setShowScrollBtn(!atBottom)
  }, [])

  // 新消息到达时自动滚动
  useEffect(() => {
    if (autoScrollRef.current && messages.length > 0) {
      parentRef.current?.scrollTo({ top: parentRef.current.scrollHeight, behavior: 'smooth' })
    }
  }, [messages.length])

  useEffect(() => {
    if (messages.length === 0) {
      autoScrollRef.current = true
      setShowScrollBtn(false)
      setExpandedWarmTurns(new Set())
      setColdPage(0)
    }
  }, [messages.length])

  const scrollToBottom = () => {
    parentRef.current?.scrollTo({ top: parentRef.current.scrollHeight, behavior: 'smooth' })
    autoScrollRef.current = true
    setShowScrollBtn(false)
  }

  // 审批响应
  const handleApproval = useCallback((allow: boolean, _session: boolean) => {
    if (!pendingApproval) return
    import('@/services/bridgeService').then(({ default: bridge }) => {
      bridge.sendApproval(pendingApproval.toolId, allow)
    })
    useChatStore.getState().setPendingApproval(null)
  }, [pendingApproval])

  // 渲染单个 DisplayItem
  const renderItem = (item: DisplayItem, key: string, allMsgs: Message[]) => {
    if (item.kind === 'folded') {
      return <FoldedStep key={key} messages={item.messages} toolCount={item.toolCount} durationMs={item.durationMs} subcalls={subcallMap} allMessages={allMsgs} thinkingMap={thinkingMap} />
    }
    if (item.kind === 'group') {
      return <ToolGroup key={key} kind={item.groupKind} messages={item.messages} subcalls={subcallMap} allMessages={allMsgs} />
    }
    const msg = item.msg
    const toolId = msg.toolId || msg.id
    return <MessageRow key={key} msg={msg} subcalls={subcallMap.get(toolId)} allMessages={allMsgs} thinkingMsg={thinkingMap.get(msg.id)} />
  }

  return (
    <div className="flex-1 overflow-hidden relative">
      <div ref={parentRef} onScroll={handleScroll} className="h-full overflow-y-auto px-3 py-2">
        <div className="space-y-0.5">
          {/* ═══════ Cold zone：加载更多 ═══════ */}
          {coldTurnCount > 0 && (
            <button
              onClick={() => setColdPage((p) => p + 1)}
              className="w-full text-center py-2 text-xs text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors"
            >
              加载更早 {Math.min(WARM_PAGE_SIZE, coldTurnCount)} 条对话…
            </button>
          )}

          {/* ═══════ Warm zone：折叠的历史轮次 ═══════ */}
          {warmTurns.map((group, _idx) => {
            const turnIdx = turnGroups.indexOf(group)
            const expanded = expandedWarmTurns.has(turnIdx)
            const warmMessages = messages.slice(group.startIdx, group.endIdx)
            const warmDisplay = buildDisplayItems(warmMessages, subcallMap, false)

            if (expanded) {
              return (
                <div key={`warm-${turnIdx}`} className="border border-slate-200 dark:border-slate-700 rounded-lg p-1 mb-1">
                  <button
                    onClick={() => setExpandedWarmTurns((prev) => { const n = new Set(prev); n.delete(turnIdx); return n })}
                    className="w-full flex items-center gap-1.5 px-2 py-1 text-xs text-slate-500 hover:text-slate-700 dark:hover:text-slate-300"
                  >
                    <ChevronDown className="w-3 h-3" />
                    <span className="truncate">{group.userText}</span>
                  </button>
                  <div className="mt-1">
                    {warmDisplay.map((item, i) => renderItem(item, `we-${turnIdx}-${i}`, warmMessages))}
                  </div>
                </div>
              )
            }

            return (
              <button
                key={`warm-${turnIdx}`}
                onClick={() => setExpandedWarmTurns((prev) => { const n = new Set(prev); n.add(turnIdx); return n })}
                className="w-full flex items-center gap-2 px-2.5 py-2 mb-1 rounded-lg bg-slate-50 dark:bg-slate-800/50 border border-slate-100 dark:border-slate-700/50 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors text-left"
              >
                <ChevronRight className="w-3.5 h-3.5 text-slate-400 shrink-0" />
                <div className="flex-1 min-w-0">
                  <div className="text-xs font-medium text-slate-600 dark:text-slate-300 truncate">{group.userText}</div>
                  {group.assistantPreview && (
                    <div className="text-[11px] text-slate-400 dark:text-slate-500 truncate mt-0.5">{group.assistantPreview}</div>
                  )}
                </div>
                {group.toolCount > 0 && (
                  <span className="text-[10px] text-slate-400 dark:text-slate-500 shrink-0">{group.toolCount} 步</span>
                )}
              </button>
            )
          })}

          {/* ═══════ Hot zone：全量渲染（含 Compact 折叠） ═══════ */}
          {/* TodoPanel：从消息流提取最新 todo_write 状态 */}
          <TodoPanel />

          {hotDisplayItems.map((item, i) => renderItem(item, `hot-${i}`, hotMessages))}

          {/* ═══════ 审批卡片 ═══════ */}
          {pendingApproval && <ApprovalCard approval={pendingApproval} onAnswer={handleApproval} />}
        </div>
      </div>

      {/* 滚动到底部按钮 */}
      {showScrollBtn && (
        <button
          onClick={scrollToBottom}
          className="absolute bottom-4 right-4 w-9 h-9 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-600 rounded-full shadow-lg flex items-center justify-center text-slate-500 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors z-10"
          title="滚动到底部"
        >
          <ChevronDown className="w-5 h-5" />
        </button>
      )}
    </div>
  )
}

// ── 折叠步骤组件 ──
function FoldedStep({
  messages,
  toolCount,
  durationMs,
  subcalls,
  allMessages,
  thinkingMap,
}: {
  messages: Message[]
  toolCount: number
  durationMs: number
  subcalls: Map<string, Message[]>
  allMessages: Message[]
  thinkingMap: Map<string, Message>
}) {
  const [open, setOpen] = useState(false)
  const seconds = Math.round(durationMs / 1000)
  const label = seconds > 0 ? `已处理 · ${toolCount} 步 · ${seconds}s` : `已处理 · ${toolCount} 步`

  if (open) {
    const displayItems = buildDisplayItems(messages, subcalls, false)
    return (
      <div className="border border-slate-200 dark:border-slate-700 rounded-lg p-1 mb-1">
        <button
          onClick={() => setOpen(false)}
          className="w-full flex items-center gap-1.5 px-2 py-1 text-xs text-slate-500 hover:text-slate-700 dark:hover:text-slate-300"
        >
          <ChevronDown className="w-3 h-3" />
          <span>{label}</span>
        </button>
        <div className="mt-1">
          {displayItems.map((item, i) => {
            if (item.kind === 'folded') {
              return (
                <FoldedStep key={`fs-${i}`} messages={item.messages} toolCount={item.toolCount} durationMs={item.durationMs} subcalls={subcalls} allMessages={allMessages} thinkingMap={thinkingMap} />
              )
            }
            if (item.kind === 'group') {
              return <ToolGroup key={`fg-${i}`} kind={item.groupKind} messages={item.messages} subcalls={subcalls} allMessages={allMessages} />
            }
            const msg = item.msg
            const toolId = msg.toolId || msg.id
            return <MessageRow key={`fm-${i}`} msg={msg} subcalls={subcalls.get(toolId)} allMessages={allMessages} thinkingMsg={thinkingMap.get(msg.id)} />
          })}
        </div>
      </div>
    )
  }

  return (
    <button
      onClick={() => setOpen(true)}
      className="w-full flex items-center gap-1.5 px-2.5 py-1.5 mb-1 rounded-lg bg-slate-50 dark:bg-slate-800/50 border border-slate-100 dark:border-slate-700/50 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors text-left group"
    >
      <ChevronRight className="w-3.5 h-3.5 text-slate-400 shrink-0 group-hover:translate-x-0.5 transition-transform" />
      <span className="text-xs text-slate-500 dark:text-slate-400 font-medium">{label}</span>
    </button>
  )
}
