/**
 * ToolGroup — 同款 Reasonix
 * 将连续的同类工具（探索/修改）合并为一个可折叠组
 */

import { memo, useState } from 'react'
import { ChevronRight, Search, Edit3, Zap, Terminal } from 'lucide-react'
import type { Message } from '@/types'
import { ToolCard } from './ToolCard'

export type ToolGroupKind = 'explore' | 'modify' | 'delegate' | 'shell'

const EXPLORE_TOOLS = new Set(['read_file', 'ls', 'grep', 'glob', 'web_fetch', 'code_index', 'read_skill', 'mcp__*', 'geometry', 'report', 'compare', 'hierarchy'])
const MODIFY_TOOLS = new Set(['write_file', 'edit_file', 'multi_edit', 'move_file', 'delete_range', 'delete_symbol', 'notebook_edit', 'design', 'piping', 'batch', 'undo_redo'])
const DELEGATE_TOOLS = new Set(['task', 'run_skill', 'explore', 'research', 'review', 'security_review'])

export function toolGroupKind(msg: Message): ToolGroupKind | null {
  const name = msg.toolName || ''
  if (!name) return null
  if (name === 'todo_write' || name === 'exit_plan_mode') return null
  if (EXPLORE_TOOLS.has(name) || name.startsWith('mcp__')) return 'explore'
  if (MODIFY_TOOLS.has(name)) return 'modify'
  if (DELEGATE_TOOLS.has(name)) return 'delegate'
  return null
}

function kindIcon(kind: ToolGroupKind) {
  switch (kind) {
    case 'explore': return <Search className="w-3.5 h-3.5" />
    case 'modify': return <Edit3 className="w-3.5 h-3.5" />
    case 'delegate': return <Zap className="w-3.5 h-3.5" />
    case 'shell': return <Terminal className="w-3.5 h-3.5" />
  }
}

function kindLabel(kind: ToolGroupKind, count: number) {
  switch (kind) {
    case 'explore': return `已读 ${count} 个文件`
    case 'modify': return `已修改 ${count} 个文件`
    case 'delegate': return `${count} 个子任务`
    case 'shell': return `${count} 个命令`
  }
}

interface ToolGroupProps {
  kind: ToolGroupKind
  messages: Message[]
  subcalls?: Map<string, Message[]>
  allMessages?: Message[]
}

export const ToolGroup = memo(function ToolGroup({ kind, messages, subcalls, allMessages }: ToolGroupProps) {
  const [expanded, setExpanded] = useState(messages.length <= 3) // 3 个以内默认展开

  return (
    <div className={`tool-group tool-group--${kind}${expanded ? ' tool-group--open' : ''}`}>
      {/* 组标题（可点击展开/折叠） — Reasonix 对标 */}
      <button
        type="button"
        onClick={() => setExpanded((v) => !v)}
        className="tool-group__head"
        aria-expanded={expanded}
      >
        <span className="tool-group__title">{kindLabel(kind, messages.length)}</span>
        <span className="tool-group__summary">
          {messages.filter(m => m.role === 'tool_call').map(m => m.toolName).join(', ')}
        </span>
        <ChevronRight
          size={12}
          className={`tool-group__chevron${expanded ? ' tool-group__chevron--open' : ''}`}
        />
      </button>

      {expanded && (
        <div className="tool-group__body">
          {messages.map((msg) => (
            <ToolCard
              key={msg.id}
              msg={msg}
              subcalls={subcalls?.get(msg.toolId || msg.id)}
              allMessages={allMessages}
            />
          ))}
        </div>
      )}
    </div>
  )
})

/**
 * 将顶层消息列表中的连续同类工具分组
 */
export function groupConsecutiveTools(
  messages: Message[],
): Array<{ kind: 'message'; msg: Message } | { kind: 'group'; groupKind: ToolGroupKind; messages: Message[] }> {
  const result: Array<{ kind: 'message'; msg: Message } | { kind: 'group'; groupKind: ToolGroupKind; messages: Message[] }> = []
  let i = 0

  while (i < messages.length) {
    const msg = messages[i]
    const gk = toolGroupKind(msg)

    if (gk && msg.role === 'tool_call') {
      // 收集连续的同类工具
      const group: Message[] = [msg]
      let j = i + 1
      while (j < messages.length) {
        const next = messages[j]
        if (next.role === 'tool_result' && next.toolId === msg.toolId) {
          group.push(next)
          j++
          break
        }
        const nextGk = toolGroupKind(next)
        if (next.role === 'tool_call' && nextGk === gk) {
          group.push(next)
          // 找对应的 result
          if (j + 1 < messages.length && messages[j + 1].role === 'tool_result' && messages[j + 1].toolId === next.toolId) {
            group.push(messages[j + 1])
            j++
          }
          j++
        } else {
          break
        }
      }

      // 只有 2 个以上同类工具才分组
      const toolCalls = group.filter((m) => m.role === 'tool_call')
      if (toolCalls.length >= 2) {
        result.push({ kind: 'group', groupKind: gk, messages: group })
        i = j
        continue
      }
    }

    result.push({ kind: 'message', msg })
    i++
  }

  return result
}
