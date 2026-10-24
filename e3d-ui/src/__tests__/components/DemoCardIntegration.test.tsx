/**
 * 演示卡片集成测试
 *
 * 验证的不是「组件能渲染」，而是「真实 store 数据流能不能把卡片喂出来」：
 *   handleToolDispatch(name) → handleToolResult(meta) → ToolCard 渲染
 *
 * 这条链路里最容易断的是 name 的形态：
 * toolName 来自 ToolDispatch 的 payload.name（LLM 给的原始名），
 * MCP 工具则是 mcp__<server>__<tool>，DemoCard 的判定必须对它成立。
 */

import { describe, it, expect, beforeEach } from 'vitest'
import { render } from '@testing-library/react'
import { useChatStore } from '@/store/useChatStore'
import { ToolCard } from '@/components/chat/ToolCard'
import type { Message } from '@/types'

const TAB = 't1'

function reset() {
  useChatStore.setState({
    tabs: [
      {
        id: TAB,
        title: '测试',
        messages: [],
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

function runTool(name: string, args: string, result: string, meta?: unknown): Message {
  const store = useChatStore.getState()
  store.appendMessage(
    { role: 'tool_call', content: `正在调用 ${name}...`, toolId: 'id1', toolName: name, toolArgs: args },
    TAB,
  )
  store.handleToolResult('id1', result, undefined, TAB, 12, meta)
  const msg = useChatStore.getState().tabs.find((t) => t.id === TAB)!.messages[0]
  return msg
}

/**
 * 运行态快照：卡片默认展开的唯一条件是「工具还在运行」。
 * 任务完成后卡片会收起（与思考块行为一致），所以断言可见性
 * 必须用未 finalize 的消息来测。
 */
function runToolRunning(name: string, args: string): Message {
  const store = useChatStore.getState()
  store.appendMessage(
    { role: 'tool_call', content: `正在调用 ${name}...`, toolId: 'id1', toolName: name, toolArgs: args },
    TAB,
  )
  // 不调用 handleToolResult → finalized 保持 undefined → isRunning = true
  return useChatStore.getState().tabs.find((t) => t.id === TAB)!.messages[0]
}

/** 把运行态消息补上结果，但仍标记为运行中（用于验证卡片内容渲染） */
function withMeta(msg: Message, meta: unknown): Message {
  return { ...msg, toolMeta: meta as never, finalized: false }
}

describe('演示卡片集成', () => {
  beforeEach(reset)

  it('design_workflow 结果经 store 后渲染流程卡片', () => {
    const meta = {
      platformSynced: true,
      steps: [
        { step: 1, name: '解析 A 版图纸', success: true, summary: '线段 268 条' },
        { step: 5, name: 'A/B 版本差异比对', success: true, summary: '新增 5' },
      ],
    }
    const msg = runTool('design_workflow', '{"drawing_no":"JG-2026-001"}', '编排完成', meta)

    expect(msg.role).toBe('tool_result')
    expect(msg.toolMeta).toEqual(meta)

    // 运行中：卡片展开可见
    const running = withMeta(msg, meta)
    const { getByText, unmount } = render(<ToolCard msg={running} />)
    expect(getByText('🏗 设计全流程')).toBeTruthy()
    unmount()

    // 已完成：收起（与思考块一致），卡片内容不再直接可见
    const { queryByText } = render(<ToolCard msg={msg} />)
    expect(queryByText('🏗 设计全流程')).toBeNull()
  })

  it('dwg_diff 结果经 store 后渲染差异卡片', () => {
    const meta = {
      fileA: 'a.dwg',
      fileB: 'b.dwg',
      tolerance: 5,
      unchangedCount: 794,
      addedCount: 5,
      removedCount: 0,
      modifiedCount: 0,
      added: [{ Layer: '土建', StartX: 0, StartY: 0, EndX: 1000, EndY: 0, Length: 1000 }],
      removed: [],
      modified: [],
    }
    const msg = runTool('dwg_diff', '{"file_a":"a.dwg","file_b":"b.dwg"}', '差异报告', meta)

    const { getByText } = render(<ToolCard msg={withMeta(msg, meta)} />)
    expect(getByText('🔀 A/B 版本差异')).toBeTruthy()
  })

  it('MCP query_history 结果渲染平台记录卡片', () => {
    const name = 'mcp__e3d-data-platform__query_history'
    const payload = JSON.stringify({
      ok: true,
      result: {
        drawings: { total: 1, rows: [{ drawingNo: 'JG-2026-001', version: 'A', layerCount: 30, entityCount: 699 }] },
      },
    })
    const msg = runTool(name, '{"kind":"all"}', payload)

    const { getByText } = render(<ToolCard msg={withMeta(msg, undefined)} />)
    expect(getByText('平台记录')).toBeTruthy()
    expect(getByText('图纸台账')).toBeTruthy()
  })

  it('MCP record_* 结果渲染写入徽标', () => {
    const name = 'mcp__e3d-data-platform__record_drawing'
    const msg = runTool(name, '{"drawingNo":"JG-2026-001"}', JSON.stringify({ ok: true, id: 'DWG-0001' }))

    const { getByText } = render(<ToolCard msg={withMeta(msg, undefined)} />)
    expect(getByText('图纸台账已登记')).toBeTruthy()
    expect(getByText('✓ 平台已记录')).toBeTruthy()
  })

  it('平台写入失败时徽标为失败态', () => {
    const msg = runTool(
      'mcp__e3d-data-platform__record_model',
      '{}',
      JSON.stringify({ ok: false, error: '磁盘写入失败' }),
    )
    // 平台返回 ok:false 属于需要用户看见的失败：保持运行态展开断言内容，
    // 另用 toolError 验证「出错必然展开」这条策略。
    const { getByText } = render(
      <ToolCard msg={{ ...withMeta(msg, undefined), toolError: '磁盘写入失败' }} />,
    )
    expect(getByText('✗ 写入失败')).toBeTruthy()
  })

  it('无 meta 的普通工具不渲染演示卡片', () => {
    const msg = runTool('query', '{"type":"SITE"}', '查询结果')
    const { container } = render(<ToolCard msg={msg} />)
    expect(container.querySelector('.wf-card')).toBeNull()
    expect(container.querySelector('.diff-card')).toBeNull()
    expect(container.querySelector('.plat-card')).toBeNull()
  })
})
