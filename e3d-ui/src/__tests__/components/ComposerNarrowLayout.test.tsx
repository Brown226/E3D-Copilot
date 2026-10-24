import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render } from '@testing-library/react'

/**
 * 输入栏窄宽度降级 —— 组件侧回归。
 *
 * 分工说明（重要）：
 *   本文件只校验「组件确实输出了可被容器查询命中的类名」。
 *   CSS 规则本身与几何效果不在这里测，原因：jsdom 不实现 @container，
 *   渲染后取不到降级后的 computed style；且该环境无文件系统访问，
 *   读不到 index.css 源码。真正几何效果由浏览器实测覆盖：
 *     760/700/620/560/500/440/380/320 八档 overlapPairs 全为 0；
 *     还原修复前样式的对照组从 500px 起出现重叠，380px 达 8 对 / 20px。
 *
 *   这里锁住的是最容易悄悄回退的一环：类名一旦丢了或被改名，
 *   CSS 里的 @container 规则会静默失效，浏览器不报任何错。
 */

vi.mock('@/services/bridgeService', () => ({
  default: {
    send: vi.fn().mockResolvedValue({ success: true }),
    sendAndWait: vi.fn().mockResolvedValue({ success: true, tools: [] }),
    onMessage: vi.fn(),
    offMessage: vi.fn(),
  },
}))

import { ApprovalModeSwitcher } from '@/components/chat/ApprovalModeSwitcher'
import { CadImportButton } from '@/components/chat/CadImportButton'
import { TracePanel } from '@/components/chat/TracePanel'
import { ModelSwitcher } from '@/components/chat/ModelSwitcher'
import { useChatStore } from '@/store/useChatStore'

describe('输入栏窄宽度降级：组件类名', () => {
  beforeEach(() => {
    useChatStore.setState({ bridgeConnected: true })
  })

  it('诊断按钮带 trace-trigger 与可隐藏标签', () => {
    const { container } = render(<TracePanel />)
    expect(container.querySelector('.trace-trigger')).toBeTruthy()
    expect(container.querySelector('.trace-trigger__label')).toBeTruthy()
  })

  it('CAD 按钮带 cad-import-trigger 与可隐藏标签', () => {
    const { container } = render(<CadImportButton />)
    expect(container.querySelector('.cad-import-trigger')).toBeTruthy()
    expect(container.querySelector('.cad-import-trigger__label')).toBeTruthy()
  })

  it('权限选择器带可隐藏标签', () => {
    const { container } = render(<ApprovalModeSwitcher />)
    expect(container.querySelector('.perm-switch__trigger')).toBeTruthy()
    expect(container.querySelector('.perm-switch__trigger-label')).toBeTruthy()
  })

  it('模型选择器带可隐藏标签与按钮类名', () => {
    const { container } = render(<ModelSwitcher />)
    expect(container.querySelector('.model-switch__trigger')).toBeTruthy()
    expect(container.querySelector('.model-switch__label')).toBeTruthy()
  })
})
