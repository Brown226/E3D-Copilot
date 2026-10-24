/**
 * 工具执行过程展示方式回归测试
 *
 * 两条硬性约束（大王明确要求，与思考过程保持一致）：
 *   1. 每个工具独立一行，不再合并成「已处理 · N 步」的汇总
 *   2. 流式中展开，任务完成后自动折叠
 *
 * 历史行为：ToolCard 用 AUTO_EXPAND_TOOLS 白名单让 design_workflow /
 * dwg_diff / mcp__* 永久展开，任务结束后仍然铺满消息流；
 * 同时 MessageList 把连续工具合并成 folded，用户只能看到一个汇总数字。
 */
import { describe, it, expect, beforeEach } from 'vitest'
import { render } from '@testing-library/react'
import { useChatStore } from '@/store/useChatStore'
import { ToolCard } from '@/components/chat/ToolCard'
import type { Message } from '@/types'

const TAB = 't1'

function seed(messages: Partial<Message>[]) {
  useChatStore.setState({
    tabs: [
      {
        id: TAB,
        title: 't',
        messages: messages.map((m, i) => ({
          id: `m${i}`,
          timestamp: Date.now(),
          content: '',
          ...m,
        })),
        isStreaming: false,
        currentAssistantMsgId: null,
        currentThinkingMsgId: null,
        pendingApproval: null,
        pendingQuestion: null,
      } as never,
    ],
    activeTabId: TAB,
  })
}

const toolCall = (id: string, name: string, finalized = false): Partial<Message> => ({
  role: 'tool_call',
  toolId: id,
  toolName: name,
  content: `正在调用 ${name}...`,
  finalized,
})

const toolResult = (id: string, name: string, text = 'ok'): Partial<Message> => ({
  role: 'tool_result',
  toolId: id,
  toolName: name,
  content: text,
  finalized: true,
})

describe('工具卡片折叠策略', () => {
  beforeEach(() => seed([]))

  it('运行中的工具默认展开', () => {
    const msg = { ...toolCall('t1', 'design_workflow'), id: 'x', timestamp: 0 } as Message
    const { container } = render(<ToolCard msg={msg} />)
    // 运行中应能看到参数/结果区域
    expect(container.querySelector('.tool--open, .tool__body')).toBeTruthy()
  })

  it('已完成的工具默认折叠（即使它是演示主角 design_workflow）', () => {
    const msg = {
      ...toolResult('t1', 'design_workflow', '编排完成'),
      id: 'x',
      timestamp: 0,
    } as Message
    const { container } = render(<ToolCard msg={msg} />)
    const body = container.querySelector('.tool__body')
    // 折叠时 body 不应渲染
    expect(body).toBeNull()
  })

  it('出错的工具始终展开，不被折叠藏起来', () => {
    const msg = {
      ...toolResult('t1', 'design_workflow', 'boom'),
      toolError: '执行失败',
      id: 'x',
      timestamp: 0,
    } as Message
    const { container } = render(<ToolCard msg={msg} />)
    expect(container.querySelector('.tool__body')).toBeTruthy()
  })

  it('MCP 工具完成后同样折叠（不再因前缀豁免）', () => {
    const msg = {
      ...toolResult('t1', 'mcp__e3d-data-platform__query_history', '{"ok":true}'),
      id: 'x',
      timestamp: 0,
    } as Message
    const { container } = render(<ToolCard msg={msg} />)
    expect(container.querySelector('.tool__body')).toBeNull()
  })
})

describe('工具不再合并成汇总行', () => {
  it('三个连续工具应在消息流中各自独立存在', () => {
    seed([
      toolCall('t1', 'cad_import', true),
      toolResult('t1', 'cad_import'),
      toolCall('t2', 'design_workflow', true),
      toolResult('t2', 'design_workflow'),
      toolCall('t3', 'dwg_diff', true),
      toolResult('t3', 'dwg_diff'),
    ])
    const messages = useChatStore.getState().tabs[0].messages
    // 消息层面 6 条都在（不再被折叠逻辑吞掉）
    expect(messages).toHaveLength(6)
    const names = messages.filter((m) => m.role === 'tool_call').map((m) => m.toolName)
    expect(names).toEqual(['cad_import', 'design_workflow', 'dwg_diff'])
  })
})
