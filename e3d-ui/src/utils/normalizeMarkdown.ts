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

  // 检查是否有分隔行特征（:---:、---、:-: 等）
  const hasSep = /\|:?-{2,}:?\||\|:-:\|/.test(text)
  if (!hasSep) return text

  // 逐行处理：只有当同一行内出现 ≥2 个「|| / | |」时，
  // 才说明这一行装着多行内容（LLM 把整表写成一行）。
  // 单个 || 是合法空单元格，绝不能拆 —— 否则整张表会被拆成单列。
  let changed = false
  const lines = text.split('\n').map((line) => {
    const seps = line.match(/\|\s*\|/g)
    if (!seps || seps.length < 2) return line
    changed = true
    // 「||」既是上一行的结束符，也是下一行的起始符，
    // 必须还原成「|\n|」，否则每行都会丢掉尾部的管道符
    return line.replace(/\|\s*\|/g, '|\n|')
  })

  if (!changed) return text

  let result = lines.join('\n')

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

  // 4. 拆分列，只去掉首尾因管道符产生的空串
  //    注意：不能删中间的空白单元格，也不能对重复列去重
  //    ——「|措施|效果|措施|效果|」这种成对表头是合法的两组建模对比
  const parts = fixed.split('|')
  if (parts.length > 2) {
    parts.shift()
    parts.pop()
  }

  return '|' + parts.join('|') + '|'
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

    // 空行：表头与分隔行之间、分隔行与数据行之间不能有空行，
    // 否则 GFM 不认成表格。表格内部的空行直接丢弃；
    // 表格外保留（并保留候选表头，等分隔行来认领）。
    if (trimmed === '') {
      if (headerCols > 0) continue
      result.push(line)
      continue
    }

    // 分隔行：确定列数，并回补表头行
    if (isSeparatorLine(trimmed) && trimmed.includes('|')) {
      // 列数以表头行为准：LLM 常把分隔行写短（|---|--|），
      // 若以分隔行为准会把 4 列表格压成 2 列
      const headerCells = pendingHeaderLine
        ? pendingHeaderLine.split('|').slice(1, -1)
        : []
      const sepCells = trimmed.split('|').slice(1, -1)
      headerCols = headerCells.length || sepCells.length || 2

      // 回补表头行（列数不足则补齐空格）
      if (pendingHeaderLine) {
        while (headerCells.length < headerCols) headerCells.push(' ')
        // 表头与分隔行之间不能有空行，否则 GFM 不认成表格
        while (result.length > 0 && result[result.length - 1].trim() === '') result.pop()
        result.push('|' + headerCells.join('|') + '|')
        pendingHeaderLine = ''
      }

      // 分隔行补齐到表头列数
      const alignedSep = sepCells.length >= headerCols
        ? sepCells.slice(0, headerCols)
        : sepCells.concat(Array(headerCols - sepCells.length).fill('---'))
      result.push('|' + alignedSep.map((c) => c.trim() || '---').join('|') + '|')
      continue
    }

    // 检测是否为潜在表格行（含 |）
    if (trimmed.includes('|') && trimmed.indexOf('|') < trimmed.length - 1) {
      // 计算当前行的列数（split by |，去掉首尾空串）
      const cellCount = trimmed.split('|').length - 2

      // 如果还没确定表头列数，先缓存这一行作为表头候选
      // 注意：此处只缓存不 push，等确认是表格后在分隔行处统一输出，
      // 否则表头会被插入两次
      if (headerCols === 0) {
        pendingHeaderLine = trimmed
        continue
      }

      // 数据行宽度达到表头 2 倍以上 → LLM 把多行压成了一行，按表头列数切块。
      // 末块不足一列时补空单元格，保证不破坏表格结构；
      // 只是略多一点（<2 倍）时不动，避免把正常宽行切错位。
      if (cellCount >= headerCols * 2) {
        const cells = trimmed.split('|').slice(1, -1)
        const chunks: string[][] = []
        for (let i = 0; i < cells.length; i += headerCols) {
          chunks.push(cells.slice(i, i + headerCols))
        }
        // 末块只剩零星内容（不足表头一半）时并入上一行，避免出现一格独占一行的残表；
        // 并入后该行会略宽，但仍在本表之内，比断成两截更易读
        if (chunks.length > 1 && chunks[chunks.length - 1].length < Math.ceil(headerCols / 2)) {
          const tail = chunks.pop()!
          chunks[chunks.length - 1].push(...tail)
        }
        for (const rowCells of chunks) {
          while (rowCells.length < headerCols) rowCells.push('')
          // 表格内部不能有空行，否则 GFM 会把表格截断
          while (result.length > 0 && result[result.length - 1].trim() === '') result.pop()
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

    // 非表格行：把上面暂存但没等到分隔行的候选表头原样还回去
    if (pendingHeaderLine) {
      result.push(pendingHeaderLine)
      pendingHeaderLine = ''
    }
    headerCols = 0
    result.push(line)
  }

  // 收尾：始终没等到分隔行的候选表头，原样保留
  if (pendingHeaderLine) result.push(pendingHeaderLine)

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
