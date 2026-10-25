/**
 * Notice 分级 — 回归测试（驱动真实的 registerStoreMappings）
 *
 * 历史缺陷：Bridge.cs 只下发 `{ text }`，前端一律 `addToast('info', text)`，
 * 于是「已注册工具 query」这类内部机制通知、上下文压缩、记忆注入等
 * 每次任务都会弹十几条右上角横幅，把界面糊住。
 *
 * 修复：
 *   后端按三档下发 level（silent / info / warn）
 *   前端 silent 直接丢弃，warn 用 warning 样式，其余 info
 */

import { describe, it, expect, beforeEach, vi } from 'vitest'
import { registerStoreMappings } from '@/services/bridge/storeMapping'
import { MessageTypes } from '@/services/messageContracts'
import { useToastStore } from '@/store/useToastStore'

type Handler = (msg: { type: string; payload?: unknown }) => void

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

/** 等待 storeMapping 内部的动态 import 完成 */
async function flush() {
  await new Promise((r) => setTimeout(r, 0))
  await new Promise((r) => setTimeout(r, 0))
}

describe('Notice 分级：silent 不弹、warn 醒目', () => {
  beforeEach(() => {
    useToastStore.getState().clearAll()
    vi.restoreAllMocks()
  })

  it('silent 级别不产生任何 Toast', async () => {
    const bridge = setup()
    bridge.emit(MessageTypes.Notice, { text: '已注册工具 query', level: 'silent' })
    await flush()

    expect(useToastStore.getState().toasts).toHaveLength(0)
  })

  it('warn 级别弹出 warning 样式', async () => {
    const bridge = setup()
    bridge.emit(MessageTypes.Notice, { text: '审批超时', level: 'warn' })
    await flush()

    const toasts = useToastStore.getState().toasts
    expect(toasts).toHaveLength(1)
    expect(toasts[0].type).toBe('warning')
    expect(toasts[0].message).toBe('审批超时')
  })

  it('info 级别弹出普通提示', async () => {
    const bridge = setup()
    bridge.emit(MessageTypes.Notice, { text: '工具审批模式: auto', level: 'info' })
    await flush()

    const toasts = useToastStore.getState().toasts
    expect(toasts).toHaveLength(1)
    expect(toasts[0].type).toBe('info')
  })

  it('缺少 level 时按 info 处理（向后兼容旧后端）', async () => {
    const bridge = setup()
    bridge.emit(MessageTypes.Notice, { text: '旧格式消息' })
    await flush()

    const toasts = useToastStore.getState().toasts
    expect(toasts).toHaveLength(1)
    expect(toasts[0].type).toBe('info')
  })

  it('连续多条 silent 一条都不弹', async () => {
    const bridge = setup()
    for (let i = 0; i < 10; i++) {
      bridge.emit(MessageTypes.Notice, { text: `内部机制 ${i}`, level: 'silent' })
    }
    await flush()

    expect(useToastStore.getState().toasts).toHaveLength(0)
  })
})
