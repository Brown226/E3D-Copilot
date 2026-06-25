/**
 * ToolCard — Reasonix 对标
 * 核心功能：
 * 1. 状态指示器（running/done/error）— ✓/✗/spinner
 * 2. 工具名 + 摘要 在一行
 * 3. 可折叠展开（GSAP 风格 border-left）
 * 4. 子代理嵌套展示
 * 5. Shell 输出预览（前 10 行 + "显示全部"）
 * 6. DiffView 集成
 */

import { useState, useMemo } from 'react'
import {
  Loader2,
  ChevronRight,
} from 'lucide-react'
import type { Message } from '@/types'
import { DiffView } from './DiffView'

interface ToolCardProps {
  msg: Message
  /** 子调用列表（由 MessageList 传入） */
  subcalls?: Message[]
  /** 所有消息（用于查找子调用） */
  allMessages?: Message[]
}

const SHELL_PREVIEW_LINES = 10

/** 美化 JSON */
function prettyJson(json: string): string {
  try {
    return JSON.stringify(JSON.parse(json), null, 2)
  } catch {
    return json
  }
}

/** 截断 Shell 输出 */
function splitPreview(text: string, n: number): { preview: string; total: number; hasMore: boolean } {
  const lines = text.split('\n')
  const total = lines.length
  if (total <= n) return { preview: text, total, hasMore: false }
  return { preview: lines.slice(0, n).join('\n'), total, hasMore: true }
}

/** 工具摘要 — Reasonix 风格 */
function summarizeTool(name: string, args?: string, error?: string): string {
  if (error) return error.slice(0, 80)
  if (!args) return ''

  try {
    const parsed = JSON.parse(args)
    switch (name) {
      case 'query': return `${parsed.queryType || ''} ${parsed.elementName || ''}`.trim()
      case 'modify': return `${parsed.elementName || ''} → ${parsed.attributeName}=${parsed.attributeValue || ''}`.trim()
      case 'calculate': return parsed.expression || ''
      case 'export': return parsed.filePath || parsed.format || ''
      case 'execute_pml': return `PML: ${(parsed.command || parsed.script || '').slice(0, 40)}`
      case 'grep': return parsed.pattern || parsed.query || ''
      case 'read_file': return parsed.filePath || parsed.path || ''
      case 'todo_write': return `${(parsed.todos || []).length} 个任务`
      case 'complete_step': return `✓ ${parsed.step || ''}`
      case 'ask': return '询问用户'
      case 'undo_redo': return parsed.action || ''
      case 'report': return `${parsed.type || ''} ${parsed.format || ''}`.trim()
      case 'compare': return `${parsed.element_a || ''} vs ${parsed.element_b || ''}`
      case 'hierarchy': return `${parsed.direction || 'info'} ${parsed.element || ''}`.trim()
      case 'batch': return `${parsed.query_type || ''} → ${Object.keys(parsed.attributes || {}).join(',')}`
      case 'design': return `${parsed.action || ''} ${parsed.type || ''} ${parsed.name || ''}`.trim()
      case 'piping': return `${parsed.action || ''} ${parsed.name || ''}`.trim()
      case 'geometry': return `${parsed.action || ''} ${parsed.element || ''}`.trim()
      default: return ''
    }
  } catch {
    return ''
  }
}

/** 子代理状态摘要 */
function subcallSummary(subcalls: Message[]): string {
  const running = subcalls.filter((m) => !m.finalized).length
  const done = subcalls.filter((m) => m.finalized && !m.toolError).length
  const failed = subcalls.filter((m) => m.toolError).length
  const parts: string[] = []
  if (running > 0) parts.push(`${running} 运行中`)
  if (done > 0) parts.push(`${done} 完成`)
  if (failed > 0) parts.push(`${failed} 失败`)
  return parts.join(' · ') || `${subcalls.length} 个子调用`
}

/** 格式化耗时 */
function formatDuration(ms?: number): string {
  if (typeof ms !== 'number' || !Number.isFinite(ms) || ms < 0) return ''
  return `${Math.round(ms)} ms`
}

