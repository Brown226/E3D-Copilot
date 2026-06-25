/**
 * MarkdownBlock — Markdown 渲染组件
 *
 * 核心功能：
 * - ReactMarkdown + remark-gfm + rehype-highlight + rehype-katex
 * - 数学公式预处理（mathNormalize：LLM分隔符转换 + 分类器防误渲染）
 * - 代码块分块渲染（正则分割，提升性能）
 * - 代码块复制按钮（PreWithCopyButton + CopyButton）
 * - 自定义 code 组件（区分内联代码 vs 块代码，块代码带语言标签）
 * - URL 自动转链接（remarkUrlToLink）
 * - 防止文件名被错误解析为加粗（remarkPreventBoldFilenames）
 * - 流式闪烁光标（showCursor）
 */

import React, { memo, useMemo, useRef } from 'react'
import ReactMarkdown from 'react-markdown'
import rehypeHighlight from 'rehype-highlight'
import rehypeKatex from 'rehype-katex'
import remarkGfm from 'remark-gfm'
import remarkMath from 'remark-math'
import type { Node } from 'unist'
import { visit } from 'unist-util-visit'

import { WithCopyButton } from '@/components/common/CopyButton'
import { normalizeMath } from '@/components/chat/mathNormalize'
import { languages } from '@/utils/highlight'

/* ─── 代码块解析（正则分割，替代 marked.lexer） ─── */

function parseMarkdownIntoBlocks(markdown: string): string[] {
  if (!markdown) return ['']
  // 按围栏代码块分割：```lang ... ```
  const blocks: string[] = []
  const regex = /(^|\n)(```[\s\S]*?```)/g
  let last = 0
  let match: RegExpExecArray | null

  while ((match = regex.exec(markdown)) !== null) {
    const start = match.index + match[1].length
    if (start > last) {
      blocks.push(markdown.slice(last, start))
    }
    blocks.push(match[2])
    last = start + match[2].length
  }
  if (last < markdown.length) {
    blocks.push(markdown.slice(last))
  }
  return blocks.length > 0 ? blocks : [markdown]
}

/* ─── 代码块包裹 + 复制按钮 ─── */

const PreWithCopyButton = ({ children, ...preProps }: React.HTMLAttributes<HTMLPreElement>) => {
  const preRef = useRef<HTMLPreElement>(null)

  const handleCopy = () => {
    if (preRef.current) {
      const codeElement = preRef.current.querySelector('code')
      const textToCopy = codeElement ? codeElement.textContent : preRef.current.textContent
      if (!textToCopy) return null
      return textToCopy
    }
    return null
  }

  return (
    <WithCopyButton ariaLabel="复制代码" onCopy={handleCopy}>
      <pre {...preProps} ref={preRef}>
        {children}
      </pre>
    </WithCopyButton>
  )
}

/* ─── Remark 插件：URL 自动转链接 ─── */

const remarkUrlToLink = () => {
  return (tree: Node) => {
    visit(tree, 'text', (node: any, index, parent) => {
      const urlRegex = /https?:\/\/[^\s<>)"]+/g
      const matches = node.value.match(urlRegex)
      if (!matches) return

      const parts = node.value.split(urlRegex)
      const children: any[] = []

      parts.forEach((part: string, i: number) => {
        if (part) {
          children.push({ type: 'text', value: part })
        }
        if (matches[i]) {
          children.push({
            type: 'link',
            url: matches[i],
            children: [{ type: 'text', value: matches[i] }],
          })
        }
      })

      if (parent) {
        parent.children.splice(index, 1, ...children)
      }
    })
  }
}

/* ─── Remark 插件：防止文件名被解析为加粗 ─── */

const remarkPreventBoldFilenames = () => {
  return (tree: any) => {
    visit(tree, 'strong', (node: any, index: number | undefined, parent: any) => {
      if (!parent || typeof index === 'undefined' || index === parent.children.length - 1) return

      const nextNode = parent.children[index + 1]

      if (nextNode.type !== 'text' || !nextNode.value.match(/^\.[a-zA-Z0-9]+/)) return

      if (node.children?.length !== 1) return

      const strongContent = node.children?.[0]?.value
      if (!strongContent || typeof strongContent !== 'string') return

      if (!strongContent.match(/^[a-zA-Z0-9_-]+$/)) return

      const newNode = {
        type: 'text',
        value: `__${strongContent}__${nextNode.value}`,
      }

      parent.children.splice(index, 2, newNode)
    })
  }
}

