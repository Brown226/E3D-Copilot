/**
 * 演示卡片渲染测试
 *
 * 用真实后端 Data 结构构造用例（字段名取自 DesignWorkflowHandler / DwgDiffHandler
 * 的实际返回，以及 MCP 平台桩的落盘 JSON），验证：
 *   1. 数据能正确渲染
 *   2. 结构不符时不渲染（不猜、不崩）
 */

import { describe, it, expect } from 'vitest'
import { render } from '@testing-library/react'
import { WorkflowCard } from '@/components/chat/WorkflowCard'
import { DwgDiffCard } from '@/components/chat/DwgDiffCard'
import { PlatformCard } from '@/components/chat/PlatformCard'

describe('WorkflowCard', () => {
  const realMeta = {
    platformSynced: true,
    steps: [
      { step: 1, name: '解析 A 版图纸', success: true, summary: '图层 30 个 / 实体 699 个 / 有效线段 268 条（耗时 904ms）' },
      { step: 2, name: 'A 版建模', success: true, summary: '线段 268 条 → 元素 268 个' },
      { step: 4, name: '解析 B 版图纸', success: true, summary: '图层 30 个 / 实体 704 个 / 有效线段 273 条' },
      { step: 5, name: 'A/B 版本差异比对', success: true, summary: '新增 5 / 删除 0 / 修改 0，未变 268（容差 5mm）' },
    ],
  }

  it('渲染步骤名与摘要', () => {
    const { getByText } = render(<WorkflowCard meta={realMeta} />)
    expect(getByText('解析 A 版图纸')).toBeTruthy()
    expect(getByText('A/B 版本差异比对')).toBeTruthy()
    expect(getByText(/4\/4 步完成/)).toBeTruthy()
  })

  it('平台同步状态正确显示', () => {
    const { getByText } = render(<WorkflowCard meta={realMeta} />)
    expect(getByText('✓ 平台已同步')).toBeTruthy()
  })

  it('平台未同步时显示告警态', () => {
    const { getByText } = render(<WorkflowCard meta={{ ...realMeta, platformSynced: false }} />)
    expect(getByText('⚠ 平台未同步')).toBeTruthy()
  })

  it('失败步骤单独计数', () => {
    const meta = {
      platformSynced: false,
      steps: [
        { step: 1, name: '解析 A 版图纸', success: true, summary: 'ok' },
        { step: 5, name: 'A/B 差异比对', success: false, summary: '文件不存在' },
      ],
    }
    const { getByText } = render(<WorkflowCard meta={meta} />)
    expect(getByText('1 步失败')).toBeTruthy()
  })

  it('结构不符时不渲染', () => {
    const { container } = render(<WorkflowCard meta={{ foo: 'bar' }} />)
    expect(container.querySelector('.wf-card')).toBeNull()
  })

  it('meta 为空时不崩', () => {
    const { container } = render(<WorkflowCard meta={null} />)
    expect(container.firstChild).toBeNull()
  })
})

