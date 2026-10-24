import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TracePanel } from '@/components/chat/TracePanel'

/**
 * 执行诊断面板。
 *
 * 重点锁住两件容易回归的事：
 * 1. 后端 `trace:latest` 返回的是 content 字符串（不是对象），
 *    且文件可能带 UTF-8 BOM —— 解析失败会让人误以为"没有记录"。
 * 2. 耗时归因要能体现"LLM 占大头"还是"工具占大头"，
 *    这是判断该优化端点还是优化工具的唯一依据。
 */

const mockSendAndWait = vi.fn()

vi.mock('@/services/bridgeService', () => ({
  default: {
    sendAndWait: (...args: unknown[]) => mockSendAndWait(...args),
  },
  MessageTypes: {
    TraceLatest: 'trace:latest',
  },
}))

/** 构造一份贴近真实落盘格式的 trace（注意：带 BOM） */
function makeTraceContent(overrides: Record<string, unknown> = {}): string {
  const doc = {
    sessionId: 'test-session',
    startTime: '2026-10-24 17:20:18.593',
    endTime: '2026-10-24 17:22:27.815',
    userInput: '执行设计全流程管理',
    model: 'qoder-local/group/never-stuck',
    outcome: 'success',
    totalSteps: 2,
    steps: [
      {
        step: 1,
        durationMs: 123792,
        llmMs: 118000,
        ttftMs: 4900,
        approvalMs: 0,
        toolCalls: [],
        stream: { chunks: 407, spanMs: 16, medianGapMs: 0, maxGapMs: 3, stalls: 0, charsPerSec: 104125 },
        context: { messages: 3, tools: 31, toolSchemaChars: 30983 },
      },
      {
        step: 2,
        durationMs: 5428,
        llmMs: 5400,
        ttftMs: 1200,
        approvalMs: 0,
        toolCalls: [
          { name: 'glob', durationMs: 63112, success: false },
          { name: 'cad_import', durationMs: 900, success: true },
        ],
      },
    ],
    ...overrides,
  }
  return '\uFEFF' + JSON.stringify(doc)
}

