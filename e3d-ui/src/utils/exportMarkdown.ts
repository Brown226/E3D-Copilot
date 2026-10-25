/**
 * 导出对话为 Markdown
 * 遍历当前 Tab 消息：user/assistant 全文、tool 调用与结果摘要（含耗时）、附件名。
 * 触发浏览器下载（Blob + a[download]）。
 */

import type { Message, Attachment } from '@/types'

function fmtTime(ts: number): string {
  const d = new Date(ts)
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`
}

function fmtAttachments(atts?: Attachment[]): string {
  if (!atts || atts.length === 0) return ''
  return '\n\n> 附件: ' + atts.map((a) => a.name).join('、')
}

function fmtToolArgs(args: unknown): string {
  if (args == null) return ''
  try {
    const s = typeof args === 'string' ? args : JSON.stringify(args)
    return s.length > 300 ? s.slice(0, 300) + '…' : s
  } catch {
    return String(args)
  }
}

export function generateConversationMarkdown(messages: Message[], title: string): string {
  const lines: string[] = [
    `# ${title}`,
    '',
    `> 导出自 E小智 · ${fmtTime(Date.now())} · 共 ${messages.length} 条消息`,
    '',
  ]

  for (const m of messages) {
    const t = fmtTime(m.timestamp)
    switch (m.role) {
      case 'user':
        lines.push(`## 🧑 用户 · ${t}`, '', (m.content || '（无文本）') + fmtAttachments(m.attachments), '')
        break
      case 'assistant':
        lines.push(`## 🤖 E小智 · ${t}`, '')
        if (m.agentName) lines.push(`*（子代理 ${m.agentName}）*`, '')
        lines.push(m.content || '（无文本）', '')
        break
      case 'tool_call':
        lines.push(
          `**🔧 ${m.toolName ?? 'tool'}** · ${m.durationMs != null ? `${(m.durationMs / 1000).toFixed(1)}s` : ''}`,
          '',
          '```',
          fmtToolArgs(m.toolArgs),
          '```',
          '',
        )
        break
      case 'tool_result':
        lines.push(
          '```',
          m.toolError ? `错误: ${m.toolError}` : fmtToolArgs(m.toolMeta ?? m.content),
          '```',
          '',
        )
        break
      case 'error':
        if (m.content) lines.push(`**⚠️ ${m.content}**`, '')
        break
      default:
        if (m.content) lines.push(`*（${m.role} · ${t}）* ${m.content}`, '')
        break
    }
  }

  return lines.join('\n')
}

export function downloadConversationMarkdown(messages: Message[], title: string): void {
  const md = generateConversationMarkdown(messages, title)
  const blob = new Blob([md], { type: 'text/markdown;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  const d = new Date()
  const p = (n: number) => String(n).padStart(2, '0')
  a.href = url
  a.download = `E小智对话-${d.getFullYear()}${p(d.getMonth() + 1)}${p(d.getDate())}-${p(d.getHours())}${p(d.getMinutes())}.md`
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(url)
}
