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

import { useRef, useEffect, useCallback, useState, useMemo, memo } from 'react'
import { ChevronDown, ChevronRight } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'
import type { Message } from '@/types'
import { MessageRow } from './MessageRow'
import { ToolGroup, groupConsecutiveTools, type ToolGroupKind } from './ToolGroup'
import { ApprovalCard } from './ApprovalCard'
import { AskUserCard } from './AskUserCard'
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

      // thinking 消息：始终独立渲染 ReasoningBlock（Reasonix 风格）
      // paired thinking (thinking → assistant) 和 orphan thinking 都统一渲染
      if (msg.role === 'thinking') {
        flushFolded()
        // 流式中保持展开，完成后折叠
        const displayMsg = isStreaming
          ? msg  // 流式中：finalized=undefined → ReasoningBlock 自动展开
          : { ...msg, finalized: true }  // 已结束：折叠
        result.push({ kind: 'message', msg: displayMsg })
        continue
      }

      // assistant 消息：有实质内容则全量渲染，空内容跳过
      if (msg.role === 'assistant') {
        flushFolded()
        if (msg.content.trim()) {
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

      // tool_call / tool_result：始终全量渲染（不折叠），保持工具调用可见
      // thinking 已经始终独立渲染，tool 卡片也应保持可见
      flushFolded()
      result.push(item)
    } else {
      // ToolGroup：始终全量渲染，保持工具组可见
      flushFolded()
      result.push(item)
    }
  }
  flushFolded()

  return result
}

