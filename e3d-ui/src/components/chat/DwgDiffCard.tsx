/**
 * DwgDiffCard — dwg_diff 的差异清单可视化
 *
 * 后端 Data = {
 *   fileA, fileB, segmentCountA, segmentCountB, tolerance,
 *   unchangedCount, addedCount, removedCount, modifiedCount,
 *   added:    SegmentInfo[],
 *   removed:  SegmentInfo[],
 *   modified: [{ layer, before, after, shift, lengthDelta }]
 * }
 * SegmentInfo = { Layer, StartX, StartY, EndX, EndY, Length }
 *
 * 只在几何层面呈现差异，不做「这堵墙是同一堵墙改造的」这类语义推断——
 * 判不出来就如实显示为「新增 + 删除」，与后端口径一致。
 */

import { useState } from 'react'
import { Plus, Minus, Pencil, ChevronDown, ChevronRight } from 'lucide-react'

interface SegmentInfo {
  Layer?: string
  StartX?: number
  StartY?: number
  EndX?: number
  EndY?: number
  Length?: number
}

interface ModifiedSegment {
  layer?: string
  Layer?: string
  before?: SegmentInfo
  after?: SegmentInfo
  shift?: number
  lengthDelta?: number
}

interface DiffData {
  fileA?: string
  fileB?: string
  segmentCountA?: number
  segmentCountB?: number
  tolerance?: number
  unchangedCount?: number
  addedCount?: number
  removedCount?: number
  modifiedCount?: number
  added?: SegmentInfo[]
  removed?: SegmentInfo[]
  modified?: ModifiedSegment[]
}

function asDiffData(meta: unknown): DiffData | null {
  if (!meta || typeof meta !== 'object') return null
  const m = meta as Record<string, unknown>
  // 用 added/removed 数组是否存在来判定，避免把别的工具结果误当 diff
  if (!Array.isArray(m.added) && !Array.isArray(m.removed)) return null
  return m as DiffData
}

function num(v: unknown, digits = 1): string {
  if (typeof v !== 'number' || !Number.isFinite(v)) return '—'
  return v.toFixed(digits)
}

function fileName(p?: string): string {
  if (!p) return ''
  const parts = p.split(/[\\/]/)
  return parts[parts.length - 1] || p
}

/** 单条线段：图层 + 起止点 + 长度 */
function SegmentRow({ seg, tone }: { seg: SegmentInfo; tone: 'add' | 'del' }) {
  const layer = seg.Layer || '未命名图层'
  return (
    <div className={`diff-seg diff-seg--${tone}`}>
      <span className="diff-seg__layer">{layer}</span>
      <span className="diff-seg__geo">
        ({num(seg.StartX)}, {num(seg.StartY)}) → ({num(seg.EndX)}, {num(seg.EndY)})
      </span>
      <span className="diff-seg__len">len={num(seg.Length, 0)}</span>
    </div>
  )
}

export function DwgDiffCard({ meta }: { meta: unknown }) {
  const data = asDiffData(meta)
  if (!data) return null

  const added = data.added ?? []
  const removed = data.removed ?? []
  const modified = data.modified ?? []
  const addedCount = data.addedCount ?? added.length
  const removedCount = data.removedCount ?? removed.length
  const modifiedCount = data.modifiedCount ?? modified.length

  return (
    <div className="diff-card">
      <div className="diff-card__head">
        <span className="diff-card__title">🔀 A/B 版本差异</span>
        <span className="diff-card__meta">
          {fileName(data.fileA)} vs {fileName(data.fileB)}
        </span>
        {typeof data.tolerance === 'number' && (
          <span className="diff-card__tol">容差 {data.tolerance}mm</span>
        )}
      </div>

      <div className="diff-counts">
        <span className="diff-count diff-count--add">
          <Plus size={12} /> 新增 <b>{addedCount}</b>
        </span>
        <span className="diff-count diff-count--del">
          <Minus size={12} /> 删除 <b>{removedCount}</b>
        </span>
        <span className="diff-count diff-count--mod">
          <Pencil size={12} /> 修改 <b>{modifiedCount}</b>
        </span>
        <span className="diff-count diff-count--same">
          未变 <b>{data.unchangedCount ?? '—'}</b>
        </span>
      </div>

      {addedCount > 0 && <DiffSection title="新增线段" tone="add" icon={<Plus size={12} />} count={addedCount}>
        {added.map((s, i) => <SegmentRow key={`a-${i}`} seg={s} tone="add" />)}
      </DiffSection>}

      {removedCount > 0 && <DiffSection title="删除线段" tone="del" icon={<Minus size={12} />} count={removedCount}>
        {removed.map((s, i) => <SegmentRow key={`r-${i}`} seg={s} tone="del" />)}
      </DiffSection>}

      {modifiedCount > 0 && <DiffSection title="修改线段" tone="mod" icon={<Pencil size={12} />} count={modifiedCount}>
        {modified.map((m, i) => (
          <div key={`m-${i}`} className="diff-mod">
            <span className="diff-mod__layer">{m.layer || m.Layer || '未命名图层'}</span>
            <span className="diff-mod__shift">位移 {num(m.shift)}mm</span>
            <span className="diff-mod__delta">长度差 {num(m.lengthDelta)}mm</span>
            {m.before && (
              <span className="diff-mod__geo">
                前: ({num(m.before.StartX)}, {num(m.before.StartY)}) → ({num(m.before.EndX)}, {num(m.before.EndY)})
              </span>
            )}
            {m.after && (
              <span className="diff-mod__geo">
                后: ({num(m.after.StartX)}, {num(m.after.StartY)}) → ({num(m.after.EndX)}, {num(m.after.EndY)})
              </span>
            )}
          </div>
        ))}
      </DiffSection>}
    </div>
  )
}

/** 可折叠的分组（新增条目多时不刷屏） */
function DiffSection({
  title, tone, icon, count, children,
}: {
  title: string
  tone: 'add' | 'del' | 'mod'
  icon: React.ReactNode
  count: number
  children: React.ReactNode
}) {
  const [open, setOpen] = useState(count <= 5)
  return (
    <div className={`diff-section diff-section--${tone}`}>
      <button className="diff-section__head" onClick={() => setOpen((v) => !v)}>
        {open ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
        {icon}
        <span>{title}</span>
        <span className="diff-section__count">({count})</span>
      </button>
      {open && <div className="diff-section__body">{children}</div>}
    </div>
  )
}
