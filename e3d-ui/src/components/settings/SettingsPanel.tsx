/**
 * SettingsPanel — 多 Tab 设置面板
 * 左侧 Tab 导航 + 右侧内容区
 * Tabs: General / Models / Skills / Appearance / Memory
 */

import { useEffect, useState } from 'react'
import { X, Settings, User, Box, Palette, Brain, Zap } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'

// ── Tab 定义 ──
type SettingsTab = 'general' | 'models' | 'skills' | 'appearance' | 'memory'

interface TabDef {
  id: SettingsTab
  label: string
  icon: React.ReactNode
}

const TABS: TabDef[] = [
  { id: 'general', label: '通用', icon: <User className="w-4 h-4" /> },
  { id: 'models', label: '模型', icon: <Box className="w-4 h-4" /> },
  { id: 'skills', label: '技能', icon: <Zap className="w-4 h-4" /> },
  { id: 'appearance', label: '外观', icon: <Palette className="w-4 h-4" /> },
  { id: 'memory', label: '记忆', icon: <Brain className="w-4 h-4" /> },
]

// ── Lazy load sections ──
const GeneralSection = () => import('./sections/GeneralSection')
const ModelsSection = () => import('./sections/ModelsSection')
const SkillsSection = () => import('./sections/SkillsSection')
const AppearanceSection = () => import('./sections/AppearanceSection')
const MemorySection = () => import('./sections/MemorySection')

const sectionLoaders: Record<SettingsTab, () => Promise<{ default: React.ComponentType<any> }>> = {
  general: GeneralSection,
  models: ModelsSection,
  skills: SkillsSection,
  appearance: AppearanceSection,
  memory: MemorySection,
}

export default function SettingsPanel() {
  const showSettings = useChatStore((s) => s.showSettings)
  const toggleSettings = useChatStore((s) => s.toggleSettings)
  const [activeTab, setActiveTab] = useState<SettingsTab>('general')
  const [SectionComponent, setSectionComponent] = useState<React.ComponentType | null>(null)

  // ESC 键关闭
  useEffect(() => {
    if (!showSettings) return
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') toggleSettings()
    }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [showSettings, toggleSettings])

  // 切换 Tab 时懒加载组件
  useEffect(() => {
    if (!showSettings) return
    let cancelled = false
    sectionLoaders[activeTab]().then((mod) => {
      if (!cancelled) setSectionComponent(() => mod.default)
    })
    return () => { cancelled = true }
  }, [activeTab, showSettings])

  if (!showSettings) return null

  return (
    <div className="fixed inset-0 z-50">
      {/* 遮罩 */}
      <div
        className="absolute inset-0 bg-black/40 backdrop-blur-sm transition-opacity animate-in fade-in"
        onClick={toggleSettings}
      />

      {/* 面板 */}
      <div className="absolute inset-y-0 right-0 w-full max-w-3xl bg-white dark:bg-slate-900 shadow-2xl flex animate-in slide-in-from-right duration-300">
        {/* 左侧 Tab 导航 */}
        <nav className="w-48 shrink-0 border-r border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/50 flex flex-col">
          {/* 标题 */}
          <div className="flex items-center gap-2 px-4 py-4 border-b border-slate-200 dark:border-slate-700">
            <Settings className="w-4 h-4 text-slate-500 dark:text-slate-400" />
            <span className="text-sm font-semibold text-slate-700 dark:text-slate-200">设置</span>
          </div>

          {/* Tab 列表 */}
          <div className="flex-1 py-2">
            {TABS.map((tab) => (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`w-full flex items-center gap-3 px-4 py-2.5 text-sm transition-colors ${
                  activeTab === tab.id
                    ? 'bg-blue-50 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300 border-r-2 border-blue-600'
                    : 'text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-700/50'
                }`}
              >
                {tab.icon}
                <span>{tab.label}</span>
              </button>
            ))}
          </div>

          {/* 底部版本信息 */}
          <div className="px-4 py-3 border-t border-slate-200 dark:border-slate-700">
            <p className="text-xs text-slate-400 dark:text-slate-500">E小智 v2.0</p>
          </div>
        </nav>

        {/* 右侧内容区 */}
        <div className="flex-1 flex flex-col min-w-0">
          {/* 内容标题栏 */}
          <div className="flex items-center justify-between px-6 py-4 border-b border-slate-200 dark:border-slate-700">
            <h2 className="text-base font-semibold text-slate-800 dark:text-slate-100">
              {TABS.find((t) => t.id === activeTab)?.label}
            </h2>
            <button
              onClick={toggleSettings}
              className="p-1.5 text-slate-400 hover:text-slate-600 rounded-lg hover:bg-slate-100 transition-colors dark:hover:text-slate-200 dark:hover:bg-slate-700"
              title="关闭 (Esc)"
            >
              <X className="w-4 h-4" />
            </button>
          </div>

          {/* 内容区 */}
          <div className="flex-1 overflow-y-auto p-6">
            {SectionComponent ? (
              <SectionComponent />
            ) : (
              <div className="flex items-center justify-center h-32">
                <div className="w-5 h-5 border-2 border-blue-500 border-t-transparent rounded-full animate-spin" />
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
