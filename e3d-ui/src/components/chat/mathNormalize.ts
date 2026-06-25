/**
 * mathNormalize — 数学公式预处理
 *
 * 将 LLM 输出的常见数学分隔符转为 remark-math 期望的 $/$$ 格式：
 * 1. 保护 Markdown 代码块/行内代码（不处理其中的 $）
 * 2. 保护 LaTeX 行间距 \\[...] 不被 LLM 分隔符转换吞掉
 * 3. LLM 原生分隔符 → 标准 $/$$：\(…\)→$…$, \[…\]→$$…$$
 * 4. 转义已转义的 \$（隐藏后恢复）
 * 5. 修复行内 $$（CommonMark 要求块数学前有空行）
 * 6. $$…$$ → display placeholder, $…$ → classifier-gated inline placeholder
 *    - 过滤非数学对（$5, $PATH 等）→ &#36; 实体
 *    - 通过的 → 保留 $…$ 供 remark-math 解析
 * 7. 恢复标准 $/$$ 分隔符 + 恢复转义的 \$
 */

import { isLikelyInlineMath } from './mathClassify'

const DM = '__E3D_MATH_DISPLAY__'
const IM = '__E3D_MATH_INLINE__'
const LB = '__E3D_LATEX_LINEBREAK__'
const ED_BASE = 'E3DESCAPEDDOLLAR'
const DOLLAR = '&#36;'

export function normalizeMath(s: string): string {
  const protectedCode = protectMarkdownCode(s)
  let r = normalizeMathText(protectedCode.text)
  // 恢复被保护的代码块
  for (let i = 0; i < protectedCode.segments.length; i += 1) {
    r = r.split(`${protectedCode.prefix}${i}__`).join(protectedCode.segments[i])
  }
  return r
}

function normalizeMathText(s: string): string {
  // Step 1: 保护 LaTeX 行间距 \\[4pt] 等，防止被 \[→$$ 转换吞掉
  let r = s.replace(/\\\\\[/g, LB)

  // Step 2: LLM 原生分隔符 → 标准 $/$$
  r = r
    .replace(/\\\[/g, () => '$$')
    .replace(/\\\]/g, () => '$$')
    .replace(/\\\(/g, () => '$')
    .replace(/\\\)/g, () => '$')
  r = r.replace(new RegExp(LB, 'g'), '\\\\[')

  // 转义的 \$ → 临时 token（隐藏后恢复，避免干扰分类器）
  const escapedDollarToken = unusedEscapedDollarToken(r)
  r = r.split('\\$').join(escapedDollarToken)

  // Step 3: 修复行内 $$ — CommonMark 要求块数学前有空行
  r = r.replace(/([A-Za-z\)\]\>\.。！？,{}])\$\$/g, (_m, prev) => prev + '\n\n$$')

  // Step 4: $$…$$ → display placeholder（remark-math 会识别 $$）
  r = r.replace(/\$\$([\s\S]*?)\$\$/g, (_m, m) => `${DM}${m}${DM}`)

  // Step 5: $…$ → classifier-gated inline math
  r = r.replace(/\$([^$\n]+)\$/g, (_m, m) => {
    if (!isLikelyInlineMath(m.trim())) return `${DOLLAR}${m}${DOLLAR}`
    return `${IM}${m}${IM}`
  })

  // Step 6: 恢复标准分隔符
  return r
    .replace(new RegExp(DM, 'g'), () => '$$')
    .replace(new RegExp(IM, 'g'), '$')
    .split(escapedDollarToken).join('\\$')
}

// ── 代码保护 ──

function unusedEscapedDollarToken(s: string): string {
  let token = ED_BASE
  let n = 0
  while (s.includes(token)) {
    n += 1
    token = `${ED_BASE}${n}`
  }
  return token
}

function protectMarkdownCode(s: string): { text: string; prefix: string; segments: string[] } {
  const prefix = unusedPlaceholderPrefix(s)
  const segments: string[] = []
  let out = ''
  let i = 0

  const pushSegment = (segment: string) => {
    const token = `${prefix}${segments.length}__`
    segments.push(segment)
    out += token
  }

  while (i < s.length) {
    // 围栏代码块
    const fenceEnd = fencedCodeEnd(s, i)
    if (fenceEnd > i) {
      pushSegment(s.slice(i, fenceEnd))
      i = fenceEnd
      continue
    }

    // 行内代码
    if (s[i] === '`') {
      const tickEnd = inlineCodeEnd(s, i)
      if (tickEnd > i) {
        pushSegment(s.slice(i, tickEnd))
        i = tickEnd
        continue
      }
    }

    out += s[i]
    i += 1
  }

  return { text: out, prefix, segments }
}

function unusedPlaceholderPrefix(s: string): string {
  let prefix = '__E3D_PROTECTED_CODE__'
  let n = 0
  while (s.includes(prefix)) {
    n += 1
    prefix = `__E3D_PROTECTED_CODE_${n}__`
  }
  return prefix
}

function fencedCodeEnd(s: string, start: number): number {
  if (start !== 0 && s[start - 1] !== '\n') return -1

  let markerStart = start
  let spaces = 0
  while (spaces < 4 && s[markerStart] === ' ') {
    markerStart += 1
    spaces += 1
  }

  const marker = s[markerStart]
  if (marker !== '`' && marker !== '~') return -1

  let fenceLen = 0
  while (s[markerStart + fenceLen] === marker) fenceLen += 1
  if (fenceLen < 3) return -1

  const openingLineEnd = lineEnd(s, markerStart + fenceLen)

  if (openingLineEnd >= s.length) {
    const fencePattern = marker.repeat(fenceLen)
    const nextFence = s.indexOf(fencePattern, markerStart + fenceLen)
    if (nextFence === -1) return s.length
    return nextFence + fenceLen
  }

  let lineStart = openingLineEnd + 1
  while (lineStart < s.length) {
    const currentLineEnd = lineEnd(s, lineStart)
    if (isClosingFenceLine(s, lineStart, currentLineEnd, marker, fenceLen)) {
      return currentLineEnd < s.length ? currentLineEnd + 1 : currentLineEnd
    }
    lineStart = currentLineEnd < s.length ? currentLineEnd + 1 : currentLineEnd
  }

  return s.length
}

function isClosingFenceLine(s: string, start: number, end: number, marker: string, minLen: number): boolean {
  let i = start
  let spaces = 0
  while (spaces < 4 && s[i] === ' ') {
    i += 1
    spaces += 1
  }

  let count = 0
  while (s[i + count] === marker) count += 1
  if (count < minLen) return false

  for (let j = i + count; j < end; j += 1) {
    if (s[j] !== ' ' && s[j] !== '\t') return false
  }
  return true
}

function inlineCodeEnd(s: string, start: number): number {
  let tickLen = 0
  while (s[start + tickLen] === '`') tickLen += 1

  const ticks = '`'.repeat(tickLen)
  const end = s.indexOf(ticks, start + tickLen)
  return end < 0 ? -1 : end + tickLen
}

function lineEnd(s: string, start: number): number {
  const end = s.indexOf('\n', start)
  return end < 0 ? s.length : end
}
