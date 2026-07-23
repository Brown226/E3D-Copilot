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
