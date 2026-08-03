/**
 * SubagentPanel — 子代理折叠面板
 * 按 agentName 分组渲染子代理的工具调用和结果
 */
import { useState, useMemo } from 'react'
import { ChevronRight, Loader2, Bot } from 'lucide-react'
import type { Message } from '@/types'
import { MessageRow } from './MessageRow'
import { ToolGroup, groupConsecutiveTools } from './ToolGroup'
import { computeFinalAssistantIds } from './MessageList'

interface SubagentPanelProps {
  agentName: string
  messages: Message[]
  subcalls: Map<string, Message[]>
}

export function SubagentPanel({ agentName, messages, subcalls }: SubagentPanelProps) {
  const [open, setOpen] = useState(true)
  const isRunning = messages.some((m) => !m.finalized)
  const doneCount = messages.filter((m) => m.finalized && !m.toolError).length
  const errorCount = messages.filter((m) => m.toolError).length
  const grouped = groupConsecutiveTools(messages)
  const finalIds = useMemo(() => computeFinalAssistantIds(messages), [messages])

  return (
    <div className="subagent-panel" data-running={isRunning ? '' : undefined}>
      <button
        type="button"
        className="tool__head"
        onClick={() => setOpen(!open)}
        aria-expanded={open}
      >
        <span className="tool__label-group">
          {isRunning ? (
            <Loader2 className="w-3.5 h-3.5 animate-spin" style={{ color: 'var(--accent)' }} />
          ) : (
            <Bot className="w-3.5 h-3.5" style={{ color: 'var(--muted)' }} />
          )}
          <span style={{ fontWeight: 600, color: 'var(--accent)', fontSize: 12 }}>
            🤖 {agentName}
          </span>
        </span>
        <span className="tool__summary">
          {isRunning ? '运行中...' : `${doneCount} 完成${errorCount > 0 ? ` · ${errorCount} 失败` : ''}`}
        </span>
        <span className={`tool__chevron${open ? ' tool__chevron--open' : ''}`}>
          <ChevronRight size={12} />
        </span>
      </button>

      {open && (
        <div style={{ padding: '4px 8px 8px 0' }}>
          {grouped.map((item, i) => {
            if (item.kind === 'group') {
              return (
                <ToolGroup
                  key={`sa-g-${i}`}
                  kind={item.groupKind}
                  messages={item.messages}
                  subcalls={subcalls}
                />
              )
            }
            const msg = item.msg
            const toolId = msg.toolId || msg.id
            const isFinal = msg.role === 'assistant' ? finalIds.has(msg.id) : undefined
            return (
              <MessageRow
                key={`sa-m-${i}`}
                msg={msg}
                subcalls={subcalls.get(toolId)}
                isFinal={isFinal}
                isStreaming={isRunning}
              />
            )
          })}
          {isRunning && (
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, padding: '8px 0' }}>
              <Loader2 className="w-3 h-3 animate-spin" style={{ color: 'var(--accent)' }} />{' '}
              <span style={{ fontSize: 12, color: 'var(--muted)' }}>{agentName} 正在工作...</span>
            </div>
          )}
        </div>
      )}
    </div>
  )
}