/**
 * 思考过程折叠回归测试
 *
 * 历史缺陷：ReasoningBlock 用 useState(!msg.finalized) 作初值，
 * 而 useState 初值只在挂载时取一次 —— 任务完成（finalized 变 true）后
 * 思考块仍然保持展开，消息流里残留一大段英文推理文本。
 */
import { describe, it, expect } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { ReasoningBlock } from '@/components/chat/AssistantBubble'
import type { Message } from '@/types'

const makeMsg = (over: Partial<Message> = {}): Message => ({
  id: 'm1',
  role: 'thinking',
  content: 'let me think about this',
  timestamp: Date.now(),
  finalized: false,
  ...over,
})

describe('ReasoningBlock 折叠行为', () => {
  it('流式进行中默认展开', () => {
    render(<ReasoningBlock msg={makeMsg({ finalized: false })} />)
    expect(screen.getByText('let me think about this')).toBeTruthy()
    expect(screen.getByText('正在思考…')).toBeTruthy()
  })

  it('已完成后默认折叠（挂载时就是完成态）', () => {
    render(<ReasoningBlock msg={makeMsg({ finalized: true })} />)
    expect(screen.queryByText('let me think about this')).toBeNull()
    expect(screen.getByText('思考过程')).toBeTruthy()
  })

  it('关键回归：流式中挂载，随后 finalized 变 true 必须自动收起', () => {
    const { rerender } = render(<ReasoningBlock msg={makeMsg({ finalized: false })} />)
    expect(screen.getByText('let me think about this')).toBeTruthy()

    rerender(<ReasoningBlock msg={makeMsg({ finalized: true })} />)

    // 修复前这里仍然展开 —— 缺陷的具体体现
    expect(screen.queryByText('let me think about this')).toBeNull()
    expect(screen.getByText('已完成')).toBeTruthy()
  })

  it('用户手动展开后，即使已完成也保持展开', () => {
    const { rerender } = render(<ReasoningBlock msg={makeMsg({ finalized: true })} />)
    expect(screen.queryByText('let me think about this')).toBeNull()

    fireEvent.click(screen.getByText('思考过程').closest('button')!)
    expect(screen.getByText('let me think about this')).toBeTruthy()

    rerender(<ReasoningBlock msg={makeMsg({ finalized: true })} />)
    expect(screen.getByText('let me think about this')).toBeTruthy()
  })

  it('用户手动收起后，流式中也保持收起', () => {
    render(<ReasoningBlock msg={makeMsg({ finalized: false })} />)
    fireEvent.click(screen.getByText('正在思考…').closest('button')!)
    expect(screen.queryByText('let me think about this')).toBeNull()
  })
})
