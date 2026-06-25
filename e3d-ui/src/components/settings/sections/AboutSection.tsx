/**
 * AboutSection — 介绍（关于）
 *
 * 展示工具信息：
 * - 版本号（动态，从后端 config:sync 获取）
 * - 在线说明书链接（可配置，从 config.json 读取）
 * - 开发者信息
 */

import { useState, useEffect } from 'react'
import { ExternalLink, Info, User, Globe, Copy, Check } from 'lucide-react'

export default function AboutSection() {
  const [version, setVersion] = useState('2.0.0')
  const [aboutUrl, setAboutUrl] = useState('')
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    // 从全局变量读取（由 config:sync 设置）
    setVersion(window.__E3D_VERSION__ || '2.0.0')
    setAboutUrl(window.__E3D_ABOUT_URL__ || '')
  }, [])

  const handleCopyVersion = () => {
    navigator.clipboard.writeText(version).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    })
  }

  const handleOpenUrl = () => {
    if (aboutUrl) {
      window.open(aboutUrl, '_blank', 'noopener,noreferrer')
    }
  }

  return (
    <div className="space-y-6">
      {/* 工具信息卡片 */}
      <div className="p-5 rounded-xl bg-gradient-to-br from-blue-50 to-indigo-50 dark:from-blue-900/20 dark:to-indigo-900/20 border border-blue-200/50 dark:border-blue-800/30">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-12 h-12 rounded-xl bg-blue-600 flex items-center justify-center shadow-lg">
            <span className="text-white font-bold text-lg">E3</span>
          </div>
          <div>
            <h3 className="text-lg font-bold text-slate-800 dark:text-slate-100">E小智</h3>
            <p className="text-xs text-slate-500 dark:text-slate-400">AVEVA E3D/PDMS 智能助手</p>
          </div>
        </div>
        <p className="text-sm text-slate-600 dark:text-slate-300 leading-relaxed">
          基于大语言模型的 E3D 工厂设计辅助工具，支持数据库查询、设计修改、
          管道下料、结构建模等专业操作的智能对话。
        </p>
      </div>

      {/* 版本信息 */}
      <div className="space-y-3">
        <h4 className="text-sm font-semibold text-slate-700 dark:text-slate-200">版本信息</h4>
        <div className="flex items-center justify-between p-3 rounded-lg bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700">
          <div className="flex items-center gap-2">
            <Info className="w-4 h-4 text-slate-400" />
            <span className="text-sm text-slate-600 dark:text-slate-300">当前版本</span>
          </div>
          <div className="flex items-center gap-2">
            <code className="text-sm font-mono font-medium text-blue-600 dark:text-blue-400">
              v{version}
            </code>
            <button
              onClick={handleCopyVersion}
              className="p-1 text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors rounded"
              title="复制版本号"
            >
              {copied ? <Check className="w-3.5 h-3.5 text-emerald-500" /> : <Copy className="w-3.5 h-3.5" />}
            </button>
          </div>
        </div>
      </div>

      {/* 在线说明书 */}
      {aboutUrl && (
        <div className="space-y-3">
          <h4 className="text-sm font-semibold text-slate-700 dark:text-slate-200">帮助文档</h4>
          <button
            onClick={handleOpenUrl}
            className="w-full flex items-center justify-between p-3 rounded-lg bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-700/50 transition-colors text-left group"
          >
            <div className="flex items-center gap-2">
              <Globe className="w-4 h-4 text-slate-400" />
              <span className="text-sm text-slate-600 dark:text-slate-300">在线说明书</span>
            </div>
            <div className="flex items-center gap-1.5">
              <span className="text-xs text-slate-400 dark:text-slate-500 truncate max-w-[200px]">
                {aboutUrl}
              </span>
              <ExternalLink className="w-3.5 h-3.5 text-slate-400 group-hover:text-blue-500 transition-colors" />
            </div>
          </button>
        </div>
      )}

      {/* 开发者信息 */}
      <div className="space-y-3">
        <h4 className="text-sm font-semibold text-slate-700 dark:text-slate-200">开发者</h4>
        <div className="p-3 rounded-lg bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700">
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 rounded-full bg-slate-200 dark:bg-slate-700 flex items-center justify-center">
              <User className="w-4 h-4 text-slate-500 dark:text-slate-400" />
            </div>
            <div>
              <p className="text-sm font-medium text-slate-700 dark:text-slate-200">E小智 开发团队</p>
              <p className="text-xs text-slate-400 dark:text-slate-500">AVEVA E3D/PDMS 二次开发</p>
            </div>
          </div>
        </div>
      </div>

      {/* 技术栈 */}
      <div className="space-y-3">
        <h4 className="text-sm font-semibold text-slate-700 dark:text-slate-200">技术栈</h4>
        <div className="grid grid-cols-2 gap-2">
          {[
            { label: '前端', value: 'React + TypeScript + Vite' },
            { label: '后端', value: '.NET Framework 4.8' },
            { label: 'UI 容器', value: 'WebView2' },
            { label: 'AI 接口', value: 'OpenAI-compatible API' },
          ].map((item) => (
            <div key={item.label} className="p-2.5 rounded-lg bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700">
              <p className="text-xs text-slate-400 dark:text-slate-500 mb-0.5">{item.label}</p>
              <p className="text-xs font-medium text-slate-600 dark:text-slate-300">{item.value}</p>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
