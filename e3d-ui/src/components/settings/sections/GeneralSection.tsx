/**
 * GeneralSection — 通用设置
 * 1. 默认模式（Plan/Act）
 * 2. 自动审批设置
 * 3. 通知设置
 */

import { useState } from 'react'
import { useChatStore } from '@/store/useChatStore'

interface SettingToggleProps {
  label: string
  description?: string
  checked: boolean
  onChange: (v: boolean) => void
  disabled?: boolean
}

function SettingToggle({ label, description, checked, onChange, disabled }: SettingToggleProps) {
  return (
    <div className="flex items-start justify-between py-3">
      <div className="flex-1 min-w-0 mr-4">
        <p className="text-sm font-medium text-slate-800 dark:text-slate-100">{label}</p>
        {description && (
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">{description}</p>
        )}
      </div>
      <button
        role="switch"
        aria-checked={checked}
        disabled={disabled}
        onClick={() => onChange(!checked)}
        className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed ${
          checked ? 'bg-blue-600' : 'bg-slate-300 dark:bg-slate-600'
        }`}
      >
        <span
          className={`pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow transform transition duration-200 ease-in-out ${
            checked ? 'translate-x-5' : 'translate-x-0'
          }`}
        />
      </button>
    </div>
  )
}

export default function GeneralSection() {
  const currentModel = useChatStore((s) => s.currentModel)
  const bridgeConnected = useChatStore((s) => s.bridgeConnected)

  // 设置状态（从 store 或 bridge 读取）
  const [defaultMode, setDefaultMode] = useState<'plan' | 'act'>('act')
  const [autoApproveTools, setAutoApproveTools] = useState(false)
  const [autoApproveEdits, setAutoApproveEdits] = useState(false)
  const [notifications, setNotifications] = useState(true)
  const [soundEnabled, setSoundEnabled] = useState(false)

  const handleSaveSetting = async (key: string, value: string | boolean | number) => {
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      if (bridge.isAvailable()) {
        await bridge.saveSetting(key, String(value))
      } else {
        localStorage.setItem(`e3d-setting-${key}`, JSON.stringify(value))
      }
    } catch {
      localStorage.setItem(`e3d-setting-${key}`, JSON.stringify(value))
    }
  }

  return (
    <div className="space-y-6">
      {/* 当前模型信息 */}
      <div className="p-4 rounded-xl bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700">
        <div className="flex items-center gap-3">
          <div className={`w-2.5 h-2.5 rounded-full ${bridgeConnected ? 'bg-emerald-500' : 'bg-red-500'}`} />
          <div>
            <p className="text-sm font-medium text-slate-800 dark:text-slate-100">
              当前模型: <span className="text-blue-600 dark:text-blue-400">{currentModel || '未选择'}</span>
            </p>
            <p className="text-xs text-slate-500 dark:text-slate-400">
              {bridgeConnected ? '已连接到 E3D' : '未连接'}
            </p>
          </div>
        </div>
      </div>

      {/* 默认模式 */}
      <div>
        <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200 mb-3">默认模式</h3>
        <div className="space-y-1">
          <div className="flex gap-2">
            {(['act', 'plan'] as const).map((mode) => (
              <button
                key={mode}
                onClick={() => {
                  setDefaultMode(mode)
                  handleSaveSetting('defaultMode', mode)
                }}
                className={`flex-1 px-4 py-2.5 rounded-lg text-sm font-medium transition-all ${
                  defaultMode === mode
                    ? 'bg-blue-600 text-white shadow-md'
                    : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 hover:bg-slate-200 dark:hover:bg-slate-700'
                }`}
              >
                {mode === 'act' ? '⚡ Act 模式' : '📋 Plan 模式'}
              </button>
            ))}
          </div>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-2">
            Act 模式：直接执行操作 · Plan 模式：先规划再执行
          </p>
        </div>
      </div>

      {/* 自动审批 */}
      <div>
        <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200 mb-1">自动审批</h3>
        <div className="divide-y divide-slate-200 dark:divide-slate-700">
          <SettingToggle
            label="自动批准工具调用"
            description="跳过工具执行确认，直接允许 AI 使用工具"
            checked={autoApproveTools}
            onChange={(v) => {
              setAutoApproveTools(v)
              handleSaveSetting('autoApproveTools', v)
            }}
          />
          <SettingToggle
            label="自动批准文件编辑"
            description="跳过文件修改确认，允许 AI 直接修改文件"
            checked={autoApproveEdits}
            onChange={(v) => {
              setAutoApproveEdits(v)
              handleSaveSetting('autoApproveEdits', v)
            }}
          />
        </div>
      </div>

      {/* 通知 */}
      <div>
        <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200 mb-1">通知</h3>
        <div className="divide-y divide-slate-200 dark:divide-slate-700">
          <SettingToggle
            label="桌面通知"
            description="任务完成或需要确认时显示系统通知"
            checked={notifications}
            onChange={(v) => {
              setNotifications(v)
              handleSaveSetting('notifications', v)
            }}
          />
          <SettingToggle
            label="提示音"
            description="任务完成时播放提示音"
            checked={soundEnabled}
            onChange={(v) => {
              setSoundEnabled(v)
              handleSaveSetting('soundEnabled', v)
            }}
          />
        </div>
      </div>
    </div>
  )
}
