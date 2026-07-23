/**
 * MermaidDiagram — Mermaid 图表渲染组件
 *
 * 职责:
 * - 接收 mermaid 源码,异步渲染为 SVG
 * - 懒初始化 mermaid(仅首次渲染时 initialize)
 * - 渲染失败时显示错误提示,不阻塞其他 Markdown 内容
 * - SSR 安全:mermaid 依赖 DOM,只在 useEffect 中调用
 *
 * 内网约束:mermaid 库本地打包,不走 CDN
 */
import { useEffect, useRef, useState } from 'react'
import mermaid from 'mermaid'

let initialized = false

interface MermaidDiagramProps {
  code: string
  id?: string
}

export default function MermaidDiagram({ code, id }: MermaidDiagramProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const [svgHtml, setSvgHtml] = useState<string>('')
  const [error, setError] = useState<string>('')

  useEffect(() => {
    if (!initialized) {
      mermaid.initialize({
        startOnLoad: false,
        theme: 'default',
        securityLevel: 'strict',
      })
      initialized = true
    }

    const renderId = id ?? `mmd-${Math.random().toString(36).slice(2, 10)}`
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
  }, [code, id])

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
