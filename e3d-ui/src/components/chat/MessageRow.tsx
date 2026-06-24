/**
 * MessageRow 组件（分发器）
 * 根据 msg.role 分发到对应的消息气泡组件
 * 增加：传递子调用关系给 ToolCard
 */

import type { Message } from '@/types'
import { UserBubble } from './UserBubble'
import { AssistantBubble } from './AssistantBubble'
import { ThinkingBlock } from './ThinkingBlock'
import { ToolCard } from './ToolCard'
import { ErrorCard } from './ErrorCard'

interface MessageRowProps {
  msg: Message
  subcalls?: Message[]
  allMessages?: Message[]
}

export function MessageRow({ msg, subcalls, allMessages }: MessageRowProps) {
  switch (msg.role) {
    case 'user':
      return <UserBubble msg={msg} />
    case 'assistant':
      return <AssistantBubble msg={msg} />
    case 'thinking':
      return <ThinkingBlock msg={msg} />
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
