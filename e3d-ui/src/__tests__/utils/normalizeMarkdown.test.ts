import { describe, it, expect } from 'vitest'
import { normalizeMarkdown } from '@/utils/normalizeMarkdown'

/**
 * 表格归一化回归测试
 *
 * 对应缺陷：4 列表格被拆成 13 行单列，界面上显示为破碎的文字竖条。
 * 根因三处：
 *   1. splitInlineTable 见到单个 || 就整篇拆分（|| 是合法空单元格）
 *   2. normalizeTableRow 把中间的空白单元格删掉、并对重复列去重
 *   3. fixInconsistentColumns 以「写短的分隔行」为准，把 4 列压成 2 列
 */
describe('normalizeMarkdown 表格', () => {
  const rows = (md: string) => md.split('\n').map((l) => l.trim()).filter(Boolean)

  it('成对表头（措施|效果|措施|效果）不被去重', () => {
    const out = normalizeMarkdown('|措施|效果|措施|效果|\n|---|---|---|---|')
    expect(rows(out)[0]).toBe('|措施|效果|措施|效果|')
  })

  it('分隔行写短时以表头列数对齐，不压缩表格', () => {
    const out = normalizeMarkdown('|措施|效果|措施|效果|\n|---|--|')
    const lines = rows(out)
    expect(lines[0]).toBe('|措施|效果|措施|效果|')
    expect(lines[1].split('|').filter(Boolean)).toHaveLength(4)
  })

  it('表头与分隔行之间有空行时仍识别为一张表', () => {
    const out = normalizeMarkdown('|措施|效果|\n\n|---|---|\n|甲|乙|')
    const lines = rows(out)
    expect(lines[0]).toBe('|措施|效果|')
    expect(lines[1]).toMatch(/^\|-+\|-+\|$/)
    expect(lines[2]).toBe('|甲|乙|')
  })

  it('单元格为空时保留该列，不塌缩相邻列', () => {
    const out = normalizeMarkdown('|a|b|c|\n|---|---|---|\n|优化布置（避开高应力区）||降低力流扰动|')
    const lines = rows(out)
    expect(lines[2]).toBe('|优化布置（避开高应力区）||降低力流扰动|')
  })

  it('整表写成一行（含多个 ||）仍能拆成多行', () => {
    // 表头 + 分隔行 + 数据行全部挤在一行，是 LLM 的典型畸形输出
    const inline = '|措施|效果||:---:|:---:||增大圆角|降低 K_t||开孔补强|引导力流|'
    const out = normalizeMarkdown(inline)
    const lines = rows(out)
    expect(lines.length).toBeGreaterThan(1)
    expect(lines[0]).toBe('|措施|效果|')
  })

  it('多行被压成一行时按表头列数切块，且不成残表', () => {
    // 13 格 / 4 列：切 4 块，最后一块只有 1 格 → 并入上一行
    const wide = '|a1|b1|c1|d1|a2|b2|c2|d2|a3|b3|c3|d3|a4|'
    const out = normalizeMarkdown(`|A|B|C|D|\n|---|---|---|---|\n${wide}`)
    const lines = rows(out)
    expect(lines[0]).toBe('|A|B|C|D|')
    // 表格必须连续（中间不能有空行），否则 GFM 会截断
    expect(out.split('\n').filter((l) => l.trim()).length).toBe(lines.length)
    // 13 格切 4 列得 4 块，末块 1 格并入上一块 → 3 个数据块
    // 合计：表头 + 分隔行 + 3 行数据 = 5 行
    expect(lines.length).toBe(5)
  })

  it('普通含 | 的文本不被吞掉', () => {
    const out = normalizeMarkdown('公式 a | b 表示按位或\n下一行普通文本')
    expect(out).toContain('公式 a | b 表示按位或')
    expect(out).toContain('下一行普通文本')
  })
})
