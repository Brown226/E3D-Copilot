import { describe, it, expect, vi } from 'vitest'
import { render } from '@testing-library/react'

vi.mock('mermaid', () => ({
  default: {
    initialize: vi.fn(),
    render: vi.fn(async (id: string, _code: string) => ({
      svg: '<svg data-testid="mock-svg"></svg>',
      bindFunctions: vi.fn(),
    })),
  },
}))

import MermaidDiagram from '@/components/common/MermaidDiagram'
import MarkdownBlock from '@/components/common/MarkdownBlock'

describe('MermaidDiagram', () => {
  it('renders mermaid code as SVG', async () => {
    const code = 'graph TD\n  A --> B'
    const { container, findByTestId } = render(<MermaidDiagram code={code} />)
    const svg = await findByTestId('mock-svg')
    expect(svg).toBeTruthy()
    expect(container.querySelector('.mermaid-container')).toBeTruthy()
  })

  it('shows error message on invalid mermaid syntax', async () => {
    const mermaidDefault = (await import('mermaid')).default
    ;(mermaidDefault.render as any).mockRejectedValueOnce(new Error('parse error'))
    const code = 'invalid syntax @@@'
    const { findByText } = render(<MermaidDiagram code={code} />)
    const errEl = await findByText(/mermaid 渲染失败/i)
    expect(errEl).toBeTruthy()
  })
})

describe('MarkdownBlock mermaid integration', () => {
  it('renders mermaid code block as MermaidDiagram', async () => {
    const md = '```mermaid\ngraph TD\n  A --> B\n```'
    const { findByTestId } = render(<MarkdownBlock markdown={md} />)
    const svg = await findByTestId('mock-svg')
    expect(svg).toBeTruthy()
  })

  it('does not render normal code blocks as mermaid', async () => {
    const md = '```javascript\nconst x = 1\n```'
    const { container } = render(<MarkdownBlock markdown={md} />)
    expect(container.querySelector('.mermaid-container')).toBeNull()
  })
})
