/**
 * MermaidDiagram — Mermaid 图表渲染组件
 *
 * 职责:
 * - 接收 mermaid 源码,异步渲染为 SVG
 * - 主题跟随暗色模式（监听 theme-changed 事件 + prefers-color-scheme 媒体查询）
 * - 渲染失败时显示错误提示,不阻塞其他 Markdown 内容
 * - SSR 安全:mermaid 依赖 DOM,只在 useEffect 中调用
 *
 * 内网约束:mermaid 库本地打包,不走 CDN
 */
import { useEffect, useRef, useState } from 'react'
import mermaid from 'mermaid'

/** 从 localStorage 读取主题设置（与 Header/CommandPalette 一致） */
function getStoredTheme(): 'light' | 'dark' | 'system' {
  try { return (localStorage.getItem('e3d-theme') as 'light' | 'dark' | 'system') || 'dark' } catch { return 'dark' }
}

/** 当前是否暗色模式 */
function isDarkMode(): boolean {
  const t = getStoredTheme()
  return t === 'dark' || (t === 'system' && window.matchMedia('(prefers-color-scheme:dark)').matches)
}

interface MermaidDiagramProps {
  code: string
  id?: string
}

export default function MermaidDiagram({ code, id }: MermaidDiagramProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const [svgHtml, setSvgHtml] = useState<string>('')
  const [error, setError] = useState<string>('')
  const [dark, setDark] = useState<boolean>(isDarkMode())

  // 订阅主题变化：theme-changed 事件（手动切换） + matchMedia（system 模式下系统主题变化）
  // 不使用 MutationObserver，避免与 Header/storeMapping 的 class 修改形成循环
  useEffect(() => {
    const sync = () => setDark(isDarkMode())
    window.addEventListener('theme-changed', sync)
    const mq = window.matchMedia('(prefers-color-scheme: dark)')
    const mqHandler = () => { if (getStoredTheme() === 'system') sync() }
    mq.addEventListener('change', mqHandler)
    return () => {
      window.removeEventListener('theme-changed', sync)
      mq.removeEventListener('change', mqHandler)
    }
  }, [])

  useEffect(() => {
    // 主题变化时重新 initialize，让 mermaid 用新主题变量重新生成 SVG
    mermaid.initialize({
      startOnLoad: false,
      theme: dark ? 'dark' : 'default',
      securityLevel: 'strict',
    })

    const renderId = (id ?? `mmd-${Math.random().toString(36).slice(2, 10)}`) + (dark ? '-d' : '-l')
    let cancelled = false

    mermaid
      .render(renderId, code)
      .then(({ svg }) => {
        if (!cancelled) {
          setSvgHtml(svg)
          setError('')
        }
      })
      .catch((err: Error) => {
        if (!cancelled) {
          setError(err?.message ?? 'unknown error')
          setSvgHtml('')
        }
      })

    return () => { cancelled = true }
  }, [code, id, dark])

  if (error) {
    return (
      <div className="mermaid-error text-xs text-red-500 border border-red-300 rounded p-2 my-1">
        mermaid 渲染失败: {error}
        <pre className="mt-1 text-[10px] text-slate-500 whitespace-pre-wrap">{code}</pre>
      </div>
    )
  }

  if (svgHtml) {
    return (
      <div
        ref={containerRef}
        className="mermaid-container my-2 overflow-x-auto"
        dangerouslySetInnerHTML={{ __html: svgHtml }}
      />
    )
  }

  return (
    <div ref={containerRef} className="mermaid-container my-2 overflow-x-auto">
      <div className="text-xs text-slate-400">渲染中...</div>
    </div>
  )
}