describe('TracePanel 执行诊断面板', () => {
  beforeEach(() => {
    mockSendAndWait.mockReset()
  })

  it('打开后自动拉取最近一轮记录', async () => {
    mockSendAndWait.mockResolvedValue({
      enabled: true,
      path: 'C:\\Users\\x\\AppData\\Local\\E3DCopilot\\traces\\trace-1.json',
      content: makeTraceContent(),
    })

    render(<TracePanel />)
    await userEvent.click(screen.getByTitle(/查看上一轮执行的耗时归因/))

    await waitFor(() => {
      expect(mockSendAndWait).toHaveBeenCalledWith('trace:latest', {}, 15000)
    })
    expect(await screen.findByText(/执行诊断/)).toBeTruthy()
    expect(await screen.findByText(/group\/never-stuck/)).toBeTruthy()
  })

  it('能解析带 BOM 的 content 字符串', async () => {
    mockSendAndWait.mockResolvedValue({
      enabled: true,
      content: makeTraceContent(),
    })

    render(<TracePanel />)
    await userEvent.click(screen.getByTitle(/查看上一轮执行的耗时归因/))

    // 若 BOM 未剥离，JSON.parse 会抛错，这里会看到错误提示而不是数据
    await waitFor(() => {
      expect(screen.getByText(/步骤 1/)).toBeTruthy()
    })
    expect(screen.queryByText(/格式无法解析/)).toBeNull()
  })

  it('展示耗时归因与分步明细', async () => {
    mockSendAndWait.mockResolvedValue({
      enabled: true,
      content: makeTraceContent(),
    })

    render(<TracePanel />)
    await userEvent.click(screen.getByTitle(/查看上一轮执行的耗时归因/))

    await waitFor(() => expect(screen.getByText(/步骤 1/)).toBeTruthy())
    // 两步都在
    expect(screen.getByText(/步骤 2/)).toBeTruthy()
    // 归因格里 LLM 耗时 = 118000 + 5400 = 123.4s
    expect(screen.getByText('123.4s')).toBeTruthy()
    // 总耗时 = 123792 + 5428 = 129.2s
    expect(screen.getByText('129.2s')).toBeTruthy()
  })

  it('旧版 trace 缺 llmMs 时不显示 0ms，而是推算并给出说明', async () => {
    // 老记录只有 durationMs 和 toolCalls，没有 llmMs —— 这正是截图里
    // 「总耗时 125.9s 但 LLM 0ms (0%)」自相矛盾的来源
    const legacy = {
      sessionId: 'legacy',
      startTime: '2026-10-24 17:23:51.869',
      model: 'qoder-local/group/never-stuck',
      outcome: 'cancelled',
      totalSteps: 2,
      steps: [
        {
          step: 1,
          durationMs: 7600,
          toolCalls: [{ name: 'glob', durationMs: 6000, success: false }],
        },
        { step: 2, durationMs: 118300, toolCalls: [] },
      ],
    }
    mockSendAndWait.mockResolvedValue({ enabled: true, content: JSON.stringify(legacy) })

    render(<TracePanel />)
    await userEvent.click(screen.getByTitle(/查看上一轮执行的耗时归因/))

    await waitFor(() => expect(screen.getByText(/步骤 1/)).toBeTruthy())
    // 必须提示是推算值
    expect(screen.getByText(/旧版本生成，缺少分段耗时字段/)).toBeTruthy()
    // LLM 推算 = (7600-6000) + 118300 = 119900 => 119.9s，不能是 0
    expect(screen.getByText('119.9s')).toBeTruthy()
  })

  it('失败的工具有明确标记', async () => {
    mockSendAndWait.mockResolvedValue({
      enabled: true,
      content: makeTraceContent(),
    })

    render(<TracePanel />)
    await userEvent.click(screen.getByTitle(/查看上一轮执行的耗时归因/))

    await waitFor(() => expect(screen.getByText('glob')).toBeTruthy())
    expect(screen.getByText('失败')).toBeTruthy()
  })

  it('没有记录时给出可操作提示，而不是空白', async () => {
    mockSendAndWait.mockResolvedValue({
      enabled: true,
      trace: null,
      message: '暂无 trace 记录，请先进行一次对话',
    })

    render(<TracePanel />)
    await userEvent.click(screen.getByTitle(/查看上一轮执行的耗时归因/))

    expect(await screen.findByText(/暂无 trace 记录/)).toBeTruthy()
  })

  it('trace 未启用时说明如何开启', async () => {
    mockSendAndWait.mockResolvedValue({
      enabled: false,
      message: 'Trace 功能未启用，请在配置中设置 logging.traceEnabled = true',
    })

    render(<TracePanel />)
    await userEvent.click(screen.getByTitle(/查看上一轮执行的耗时归因/))

    expect(await screen.findByText(/traceEnabled/)).toBeTruthy()
  })

  it('流式卡顿数超阈值时高亮', async () => {
    const content = makeTraceContent()
    const doc = JSON.parse(content.replace(/^\uFEFF/, ''))
    doc.steps[0].stream = { chunks: 400, spanMs: 9000, medianGapMs: 20, maxGapMs: 2500, stalls: 7, charsPerSec: 45 }
    mockSendAndWait.mockResolvedValue({ enabled: true, content: '\uFEFF' + JSON.stringify(doc) })

    render(<TracePanel />)
    await userEvent.click(screen.getByTitle(/查看上一轮执行的耗时归因/))

    await waitFor(() => expect(screen.getByText(/卡顿 7/)).toBeTruthy())
    // 最长间隔 2.5s 应带告警样式
    const gapEl = screen.getByText(/最长间隔 2\.5s/)
    expect(gapEl.className).toContain('trace-warn')
  })
})
