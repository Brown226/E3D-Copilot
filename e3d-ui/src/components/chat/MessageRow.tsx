/**
 * MessageRow 组件（分发器）
 * 根据 msg.role 分发到对应消息组件
 * 特殊处理：将 thinking 消息传递给 AssistantBubble 作为内联 reasoning
 */

import type { Message } from '@/types'
import { UserBubble } from './UserBubble'
import { AssistantBubble, ReasoningBlock } from './AssistantBubble'
import { ToolCard } from './ToolCard'
import { ErrorCard } from './ErrorCard'
import { useChatStore } from '@/store/useChatStore'

interface MessageRowProps {
  msg: Message
  subcalls?: Message[]
  allMessages?: Message[]
}

export function MessageRow({ msg, subcalls, allMessages }: MessageRowProps) {
  const rollbackToMessage = useChatStore((s) => s.rollbackToMessage)
  const isStreaming = useChatStore((s) =>
    s.tabs.find((t) => t.id === s.activeTabId)?.isStreaming ?? false
  )

  switch (msg.role) {
    case 'user':
      return (
        <div className="msg-row msg-row--user" data-msg-id={msg.id}>
          <UserBubble msg={msg} />
          {!isStreaming && (
            <button
              className="msg-rollback-btn"
              onClick={() => rollbackToMessage(msg.id)}
              title="从这继续"
            >
              ↩
            </button>
          )}
        </div>
      )
    case 'assistant': {
      // 判断是否为最终回复：后面没有 tool_call/tool_result/assistant 的就是最终回复
      const msgIdx = allMessages?.findIndex(m => m.id === msg.id) ?? -1
      const isFinal = msgIdx < 0 || !allMessages?.some((m, i) =>
        i > msgIdx && (m.role === 'tool_call' || m.role === 'tool_result' || m.role === 'assistant')
      )
      return (
        <div className="msg-row msg-row--assistant" data-msg-id={msg.id}>
          {msg.confidence === 'low' && (
            <span className="confidence-badge" title="低置信度，请核实">⚠️</span>
          )}
          <AssistantBubble msg={msg} isFinal={isFinal} />
        </div>
      )
    }
    case 'thinking':
      // thinking 消息始终独立渲染（Reasonix 风格：思考在前、工具在中、回复在后）
      return <ReasoningBlock msg={msg} />
    case 'tool_call':
    case 'tool_result':
      return (
        <ToolCard
          msg={msg}
          subcalls={subcalls}
          allMessages={allMessages}
        />
      )
    case 'error':
      return <ErrorCard msg={msg} />
    default:
      return null
  }
}
