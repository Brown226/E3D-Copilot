/**
 * DiffView — 数据修改前后对比视图
 * 支持统一 diff 展示 + 复制新内容
 */

import { useState, useMemo } from 'react'
import { Copy, Check } from 'lucide-react'

interface DiffLine {
  type: 'add' | 'remove' | 'context'
  content: string
  oldLineNum?: number
  newLineNum?: number
}

interface DiffViewProps {
  oldText: string
  newText: string
  fileName?: string
}

function parseDiff(oldText: string, newText: string): DiffLine[] {
  const oldLines = oldText.split('\n')
  const newLines = newText.split('\n')
  const result: DiffLine[] = []
  let oldIdx = 0
  let newIdx = 0

  while (oldIdx < oldLines.length || newIdx < newLines.length) {
    const oldLine = oldLines[oldIdx]
    const newLine = newLines[newIdx]

    if (oldIdx >= oldLines.length) {
      result.push({ type: 'add', content: newLine, newLineNum: newIdx + 1 })
      newIdx++
    } else if (newIdx >= newLines.length) {
      result.push({ type: 'remove', content: oldLine, oldLineNum: oldIdx + 1 })
      oldIdx++
    } else if (oldLine === newLine) {
      result.push({ type: 'context', content: oldLine, oldLineNum: oldIdx + 1, newLineNum: newIdx + 1 })
      oldIdx++
      newIdx++
    } else {
      // 向前查找匹配
      let foundInNew = -1
      for (let i = 1; i < 5 && newIdx + i < newLines.length; i++) {
        if (newLines[newIdx + i] === oldLine) { foundInNew = i; break }
      }
      let foundInOld = -1
      for (let i = 1; i < 5 && oldIdx + i < oldLines.length; i++) {
        if (oldLines[oldIdx + i] === newLine) { foundInOld = i; break }
      }

      if (foundInNew >= 0 && (foundInOld < 0 || foundInNew <= foundInOld)) {
        for (let i = 0; i < foundInNew; i++) {
          result.push({ type: 'add', content: newLines[newIdx + i], newLineNum: newIdx + i + 1 })
        }
        newIdx += foundInNew
      } else if (foundInOld >= 0) {
        for (let i = 0; i < foundInOld; i++) {
          result.push({ type: 'remove', content: oldLines[oldIdx + i], oldLineNum: oldIdx + i + 1 })
        }
        oldIdx += foundInOld
      } else {
        result.push({ type: 'remove', content: oldLine, oldLineNum: oldIdx + 1 })
        result.push({ type: 'add', content: newLine, newLineNum: newIdx + 1 })
        oldIdx++
        newIdx++
      }
    }
  }
  return result
}

export function DiffView({ oldText, newText, fileName }: DiffViewProps) {
  const [copied, setCopied] = useState(false)
  const lines = useMemo(() => parseDiff(oldText, newText), [oldText, newText])
  const addedCount = lines.filter((l) => l.type === 'add').length
  const removedCount = lines.filter((l) => l.type === 'remove').length

  const handleCopy = () => {
    navigator.clipboard.writeText(newText).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    })
  }

  return (
    <div className="rounded-lg border border-slate-200 dark:border-slate-700 overflow-hidden bg-white dark:bg-slate-800">
      {/* 头部 */}
      <div className="flex items-center justify-between px-3 py-2 bg-slate-50 dark:bg-slate-700/50 border-b border-slate-200 dark:border-slate-700">
        <div className="flex items-center gap-2">
          {fileName && <span className="text-xs font-mono text-slate-500 dark:text-slate-400">{fileName}</span>}
          <span className="text-xs text-emerald-600 dark:text-emerald-400">+{addedCount}</span>
          <span className="text-xs text-red-600 dark:text-red-400">-{removedCount}</span>
        </div>
        <button
          onClick={handleCopy}
          className="flex items-center gap-1 text-xs text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors px-2 py-1 rounded hover:bg-slate-200 dark:hover:bg-slate-600"
        >
          {copied ? <Check className="w-3 h-3" /> : <Copy className="w-3 h-3" />}
          {copied ? '已复制' : '复制新内容'}
        </button>
      </div>

      {/* Diff 内容 */}
      <div className="max-h-[400px] overflow-y-auto">
        {lines.map((line, i) => (
          <div
            key={i}
            className={`flex font-mono text-xs leading-5 ${
              line.type === 'add'
                ? 'bg-emerald-50 dark:bg-emerald-900/20 text-emerald-800 dark:text-emerald-300'
                : line.type === 'remove'
                ? 'bg-red-50 dark:bg-red-900/20 text-red-800 dark:text-red-300'
                : 'text-slate-600 dark:text-slate-400'
            }`}
          >
            <span className="w-12 shrink-0 text-right pr-2 text-slate-400 dark:text-slate-500 select-none border-r border-slate-200 dark:border-slate-700">
              {line.oldLineNum ?? ''}
            </span>
            <span className="w-12 shrink-0 text-right pr-2 text-slate-400 dark:text-slate-500 select-none border-r border-slate-200 dark:border-slate-700">
              {line.newLineNum ?? ''}
            </span>
            <span className="w-6 shrink-0 text-center select-none">
              {line.type === 'add' ? '+' : line.type === 'remove' ? '-' : ' '}
            </span>
            <span className="flex-1 px-2 whitespace-pre-wrap break-all">{line.content}</span>
          </div>
        ))}
      </div>
    </div>
  )
}
