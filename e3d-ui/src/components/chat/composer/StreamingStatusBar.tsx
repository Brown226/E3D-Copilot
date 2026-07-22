/**
 * 流式状态栏组件
 * 职责：运行中显示旋转词 + 时长 + token + 停止按钮
 */

import { useState, useEffect } from 'react'
import { Square } from 'lucide-react'

// ── 旋转词 ──
const SPINNER_WORDS = ['嘎吱运算', '飞速思考', '搜索中', '分析中', '推理中', '生成中']

/** 格式化 token 数量 */
export function fmtTokens(n: number): string {
  if (n >= 1000) return (n / 1000).toFixed(1).replace(/\.0$/, '') + 'k'
  return String(n)
}

/** 格式化运行时长 */
export function fmtElapsed(ms: number): string {
  const s = Math.floor(ms / 1000)
  if (s < 60) return `${s}s`
  return `${Math.floor(s / 60)}m ${s % 60}s`
}

/** 每秒触发的 tick hook（用于实时更新运行时长） */
function useTick(on: boolean): number {
  const [, setN] = useState(0)
  useEffect(() => {
    if (!on) return
    const id = window.setInterval(() => setN((n) => n + 1), 1000)
    return () => window.clearInterval(id)
  }, [on])
  return Date.now()
}

interface StreamingStatusBarProps {
  isStreaming: boolean
  turnStartAt: number | null
  turnTokens: number
  onCancel: () => void
}

export function StreamingStatusBar({ isStreaming, turnStartAt, turnTokens, onCancel }: StreamingStatusBarProps) {
  const now = useTick(isStreaming)

  if (!isStreaming || !turnStartAt) return null

  const elapsedMs = Math.max(0, now - turnStartAt)
  const word = SPINNER_WORDS[Math.floor(elapsedMs / 3000) % SPINNER_WORDS.length]
  const tok = turnTokens > 0 ? ` · ↓ ${fmtTokens(turnTokens)} tokens` : ''
  const statusText = `${word}… ${fmtElapsed(elapsedMs)}${tok}`

  return (
    <div className="composer-toolbar composer-toolbar--status-only">
      <div className="composer-runstatus" role="status" aria-live="polite">
        <span className="composer-runstatus__dot" />
        <span className="composer-runstatus__text">{statusText}</span>
        <button className="composer-runstatus__stop" type="button" onClick={onCancel}>
          <Square size={10} fill="currentColor" />
          <span>停止</span>
        </button>
      </div>
    </div>
  )
}
