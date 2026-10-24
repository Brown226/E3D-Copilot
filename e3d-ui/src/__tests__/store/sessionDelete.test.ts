/**
 * 会话删除 / 持久化回归测试
 *
 * 背景（大王反馈「对话历史无法删除」）：
 *  1) saveSessionsToStorage 把后端 GUID 条目也写进 localStorage，
 *     导致同一段对话在列表里出现两份，删掉一份另一份还在。
 *  2) deleteSession 删除当前会话后不换 id，下一轮 saveSession 又把条目写回去。
 */
import { describe, it, expect, beforeEach } from 'vitest'
import { useChatStore } from '../../store/useChatStore'
import type { SessionMeta } from '../../store/types'

const STORAGE_KEY = 'e3d-chat-sessions'

function meta(id: string, title = 't', count = 1): SessionMeta {
  return {
    id,
    title,
    preview: '',
    messageCount: count,
    createdAt: 1,
    lastActivityAt: 1,
  }
}

describe('会话持久化与删除', () => {
  beforeEach(() => {
    localStorage.clear()
    useChatStore.setState({
      sessions: [],
      sessionId: 'session_local_1',
      tabs: [{
        id: 'test_tab',
        title: '新对话',
        messages: [],
        isStreaming: false,
        currentAssistantMsgId: null,
        currentThinkingMsgId: null,
        pendingApproval: null,
        pendingQuestion: null,
      }],
      activeTabId: 'test_tab',
    })
  })

  it('只把本地会话写进 localStorage，不污染后端 GUID 条目', () => {
    // 混合两类：本地 session_ 前缀 + 后端 GUID
    useChatStore.getState().setSessions([
      meta('session_local_1'),
      meta('3f2a9c8e7b1d4a5f9c0e8d7a6b5c4d3e'),
    ])

    const raw = localStorage.getItem(STORAGE_KEY)
    expect(raw).toBeTruthy()
    const stored = JSON.parse(raw!) as SessionMeta[]

    // 关键断言：后端条目不能被持久化，否则删除后又会从本地缓存刷回来
    expect(stored.map((s) => s.id)).toEqual(['session_local_1'])
  })

  it('删除当前会话后 sessionId 必须更换，避免 saveSession 复活已删条目', () => {
    const target = 'session_local_1'
    useChatStore.setState({
      sessions: [meta(target), meta('session_local_2')],
      sessionId: target,
    })

    useChatStore.getState().deleteSession(target)

    const after = useChatStore.getState()
    expect(after.sessions.map((s) => s.id)).toEqual(['session_local_2'])
    expect(after.sessionId).not.toBe(target)
  })

  it('删除非当前会话时 sessionId 保持不变', () => {
    useChatStore.setState({
      sessions: [meta('session_local_1'), meta('session_local_2')],
      sessionId: 'session_local_1',
    })

    useChatStore.getState().deleteSession('session_local_2')

    expect(useChatStore.getState().sessionId).toBe('session_local_1')
    expect(useChatStore.getState().sessions.map((s) => s.id)).toEqual(['session_local_1'])
  })

  it('多个 Tab 各自持有独立 sessionId，保存时互不覆盖', () => {
    const tabA = useChatStore.getState().createTab('会话A')
    const tabB = useChatStore.getState().createTab('会话B')

    const tabs = useChatStore.getState().tabs
    const a = tabs.find((t) => t.id === tabA)!
    const b = tabs.find((t) => t.id === tabB)!
    expect(a.sessionId).toBeTruthy()
    expect(b.sessionId).toBeTruthy()
    expect(a.sessionId).not.toBe(b.sessionId)

    // 两个 Tab 各有一轮自己的对话，分别保存后应产生两条独立会话
    useChatStore.setState({
      tabs: tabs.map((t) => {
        if (t.id === tabA) return { ...t, messages: [{ id: 'm1', role: 'user', content: 'A 的问题' } as never] }
        if (t.id === tabB) return { ...t, messages: [{ id: 'm2', role: 'user', content: 'B 的问题' } as never] }
        return t
      }),
      activeTabId: tabA,
    })
    useChatStore.getState().saveSession()

    useChatStore.setState({ activeTabId: tabB })
    useChatStore.getState().saveSession()

    const stored = JSON.parse(localStorage.getItem(STORAGE_KEY)!) as SessionMeta[]
    const titles = stored.map((s) => s.title)
    // 修复前两个 Tab 写同一个 id，这里只会剩一条
    expect(titles).toContain('A 的问题')
    expect(titles).toContain('B 的问题')
  })
})
