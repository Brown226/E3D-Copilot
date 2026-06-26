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

  // 5. 去重：LLM 有时输出 | 属性 | 值 | 属性 | 值 这种重复表头
  const deduped: string[] = []
  const seen = new Set<string>()
  for (const c of cleaned) {
    const key = c.trim().toLowerCase()
    if (seen.has(key)) continue  // 跳过重复列
    seen.add(key)
    deduped.push(c)
  }

  return '|' + deduped.join('|') + '|'
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
 * 修复表格列数不一致：
 * 1. 补齐表头/数据行缺失的尾部 |
 * 2. 数据行列数超过表头时拆分为多行
 */
function fixInconsistentColumns(lines: string[]): string[] {
  const result: string[] = []
  let headerCols = 0
  let pendingHeaderLine = '' // 缓存表头行（分隔行上一行）

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    const trimmed = line.trim()

    // 分隔行：确定列数，并回补表头行
    if (isSeparatorLine(trimmed)) {
      headerCols = trimmed.split('|').length - 2
      if (headerCols < 1) headerCols = 2

      // 回补表头行（如果列数不够则补齐）
      if (pendingHeaderLine) {
        const headerCells = pendingHeaderLine.split('|')
        // 分隔行 split 后有 headerCols+2 个元素（首尾各一个空串）
        while (headerCells.length < headerCols + 2) {
          headerCells.splice(headerCells.length - 1, 0, ' ')
        }
        result.push(headerCells.join('|'))
        pendingHeaderLine = ''
      }

      result.push(line)
      continue
    }

    // 检测是否为潜在表格行（含 |）
    if (trimmed.includes('|') && trimmed.indexOf('|') < trimmed.length - 1) {
      // 计算当前行的列数（split by |，去掉首尾空串）
      const cellCount = trimmed.split('|').length - 2

      // 如果还没确定表头列数，先缓存这一行作为表头候选
      if (headerCols === 0) {
        pendingHeaderLine = trimmed
        result.push(line)
        continue
      }

      // 数据行列数是表头的整数倍 → 拆分
      if (cellCount > headerCols && cellCount % headerCols === 0) {
        const cells = trimmed.split('|').filter((_, idx, arr) => {
          if (idx === 0 || idx === arr.length - 1) return false
          return true
        })
        const rowCount = cellCount / headerCols
        for (let r = 0; r < rowCount; r++) {
          const rowCells = cells.slice(r * headerCols, (r + 1) * headerCols)
          result.push('|' + rowCells.join('|') + '|')
        }
        continue
      }

      // 数据行列数不够表头 → 补齐尾部 |
      if (cellCount > 0 && cellCount < headerCols) {
        let fixed = trimmed
        if (!fixed.endsWith('|')) fixed += '|'
        // 补齐列数
        const currentCells = fixed.split('|')
        while (currentCells.length < headerCols + 2) {
          currentCells.splice(currentCells.length - 1, 0, ' ')
        }
        result.push(currentCells.join('|'))
        continue
      }

      // 列数一致，正常通过
      result.push(line)
      continue
    }

    // 非表格行
    headerCols = 0
    pendingHeaderLine = ''
    result.push(line)
  }
  return result
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

  // 0.6 畸形分隔行 → 标准化或转水平线
  // GFM 表格最少 2 列，单列 |---| remark-gfm 不认，会原样显示
  // 匹配条件：|开头结尾 + 中间只有空格/冒号/横杠/管道符 + 至少有一段连续2+横杠
  text = text.replace(/^\|[\s|:\-]+\|$/gm, (match) => {
    if (!/-{2,}/.test(match)) return match  // 安全检查：必须含横杠段
    const cells = match.split('|').filter(c => c.trim().length > 0)
    if (cells.length === 0) return '---'
    if (cells.length === 1) return '---'   // 单列 → 转为水平线（GFM 不认单列表格）
    return '|' + cells.map(() => '---').join('|') + '|'  // 多列 → 标准化分隔行
  })

  const lines = text.split('\n')
  let processed = lines

  // 0.7 修复表格列数不一致（数据行列数超过表头）
  processed = fixInconsistentColumns(processed)

  const result: string[] = []

  let inTable = false

  for (let i = 0; i < processed.length; i++) {
    const line = processed[i]
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

    // ── 分隔行（必须含 |，纯 --- 不在此处理）──
    if (isSeparatorLine(trimmed) && trimmed.includes('|')) {
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
