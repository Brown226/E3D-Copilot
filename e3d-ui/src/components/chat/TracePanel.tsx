import { useCallback, useEffect, useState } from 'react'
import { Activity, X, RefreshCw } from 'lucide-react'
import bridge, { MessageTypes } from '@/services/bridgeService'

/**
 * 执行诊断面板 —— 不用导出文件、不用复制粘贴，直接看最近一轮到底慢在哪。
 *
 * 背景：E小智 原先只把 trace 写到
 *   %LOCALAPPDATA%\E3DCopilot\traces\*.json
 * 后端也有 trace:latest / trace:list / trace:read 三个接口，
 * 但前端从未调用过它们，所以用户完全看不到执行过程。
 *
 * 这里把「最近一轮」的结构化数据直接摊开：
 *   - 耗时归因（LLM / 工具 / 审批 / 其它）
 *   - 每步明细（首 token、流式节奏、上下文规模）
 *   - 工具调用（名称、耗时、成功与否、审批等待）
 * 目的是让"卡在哪一步"当场可见，而不是靠事后翻日志。
 */

interface TraceToolCall {
  name?: string
  durationMs?: number
  success?: boolean
  approvalMs?: number
  error?: string
}

interface TraceStreamStats {
  chunks?: number
  spanMs?: number
  medianGapMs?: number
  maxGapMs?: number
  stalls?: number
  charsPerSec?: number
}

interface TraceStep {
  step?: number
  durationMs?: number
  llmMs?: number
  ttftMs?: number
  approvalMs?: number
  toolCalls?: TraceToolCall[]
  stream?: TraceStreamStats
  context?: { messages?: number; tools?: number; toolSchemaChars?: number }
}

interface TraceDoc {
  startTime?: string
  endTime?: string
  model?: string
  userInput?: string
  outcome?: string
  totalSteps?: number
  steps?: TraceStep[]
}

function fmtMs(ms?: number): string {
  if (ms == null || ms < 0) return '—'
  if (ms < 1000) return `${ms}ms`
  return `${(ms / 1000).toFixed(1)}s`
}

/** 单步耗时构成的百分比 */
function pct(part: number, total: number): number {
  if (!total) return 0
  return Math.round((part * 100) / total)
}

