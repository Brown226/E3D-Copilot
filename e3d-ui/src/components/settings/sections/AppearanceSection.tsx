/**
 * AppearanceSection — 外观设置
 * 1. 主题（亮色/暗色/跟随系统）
 * 2. 字号（小/中/大/特大）
 * 3. 字体（默认/等宽）
 */

import { useState, useEffect, useCallback } from 'react'
import { Monitor, Sun, Moon, Type } from 'lucide-react'

// ── 主题 ──
type Theme = 'light' | 'dark' | 'system'

const THEME_OPTIONS: { id: Theme; label: string; icon: React.ReactNode }[] = [
  { id: 'light', label: '亮色', icon: <Sun className="w-5 h-5" /> },
  { id: 'dark', label: '暗色', icon: <Moon className="w-5 h-5" /> },
  { id: 'system', label: '跟随系统', icon: <Monitor className="w-5 h-5" /> },
]

// ── 字号 ──
type TextSize = 'sm' | 'md' | 'lg' | 'xl'

const TEXT_SIZE_OPTIONS: { id: TextSize; label: string; preview: string }[] = [
  { id: 'sm', label: '小', preview: '14px' },
  { id: 'md', label: '中', preview: '16px' },
  { id: 'lg', label: '大', preview: '18px' },
  { id: 'xl', label: '特大', preview: '20px' },
]

// ── 字体 ──
type FontFamily = 'default' | 'mono'

const FONT_OPTIONS: { id: FontFamily; label: string; family: string }[] = [
  { id: 'default', label: '默认', family: 'system-ui, -apple-system, sans-serif' },
  { id: 'mono', label: '等宽', family: 'JetBrains Mono, Fira Code, Consolas, monospace' },
]

function getStoredTheme(): Theme {
  try {
    return (localStorage.getItem('e3d-theme') as Theme) || 'dark'
  } catch { return 'dark' }
}

function getStoredTextSize(): TextSize {
  try {
    return (localStorage.getItem('e3d-text-size') as TextSize) || 'md'
  } catch { return 'md' }
}

function applyTheme(theme: Theme) {
  const root = document.documentElement
  if (theme === 'dark') {
    root.classList.add('dark')
    root.classList.remove('light')
  } else if (theme === 'light') {
    root.classList.add('light')
    root.classList.remove('dark')
  } else {
    // system
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches
    if (prefersDark) {
      root.classList.add('dark')
      root.classList.remove('light')
    } else {
      root.classList.add('light')
      root.classList.remove('dark')
    }
  }
  localStorage.setItem('e3d-theme', theme)
}

function applyTextSize(size: TextSize) {
  const root = document.documentElement
  const sizes = { sm: '14px', md: '16px', lg: '18px', xl: '20px' }
  root.style.fontSize = sizes[size]
  localStorage.setItem('e3d-text-size', size)
}

// 字号映射到数字（后端 CopilotConfig.UiConfig.FontSize 使用数字）
const TEXT_SIZE_TO_NUMBER: Record<TextSize, number> = { sm: 14, md: 16, lg: 18, xl: 20 }

// 同步设置到后端
function syncToBackend(key: string, value: string) {
  import('@/services/bridgeService').then(({ default: bridge }) => {
    if (bridge.isAvailable()) {
      bridge.saveSetting(key, value)
    }
  })
}

export default function AppearanceSection() {
  const [theme, setTheme] = useState<Theme>(getStoredTheme)
  const [textSize, setTextSize] = useState<TextSize>(getStoredTextSize)

  // 初始化应用主题
  useEffect(() => {
    applyTheme(theme)
  }, [])

  const handleThemeChange = useCallback((t: Theme) => {
    setTheme(t)
    applyTheme(t)
    syncToBackend('theme', t)
  }, [])

  const handleTextSizeChange = useCallback((s: TextSize) => {
    setTextSize(s)
    applyTextSize(s)
    syncToBackend('fontSize', String(TEXT_SIZE_TO_NUMBER[s]))
  }, [])

  const handleFontChange = useCallback((opt: { id: FontFamily; label: string; family: string }) => {
    localStorage.setItem('e3d-font', opt.id)
    document.documentElement.style.setProperty('--font-family', opt.family)
    syncToBackend('fontFamily', opt.id)
  }, [])

  return (
    <div className="space-y-6">
      {/* 主题 */}
      <div>
        <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200 mb-3">主题</h3>
        <div className="grid grid-cols-3 gap-3">
          {THEME_OPTIONS.map((opt) => (
            <button
              key={opt.id}
              onClick={() => handleThemeChange(opt.id)}
              className={`flex flex-col items-center gap-2 p-4 rounded-xl border-2 transition-all ${
                theme === opt.id
                  ? 'border-blue-500 bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300'
                  : 'border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-600 dark:text-slate-400 hover:border-slate-300 dark:hover:border-slate-600'
              }`}
            >
              {opt.icon}
              <span className="text-sm font-medium">{opt.label}</span>
            </button>
          ))}
        </div>
      </div>

      {/* 字号 */}
      <div>
        <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200 mb-3">字号</h3>
        <div className="flex gap-2">
          {TEXT_SIZE_OPTIONS.map((opt) => (
            <button
              key={opt.id}
              onClick={() => handleTextSizeChange(opt.id)}
              className={`flex-1 py-3 rounded-lg text-sm font-medium transition-all ${
                textSize === opt.id
                  ? 'bg-blue-600 text-white shadow-md'
                  : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 hover:bg-slate-200 dark:hover:bg-slate-700'
              }`}
            >
              <span className="block">{opt.label}</span>
              <span className="block text-xs opacity-70 mt-0.5">{opt.preview}</span>
            </button>
          ))}
        </div>
      </div>

      {/* 字体 */}
      <div>
        <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200 mb-3">字体</h3>
        <div className="space-y-2">
          {FONT_OPTIONS.map((opt) => (
            <button
              key={opt.id}
              onClick={() => handleFontChange(opt)}
              className="w-full flex items-center justify-between px-4 py-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-colors text-left"
            >
              <span className="text-sm text-slate-800 dark:text-slate-100">{opt.label}</span>
              <span className="text-xs text-slate-500 dark:text-slate-400" style={{ fontFamily: opt.family }}>
                Aa Bb 123
              </span>
            </button>
          ))}
        </div>
      </div>

      {/* 预览 */}
      <div className="p-4 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/50">
        <div className="flex items-center gap-2 mb-2">
          <Type className="w-4 h-4 text-slate-500" />
          <span className="text-xs font-medium text-slate-500 dark:text-slate-400">预览</span>
        </div>
        <p className="text-slate-800 dark:text-slate-100">
          这是预览文本。E小智 AI 助手将帮助您在 E3D 中完成各种设计任务。
        </p>
        <p className="text-xs text-slate-500 dark:text-slate-400 mt-2">
          当前: {THEME_OPTIONS.find((t) => t.id === theme)?.label} · {TEXT_SIZE_OPTIONS.find((t) => t.id === textSize)?.label}
        </p>
      </div>
    </div>
  )
}
