/**
 * ApprovalModeSwitcher — 四档权限模式选择器测试
 *
 * 覆盖两个关键点：
 * 1. 四档全部可选、选中后写进 store（regression：以前只有 ask/auto 二态，yolo 选不到）
 * 2. 每档都有正确的 label 与 hint（用户能看懂区别）
 */

import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { ApprovalModeSwitcher } from '@/components/chat/ApprovalModeSwitcher'
import { useChatStore } from '@/store/useChatStore'

describe('ApprovalModeSwitcher', () => {
  beforeEach(() => {
    useChatStore.setState({
      toolApprovalMode: 'auto',
      bridgeConnected: true,
    })
  })

  it('默认显示当前模式的 label', () => {
    render(<ApprovalModeSwitcher />)
    expect(screen.getByText('自主模式')).toBeTruthy()
  })

  it('点击后展开四档选项', () => {
    render(<ApprovalModeSwitcher />)
    fireEvent.click(screen.getByRole('button'))

    expect(screen.getByText('计划模式')).toBeTruthy()
    expect(screen.getByText('变更前确认')).toBeTruthy()
    // 「自主模式」此时出现两次：触发器上显示的当前值 + 菜单项
    expect(screen.getAllByText('自主模式').length).toBe(2)
    expect(screen.getByText('完全访问')).toBeTruthy()
  })

  it('每档都带说明文字', () => {
    render(<ApprovalModeSwitcher />)
    fireEvent.click(screen.getByRole('button'))

    expect(screen.getByText('编辑前先出计划')).toBeTruthy()
    expect(screen.getByText('改文件前先问我')).toBeTruthy()
    expect(screen.getByText('在有风险时询问')).toBeTruthy()
    expect(screen.getByText('减少确认次数')).toBeTruthy()
  })

  it('选择「完全访问」后写入 store 为 yolo', () => {
    render(<ApprovalModeSwitcher />)
    fireEvent.click(screen.getByRole('button'))
    fireEvent.click(screen.getByText('完全访问'))

    expect(useChatStore.getState().toolApprovalMode).toBe('yolo')
  })

  it('选择「计划模式」后写入 store 为 plan', () => {
    render(<ApprovalModeSwitcher />)
    fireEvent.click(screen.getByRole('button'))
    fireEvent.click(screen.getByText('计划模式'))

    expect(useChatStore.getState().toolApprovalMode).toBe('plan')
  })

  it('选择「变更前确认」后写入 store 为 ask', () => {
    useChatStore.setState({ toolApprovalMode: 'yolo' })
    render(<ApprovalModeSwitcher />)
    fireEvent.click(screen.getByRole('button'))
    fireEvent.click(screen.getByText('变更前确认'))

    expect(useChatStore.getState().toolApprovalMode).toBe('ask')
  })

  it('选中后浮层关闭', () => {
    render(<ApprovalModeSwitcher />)
    fireEvent.click(screen.getByRole('button'))
    fireEvent.click(screen.getByText('完全访问'))

    // 浮层关闭后「编辑前先出计划」这条 hint 不再存在
    expect(screen.queryByText('编辑前先出计划')).toBeNull()
  })

  it('未连接后端时触发器禁用', () => {
    useChatStore.setState({ bridgeConnected: false })
    render(<ApprovalModeSwitcher />)
    expect(screen.getByRole('button')).toHaveProperty('disabled', true)
  })
})
