/**
 * GeneralSection — 通用设置
 *
 * 这里曾经有 5 个开关（默认模式 Plan/Act、自动批准工具调用、自动批准文件编辑、
 * 桌面通知、提示音），已全部移除，原因都是同一个：**改了不生效**。
 *
 *   defaultMode        → 写入 config.Ui.DefaultMode，全项目无任何读取方
 *   autoApproveTools   → 写入 config.Safety.AutoApproveTools，无任何读取方
 *   autoApproveEdits   → 写入 config.Safety.AutoApproveEdits，无任何读取方
 *   notifications      → 写入 config.Ui.Notifications，前端无 Notification 调用
 *   soundEnabled       → 写入 config.Ui.SoundEnabled，前端无 Audio 调用
 *
 * 而且前三个与输入栏的四档权限选择器（计划模式 / 变更前确认 / 自主 / 完全访问）语义重叠，
 * 两处都能改会让人不知道该信哪一个。权限只在输入栏那一处维护。
 */

import { useState, useEffect, useRef } from 'react'
import { useChatStore } from '@/store/useChatStore'
import type { ToolApprovalMode } from '@/store/types'

const MODE_LABELS: Record<ToolApprovalMode, string> = {
  plan: '计划模式',
  ask: '变更前确认',
  auto: '自主模式',
  yolo: '完全访问',
}

const INSTRUCTIONS_KEY = 'e3d-custom-instructions'

export default function GeneralSection() {
  const currentModel = useChatStore((s) => s.currentModel)
  const bridgeConnected = useChatStore((s) => s.bridgeConnected)
  const toolApprovalMode = useChatStore((s) => s.toolApprovalMode)

  const [instructions, setInstructions] = useState(() => {
    try { return localStorage.getItem(INSTRUCTIONS_KEY) ?? '' } catch { return '' }
  })
  const [savedHint, setSavedHint] = useState(false)
  const debounceRef = useRef<number | null>(null)

  // 停止输入 800ms 后保存（settings:save → 后端注入系统提示词）
  useEffect(() => {
    if (debounceRef.current) window.clearTimeout(debounceRef.current)
    debounceRef.current = window.setTimeout(() => {
      try { localStorage.setItem(INSTRUCTIONS_KEY, instructions) } catch { /* 配额满忽略 */ }
      import('@/services/bridgeService').then(({ default: bridge }) => {
        bridge.send('settings:save', { key: 'customInstructions', value: instructions })
        setSavedHint(true)
        window.setTimeout(() => setSavedHint(false), 1500)
      }).catch(() => { /* bridge 不可用时仅本地暂存 */ })
    }, 800)
    return () => { if (debounceRef.current) window.clearTimeout(debounceRef.current) }
  }, [instructions])

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

      {/* 自定义指令（注入系统提示词，对齐主流 Agent 的 custom instructions） */}
      <div>
        <div className="flex items-center justify-between mb-1">
          <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200">自定义指令</h3>
          <span className={`text-xs transition-opacity ${savedHint ? 'opacity-100 text-emerald-500' : 'opacity-0'}`}>已保存 ✓</span>
        </div>
        <p className="text-xs text-slate-500 dark:text-slate-400 mb-2">
          每轮对话都会注入给 AI 的固定要求。例如：回复一律用中文；修改属性前先查询确认；项目命名规范等。
        </p>
        <textarea
          value={instructions}
          onChange={(e) => setInstructions(e.target.value)}
          placeholder={'例：\n- 回复使用中文\n- 所有元素名前缀 MYPROJ-\n- 修改前先用 query 确认目标'}
          rows={5}
          className="w-full px-3 py-2 text-sm rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800/60 text-slate-800 dark:text-slate-100 placeholder:text-slate-400 dark:placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500/40 resize-y"
        />
      </div>

      {/* 当前权限（只读展示，切换入口在输入栏） */}
      <div>
        <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200 mb-1">当前权限</h3>
        <div className="flex items-center justify-between py-3">
          <div className="flex-1 min-w-0 mr-4">
            <p className="text-sm font-medium text-slate-800 dark:text-slate-100">
              {MODE_LABELS[toolApprovalMode]}
            </p>
            <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
              在输入栏的权限按钮里切换（计划模式 / 变更前确认 / 自主模式 / 完全访问）
            </p>
          </div>
        </div>
      </div>
    </div>
  )
}
