import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useToastStore } from '../../store/useToastStore'

describe('useToastStore', () => {
  beforeEach(() => {
    // Reset store
    useToastStore.setState({ toasts: [] })
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  describe('addToast', () => {
    it('should add a toast to the list', () => {
      useToastStore.getState().addToast('info', 'Hello')
      const { toasts } = useToastStore.getState()
      expect(toasts).toHaveLength(1)
      expect(toasts[0].type).toBe('info')
      expect(toasts[0].message).toBe('Hello')
    })

    it('should assign a unique id', () => {
      useToastStore.getState().addToast('info', 'A')
      useToastStore.getState().addToast('info', 'B')
      const { toasts } = useToastStore.getState()
      expect(toasts[0].id).not.toBe(toasts[1].id)
    })

    it('should set default duration of 4000ms', () => {
      useToastStore.getState().addToast('info', 'Test')
      const { toasts } = useToastStore.getState()
      expect(toasts[0].duration).toBe(4000)
    })

    it('should allow custom duration', () => {
      useToastStore.getState().addToast('warning', 'Test', 2000)
      const { toasts } = useToastStore.getState()
      expect(toasts[0].duration).toBe(2000)
    })

    it('should auto-remove after duration', () => {
      useToastStore.getState().addToast('info', 'Auto', 3000)
      expect(useToastStore.getState().toasts).toHaveLength(1)

      vi.advanceTimersByTime(3000)

      expect(useToastStore.getState().toasts).toHaveLength(0)
    })

    it('should not auto-remove when duration is 0', () => {
      useToastStore.getState().addToast('info', 'Persistent', 0)
      vi.advanceTimersByTime(10000)
      expect(useToastStore.getState().toasts).toHaveLength(1)
    })

    it('should support all toast types', () => {
      const types = ['success', 'info', 'warning', 'error'] as const
      types.forEach((type) => {
        useToastStore.getState().addToast(type, `Message ${type}`, 0)
      })
      const { toasts } = useToastStore.getState()
      expect(toasts).toHaveLength(4)
      expect(toasts.map(t => t.type)).toEqual(types)
    })
  })

  describe('removeToast', () => {
    it('should remove a specific toast by id', () => {
      useToastStore.getState().addToast('info', 'A', 0)
      useToastStore.getState().addToast('info', 'B', 0)
      const { toasts } = useToastStore.getState()
      const idToRemove = toasts[0].id

      useToastStore.getState().removeToast(idToRemove)

      const remaining = useToastStore.getState().toasts
      expect(remaining).toHaveLength(1)
      expect(remaining[0].message).toBe('B')
    })

    it('should not throw when removing non-existent id', () => {
      useToastStore.getState().addToast('info', 'A', 0)
      expect(() => {
        useToastStore.getState().removeToast('nonexistent')
      }).not.toThrow()
      expect(useToastStore.getState().toasts).toHaveLength(1)
    })
  })

  describe('clearAll', () => {
    it('should remove all toasts', () => {
      useToastStore.getState().addToast('info', 'A', 0)
      useToastStore.getState().addToast('error', 'B', 0)
      useToastStore.getState().addToast('warning', 'C', 0)

      useToastStore.getState().clearAll()

      expect(useToastStore.getState().toasts).toHaveLength(0)
    })
  })
})
