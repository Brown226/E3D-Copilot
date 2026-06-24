/**
 * MarkdownBlock — Markdown 渲染组件
 *
 * 保留核心功能：
 * - ReactMarkdown + remark-gfm + rehype-highlight
 * - 代码块分块渲染（正则分割，无需 marked 库）
 * - 代码块复制按钮（PreWithCopyButton + CopyButton）
 * - URL 自动转链接（remarkUrlToLink）
 * - 防止文件名被错误解析为加粗（remarkPreventBoldFilenames）
 * - 流式闪烁光标（showCursor）
 */

import clsx from 'clsx'
import React, { memo, useMemo, useRef } from 'react'
import ReactMarkdown from 'react-markdown'
import rehypeHighlight from 'rehype-highlight'
import rehypeKatex from 'rehype-katex'
import remarkGfm from 'remark-gfm'
import remarkMath from 'remark-math'
import type { Node } from 'unist'
import { visit } from 'unist-util-visit'

import { WithCopyButton } from '@/components/common/CopyButton'
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

/* ─── 分块 Markdown 渲染 ─── */

const MemoizedMarkdownBlock = memo(({ content }: { content: string }) => {
  return (
    <ReactMarkdown
      components={{
        pre: ({ children, ...preProps }: React.HTMLAttributes<HTMLPreElement>) => {
          return <PreWithCopyButton {...preProps}>{children}</PreWithCopyButton>
        },
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
  const blocks = useMemo(() => parseMarkdownIntoBlocks(content), [content])
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
  return (
    <div className="inline-markdown-block">
      <span
        className={clsx('inline [&>p]:mt-0', {
          'inline-cursor-container': showCursor,
        })}
      >
        {markdown ? <MemoizedMarkdown content={markdown} /> : markdown}
      </span>
    </div>
  )
})

MarkdownBlock.displayName = 'MarkdownBlock'

export default MarkdownBlock