export function MessageList() {
  const messages = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.messages ?? [])
  const pendingApproval = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.pendingApproval ?? null)
  const pendingQuestion = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.pendingQuestion ?? null)
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
    const SUBAGENT_TOOLS = new Set(['explore', 'research', 'review'])

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

  // 过滤掉已被归为子调用的消息（保留 thinking 消息，由 buildDisplayItems 处理）
  const topLevelMessages = useMemo(() => {
    const childIds = new Set<string>()
    for (const children of subcallMap.values()) {
      for (const child of children) childIds.add(child.id)
    }
    return messages.filter((msg) => !childIds.has(msg.id))
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

  // 流式时追踪最后一条消息的 content 变化（messages.length 不变但 content 增长）
  const lastMsgContentLen = useMemo(() => {
    const last = messages[messages.length - 1]
    return last ? (last.content?.length ?? 0) : 0
  }, [messages])

  // 新消息到达或 content 增长时自动滚动
  useEffect(() => {
    if (autoScrollRef.current && messages.length > 0) {
      parentRef.current?.scrollTo({ top: parentRef.current.scrollHeight, behavior: 'auto' })
    }
  }, [messages.length, lastMsgContentLen])

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

  // AI 提问响应
  const handleAskUser = useCallback((questionId: string, answer: string) => {
    import('@/services/bridgeService').then(({ default: bridge }) => {
      bridge.sendAskResponse(questionId, answer)
    })
    useChatStore.getState().setPendingQuestion(null)
  }, [])

  // 新版多问题回答（对齐 Reasonix AnswerQuestion）
  const handleAskAnswer = useCallback((askId: string, answers: Array<{ questionId: string; selected: string[] }>) => {
    import('@/services/bridgeService').then(({ default: bridge }) => {
      bridge.sendAskAnswer(askId, answers)
    })
    useChatStore.getState().setPendingQuestion(null)
  }, [])

  // 渲染单个 DisplayItem
  const renderItem = (item: DisplayItem, key: string, allMsgs: Message[]) => {
    if (item.kind === 'folded') {
      return <FoldedStep key={key} messages={item.messages} toolCount={item.toolCount} durationMs={item.durationMs} subcalls={subcallMap} allMessages={allMsgs} />
    }
    if (item.kind === 'group') {
      return <ToolGroup key={key} kind={item.groupKind} messages={item.messages} subcalls={subcallMap} allMessages={allMsgs} />
    }
    const msg = item.msg
    const toolId = msg.toolId || msg.id
    return <MessageRow key={key} msg={msg} subcalls={subcallMap.get(toolId)} allMessages={allMsgs} />
  }

  return (
    <div className="transcript-shell" style={{ position: 'relative', flex: '1 1 auto', minHeight: 0, height: '100%' }}>
      <div ref={parentRef} onScroll={handleScroll} className="transcript">
        <div>
          {/* ═══════ Cold zone：加载更多 ═══════ */}
          {coldTurnCount > 0 && (
            <button
              onClick={() => setColdPage((p) => p + 1)}
              className="warm-collapse"
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
                <div key={`warm-${turnIdx}`} className="warm-turn warm-turn--expanded">
                  <button
                    className="warm-turn__head"
                    onClick={() => setExpandedWarmTurns((prev) => { const n = new Set(prev); n.delete(turnIdx); return n })}
                    aria-expanded={true}
                  >
                    <ChevronRight size={13} className="warm-turn__chevron warm-turn__chevron--open" />
                    <span className="warm-turn__preview">{group.userText}</span>
                    {group.toolCount > 0 && (
                      <span className="warm-turn__meta">{group.toolCount} 步</span>
                    )}
                  </button>
                  <div className="warm-turn__content">
                    {warmDisplay.map((item, i) => renderItem(item, `we-${turnIdx}-${i}`, warmMessages))}
                  </div>
                </div>
              )
            }

            return (
              <div key={`warm-${turnIdx}`} className="warm-turn">
                <button
                  className="warm-turn__head"
                  onClick={() => setExpandedWarmTurns((prev) => { const n = new Set(prev); n.add(turnIdx); return n })}
                  aria-expanded={false}
                >
                  <ChevronRight size={13} className="warm-turn__chevron" />
                  <span className="warm-turn__preview">{group.userText}</span>
                  {group.assistantPreview && (
                    <span className="warm-turn__assistant" style={{ display: undefined }}>{group.assistantPreview}</span>
                  )}
                  {group.toolCount > 0 && (
                    <span className="warm-turn__meta">{group.toolCount} 步</span>
                  )}
                </button>
              </div>
            )
          })}

          {/* ═══════ Hot zone：全量渲染（含 Compact 折叠） ═══════ */}
          {/* TodoPanel：从消息流提取最新 todo_write 状态 */}
          <TodoPanel />

          {hotDisplayItems.map((item, i) => renderItem(item, `hot-${i}`, hotMessages))}

          {/* ═══════ 审批卡片 ═══════ */}
          {pendingApproval && <ApprovalCard approval={pendingApproval} onAnswer={handleApproval} />}

          {/* ═══════ AI 提问卡片 ═══════ */}
          {pendingQuestion && <AskUserCard question={pendingQuestion} onAnswer={handleAskUser} onAskAnswer={handleAskAnswer} />}
        </div>
      </div>

      {/* 滚动到底部按钮 */}
      {showScrollBtn && (
        <button
          onClick={scrollToBottom}
          className="transcript__jump-bottom"
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
}: {
  messages: Message[]
  toolCount: number
  durationMs: number
  subcalls: Map<string, Message[]>
  allMessages: Message[]
}) {
  const [open, setOpen] = useState(false)
  const seconds = Math.round(durationMs / 1000)
  const label = seconds > 0 ? `已处理 · ${toolCount} 步 · ${seconds}s` : `已处理 · ${toolCount} 步`

  if (open) {
    const displayItems = buildDisplayItems(messages, subcalls, false)
    return (
      <div className="turn-collapse turn-collapse--open">
        <button
          onClick={() => setOpen(false)}
          className="reasoning__head"
          aria-expanded={true}
        >
          <ChevronRight size={12} className="reasoning__chevron reasoning__chevron--open" />
          <span>{label}</span>
        </button>
        <div className="turn-collapse__body">
          {displayItems.map((item, i) => {
            if (item.kind === 'folded') {
              return (
                <FoldedStep key={`fs-${i}`} messages={item.messages} toolCount={item.toolCount} durationMs={item.durationMs} subcalls={subcalls} allMessages={allMessages} />
              )
            }
            if (item.kind === 'group') {
              return <ToolGroup key={`fg-${i}`} kind={item.groupKind} messages={item.messages} subcalls={subcalls} allMessages={allMessages} />
            }
            const msg = item.msg
            const toolId = msg.toolId || msg.id
            return <MessageRow key={`fm-${i}`} msg={msg} subcalls={subcalls.get(toolId)} allMessages={allMessages} />
          })}
        </div>
      </div>
    )
  }

  return (
    <div className="turn-collapse">
      <button
        onClick={() => setOpen(true)}
        className="reasoning__head"
        aria-expanded={false}
      >
        <ChevronRight size={12} className="reasoning__chevron" />
        <span>{label}</span>
      </button>
    </div>
  )
}
