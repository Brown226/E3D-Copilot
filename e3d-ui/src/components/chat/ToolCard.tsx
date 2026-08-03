/**
 * ToolCard — Reasonix 对标
 * 核心功能：
 * 1. 状态指示器（running/done/error）— ✓/✗/spinner
 * 2. 工具名 + 摘要 在一行
 * 3. 可折叠展开（GSAP 风格 border-left）
 * 4. 子代理嵌套展示
 * 5. Shell 输出预览（前 10 行 + "显示全部"）
 * 6. DiffView 集成
 * 7. 结构化结果卡片（CAD 工具的 layerStats / export / entity 信息）
 */

import { useState, useMemo, memo } from 'react'
import {
  Loader2,
  ChevronRight,
  Bot,
} from 'lucide-react'
import type { Message } from '@/types'
import { DiffView } from './DiffView'
import MarkdownBlock from '@/components/common/MarkdownBlock'

interface ToolCardProps {
  msg: Message
  /** 子调用列表（由 MessageList 传入） */
  subcalls?: Message[]
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

/** 工具摘要 — 对齐后端真实 Schema 字段名 */
function summarizeTool(name: string, args?: string, error?: string): string {
  if (error) return error.slice(0, 80)
  if (!args) return ''

  try {
    const p = JSON.parse(args)
    switch (name) {
      case 'query': return [p.type, p.name, p.scope].filter(Boolean).join(' · ')
      case 'modify': {
        const target = p.dburi || p.element || ''
        const attrs = p.attributes
        if (attrs && typeof attrs === 'object') {
          const kv = Object.entries(attrs).map(([k, v]) => `${k}=${v ?? ''}`).join(', ')
          return `${target} → ${kv}`
        }
        if (p.attribute) return `${target} → ${p.attribute}=${p.value ?? ''}`
        return target
      }
      case 'check': return `${p.type || ''} ${p.element || p.target || ''}`.trim()
      case 'get_attributes': return `${p.element || ''}${p.all ? ' (全部)' : ''}`.trim()
      case 'calculate': return `${p.operation || ''} ${p.expression || ''}`.trim()
      case 'export': return `${p.action || ''} ${p.format || ''} ${p.filePath || ''}`.trim()
      case 'execute_pml': return `PML: ${(p.script || '').slice(0, 40)}`
      case 'report': return `${p.type || ''} ${p.format || ''}`.trim()
      case 'compare': return `${p.element_a || ''} vs ${p.element_b || ''}`
      case 'hierarchy': return `${p.direction || 'info'} ${p.element || ''}`.trim()
      case 'batch': return `${p.query_type || ''} → ${p.attributes ? Object.keys(p.attributes).join(',') : ''}`.trim()
      case 'design': return `${p.action || ''} ${p.type || ''} ${p.name || ''}`.trim()
      case 'piping': return `${p.action || ''} ${p.name || p.pipe || ''}`.trim()
      case 'geometry': return `${p.action || ''} ${p.element || ''}`.trim()
      case 'read_file': return p.path || p.filePath || ''
      case 'write_file': return p.path || ''
      case 'grep': return p.pattern || p.query || ''
      case 'glob': return p.pattern || ''
      case 'todo_write': return `${(p.todos || []).length} 个任务`
      case 'complete_step': return `✓ ${p.step || ''}`
      case 'ask': return `${(p.questions || []).length} 个问题`
      case 'undo_redo': return p.action || ''
      case 'generate_iso_drawing': return `${p.action || ''} ${p.pipe_name || (p.pipe_names && p.pipe_names.length ? p.pipe_names.join(',') : '')}`.trim()
      case 'query_material': return `${p.action || ''} ${p.keyword || p.material_code || ''}`.trim()
      case 'get_pipe_info': return `${p.action || ''} ${p.pipe_name || ''}`.trim()
      case 'structure_drawing': return `${p.action || ''} ${p.direction || ''}`.trim()
      case 'cad_import': return `${p.action || ''} ${p.file_path || ''}`.trim()
      case 'autocad': return p.action || ''
      case 'memory': return `${p.action || ''} ${p.query || p.key || ''}`.trim()
      case 'run_skill': return p.name || ''
      case 'dispatch_subagent': return p.name || ''
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

export const ToolCard = memo(function ToolCard({ msg, subcalls = [] }: ToolCardProps) {
  const [userOpen, setUserOpen] = useState<boolean | null>(null)
  const [showAll, setShowAll] = useState(false)

  const isRunning = !msg.finalized
  const isError = !!msg.toolError
  const hasSubcalls = subcalls.length > 0

  // 智能默认：修改类工具+出错+运行中 自动展开（对齐 E小智 真实工具名）
  const AUTO_EXPAND_TOOLS = new Set([
    'modify', 'design', 'piping', 'batch', 'structure_drawing',
    'generate_iso_drawing', 'write_file', 'cad_import', 'autocad', 'undo_redo',
  ])
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

  // 检测 diff 数据（modify 工具）
  const diffData = useMemo(() => {
    if (msg.toolName !== 'modify') return null
    try {
      const args = msg.toolArgs
        ? (typeof msg.toolArgs === 'string' ? JSON.parse(msg.toolArgs) : msg.toolArgs) as Record<string, unknown>
        : null
      const result = msg.content
        ? (typeof msg.content === 'string' ? JSON.parse(msg.content) : msg.content) as Record<string, unknown>
        : null
      const target = String((result?.target as string) || (args?.dburi as string) || (args?.element as string) || '')

      // 后端 modify 成功时返回 changes: [{ attribute, old, new }]
      const changes = result?.changes
      if (Array.isArray(changes) && changes.length > 0) {
        const oldLines = (changes as Array<Record<string, unknown>>).map((c) => `${c.attribute} = ${c.old ?? ''}`)
        const newLines = (changes as Array<Record<string, unknown>>).map((c) => `${c.attribute} = ${c.new ?? ''}`)
        return { oldText: oldLines.join('\n'), newText: newLines.join('\n'), fileName: target }
      }

      // 兜底：无 changes 时从 args 的 attributes 展示将要设置的新值（无旧值）
      const attrs = args?.attributes
      if (attrs && typeof attrs === 'object') {
        const lines = Object.entries(attrs as Record<string, unknown>).map(([k, v]) => `${k} = ${v ?? ''}`)
        return { oldText: '', newText: lines.join('\n'), fileName: target }
      }
      if (args?.attribute) {
        return { oldText: '', newText: `${args.attribute} = ${args.value ?? ''}`, fileName: target }
      }
    } catch { /* ignore */ }
    return null
  }, [msg.toolArgs, msg.toolName, msg.content])

  // Shell 输出预览
  const shellPreview = useMemo(() => {
    if (!resultStr) return null
    return splitPreview(resultStr, SHELL_PREVIEW_LINES)
  }, [resultStr])

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
          {msg.toolName === 'dispatch_subagent' ? (
            <Bot className="w-3.5 h-3.5" style={{ color: 'var(--accent)' }} />
          ) : isRunning ? (
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

              {/* 结构化结果卡片 — CAD 工具优先渲染（P1-12） */}
              {msg.toolMeta && (
                <div className="tool__section">
                  <ToolResultCard toolName={msg.toolName || ''} meta={msg.toolMeta} />
                </div>
              )}

              {/* 输出结果 — Markdown 渲染（支持表格/代码块等） */}
              {resultStr && !diffData && (
                <div className="tool__section">
                  <div className="tool__section-label">输出</div>
                  <div className="tool__output-md" style={{ maxHeight: showAll ? 480 : 280, overflow: 'auto' }}>
                    <MarkdownBlock markdown={resultStr} />
                  </div>
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
})

// ═══════════════════════════════════════════
// 子调用行（内联展示）— Reasonix 紧凑风格
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
          <div className="tool__output-md" style={{ maxHeight: 100, fontSize: 11, overflow: 'auto' }}>
            <MarkdownBlock markdown={msg.toolError || msg.content || ''} />
          </div>
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

// ═══════════════════════════════════════════
// 结构化结果卡片 — CAD 工具专用（P1-12）
// 根据 msg.toolMeta 渲染图层统计/导出结果/元素表格
// ═══════════════════════════════════════════

interface StatsRow {
  key: string
  value: number | string
}

/** 图层/类型统计表格 */
function StatsTable({ title, rows }: { title: string; rows: StatsRow[] }) {
  if (!rows.length) return null
  const total = rows.reduce((s, r) => s + (typeof r.value === 'number' ? r.value : 0), 0)
  return (
    <div className="tool__card-section">
      <div className="tool__card-title">{title} <span className="tool__card-count">({total})</span></div>
      <div className="tool__stats-table">
        {rows.map((r) => (
          <div key={r.key} className="tool__stats-row">
            <span className="tool__stats-key">{r.key}</span>
            <span className="tool__stats-val">{r.value}</span>
          </div>
        ))}
      </div>
    </div>
  )
}

/** 键值对展示（导出路径、CAD handle 等） */
function MetaFields({ fields }: { fields: Array<[string, string]> }) {
  if (!fields.length) return null
  return (
    <div className="tool__card-section">
      <div className="tool__meta-grid">
        {fields.map(([k, v]) => (
          <div key={k} className="tool__meta-row">
            <span className="tool__meta-key">{k}</span>
            <span className="tool__meta-val">{v}</span>
          </div>
        ))}
      </div>
    </div>
  )
}

/** 把对象转为 StatsRow[]，按数量降序 */
function toStatsRows(stats: unknown): StatsRow[] {
  if (!stats || typeof stats !== 'object') return []
  const entries = Object.entries(stats as Record<string, unknown>)
  return entries
    .map(([k, v]) => ({ key: k, value: typeof v === 'number' ? v : String(v ?? '') }))
    .sort((a, b) => (typeof b.value === 'number' && typeof a.value === 'number' ? b.value - a.value : 0))
}

function fmtVal(v: unknown): string {
  if (v === null || v === undefined) return ''
  if (typeof v === 'boolean') return v ? '是' : '否'
  return String(v)
}

/** 结构化结果卡片 — 根据 toolName + toolMeta 渲染 */
function ToolResultCard({ toolName, meta }: { toolName: string; meta: unknown }) {
  if (!meta || typeof meta !== 'object') return null
  const m = meta as Record<string, unknown>

  // e3d_to_cad_export / autocad_control 共用
  const layerStats = toStatsRows(m.layerStats)
  const typeStats = toStatsRows(m.typeStats)

  // 通用字段
  const fields: Array<[string, string]> = []
  if (m.outputPath) fields.push(['输出文件', String(m.outputPath)])
  if (m.elementCount !== undefined) fields.push(['元素数', String(m.elementCount)])
  if (m.totalCount !== undefined) fields.push(['总数', String(m.totalCount)])
  if (m.format) fields.push(['格式', String(m.format).toUpperCase()])
  if (m.scope) fields.push(['范围', String(m.scope)])
  if (m.elementType) fields.push(['类型过滤', String(m.elementType)])
  if (m.projection) fields.push(['投影', String(m.projection)])
  if (m.scale !== undefined) fields.push(['比例', String(m.scale)])
  if (m.includeText !== undefined) fields.push(['标注', m.includeText ? '是' : '否'])
  // CAD 实体相关
  if (m.handle) fields.push(['Handle', String(m.handle)])
  if (m.entityType) fields.push(['实体类型', String(m.entityType)])
  if (m.layer) fields.push(['图层', String(m.layer)])
  if (m.command) fields.push(['命令', String(m.command)])
  if (m.text) fields.push(['文字', String(m.text)])
  if (m.height !== undefined) fields.push(['高度', String(m.height)])
  if (m.rotation !== undefined) fields.push(['旋转', String(m.rotation)])
  if (m.color !== undefined) fields.push(['颜色', fmtVal(m.color)])
  if (m.linetype !== undefined) fields.push(['线型', fmtVal(m.linetype)])
  if (m.frozen !== undefined) fields.push(['冻结', m.frozen ? '是' : '否'])
  if (m.locked !== undefined) fields.push(['锁定', m.locked ? '是' : '否'])
  if (m.savedPath !== undefined) fields.push(['保存路径', String(m.savedPath)])
  if (m.count !== undefined) fields.push(['数量', String(m.count)])

  const isExportTool = toolName === 'e3d_to_cad_export'
  const isCadTool = toolName === 'autocad_control'

  if (!fields.length && !layerStats.length && !typeStats.length) return null

  return (
    <div className="tool__result-card" data-tool={toolName}>
      {/* 导出工具头部标记 */}
      {isExportTool && (
        <div className="tool__card-header">📐 导出结果</div>
      )}
      {isCadTool && (
        <div className="tool__card-header">🎯 CAD 操作结果</div>
      )}

      <MetaFields fields={fields} />

      {layerStats.length > 0 && (
        <StatsTable title="图层分布" rows={layerStats} />
      )}
      {typeStats.length > 0 && (
        <StatsTable title="类型分布" rows={typeStats} />
      )}

      {/* CAD 图层列表（layers 数组） */}
      {Array.isArray(m.layers) && (m.layers as unknown[]).length > 0 && (
        <div className="tool__card-section">
          <div className="tool__card-title">图层列表 ({(m.layers as unknown[]).length})</div>
          <div className="tool__stats-table">
            {(m.layers as Array<Record<string, unknown>>).slice(0, 50).map((l, i) => (
              <div key={l.name ? String(l.name) : i} className="tool__stats-row">
                <span className="tool__stats-key">{String(l.name ?? `layer-${i}`)}</span>
                <span className="tool__stats-val">
                  {[
                    l.color !== undefined ? `颜色=${l.color}` : '',
                    l.on !== undefined ? (l.on ? '开' : '关') : '',
                    l.frozen !== undefined ? (l.frozen ? '冻结' : '') : '',
                    l.locked !== undefined ? (l.locked ? '锁' : '') : '',
                  ].filter(Boolean).join(' · ')}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

