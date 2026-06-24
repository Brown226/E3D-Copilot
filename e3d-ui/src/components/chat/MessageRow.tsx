/**
 * MessageRow 组件（分发器）
 * 根据 msg.role 分发到对应消息组件
 * 特殊处理：将 thinking 消息传递给 AssistantBubble 作为内联 reasoning
 */

import type { Message } from '@/types'
import { UserBubble } from './UserBubble'
import { AssistantBubble } from './AssistantBubble'
import { ToolCard } from './ToolCard'
import { ErrorCard } from './ErrorCard'

interface MessageRowProps {
  msg: Message
  subcalls?: Message[]
  allMessages?: Message[]
  /** 紧接其前的 thinking 消息（用于内联 reasoning 展示） */
  thinkingMsg?: Message
}

export function MessageRow({ msg, subcalls, allMessages, thinkingMsg }: MessageRowProps) {
  switch (msg.role) {
    case 'user':
      return <UserBubble msg={msg} />
    case 'assistant':
      return <AssistantBubble msg={msg} thinkingMsg={thinkingMsg} />
    case 'thinking':
      // thinking 消息已通过 thinkingMsg 传递给 AssistantBubble
      // 如果后面没有 assistant 消息（流式中断），则不渲染
      return null
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
