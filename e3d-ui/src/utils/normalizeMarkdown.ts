/**
 * normalizeMarkdown — 模型输出 markdown 格式归一化
 *
 * 修复 LLM（尤其是小模型）输出的 markdown 格式问题：
 * 1. 表格行内联：LLM 可能把所有表格行写在一行用 || 分隔 → 拆分为多行
 * 2. 表格：|| → |，||| → |，修复分隔行
 * 3. 表格前后空行保证（GFM 需要）
 * 4. 连续空行规范化
 * 5. 标题前后空行保证
 */

// ── 检测分隔行：至少有一个 --- 且只包含 |、-、:、空格 ──
const SEPARATOR_RE = /^[\s|:\-]+$/

// ── 检测表格行：至少有两个 | ──
const TABLE_CELL_RE = /\|[^|]+\|[^|]+\|/

/**
 * 预处理：检测并拆分被 LLM 写成单行的内联表格
 * 特征：一行里有多个 || 分隔符 + 至少一个分隔行模式（|---|---|）
 */
function splitInlineTable(text: string): string {
  // 检测分隔行模式：|---|---| 或 |:---|:---| 等
  const sepPattern = /\|[:\s]*---[:\s]*\|/
  if (!sepPattern.test(text)) return text

  // 按 || 拆分为多行（LLM 把表格行连在一起的特征）
  // 但要小心不要误拆代码块和普通文本
  const lines = text.split('\n')
  const result: string[] = []

  for (const line of lines) {
    // 只处理包含表格分隔行特征的行
    if (!sepPattern.test(line)) {
      result.push(line)
      continue
    }

    // 如果行已经有换行，不需要处理
    if (line.split('|').length < 8) {
      result.push(line)
      continue
    }

    // 尝试按 || 拆分（注意：需要保护 |---| 这类分隔行不被拆散）
    // 策略：在 || 处拆分，但保留 |---| 段完整
    const parts = line.split(/(?<=\|)\s*\|\|\s*(?=\|)/g)
    if (parts.length <= 1) {
      result.push(line)
      continue
    }

    // 每个 part 是一行
    for (const part of parts) {
      const trimmed = part.trim()
      if (trimmed) result.push(trimmed)
    }
  }

  return result.join('\n')
}

/**
 * 修复表格管道符：|| → |，||| → |
 * 然后拆分、去重空列、重新拼接
 */
function normalizeTableRow(line: string): string {
  // 1. 连续管道符合并：||| → |，|| → |
  let fixed = line.replace(/\|{3,}/g, '|')
  // 2. 行首 || → |
  fixed = fixed.replace(/^\|\|/, '|')
  // 3. 行尾 || → |
  fixed = fixed.replace(/\|\|$/, '|')

  // 4. 拆分列，去除空列（模型重复导致的空白列）
  const parts = fixed.split('|')
  // 首尾 split 空串（因为行首/尾有 |）
  const cleaned: string[] = []
  for (let i = 0; i < parts.length; i++) {
    const p = parts[i]
    // 首尾空串跳过，中间空串也跳过（重复管道符产生的）
    if (p === '' && (i === 0 || i === parts.length - 1)) continue
    if (p.trim() === '') continue
    cleaned.push(p)
  }

  return '|' + cleaned.join('|') + '|'
}

/**
 * 修复分隔行：提取所有 --- 段，重建标准格式
 */
function normalizeSeparator(line: string): string {
  // 提取所有 --- 段（可能带冒号）
  const dashes: string[] = []
  const regex = /:?-{3,}:?/g
  let m: RegExpExecArray | null
  while ((m = regex.exec(line)) !== null) {
    const s = m[0]
    const left = s.startsWith(':')
    const right = s.endsWith(':')
    const core = s.replace(/:/g, '')
    dashes.push((left ? ':' : '') + core + (right ? ':' : ''))
  }

  if (dashes.length === 0) return line
  return '|' + dashes.join('|') + '|'
}

/**
 * 检测是否为表格行（含管道符分隔的多列）
 */
function isTableRow(line: string): boolean {
  return TABLE_CELL_RE.test(line)
}

/**
 * 检测是否为分隔行（全是 |、-、:、空格）
 */
function isSeparatorLine(line: string): boolean {
  return SEPARATOR_RE.test(line) && line.includes('---')
}

/**
 * 主归一化函数
 */
export function normalizeMarkdown(text: string): string {
  if (!text) return text

  // 0. 预处理：拆分 LLM 写在一行的内联表格
  text = splitInlineTable(text)

  const lines = text.split('\n')
  const result: string[] = []

  let inTable = false

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    const trimmed = line.trim()

    // ── 空行：连续 2+ 空行压缩为 1 个 ──
    if (trimmed === '') {
      if (result.length >= 2 && result[result.length - 1] === '' && result[result.length - 2] === '') {
        continue
      }
      inTable = false
      result.push('')
      continue
    }

    // ── 分隔行 ──
    if (isSeparatorLine(trimmed)) {
      if (!inTable && result.length > 0 && result[result.length - 1] !== '') {
        result.push('')  // 表格前插空行
      }
      result.push(normalizeSeparator(trimmed))
      inTable = true
      continue
    }

    // ── 表格行 ──
    if (isTableRow(trimmed)) {
      if (!inTable && result.length > 0 && result[result.length - 1] !== '') {
        result.push('')
      }
      result.push(normalizeTableRow(trimmed))
      inTable = true
      continue
    }

    // ── 普通行 ──
    if (inTable) {
      result.push('')  // 表格后插空行
    }
    result.push(line)
    inTable = false
  }

  return result.join('\n')
}