/* ─── Remark 插件：自动给无语言代码块标记 javascript ─── */

const remarkDefaultLang = () => {
  return (tree: any) => {
    visit(tree, 'code', (node: any) => {
      if (!node.lang) {
        node.lang = 'javascript'
      } else if (node.lang.includes('.')) {
        node.lang = node.lang.split('.').slice(-1)[0]
      }
    })
  }
}

/* ─── 自定义 code 组件：区分内联 vs 块代码 ─── */

const CodeBlock = ({ children, className, ...rest }: React.HTMLAttributes<HTMLElement>) => {
  // 块代码（rehype-highlight 添加 hljs class）
  const isBlock = className?.includes('hljs')
  if (isBlock) {
    // 从 className 提取语言标签（hljs lang-xxx）
    const langMatch = className?.match(/\blang-(\w+)/)
    const lang = langMatch?.[1]?.toUpperCase() ?? ''
    return (
      <div className="relative group">
        {lang && (
          <span className="absolute top-2 right-12 text-[10px] font-mono text-[var(--fg-faint)] uppercase select-none z-10">
            {lang}
          </span>
        )}
        <code className={className} {...rest}>
          {children}
        </code>
      </div>
    )
  }
  // 内联代码
  return (
    <code className="md-code" {...rest}>
      {children}
    </code>
  )
}

/* ─── 分块 Markdown 渲染 ─── */

const MemoizedMarkdownBlock = memo(({ content }: { content: string }) => {
  return (
    <ReactMarkdown
      components={{
        pre: ({ children, ...preProps }: React.HTMLAttributes<HTMLPreElement>) => {
          return <PreWithCopyButton {...preProps}>{children}</PreWithCopyButton>
        },
        code: CodeBlock,
        img: (props) => (
          <img
            {...props}
            className="max-w-full h-auto rounded"
            alt={props.alt ?? ''}
          />
        ),
      }}
      rehypePlugins={[[rehypeHighlight as any, { languages }], rehypeKatex]}
      remarkPlugins={[
        [remarkGfm, { singleTilde: false }],
        remarkMath,
        remarkPreventBoldFilenames,
        remarkUrlToLink,
        remarkDefaultLang,
      ]}
    >
      {content}
    </ReactMarkdown>
  )
})

MemoizedMarkdownBlock.displayName = 'MemoizedMarkdownBlock'

const MemoizedMarkdown = memo(({ content }: { content: string }) => {
  // 数学公式预处理：LLM 分隔符转换 + 分类器防误渲染
  const normalized = useMemo(() => normalizeMath(content), [content])
  const blocks = useMemo(() => parseMarkdownIntoBlocks(normalized), [normalized])
  return blocks.map((block, index) => (
    <MemoizedMarkdownBlock content={block} key={index} />
  ))
})

MemoizedMarkdown.displayName = 'MemoizedMarkdown'

/* ─── 主组件 ─── */

interface MarkdownBlockProps {
  markdown?: string
  showCursor?: boolean
}

const MarkdownBlock = memo(({ markdown, showCursor }: MarkdownBlockProps) => {
  // 流式时（showCursor=true）使用纯文本渲染，避免每个 delta 都重新解析 Markdown
  // 完成后（showCursor=false）才用 ReactMarkdown 解析完整内容
  const isStreaming = showCursor === true && !!markdown

  return (
    <div className="inline-markdown-block">
      {isStreaming ? (
        <span className="inline-cursor-container text-sm text-slate-800 dark:text-slate-200 leading-relaxed whitespace-pre-wrap break-words">
          {markdown}
        </span>
      ) : markdown ? (
        <MemoizedMarkdown content={markdown} />
      ) : markdown}
    </div>
  )
})

MarkdownBlock.displayName = 'MarkdownBlock'

export default MarkdownBlock
