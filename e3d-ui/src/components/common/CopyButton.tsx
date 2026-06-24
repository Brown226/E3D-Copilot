/**
 * CopyButton — 复制按钮组件
 * 从旧项目裁剪：移除 i18next、cn、Button 依赖
 * 纯 UI 组件，只用 lucide-react 图标 + clsx
 */

import clsx from 'clsx'
import { Check, Copy } from 'lucide-react'
import { useCallback, useRef, useState } from 'react'

const COPIED_TIMEOUT = 1500

interface CopyButtonProps {
  textToCopy?: string
  onCopy?: () => string | undefined | null
  className?: string
  ariaLabel?: string
}

/**
 * 独立复制按钮 — 点击后复制文本，显示 ✓ 1.5 秒
 */
export function CopyButton({ textToCopy, onCopy, className, ariaLabel }: CopyButtonProps) {
  const [copied, setCopied] = useState(false)
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const handleCopy = useCallback(() => {
    const text = onCopy?.() ?? textToCopy
    if (!text) return

    navigator.clipboard
      .writeText(text)
      .then(() => {
        if (timerRef.current) clearTimeout(timerRef.current)
        setCopied(true)
        timerRef.current = setTimeout(() => setCopied(false), COPIED_TIMEOUT)
      })
      .catch((err) => console.error('Copy failed', err))
  }, [textToCopy, onCopy])

  return (
    <button
      type="button"
      aria-label={copied ? '已复制' : ariaLabel ?? '复制'}
      className={clsx(
        'inline-flex items-center justify-center rounded-md p-1',
        'text-slate-400 hover:text-slate-600 hover:bg-slate-100',
        'dark:text-slate-500 dark:hover:text-slate-300 dark:hover:bg-slate-700',
        'transition-colors cursor-pointer',
        className,
      )}
      onClick={handleCopy}
    >
      {copied ? (
        <Check className="h-4 w-4 text-green-500" />
      ) : (
        <Copy className="h-4 w-4" />
      )}
      <span className="ml-1 text-xs">{copied ? '已复制' : '复制'}</span>
    </button>
  )
}

interface WithCopyButtonProps {
  children: React.ReactNode
  textToCopy?: string
  onCopy?: () => string | undefined | null
  className?: string
  ariaLabel?: string
}

/**
 * 包裹容器，hover 时右上角显示复制按钮
 */
export function WithCopyButton({
  children,
  textToCopy,
  onCopy,
  className,
  ariaLabel,
}: WithCopyButtonProps) {
  return (
    <div className={clsx('group relative w-full', className)}>
      <div className="absolute top-2 right-2 opacity-0 group-hover:opacity-100 transition-opacity z-10">
        <CopyButton ariaLabel={ariaLabel} onCopy={onCopy} textToCopy={textToCopy} />
      </div>
      {children}
    </div>
  )
}
