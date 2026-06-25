import { describe, it, expect, beforeEach, vi } from 'vitest'
import bridge from '../../services/bridgeService'

describe('bridgeService', () => {
  describe('Bridge instance', () => {
    it('should export a bridge instance', () => {
      expect(bridge).toBeDefined()
      expect(typeof bridge.send).toBe('function')
      expect(typeof bridge.on).toBe('function')
    })

    it('should report availability based on chrome.webview', () => {
      // In test env, chrome.webview is mocked in setup.ts
      const available = bridge.isAvailable()
      expect(typeof available).toBe('boolean')
    })
  })

  describe('on / listener management', () => {
    it('should register a listener and return unsubscribe function', () => {
      const callback = vi.fn()
      const unsub = bridge.on(callback)
      expect(typeof unsub).toBe('function')
      unsub() // cleanup
    })

    it('should call listener when message dispatched via startMock', () => {
      const callback = vi.fn()
      const unsub = bridge.on(callback)
      // startMock sends host:ready immediately
      bridge.startMock()
      expect(callback).toHaveBeenCalled()
      unsub()
      bridge.stopMock()
    })

    it('should not call listener after unsubscribe', () => {
      const callback = vi.fn()
      const unsub = bridge.on(callback)
      unsub()
      bridge.startMock()
      expect(callback).not.toHaveBeenCalled()
      bridge.stopMock()
    })
  })

  describe('send', () => {
    it('should not throw when sending a message', () => {
      expect(() => bridge.send('test:type', { data: 'hello' })).not.toThrow()
    })

    it('should send user message via sendUserMessage', () => {
      expect(() => bridge.sendUserMessage('hello')).not.toThrow()
    })

    it('should send approval via sendApproval', () => {
      expect(() => bridge.sendApproval('tool_1', true)).not.toThrow()
    })

    it('should send cancel via cancel', () => {
      expect(() => bridge.cancel()).not.toThrow()
    })

    it('should send new session via newSession', () => {
      expect(() => bridge.newSession()).not.toThrow()
    })
  })

  describe('typed convenience methods', () => {
    it('should register onLlmStreamDelta', () => {
      const cb = vi.fn()
      const unsub = bridge.onLlmStreamDelta(cb)
      expect(typeof unsub).toBe('function')
      unsub()
    })

    it('should register onToolDispatch', () => {
      const cb = vi.fn()
      const unsub = bridge.onToolDispatch(cb)
      expect(typeof unsub).toBe('function')
      unsub()
    })

    it('should register onToolResult', () => {
      const cb = vi.fn()
      const unsub = bridge.onToolResult(cb)
      expect(typeof unsub).toBe('function')
      unsub()
    })

    it('should register onToolError', () => {
      const cb = vi.fn()
      const unsub = bridge.onToolError(cb)
      expect(typeof unsub).toBe('function')
      unsub()
    })

    it('should register onHostReady', () => {
      const cb = vi.fn()
      const unsub = bridge.onHostReady(cb)
      expect(typeof unsub).toBe('function')
      unsub()
    })

    it('should register onConfigSync', () => {
      const cb = vi.fn()
      const unsub = bridge.onConfigSync(cb)
      expect(typeof unsub).toBe('function')
      unsub()
    })

    it('should register onNotice', () => {
      const cb = vi.fn()
      const unsub = bridge.onNotice(cb)
      expect(typeof unsub).toBe('function')
      unsub()
    })

    it('should register onError', () => {
      const cb = vi.fn()
      const unsub = bridge.onError(cb)
      expect(typeof unsub).toBe('function')
      unsub()
    })

    it('should register onDisconnected', () => {
      const cb = vi.fn()
      const unsub = bridge.onDisconnected(cb)
      expect(typeof unsub).toBe('function')
      unsub()
    })

    it('should register onReconnected', () => {
      const cb = vi.fn()
      const unsub = bridge.onReconnected(cb)
      expect(typeof unsub).toBe('function')
      unsub()
    })
  })

  describe('emitDisconnected / emitReconnected', () => {
    it('should emit disconnected event', () => {
      const cb = vi.fn()
      const unsub = bridge.onDisconnected(cb)
      bridge.emitDisconnected()
      expect(cb).toHaveBeenCalled()
      unsub()
    })

    it('should emit reconnected event', () => {
      const cb = vi.fn()
      const unsub = bridge.onReconnected(cb)
      bridge.emitReconnected()
      expect(cb).toHaveBeenCalled()
      unsub()
    })
  })

  describe('sendAndWait', () => {
    it('should have sendAndWait method', () => {
      expect(typeof bridge.sendAndWait).toBe('function')
    })
  })
})
