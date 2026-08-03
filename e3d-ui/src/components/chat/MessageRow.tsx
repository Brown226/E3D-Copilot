/**
 * MessageRow 组件（分发器）
 * 根据 msg.role 分发到对应消息组件
 * 特殊处理：将 thinking 消息传递给 AssistantBubble 作为内联 reasoning
 */

import { memo } from 'react'
import type { Message } from '@/types'
import { UserBubble } from './UserBubble'
import { AssistantBubble, ReasoningBlock } from './AssistantBubble'
import { ToolCard } from './ToolCard'
import { ErrorCard } from './ErrorCard'
import { useChatStore } from '@/store/useChatStore'

interface MessageRowProps {
  msg: Message
  subcalls?: Message[]
  /** assistant 消息是否为最终回复（由父组件预计算，避免 O(n²) 遍历） */
  isFinal?: boolean
  /** 当前是否正在流式接收（由父组件传入，避免每条消息独立订阅 store） */
  isStreaming: boolean
}

export const MessageRow = memo(function MessageRow({ msg, subcalls, isFinal, isStreaming }: MessageRowProps) {
  switch (msg.role) {
    case 'user':
      return (
        <div className="msg-row msg-row--user" data-msg-id={msg.id}>
          <UserBubble msg={msg} />
          {!isStreaming && (
            <button
              className="msg-rollback-btn"
              onClick={() => useChatStore.getState().rollbackToMessage(msg.id)}
              title="从这继续"
            >
              ↩
            </button>
          )}
        </div>
      )
    case 'assistant':
      return (
        <div className="msg-row msg-row--assistant" data-msg-id={msg.id}>
          {msg.confidence === 'low' && (
            <span className="confidence-badge" title="低置信度，请核实">⚠️</span>
          )}
          <AssistantBubble msg={msg} isFinal={isFinal ?? false} />
        </div>
      )
    case 'thinking':
      // thinking 消息始终独立渲染（Reasonix 风格：思考在前、工具在中、回复在后）
      return <ReasoningBlock msg={msg} />
    case 'tool_call':
    case 'tool_result':
      return (
        <ToolCard
          msg={msg}
          subcalls={subcalls}
        />
      )
    case 'error':
      return <ErrorCard msg={msg} />
    default:
      return null
  }
})
