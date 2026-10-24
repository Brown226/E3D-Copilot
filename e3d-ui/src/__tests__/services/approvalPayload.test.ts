/**
 * 审批请求载荷解析 — 回归测试（驱动真实的 registerStoreMappings）
 *
 * 历史缺陷：后端 Bridge.cs 用 `evt.Data?.ToString()` 传 args，
 * 而 evt.Data 是 C# 匿名对象，ToString() 产出
 *   "{ ToolName = design_workflow, Args = {...} }"
 * 这种非 JSON 文本。前端 JSON.parse 抛异常后，整个 setPendingApproval
 * 不执行，审批卡片永远不显示，后台一直等到 2 分钟超时。
 *
 * 修复分两层：
 *   后端 Bridge.cs 改为取匿名对象的 Args 属性（真正的 JSON 字符串）
 *   前端 storeMapping.ts 加 try/catch 兜底，绝不因解析失败中断整条链
 *
 * 这里锁住前端这一层：即使是旧的畸形载荷，审批也必须能弹出来。
 */

import { describe, it, expect, beforeEach } from 'vitest'
import { registerStoreMappings } from '@/services/bridge/storeMapping'
import { MessageTypes } from '@/services/messageContracts'
import { useChatStore } from '@/store/useChatStore'

type Handler = (msg: { type: string; payload?: unknown }) => void

/** 最小 Bridge 替身：只保留 on() 收集回调 */
function createFakeBridge() {
  let handler: Handler | null = null
  return {
    on(h: Handler) {
      handler = h
    },
    emit(type: string, payload?: unknown) {
      handler?.({ type, payload })
    },
  }
}

function setup() {
  const bridge = createFakeBridge()
  registerStoreMappings(bridge as never)
  return bridge
}

describe('审批载荷解析（真实 storeMapping）', () => {
  beforeEach(() => {
    useChatStore.setState({
      tabs: [
        {
          id: 'tab-1',
          title: 'tab',
          messages: [],
          isStreaming: false,
          currentAssistantMsgId: null,
          currentThinkingMsgId: null,
          pendingApproval: null,
          pendingQuestion: null,
        },
      ],
      activeTabId: 'tab-1',
      streamingTabId: null,
    })
  })

  it('合法 JSON 的 args 能弹出审批卡片', () => {
    const bridge = setup()
    bridge.emit(MessageTypes.ToolApproval, {
      id: 'call-1',
      name: 'design_workflow',
      args: '{"drawing_no":"JG-2026-001"}',
      description: 'design_workflow',
    })

    const pending = useChatStore.getState().tabs[0].pendingApproval
    expect(pending).toBeTruthy()
    expect(pending?.toolName).toBe('design_workflow')
    expect(pending?.args).toEqual({ drawing_no: 'JG-2026-001' })
  })

  it('C# 匿名对象 ToString 的畸形 args 也不能吞掉审批', () => {
    const bridge = setup()
    const legacy = '{ ToolName = design_workflow, Args = {"a":1} }'
    bridge.emit(MessageTypes.ToolApproval, {
      id: 'call-2',
      name: 'design_workflow',
      args: legacy,
      description: 'design_workflow',
    })

    const pending = useChatStore.getState().tabs[0].pendingApproval
    // 关键回归点：以前这里会抛异常，pendingApproval 仍是 null
    expect(pending).toBeTruthy()
    expect(pending?.toolName).toBe('design_workflow')
    expect(pending?.args).toBe(legacy)
  })

  it('残缺 JSON 同样不吞掉审批', () => {
    const bridge = setup()
    bridge.emit(MessageTypes.ToolApproval, {
      id: 'call-3',
      name: 'execute_pml',
      args: '{"a":',
      description: 'execute_pml',
    })

    expect(useChatStore.getState().tabs[0].pendingApproval).toBeTruthy()
  })

  it('没有 args 时正常弹出', () => {
    const bridge = setup()
    bridge.emit(MessageTypes.ToolApproval, {
      id: 'call-4',
      name: 'query',
      description: 'query',
    })

    const pending = useChatStore.getState().tabs[0].pendingApproval
    expect(pending?.toolName).toBe('query')
    expect(pending?.args).toBeUndefined()
  })
})