describe('DwgDiffCard', () => {
  const realMeta = {
    fileA: 'E:\\图纸\\结构图.dwg',
    fileB: 'E:\\图纸\\结构图-测试.dwg',
    segmentCountA: 794,
    segmentCountB: 799,
    tolerance: 5,
    unchangedCount: 794,
    addedCount: 5,
    removedCount: 0,
    modifiedCount: 0,
    added: [
      { Layer: '土建', StartX: 113365.3, StartY: -135570.1, EndX: 114047.3, EndY: -136301.5, Length: 1000 },
      { Layer: '土建', StartX: 113400.0, StartY: -135600.0, EndX: 114000.0, EndY: -136200.0, Length: 888 },
    ],
    removed: [],
    modified: [],
  }

  it('渲染数量统计', () => {
    const { getByText } = render(<DwgDiffCard meta={realMeta} />)
    expect(getByText('5')).toBeTruthy()
    expect(getByText(/未变/)).toBeTruthy()
    expect(getByText('794')).toBeTruthy()
  })

  it('渲染文件名而非全路径', () => {
    const { getByText } = render(<DwgDiffCard meta={realMeta} />)
    expect(getByText('结构图.dwg vs 结构图-测试.dwg')).toBeTruthy()
  })

  it('展开后显示线段坐标', () => {
    const { getByText } = render(<DwgDiffCard meta={realMeta} />)
    expect(getByText(/113365\.3/)).toBeTruthy()
    expect(getByText('len=1000')).toBeTruthy()
  })

  it('渲染修改项（位移与长度差）', () => {
    const meta = {
      ...realMeta,
      addedCount: 0,
      modifiedCount: 1,
      added: [],
      modified: [
        {
          layer: '土建',
          shift: 2.8,
          lengthDelta: 0,
          before: { Layer: '土建', StartX: 0, StartY: 0, EndX: 1000, EndY: 0, Length: 1000 },
          after: { Layer: '土建', StartX: 2, StartY: 2, EndX: 1002, EndY: 2, Length: 1000 },
        },
      ],
    }
    const { getByText } = render(<DwgDiffCard meta={meta} />)
    expect(getByText('位移 2.8mm')).toBeTruthy()
    expect(getByText('长度差 0.0mm')).toBeTruthy()
  })

  it('无 added/removed 数组时不渲染（避免误判）', () => {
    const { container } = render(<DwgDiffCard meta={{ fileA: 'a.dwg', fileB: 'b.dwg' }} />)
    expect(container.querySelector('.diff-card')).toBeNull()
  })
})

describe('PlatformCard', () => {
  // 结构与桩的 query_history 返回一致
  const realContent = JSON.stringify({
    ok: true,
    result: {
      drawings: {
        total: 2,
        rows: [
          { id: 'DWG-0001', drawingNo: 'JG-2026-001', version: 'A', layerCount: 30, entityCount: 699, submittedBy: '演示用户' },
          { id: 'DWG-0002', drawingNo: 'JG-2026-001', version: 'B', layerCount: 30, entityCount: 704, submittedBy: '演示用户' },
        ],
      },
      operations: {
        total: 4,
        rows: [
          { id: 'OP-0001', drawingNo: 'JG-2026-001', version: 'A', operation: 'parse_drawing', detail: '图层 30 个', operator: '演示用户', result: 'success' },
        ],
      },
      diffs: {
        total: 1,
        rows: [
          { id: 'DIFF-0001', drawingNo: 'JG-2026-001', versionA: 'A', versionB: 'B', counts: { added: 5, removed: 0, modified: 0 }, recordCount: 5 },
        ],
      },
    },
  })

  it('渲染分组标题与总数', () => {
    const { getByText } = render(<PlatformCard content={realContent} />)
    expect(getByText('平台记录')).toBeTruthy()
    expect(getByText('图纸台账')).toBeTruthy()
    expect(getByText('操作留痕')).toBeTruthy()
    expect(getByText('版本差异')).toBeTruthy()
    // 2 + 1 + 1 = 4 条
    expect(getByText('共 4 条')).toBeTruthy()
  })

  it('渲染图纸记录的关键字段', () => {
    const { getByText } = render(<PlatformCard content={realContent} />)
    expect(getByText('JG-2026-001 · A 版')).toBeTruthy()
    // A/B 两版图层数相同，用实体数区分（699 / 704 唯一）
    expect(getByText('图层 30 · 实体 699 · 提交人 演示用户')).toBeTruthy()
    expect(getByText('图层 30 · 实体 704 · 提交人 演示用户')).toBeTruthy()
  })

  it('渲染差异记录的 counts', () => {
    const { getByText } = render(<PlatformCard content={realContent} />)
    expect(getByText(/added=5/)).toBeTruthy()
  })

  it('非 JSON 内容不渲染', () => {
    const { container } = render(<PlatformCard content="(no resources)" />)
    expect(container.firstChild).toBeNull()
  })

  it('JSON 但结构不符时不渲染', () => {
    const { container } = render(<PlatformCard content='{"foo":1}' />)
    expect(container.firstChild).toBeNull()
  })
})