export function TracePanel() {
  const [open, setOpen] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [trace, setTrace] = useState<TraceDoc | null>(null)
  const [path, setPath] = useState<string>('')

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      // 后端 trace:latest 返回的是 { enabled, path, fileName, content }
      // 其中 content 是 trace 文件的「原始 JSON 字符串」，不是对象，需自行解析
      const resp = (await bridge.sendAndWait(MessageTypes.TraceLatest, {}, 15000)) as {
        enabled?: boolean
        trace?: TraceDoc
        content?: string
        path?: string
        fileName?: string
        message?: string
      }

      let doc: TraceDoc | null = null
      if (resp?.trace) {
        doc = resp.trace
      } else if (typeof resp?.content === 'string' && resp.content.trim()) {
        try {
          // 文件带 UTF-8 BOM 时 JSON.parse 会直接报错，先剥掉
          doc = JSON.parse(resp.content.replace(/^﻿/, '')) as TraceDoc
        } catch {
          setTrace(null)
          setError('执行记录格式无法解析（文件可能损坏）')
          setPath(resp.path || '')
          return
        }
      }

      if (doc) {
        setTrace(doc)
        setPath(resp.path || '')
      } else {
        setTrace(null)
        setError(resp?.message || '暂无执行记录，先进行一次对话')
        setPath('')
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : '读取执行记录失败')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (open) void load()
  }, [open, load])

  const steps = trace?.steps || []
  const totalMs = steps.reduce((s, x) => s + (x.durationMs || 0), 0)
  const toolMs = steps.reduce(
    (s, x) => s + (x.toolCalls || []).reduce((a, t) => a + (t.durationMs || 0), 0),
    0,
  )
  const approvalMs = steps.reduce((s, x) => s + (x.approvalMs || 0), 0)

  // 旧版 trace 没有 llmMs 字段。此时不能把 0 当成「真的没花时间」显示，
  // 否则会出现「总耗时 125.9s 但 LLM 0ms (0%)」这种自相矛盾的读数。
  // 用 duration - 工具 - 审批 推算，并明确标注是推算值。
  const hasLlmField = steps.some((x) => x.llmMs != null)
  const llmMs = hasLlmField
    ? steps.reduce((s, x) => s + (x.llmMs || 0), 0)
    : Math.max(0, totalMs - toolMs - approvalMs)
  const otherMs = Math.max(0, totalMs - llmMs - toolMs - approvalMs)

  const stepLlmMs = (x: TraceStep): number => {
    if (x.llmMs != null) return x.llmMs
    const st = (x.toolCalls || []).reduce((a, t) => a + (t.durationMs || 0), 0)
    return Math.max(0, (x.durationMs || 0) - st - (x.approvalMs || 0))
  }

  return (
    <>
      <button
        type="button"
        className="composer-auto-toggle"
        onClick={() => setOpen(true)}
        title="查看上一轮执行的耗时归因（不用导出文件）"
      >
        <Activity size={15} />
        <span>诊断</span>
      </button>

      {open && (
        <div className="trace-overlay" onClick={() => setOpen(false)}>
          <div className="trace-panel" onClick={(e) => e.stopPropagation()}>
            <div className="trace-panel__head">
              <div className="trace-panel__title">
                <Activity size={16} />
                <span>执行诊断</span>
                {trace?.startTime && <span className="trace-panel__time">{trace.startTime}</span>}
              </div>
              <div className="trace-panel__actions">
                <button className="trace-panel__icon" onClick={() => void load()} title="刷新">
                  <RefreshCw size={14} className={loading ? 'trace-spin' : ''} />
                </button>
                <button className="trace-panel__icon" onClick={() => setOpen(false)} title="关闭">
                  <X size={14} />
                </button>
              </div>
            </div>

            <div className="trace-panel__body">
              {loading && <div className="trace-empty">正在读取…</div>}

              {!loading && error && <div className="trace-empty">{error}</div>}

              {!loading && !error && trace && (
                <>
                  <div className="trace-meta">
                    <div>
                      <span className="trace-meta__k">模型</span>
                      <span className="trace-meta__v">{trace.model || '—'}</span>
                    </div>
                    <div>
                      <span className="trace-meta__k">结果</span>
                      <span className="trace-meta__v">{trace.outcome || '—'}</span>
                    </div>
                    <div>
                      <span className="trace-meta__k">步数</span>
                      <span className="trace-meta__v">{trace.totalSteps ?? steps.length}</span>
                    </div>
                  </div>

                  {trace.userInput && (
                    <div className="trace-input" title={trace.userInput}>
                      {trace.userInput.length > 120
                        ? trace.userInput.slice(0, 120) + '…'
                        : trace.userInput}
                    </div>
                  )}

                  {/* 耗时归因条 */}
                  <div className="trace-attr">
                    <div className="trace-attr__row">
                      <span>总耗时</span>
                      <strong>{fmtMs(totalMs)}</strong>
                    </div>

                    {!hasLlmField && (
                      <div className="trace-legacy-note">
                        这条记录由旧版本生成，缺少分段耗时字段，下列数值按「总时长 − 工具 − 审批」推算。
                      </div>
                    )}

                    <div className="trace-attr__bar">
                      {llmMs > 0 && (
                        <div
                          // 旧记录的 llmMs 是推算值，用斜纹区分「实测」与「推算」，
                          // 避免让人以为这就是精确归因
                          className={
                            'trace-attr__seg trace-attr__seg--llm' +
                            (hasLlmField ? '' : ' trace-attr__seg--derived')
                          }
                          style={{ width: `${pct(llmMs, totalMs)}%` }}
                          title={hasLlmField ? `LLM ${fmtMs(llmMs)}` : `LLM（推算）${fmtMs(llmMs)}`}
                        />
                      )}
                      {toolMs > 0 && (
                        <div
                          className="trace-attr__seg trace-attr__seg--tool"
                          style={{ width: `${pct(toolMs, totalMs)}%` }}
                          title={`工具 ${fmtMs(toolMs)}`}
                        />
                      )}
                      {approvalMs > 0 && (
                        <div
                          className="trace-attr__seg trace-attr__seg--appr"
                          style={{ width: `${pct(approvalMs, totalMs)}%` }}
                          title={`审批 ${fmtMs(approvalMs)}`}
                        />
                      )}
                      {otherMs > 0 && (
                        <div
                          className="trace-attr__seg trace-attr__seg--other"
                          style={{ width: `${pct(otherMs, totalMs)}%` }}
                          title={`其它（等待/调度/渲染）${fmtMs(otherMs)}`}
                        />
                      )}
                    </div>

                    <div className="trace-attr__grid">
                      <div>
                        <i className="dot dot--llm" />
                        <span>LLM</span>
                        <strong>{fmtMs(llmMs)}</strong>
                        <em>{pct(llmMs, totalMs)}%</em>
                      </div>
                      <div>
                        <i className="dot dot--tool" />
                        <span>工具</span>
                        {/* 没有工具调用时显示 — 而不是 0ms，避免被读成"工具没花时间" */}
                        <strong>{toolMs > 0 ? fmtMs(toolMs) : '—'}</strong>
                        <em>{toolMs > 0 ? `${pct(toolMs, totalMs)}%` : '无调用'}</em>
                      </div>
                      {approvalMs > 0 && (
                        <div>
                          <i className="dot dot--appr" />
                          <span>审批</span>
                          <strong>{fmtMs(approvalMs)}</strong>
                          <em>{pct(approvalMs, totalMs)}%</em>
                        </div>
                      )}
                      {otherMs > 0 && (
                        <div>
                          <i className="dot dot--other" />
                          <span>其它</span>
                          <strong>{fmtMs(otherMs)}</strong>
                          <em>{pct(otherMs, totalMs)}%</em>
                        </div>
                      )}
                    </div>
                  </div>

                  {/* 分步明细 */}
                  <div className="trace-steps">
                    {steps.map((s, i) => {
                      const st = s.stream
                      const tools = s.toolCalls || []
                      return (
                        <div className="trace-step" key={i}>
                          <div className="trace-step__head">
                            <span className="trace-step__no">步骤 {s.step ?? i + 1}</span>
                            <span className="trace-step__dur">{fmtMs(s.durationMs)}</span>
                          </div>
                          <div className="trace-step__facts">
                            <span>LLM {fmtMs(stepLlmMs(s))}</span>
                            {s.ttftMs != null && s.ttftMs >= 0 && (
                              <span>首字 {fmtMs(s.ttftMs)}</span>
                            )}
                            {st && (
                              <>
                                <span>{st.chunks || 0} 块</span>
                                <span
                                  className={(st.stalls || 0) > 0 ? 'trace-warn' : undefined}
                                  title="间隔 >300ms 的次数"
                                >
                                  卡顿 {st.stalls || 0}
                                </span>
                                <span
                                  className={(st.maxGapMs || 0) >= 2000 ? 'trace-warn' : undefined}
                                >
                                  最长间隔 {fmtMs(st.maxGapMs)}
                                </span>
                              </>
                            )}
                            {s.context && (
                              <span title="本步请求规模">
                                {s.context.messages ?? 0} 条 / {s.context.tools ?? 0} 工具
                              </span>
                            )}
                          </div>

                          {tools.length > 0 && (
                            <div className="trace-step__tools">
                              {tools.map((t, j) => (
                                <div
                                  className={`trace-tool${t.success === false ? ' trace-tool--fail' : ''}`}
                                  key={j}
                                >
                                  <span className="trace-tool__name">{t.name || '工具'}</span>
                                  <span className="trace-tool__dur">{fmtMs(t.durationMs)}</span>
                                  {(t.approvalMs || 0) > 0 && (
                                    <span className="trace-tool__appr">审批 {fmtMs(t.approvalMs)}</span>
                                  )}
                                  {t.success === false && <span className="trace-tool__badge">失败</span>}
                                </div>
                              ))}
                            </div>
                          )}
                        </div>
                      )
                    })}
                  </div>

                  {path && <div className="trace-path">原始文件：{path}</div>}
                </>
              )}
            </div>
          </div>
        </div>
      )}
    </>
  )
}
