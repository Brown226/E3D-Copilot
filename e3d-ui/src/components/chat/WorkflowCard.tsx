/**
 * WorkflowCard — design_workflow 的流程可视化
 *
 * 演示立意是「设计全流程管理」，核心卖点是步骤可见、可追溯。
 * 后端 Data = { platformSynced: bool, steps: [{step, name, success, summary}] }
 *
 * 只做展示，不在前端做任何推断：步骤名与结论都来自后端，前端不重算。
 */

import { CheckCircle2, XCircle, Circle } from 'lucide-react'

interface WorkflowStep {
  step?: number
  name?: string
  success?: boolean
  summary?: string
}

interface WorkflowData {
  platformSynced?: boolean
  steps?: WorkflowStep[]
}

function asWorkflowData(meta: unknown): WorkflowData | null {
  if (!meta || typeof meta !== 'object') return null
  const m = meta as Record<string, unknown>
  if (!Array.isArray(m.steps)) return null
  return m as WorkflowData
}

/** 六步是固定语义，即使后端只返回部分步骤，也用统一编号展示 */
const STEP_LABELS: Record<number, string> = {
  1: '解析 A 版图纸',
  2: 'A 版建模',
  3: '回写模型清单',
  4: '解析 B 版图纸',
  5: 'A/B 差异比对',
  6: '回写差异清单',
}

export function WorkflowCard({ meta }: { meta: unknown }) {
  const data = asWorkflowData(meta)
  if (!data) return null

  const steps = data.steps ?? []
  const done = steps.filter((s) => s.success).length
  const failed = steps.filter((s) => !s.success).length

  return (
    <div className="wf-card">
      <div className="wf-card__head">
        <span className="wf-card__title">🏗 设计全流程</span>
        <span className="wf-card__badge">{done}/{steps.length} 步完成</span>
        {failed > 0 && <span className="wf-card__badge wf-card__badge--err">{failed} 步失败</span>}
        <span
          className={
            'wf-card__platform' + (data.platformSynced ? ' wf-card__platform--ok' : ' wf-card__platform--off')
          }
          title={
            data.platformSynced
              ? '平台回写全部成功'
              : '平台未就绪，本地步骤已完成，平台侧记录缺失'
          }
        >
          {data.platformSynced ? '✓ 平台已同步' : '⚠ 平台未同步'}
        </span>
      </div>

      <div className="wf-steps">
        {steps.map((s) => {
          const idx = typeof s.step === 'number' ? s.step : 0
          const label = s.name || STEP_LABELS[idx] || `步骤 ${idx}`
          const Icon = s.success === false ? XCircle : s.success ? CheckCircle2 : Circle
          const state = s.success === false ? 'err' : s.success ? 'ok' : 'idle'
          return (
            <div key={idx} className={`wf-step wf-step--${state}`}>
              <span className="wf-step__icon">
                <Icon size={13} />
              </span>
              <span className="wf-step__idx">{idx || '·'}</span>
              <span className="wf-step__name">{label}</span>
              <span className="wf-step__summary">{s.summary || ''}</span>
            </div>
          )
        })}
      </div>
    </div>
  )
}
