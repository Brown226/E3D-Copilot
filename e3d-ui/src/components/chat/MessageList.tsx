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
import { useChatStore, useActiveTab } from '@/store/useChatStore'
import type { Message } from '@/types'
import { MessageRow } from './MessageRow'
import { ToolGroup, groupConsecutiveTools, type ToolGroupKind } from './ToolGroup'
import { ApprovalCard } from './ApprovalCard'
import { AskUserCard } from './AskUserCard'
import { TodoPanel } from './TodoPanel'
import { SubagentPanel } from './SubagentPanel'
import { SearchOverlay } from './SearchOverlay'

// ── 分层常量 ──
const HOT_TURNS = 8
const WARM_PAGE_SIZE = 5

// ── 类型 ──
type DisplayItem =
  | { kind: 'message'; msg: Message }
  | { kind: 'group'; groupKind: ToolGroupKind; messages: Message[] }

/**
 * 计算给定消息序列中"最终 assistant 消息"的 id 集合。
 * 最终 = 该 assistant 之后没有更多 tool_call/tool_result/assistant 消息。
 * 用于让 MessageRow 的 memo 生效（避免每条消息独立 O(n) 遍历）。
 */
export function computeFinalAssistantIds(messages: Message[]): Set<string> {
  const ids = new Set<string>()
  for (let i = 0; i < messages.length; i++) {
    const m = messages[i]
    if (m.role !== 'assistant') continue
    let hasAfter = false
    for (let j = i + 1; j < messages.length; j++) {
      const r = messages[j].role
      if (r === 'tool_call' || r === 'tool_result' || r === 'assistant') {
        hasAfter = true
        break
      }
    }
    if (!hasAfter) ids.add(m.id)
  }
  return ids
}

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

// ── 将消息转换为 DisplayItem ──
// 每个 tool_call / tool_result 保持独立一行。
// 折叠由 ToolCard 自己负责（运行中展开、完成后收起），与思考块交互一致。
// 这里不再把连续工具合并成「已处理 · N 步」——那样用户只能看到一个汇总数字，
// 看不到到底执行了哪几个工具。
function buildDisplayItems(
  messages: Message[],
  _subcallMap: Map<string, Message[]>,
  isStreaming: boolean,
): DisplayItem[] {
  const grouped = groupConsecutiveTools(messages)
  const result: DisplayItem[] = []

  for (const item of grouped) {
    if (item.kind === 'message') {
      const msg = item.msg

      // user：始终全量渲染
      if (msg.role === 'user') {
        result.push(item)
        continue
      }

      // thinking：独立渲染 ReasoningBlock，流式中展开、完成后折叠
      if (msg.role === 'thinking') {
        const displayMsg = isStreaming ? msg : { ...msg, finalized: true }
        result.push({ kind: 'message', msg: displayMsg })
        continue
      }

      // assistant：有实质内容才渲染，空内容跳过
      if (msg.role === 'assistant') {
        if (msg.content.trim()) result.push(item)
        continue
      }

      // error / tool_call / tool_result：全量渲染（工具自身的折叠由 ToolCard 处理）
      result.push(item)
    } else {
      // ToolGroup：保持独立渲染
      result.push(item)
    }
  }

  return result
}

