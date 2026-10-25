/**
 * ContextMeter — 上下文占用指示条
 * 显示最近一次 LLM 请求的 prompt tokens 占当前模型窗口的百分比。
 * 数据来自 Usage 事件（stream_options.include_usage 链路），未收到前不渲染。
 * 配色分级：<50% 常规、50–80% 注意、>80% 紧张（压缩器即将介入）。
 */

import { memo } from 'react'
import { useChatStore } from '@/store/useChatStore'
import { fmtTokens } from './StreamingStatusBar'

function levelOf(pct: number): string {
  if (pct >= 80) return 'ctx-meter--danger'
  if (pct >= 50) return 'ctx-meter--warn'
  return ''
}

export const ContextMeter = memo(function ContextMeter() {
  const promptTokens = useChatStore((s) => s.lastPromptTokens)
  const contextWindow = useChatStore((s) => s.contextWindow)

  if (promptTokens <= 0 || contextWindow <= 0) return null

  const pct = Math.min(100, (promptTokens / contextWindow) * 100)
  const shownPct = pct < 1 ? '<1' : pct.toFixed(0)

  return (
    <span
      className={`ctx-meter ${levelOf(pct)}`}
      title={`上下文占用 ${promptTokens.toLocaleString()} / ${contextWindow.toLocaleString()} tokens（${pct.toFixed(1)}%）`}
    >
      <span className="ctx-meter__label">ctx {fmtTokens(promptTokens)}/{fmtTokens(contextWindow)}</span>
      <span className="ctx-meter__bar" role="progressbar" aria-valuenow={Math.round(pct)} aria-valuemin={0} aria-valuemax={100}>
        <span className="ctx-meter__fill" style={{ width: `${Math.max(2, pct)}%` }} />
      </span>
      <span className="ctx-meter__pct">{shownPct}%</span>
    </span>
  )
})
