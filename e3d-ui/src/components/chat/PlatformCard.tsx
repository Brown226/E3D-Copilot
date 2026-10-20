/**
 * PlatformCard — 数据管理平台记录的可视化
 *
 * 演示立意是「全流程管理」，需要让观众看到数据**真的落到了平台上**，
 * 而不是只在聊天里说了一句「已同步」。
 *
 * 数据来源：MCP 工具（record_* / query_history）。
 * 注意：McpRemoteTool 只把结果写在 ToolResult.Text，没有填充 Data，
 * 所以这里从 tool_result 的 content 里解析 JSON，解析不出来就不渲染。
 */

import { useState } from 'react'
import { Database, FileText, ListChecks, Box, GitCompare, ChevronDown, ChevronRight } from 'lucide-react'

/** MCP 工具返回的 content 文本 → 解析成对象 */
function parseToolContent(content: string | undefined): unknown {
  if (!content) return null
  const trimmed = content.trim()
  if (!trimmed.startsWith('{') && !trimmed.startsWith('[')) return null
  try {
    return JSON.parse(trimmed)
  } catch {
    return null
  }
}

const KIND_META: Record<string, { label: string; icon: React.ReactNode; tone: string }> = {
  drawings: { label: '图纸台账', icon: <FileText size={12} />, tone: 'dwg' },
  operations: { label: '操作留痕', icon: <ListChecks size={12} />, tone: 'op' },
  models: { label: '模型清单', icon: <Box size={12} />, tone: 'mdl' },
  diffs: { label: '版本差异', icon: <GitCompare size={12} />, tone: 'dif' },
}

interface HistoryGroup {
  total?: number
  rows?: Array<Record<string, unknown>>
}

interface HistoryResult {
  ok?: boolean
  result?: Record<string, HistoryGroup>
}

export function PlatformCard({ content }: { content?: string }) {
  const parsed = parseToolContent(content) as HistoryResult | null
  if (!parsed || !parsed.result || typeof parsed.result !== 'object') return null

  const groups = Object.entries(parsed.result).filter(
    ([, g]) => g && typeof g === 'object' && Array.isArray((g as HistoryGroup).rows),
  )
  if (!groups.length) return null

  const grandTotal = groups.reduce((s, [, g]) => s + ((g as HistoryGroup).rows?.length ?? 0), 0)

  return (
    <div className="plat-card">
      <div className="plat-card__head">
        <Database size={13} />
        <span className="plat-card__title">平台记录</span>
        <span className="plat-card__total">共 {grandTotal} 条</span>
      </div>
      {groups.map(([kind, g]) => {
        const meta = KIND_META[kind] ?? { label: kind, icon: <Database size={12} />, tone: 'oth' }
        return (
          <PlatformGroup
            key={kind}
            label={meta.label}
            icon={meta.icon}
            tone={meta.tone}
            total={(g as HistoryGroup).total ?? (g as HistoryGroup).rows?.length ?? 0}
            rows={(g as HistoryGroup).rows ?? []}
          />
        )
      })}
    </div>
  )
}

/** 一组记录：默认折叠，超过 3 条时只显示前 3 条 */
function PlatformGroup({
  label, icon, tone, total, rows,
}: {
  label: string
  icon: React.ReactNode
  tone: string
  total: number
  rows: Array<Record<string, unknown>>
}) {
  const [open, setOpen] = useState(true)
  const visible = rows.slice(0, 3)
  const [showAll, setShowAll] = useState(false)
  const list = showAll ? rows : visible

  return (
    <div className={`plat-group plat-group--${tone}`}>
      <button className="plat-group__head" onClick={() => setOpen((v) => !v)}>
        {open ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
        {icon}
        <span className="plat-group__label">{label}</span>
        <span className="plat-group__count">{total} 条</span>
      </button>
      {open && (
        <div className="plat-group__body">
          {rows.length === 0 && <div className="plat-empty">（暂无记录）</div>}
          {list.map((r, i) => (
            <PlatformRow key={i} kind={tone} row={r} />
          ))}
          {rows.length > 3 && (
            <button className="plat-more" onClick={() => setShowAll((v) => !v)}>
              {showAll ? '收起' : `显示全部 ${rows.length} 条`}
            </button>
          )}
        </div>
      )}
    </div>
  )
}

function v(x: unknown): string {
  if (x === null || x === undefined || x === '') return '—'
  if (typeof x === 'object') return JSON.stringify(x)
  return String(x)
}

/** 单行记录：按 kind 取不同字段，字段名与后端落盘 JSON 一致 */
function PlatformRow({ kind, row }: { kind: string; row: Record<string, unknown> }) {
  let primary = ''
  let secondary: string[] = []

  switch (kind) {
    case 'dwg':
      primary = `${v(row.drawingNo)} · ${v(row.version)} 版`
      secondary = [
        row.layerCount !== undefined ? `图层 ${v(row.layerCount)}` : '',
        row.entityCount !== undefined ? `实体 ${v(row.entityCount)}` : '',
        row.submittedBy ? `提交人 ${v(row.submittedBy)}` : '',
      ]
      break
    case 'op':
      primary = `${v(row.operation)} · ${v(row.result)}`
      secondary = [v(row.detail), row.operator ? `操作人 ${v(row.operator)}` : '']
      break
    case 'mdl':
      primary = `${v(row.drawingNo)} · ${v(row.version)} 版 · ${v(row.modelName)}`
      secondary = [
        row.componentTotal !== undefined ? `构件 ${v(row.componentTotal)}` : '',
        Array.isArray(row.components) ? `类型 ${(row.components as unknown[]).length} 种` : '',
      ]
      break
    case 'dif':
      primary = `${v(row.drawingNo)} ${v(row.versionA)}→${v(row.versionB)}`
      secondary = [
        row.counts && typeof row.counts === 'object'
          ? Object.entries(row.counts as Record<string, unknown>)
              .map(([k, n]) => `${k}=${n}`)
              .join(' ')
          : '',
        row.recordCount !== undefined ? `明细 ${v(row.recordCount)} 条` : '',
      ]
      break
    default:
      primary = v(row.id)
  }

  const detail = secondary.filter(Boolean).join(' · ')

  return (
    <div className="plat-row">
      <span className="plat-row__primary">{primary}</span>
      {detail && <span className="plat-row__secondary">{detail}</span>}
    </div>
  )
}