export function MessageList() {
  const { messages, isStreaming, pendingApproval, pendingQuestion } = useActiveTab()
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

  // 收集子代理消息（按 agentName 分组）
  const subagentGroups = useMemo(() => {
    const groups = new Map<string, Message[]>()
    for (const msg of messages) {
      if (msg.agentName) {
        const list = groups.get(msg.agentName) || []
        list.push(msg)
        groups.set(msg.agentName, list)
      }
    }
    return groups
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
  // 子代理消息（agentName != null）由 SubagentPanel 统一渲染，不在主消息流中显示
  // 跳过后续 item 的生成
  const hotDisplayItems = useMemo(
    () => buildDisplayItems(hotMessages.filter(m => !m.agentName), subcallMap, isStreaming),
    [hotMessages, subcallMap, isStreaming],
  )

  // Warm zone turns（从后往前，跳过已展开的）
  const warmTurns = useMemo(() => {
    if (warmTurnCount === 0) return []
    const start = shownWarmCount < warmTurnCount ? warmTurnCount - shownWarmCount : 0
    return turnGroups.slice(start, warmTurnCount)
  }, [turnGroups, warmTurnCount, shownWarmCount])

  // 「粘底」状态：内容高度变化时是否继续跟随到底部。
  // 与 autoScrollRef 的区别：autoScrollRef 表示「用户当前是否在底部」，
  // stickToBottomRef 表示「是否允许自动跟随」——首次进入/切换会话后短暂为 true，
  // 用来自动跨过 Markdown/KaTeX/Mermaid 的异步渲染撑高。
  const stickToBottomRef = useRef(true)

  // 检测用户是否手动上滚
  const handleScroll = useCallback(() => {
    const el = parentRef.current
    if (!el) return
    const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 60
    autoScrollRef.current = atBottom
    setShowScrollBtn(!atBottom)
    // 用户主动离开底部后立刻停止跟随，避免和用户抢滚动条
    if (!atBottom) stickToBottomRef.current = false
  }, [])

  // 流式时追踪最后一条消息的 content 变化（messages.length 不变但 content 增长）
  const lastMsgContentLen = useMemo(() => {
    const last = messages[messages.length - 1]
    return last ? (last.content?.length ?? 0) : 0
  }, [messages])

  // 消息「指纹」：切换会话时即使条数恰好相同，也能识别出内容已换。
  // 只依赖 messages.length 会导致「进入另一个同等长度的会话」时完全不滚动。
  const messagesKey = useMemo(() => {
    const first = messages[0]
    const last = messages[messages.length - 1]
    return `${messages.length}:${first?.id ?? ''}:${last?.id ?? ''}`
  }, [messages])

  // 会话级 key：当前 Tab 的 sessionId，用于在切换会话时强制重建子树。
  // 单独选 s.sessionId 作兜底，避免在 selector 里做字符串拼接导致快照不稳定（React #185）。
  const tabSessionId = useChatStore((s) => s.tabs.find((x) => x.id === s.activeTabId)?.sessionId)
  const globalSessionId = useChatStore((s) => s.sessionId)
  const sessionIdForKey = tabSessionId || globalSessionId

  // 会话切换 / 首次挂载：无论用户之前滚到哪里，都应贴底。
  // 同时用 ResizeObserver 跟随异步内容撑高，直到内容稳定或用户上滚。
  useEffect(() => {
    const el = parentRef.current
    if (!el) return
    if (messages.length === 0) {
      stickToBottomRef.current = false
      return
    }
    stickToBottomRef.current = true
    autoScrollRef.current = true

    const pin = () => {
      if (!stickToBottomRef.current) return
      el.scrollTop = el.scrollHeight
    }
    pin()

    const ro = new ResizeObserver(() => {
      pin()
    })
    // 观察内层内容容器：它的高度变化代表渲染完成度的推进
    const inner = el.firstElementChild
    if (inner) ro.observe(inner)

    // 兜底：异步内容通常在 1.2s 内稳定，之后不再强拉
    const timer = window.setTimeout(() => { stickToBottomRef.current = false }, 1200)

    return () => {
      ro.disconnect()
      window.clearTimeout(timer)
    }
    // sessionIdForKey 变化会重建 transcript 容器（key），必须重新贴底
  }, [messagesKey, messages.length, sessionIdForKey])

  // 同一会话内的增量滚动：新消息、content 增长、审批/提问卡片出现
  // 尊重用户是否在底部（autoScrollRef），不强行拉回。
  useEffect(() => {
    const el = parentRef.current
    if (!el || messages.length === 0) return
    if (!autoScrollRef.current && !stickToBottomRef.current) return
    const hasCard = pendingApproval != null || pendingQuestion != null
    el.scrollTo({ top: el.scrollHeight, behavior: hasCard ? 'smooth' : 'auto' })
  }, [messages.length, lastMsgContentLen, pendingApproval, pendingQuestion])

  // Warm zone 展开态与分页按「消息下标」存储，切换会话必须重置，
  // 否则新会话里相同下标的另一轮历史会莫名其妙处于展开/已加载状态。
  useEffect(() => {
    autoScrollRef.current = true
    setShowScrollBtn(false)
    setExpandedWarmTurns(new Set())
    setColdPage(0)
  }, [sessionIdForKey])

  const scrollToBottom = () => {
    parentRef.current?.scrollTo({ top: parentRef.current.scrollHeight, behavior: 'smooth' })
    autoScrollRef.current = true
    setShowScrollBtn(false)
  }

  // 审批响应。session = 用户点了「本次允许」，后端据此在本轮对话内放行该工具。
  const handleApproval = useCallback((allow: boolean, session: boolean) => {
    if (!pendingApproval) return
    import('@/services/bridgeService').then(({ default: bridge }) => {
      bridge.sendApproval(pendingApproval.toolId, allow, session)
    })
    useChatStore.getState().setPendingApproval(null)
  }, [pendingApproval])

  // AI 提问响应（统一 AskAnswer 协议）
  const handleAskAnswer = useCallback((askId: string, answers: Array<{ questionId: string; selected: string[] }>) => {
    import('@/services/bridgeService').then(({ default: bridge }) => {
      bridge.sendAskAnswer(askId, answers)
    })
    useChatStore.getState().setPendingQuestion(null)
  }, [])

  // 预计算 Hot zone 内"最终 assistant 消息"id 集合（避免 renderItem 内 O(n²) 遍历）
  const finalAssistantIds = useMemo(
    () => computeFinalAssistantIds(hotMessages),
    [hotMessages],
  )

  // 渲染单个 DisplayItem
  const renderItem = (item: DisplayItem, key: string, finalIds: Set<string> = finalAssistantIds) => {
    if (item.kind === 'group') {
      return <ToolGroup key={key} kind={item.groupKind} messages={item.messages} subcalls={subcallMap} />
    }
    const msg = item.msg
    const toolId = msg.toolId || msg.id
    const isFinal = msg.role === 'assistant' ? finalIds.has(msg.id) : undefined
    return <MessageRow key={key} msg={msg} subcalls={subcallMap.get(toolId)} isFinal={isFinal} isStreaming={isStreaming} />
  }

  return (
    <div className="transcript-shell" style={{ position: 'relative', flex: '1 1 auto', minHeight: 0, height: '100%' }}>
      <SearchOverlay />
      {/* key 用会话指纹：切换会话时强制重建内部消息组件，
          避免 UserBubble 的编辑态、ToolCard 的展开态等交互状态串到另一个会话 */}
      <div ref={parentRef} onScroll={handleScroll} className="transcript" key={`transcript-${sessionIdForKey}`}>
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
            const warmFinalIds = computeFinalAssistantIds(warmMessages)

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
                    {warmDisplay.map((item, i) => renderItem(item, `we-${turnIdx}-${i}`, warmFinalIds))}
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

          {hotDisplayItems.map((item, i) => renderItem(item, `hot-${i}`))}

          {/* ═══════ 子代理面板 ═══════ */}
          {Array.from(subagentGroups.entries()).map(([name, msgs]) => (
            <SubagentPanel
              key={`subagent-${name}`}
              agentName={name}
              messages={msgs}
              subcalls={subcallMap}
            />
          ))}

          {/* ═══════ 审批卡片 ═══════ */}
          {/* key 用 toolId：连续出现不同审批时强制重建，避免选中态与闭包串到上一条 */}
          {pendingApproval && (
            <ApprovalCard
              key={pendingApproval.toolId}
              approval={pendingApproval}
              onAnswer={handleApproval}
            />
          )}

          {/* ═══════ AI 提问卡片 ═══════ */}
          {/* key 用 askId：questions 只按挂载时的 props 初始化答案数组，换题必须重建 */}
          {pendingQuestion && (
            <AskUserCard
              key={pendingQuestion.askData?.askId || pendingQuestion.questionId}
              question={pendingQuestion}
              onAskAnswer={handleAskAnswer}
            />
          )}
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
