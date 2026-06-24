/**
 * ToolCard — 统一工具调用 + 结果展示（同款设计）
 * 核心功能：
 * 1. 状态指示器（running/done/error）
 * 2. 工具名 + 参数 + 结果 在一个卡片中
 * 3. 可折叠展开
 * 4. 子代理嵌套展示（task/explore 等工具的子调用）
 * 5. Shell 输出预览（前 10 行 + "显示全部"）
 */

import { useState, useMemo } from 'react'
import {
  Loader2,
  Copy,
  Check,
  ChevronRight,
  Layers,
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

/** 工具摘要 */
function summarizeTool(name: string, args?: string, error?: string): string {
  if (error) return `失败: ${error.slice(0, 80)}`
  if (!args) return name

  try {
    const parsed = JSON.parse(args)
    switch (name) {
      case 'query': return `查询 ${parsed.queryType || ''} ${parsed.elementName || ''}`.trim()
      case 'modify': return `修改 ${parsed.elementName || ''} → ${parsed.attributeName}=${parsed.attributeValue || ''}`.trim()
      case 'calculate': return `计算 ${parsed.expression || ''}`
      case 'export': return `导出到 ${parsed.filePath || parsed.format || ''}`
      case 'execute_pml': return `执行 PML: ${(parsed.command || parsed.script || '').slice(0, 40)}`
      case 'search_knowledge': return `搜索: ${parsed.query || ''}`
      case 'read_file': return `读取 ${parsed.filePath || parsed.path || ''}`
      case 'task': return `任务: ${parsed.description || parsed.task || ''}`.slice(0, 60)
      case 'ask_user': return `询问用户`
      default: return name
    }
  } catch {
    return name
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

/** 默认展开的工具（修改类 + 出错 + 运行中） */
const AUTO_EXPAND_TOOLS = new Set(['write_file', 'edit_file', 'multi_edit', 'modify', 'move_file', 'delete_range', 'notebook_edit'])

function shouldAutoExpand(toolName: string | undefined, isError: boolean, isRunning: boolean): boolean {
  if (isError) return true
  if (isRunning) return true
  return AUTO_EXPAND_TOOLS.has(toolName || '')
}

export function ToolCard({ msg, subcalls = [] }: ToolCardProps) {
  const [userOpen, setUserOpen] = useState<boolean | null>(null)
  const [showAll, setShowAll] = useState(false)
  const [copied, setCopied] = useState(false)

  const isRunning = !msg.finalized
  const isError = !!msg.toolError
  const hasSubcalls = subcalls.length > 0

  // 智能默认：用户操作后尊重用户意图
  const expanded = userOpen ?? shouldAutoExpand(msg.toolName, isError, isRunning)
  const toggleExpand = () => setUserOpen((prev) => prev === null ? !shouldAutoExpand(msg.toolName, isError, isRunning) : !prev)

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

  // 检测是否包含 diff 数据（modify 工具的结果）
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
  const summary = useMemo(
    () => summarizeTool(msg.toolName || 'tool', argsStr || undefined, msg.toolError),
    [msg.toolName, argsStr, msg.toolError]
  )

  // 复制结果
  const handleCopy = () => {
    const text = displayResult || ''
    navigator.clipboard.writeText(text).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    })
  }

  const hasBody = !!(argsStr || resultStr)

  return (
    <div className="flex justify-start my-0.5">
      <div className="max-w-[85%] w-full">
        {/* 卡片头部 — 紧凑行内风格 */}
        <button
          onClick={() => hasBody && toggleExpand()}
          className={`w-full flex items-center gap-1.5 px-2 py-1 rounded text-left transition-all group ${
            expanded
              ? 'bg-slate-50 dark:bg-slate-800/50'
              : 'hover:bg-slate-50 dark:hover:bg-slate-800/30'
          }`}
        >
          {/* 状态图标 — 简洁的 ✓/✗/• 符号 */}
          <span className="shrink-0 text-xs leading-none">
            {isRunning ? (
              <Loader2 className="w-3.5 h-3.5 text-blue-500 animate-spin" />
            ) : isError ? (
              <span className="text-red-500">✗</span>
            ) : (
              <span className="text-emerald-500">✓</span>
            )}
          </span>

          {/* 工具名 */}
          <span className="text-xs font-semibold text-slate-600 dark:text-slate-400 font-mono shrink-0">
            {msg.toolName || 'tool_call'}
          </span>

          {/* 子代理嵌套指示 */}
          {hasSubcalls && (
            <span className="flex items-center gap-0.5 text-xs text-purple-500 dark:text-purple-400">
              <Layers className="w-3 h-3" />
              {subcalls.length}
            </span>
          )}

          {/* 摘要 */}
          <span className="text-xs text-slate-400 dark:text-slate-500 truncate flex-1">
            {hasSubcalls ? subcallSummary(subcalls) : summary}
          </span>

          {/* 执行耗时 */}
          {msg.durationMs != null && msg.durationMs > 0 && !isRunning && (
            <span className="text-xs text-slate-400 dark:text-slate-500 shrink-0 tabular-nums">
              {msg.durationMs} ms
            </span>
          )}

          {/* 展开箭头 */}
          {(hasBody || hasSubcalls) && (
            <ChevronRight
              className={`w-3.5 h-3.5 text-slate-400 transition-transform duration-200 shrink-0 ${
                expanded ? 'rotate-90' : ''
              }`}
            />
          )}
        </button>

        {/* 展开内容（带平滑动画） */}
        {expanded && (
          <div className="mt-1 ml-4 space-y-1.5 tool-card-body border-l-2 border-slate-200 dark:border-slate-700 pl-3">
            {/* 参数 */}
            {argsStr && (
              <div className="rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 overflow-hidden">
                <div className="flex items-center justify-between px-3 py-1.5 bg-slate-50 dark:bg-slate-700/50 border-b border-slate-200 dark:border-slate-700">
                  <span className="text-xs font-medium text-slate-500 dark:text-slate-400">参数</span>
                </div>
                <pre className="px-3 py-2 text-xs text-slate-700 dark:text-slate-300 font-mono whitespace-pre-wrap break-all max-h-[180px] overflow-y-auto">
                  {argsStr}
                </pre>
              </div>
            )}

            {/* 子调用列表（嵌套展示） */}
            {hasSubcalls && (
              <div className="space-y-1 pl-2 border-l-2 border-purple-200 dark:border-purple-800">
                {subcalls.map((sub) => (
                  <SubToolRow key={sub.id} msg={sub} />
                ))}
              </div>
            )}

            {/* 结果 */}
            {diffData ? (
              <DiffView oldText={diffData.oldText} newText={diffData.newText} fileName={diffData.fileName} />
            ) : resultStr && (
              <div className={`rounded-lg border overflow-hidden ${
                isError
                  ? 'border-red-200 dark:border-red-800 bg-red-50 dark:bg-red-900/20'
                  : 'border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800'
              }`}>
                <div className="flex items-center justify-between px-3 py-1.5 bg-slate-50 dark:bg-slate-700/50 border-b border-slate-200 dark:border-slate-700">
                  <span className={`text-xs font-medium ${
                    isError ? 'text-red-500' : 'text-slate-500 dark:text-slate-400'
                  }`}>
                    {isError ? '错误' : '结果'}
                  </span>
                  <button
                    onClick={handleCopy}
                    className="flex items-center gap-1 text-xs text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors"
                  >
                    {copied ? <Check className="w-3 h-3" /> : <Copy className="w-3 h-3" />}
                    {copied ? '已复制' : '复制'}
                  </button>
                </div>
                <pre className={`px-3 py-2 text-xs font-mono whitespace-pre-wrap break-all ${
                  isError
                    ? 'text-red-700 dark:text-red-300'
                    : 'text-slate-700 dark:text-slate-300'
                } ${showAll ? 'max-h-[480px]' : 'max-h-[280px]'} overflow-y-auto`}>
                  {displayResult}
                </pre>
                {shellPreview?.hasMore && !showAll && (
                  <button
                    onClick={() => setShowAll(true)}
                    className="w-full px-3 py-1.5 text-xs text-blue-600 dark:text-blue-400 hover:bg-slate-50 dark:hover:bg-slate-700/50 border-t border-slate-200 dark:border-slate-700 transition-colors"
                  >
                    显示全部 {shellPreview.total} 行
                  </button>
                )}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  )
}

// ═══════════════════════════════════════════
// 子调用行（内联展示） — 紧凑风格
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
    <div className="rounded bg-transparent">
      <button
        onClick={() => resultPreview && setExpanded(!expanded)}
        className="w-full flex items-center gap-1.5 px-1.5 py-1 text-left hover:bg-slate-50 dark:hover:bg-slate-700/30 transition-colors rounded"
      >
        <span className="shrink-0 text-xs leading-none">
          {isRunning ? (
            <Loader2 className="w-3 h-3 text-blue-500 animate-spin" />
          ) : isError ? (
            <span className="text-red-500">✗</span>
          ) : (
            <span className="text-emerald-500">✓</span>
          )}
        </span>
        <span className="text-xs font-mono text-slate-500 dark:text-slate-400 shrink-0">
          {msg.toolName || 'tool'}
        </span>
        <span className="text-xs text-slate-400 dark:text-slate-500 truncate flex-1">
          {summarizeTool(msg.toolName || 'tool', argsStr(msg), msg.toolError)}
        </span>
        {resultPreview && (
          <ChevronRight className={`w-3 h-3 text-slate-400 transition-transform ${expanded ? 'rotate-90' : ''}`} />
        )}
      </button>
      {expanded && resultPreview && (
        <div className="px-2 pb-1.5">
          <pre className="text-[11px] text-slate-500 dark:text-slate-400 font-mono whitespace-pre-wrap break-all max-h-[100px] overflow-y-auto bg-slate-50 dark:bg-slate-900 rounded p-1.5">
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