export function ToolCard({ msg, subcalls = [] }: ToolCardProps) {
  const [userOpen, setUserOpen] = useState<boolean | null>(null)
  const [showAll, setShowAll] = useState(false)

  const isRunning = !msg.finalized
  const isError = !!msg.toolError
  const hasSubcalls = subcalls.length > 0

  // 智能默认：修改类工具+出错+运行中 自动展开
  const AUTO_EXPAND_TOOLS = new Set(['write_file', 'edit_file', 'multi_edit', 'modify', 'move_file', 'delete_range', 'notebook_edit'])
  const shouldAutoExpand = isError || isRunning || AUTO_EXPAND_TOOLS.has(msg.toolName || '')
  const open = userOpen ?? shouldAutoExpand
  const toggleOpen = () => setUserOpen((prev) => prev === null ? !shouldAutoExpand : !prev)

  // 解析参数和结果
  const argsStr = useMemo(() => {
    if (!msg.toolArgs) return null
    try {
      return typeof msg.toolArgs === 'string'
        ? prettyJson(msg.toolArgs)
        : JSON.stringify(msg.toolArgs, null, 2)
    } catch {
      return String(msg.toolArgs)
    }
  }, [msg.toolArgs])

  const resultStr = useMemo(() => {
    if (msg.toolError) return msg.toolError
    if (!msg.content) return null
    try {
      const parsed = JSON.parse(msg.content)
      return JSON.stringify(parsed, null, 2)
    } catch {
      return msg.content
    }
  }, [msg.content, msg.toolError])

  // 检测 diff 数据
  const diffData = useMemo(() => {
    if (msg.toolName !== 'modify') return null
    try {
      const args = msg.toolArgs
        ? (typeof msg.toolArgs === 'string' ? JSON.parse(msg.toolArgs) : msg.toolArgs) as Record<string, unknown>
        : null
      const result = msg.content
        ? (typeof msg.content === 'string' ? JSON.parse(msg.content) : msg.content) as Record<string, unknown>
        : null
      if (args?.oldValue !== undefined && args?.newValue !== undefined) {
        return { oldText: String(args.oldValue), newText: String(args.newValue), fileName: String(args.filePath || args.elementName || '') }
      }
      if (result?.oldValue !== undefined && result?.newValue !== undefined) {
        return { oldText: String(result.oldValue), newText: String(result.newValue), fileName: String(result.filePath || '') }
      }
    } catch { /* ignore */ }
    return null
  }, [msg.toolArgs, msg.toolName, msg.content])

  // Shell 输出预览
  const shellPreview = useMemo(() => {
    if (!resultStr) return null
    return splitPreview(resultStr, SHELL_PREVIEW_LINES)
  }, [resultStr])

  const displayResult = showAll ? resultStr : shellPreview?.preview

  // 摘要
  const summary = isRunning
    ? ''
    : summarizeTool(msg.toolName || 'tool', argsStr || undefined, msg.toolError)

  // 耗时
  const duration = isRunning ? '' : formatDuration(msg.durationMs)

  const hasBody = !!(argsStr || resultStr || hasSubcalls || diffData)

  return (
    <div className="tool" data-entrance={msg.id} data-error={isError ? '' : undefined}>
      {/* 卡片头部 — Reasonix 紧凑行内风格 */}
      <button
        type="button"
        className="tool__head"
        data-running={isRunning ? '' : undefined}
        onClick={() => hasBody && toggleOpen()}
        aria-expanded={hasBody ? open : undefined}
      >
        <span className="tool__label-group">
          {/* 子代理嵌套指示 */}
          {hasSubcalls && <span className="tool__nested-count">⊞{subcalls.length}</span>}

          {/* 状态图标 */}
          {isRunning ? (
            <Loader2 className="w-3.5 h-3.5 animate-spin" style={{ color: 'var(--accent)' }} />
          ) : isError ? (
            <span className="tool__status-icon tool__status-icon--err">✗</span>
          ) : (
            <span className="tool__status-icon tool__status-icon--ok">✓</span>
          )}

          {/* 工具名 */}
          <span className="tool__name">{msg.toolName || 'tool_call'}</span>
        </span>

        {/* 摘要 */}
        {summary && <span className="tool__summary">{summary}</span>}

        {/* 子代理摘要 */}
        {hasSubcalls && !summary && (
          <span className="tool__subject">{subcallSummary(subcalls)}</span>
        )}

        {/* 执行耗时 */}
        {duration && <span className="tool__duration">{duration}</span>}

        {/* 展开箭头 */}
        {hasBody && (
          <span className={`tool__chevron${open ? ' tool__chevron--open' : ''}`}>
            <ChevronRight size={12} />
          </span>
        )}
      </button>

      {/* 展开内容 */}
      {open && hasBody && (
        <div className="tool__body">
          {/* Diff 视图 */}
          {diffData ? (
            <DiffView oldText={diffData.oldText} newText={diffData.newText} fileName={diffData.fileName} />
          ) : (
            <>
              {/* 输入参数 — 带标签 */}
              {argsStr && (
                <div className="tool__section">
                  <div className="tool__section-label">输入</div>
                  <pre className="code-viewer" style={{ maxHeight: 180 }}>
                    {argsStr}
                  </pre>
                </div>
              )}

              {/* 子调用列表 */}
              {hasSubcalls && (
                <div className="tool__nested">
                  {subcalls.map((sub) => (
                    <SubToolRow key={sub.id} msg={sub} />
                  ))}
                </div>
              )}

              {/* 输出结果 — 带标签 */}
              {resultStr && !diffData && (
                <div className="tool__section">
                  <div className="tool__section-label">输出</div>
                  <pre className="code-viewer" style={{ maxHeight: showAll ? 480 : 280 }}>
                    {displayResult}
                  </pre>
                  {shellPreview?.hasMore && !showAll && (
                    <button className="tool__showall" onClick={() => setShowAll(true)}>
                      显示全部 {shellPreview.total} 行
                    </button>
                  )}
                </div>
              )}
            </>
          )}

          {/* 错误 */}
          {msg.toolError && (
            <div className="tool__section tool__section--error">
              <div className="tool__section-label">错误</div>
              <div className="tool__err">{msg.toolError}</div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

// ═══════════════════════════════════════════
// 子调用行（内联展示） — Reasonix 紧凑风格
// ═══════════════════════════════════════════

function SubToolRow({ msg }: { msg: Message }) {
  const [expanded, setExpanded] = useState(false)
  const isRunning = !msg.finalized
  const isError = !!msg.toolError

  const resultPreview = useMemo(() => {
    if (msg.toolError) return msg.toolError.slice(0, 100)
    if (!msg.content) return null
    try {
      const parsed = JSON.parse(msg.content)
      return JSON.stringify(parsed).slice(0, 100)
    } catch {
      return msg.content.slice(0, 100)
    }
  }, [msg.content, msg.toolError])

  return (
    <div className="tool" style={{ margin: '2px 0' }}>
      <button
        type="button"
        className="tool__head"
        data-running={isRunning ? '' : undefined}
        onClick={() => resultPreview && setExpanded(!expanded)}
      >
        <span className="tool__label-group">
          {isRunning ? (
            <Loader2 className="w-3 h-3 animate-spin" style={{ color: 'var(--accent)' }} />
          ) : isError ? (
            <span className="tool__status-icon tool__status-icon--err">✗</span>
          ) : (
            <span className="tool__status-icon tool__status-icon--ok">✓</span>
          )}
          <span className="tool__name">{msg.toolName || 'tool'}</span>
        </span>
        <span className="tool__summary">
          {summarizeTool(msg.toolName || 'tool', argsStr(msg), msg.toolError)}
        </span>
        {resultPreview && (
          <span className={`tool__chevron${expanded ? ' tool__chevron--open' : ''}`}>
            <ChevronRight size={12} />
          </span>
        )}
      </button>
      {expanded && resultPreview && (
        <div className="tool__body">
          <pre className="code-viewer" style={{ maxHeight: 100, fontSize: 11 }}>
            {msg.toolError || msg.content}
          </pre>
        </div>
      )}
    </div>
  )
}

function argsStr(msg: Message): string | undefined {
  if (!msg.toolArgs) return undefined
  try {
    return typeof msg.toolArgs === 'string' ? msg.toolArgs : JSON.stringify(msg.toolArgs)
  } catch {
    return String(msg.toolArgs)
  }
}
