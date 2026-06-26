/**
 * normalizeMarkdown — 模型输出 markdown 格式归一化
 *
 * 修复 LLM（尤其是小模型 deepseek-v4-flash）输出的 markdown 格式问题：
 * 1. 整表单行：LLM 把整个表格写成一行，用 || 或 | | 分隔行 → 拆分为多行
 * 2. 整个标题写在一起：多个 # 标题连写 → 拆分
 * 3. 分隔行横杠不够：:-: 只有1个横杠 → 补齐到3个（GFM 要求 3+）
 * 4. 表格 || → | 归一化
 * 5. 表格前后空行保证（GFM 需要）
 * 6. 连续空行压缩
 */

// ── 检测表格行：至少有两个 | 且含非空内容 ──
const TABLE_CELL_RE = /\|[^|]+\|[^|]+\|/

/**
 * 预处理：拆分被 LLM 写成单行的表格
 *
 * 识别特征：一行中管道符 ≥ 8 个 + 包含分隔行模式（:---: 或 ---）
 * 拆分策略：按 |\s*| 替换为换行 + |
 */
function splitInlineTable(text: string): string {
  if (!/\|\s*\|/.test(text)) return text

  const pipeCount = (text.match(/\|/g) || []).length
  if (pipeCount < 8) return text

  // 检查是否有分隔行特征（:---:、---、:-: 等）
  const hasSep = /\|:?-{2,}:?\||\|:-:\|/.test(text)
  if (!hasSep) return text

  // 按 |\s*| 拆分（覆盖 || 和 | | 两种行分隔符）
  let result = text.replace(/\|\s*\|/g, '\n|')

  // 修复分隔行横杠：1-2个横杠 → 3个（GFM 要求3+）
  result = result.replace(/\|:?-{1,2}:?\|/g, (match) => {
    if (!/-/.test(match)) return match
    return match.replace(/-{1,2}/g, '---')
  })

  return result
}

/**
 * 预处理：拆分被 LLM 连写在一起的标题
 *
 * 两轮匹配：
 * 1. 非#字符 + 2-6个# + 标题文本 → 拆分（安全）
 * 2. 中文字符/标点 + # + 中文/大写字母 → 拆分（排除 C#、F#）
 */
function splitInlineHeadings(text: string): string {
  let result = text

  // 第一轮：2+ 个 # → 安全拆分
  result = result.replace(/([^\n#\s])(#{2,6})(\s*[^\s#])/g, (_, prefix, hashes, title) => {
    return `${prefix}\n\n${hashes}${title}`
  })

  // 第二轮：中文字符/# 后跟中文/大写字母 → 标题
  result = result.replace(/([\u4e00-\u9fff，。；：！？、])(#\s*)([\u4e00-\u9fffA-Z])/g, (_, prefix, hashes, title) => {
    return `${prefix}\n\n#${hashes}${title}`
  })

  return result
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
  const cleaned: string[] = []
  for (let i = 0; i < parts.length; i++) {
    const p = parts[i]
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

function isTableRow(line: string): boolean {
  return TABLE_CELL_RE.test(line)
}

function isSeparatorLine(line: string): boolean {
  // 分隔行特征：包含横杠，且其余字符只包含 |、-、:、空格
  return /-/.test(line) && /^[\s|:\-]+$/.test(line)
}

/**
 * 主归一化函数
 */
export function normalizeMarkdown(text: string): string {
  if (!text) return text

  // 0. 预处理：拆分 LLM 写在一行的内联表格
  text = splitInlineTable(text)

  // 0.5 预处理：拆分 LLM 连写在一起的标题
  text = splitInlineHeadings(text)

  // 0.6 单列 |---| 或 |:---| → 转为水平线（LLM 用它当分隔线）
  // GFM 表格最少 2 列，单列分隔符 remark-gfm 不认，会原样显示 |---|
  text = text.replace(/^\|:?-{2,}:?\|$/gm, '---')

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
