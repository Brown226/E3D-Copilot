import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useChatStore } from '../../store/useChatStore'

describe('useChatStore', () => {
  beforeEach(() => {
    // Reset store to initial state
    useChatStore.setState({
      inputValue: '',
      currentProvider: '',
      currentModel: '',
      isPlanMode: false,
      toolApprovalMode: 'ask',
      providers: [],
      models: [],
      showSettings: false,
      showHistory: false,
      showCommandPalette: false,
      isLoadingModels: false,
      error: null,
      bridgeConnected: false,
      lastPingTime: null,
      turnStartAt: null,
      turnTokens: 0,
      sessionTokens: 0,
      isRetrying: false,
    })
    // Reset tabs
    const initialTab = {
      id: 'test_tab',
      title: '新对话',
      messages: [],
      isStreaming: false,
      currentAssistantMsgId: null,
      currentThinkingMsgId: null,
      pendingApproval: null,
    }
    useChatStore.setState({
      tabs: [initialTab],
      activeTabId: 'test_tab',
    })
    vi.clearAllMocks()
  })

  describe('initial state', () => {
    it('should start with empty tabs messages', () => {
      expect(useChatStore.getState().tabs[0].messages).toEqual([])
    })

    it('should start with bridgeConnected=false', () => {
      expect(useChatStore.getState().bridgeConnected).toBe(false)
    })

    it('should start with empty inputValue', () => {
      expect(useChatStore.getState().inputValue).toBe('')
    })

    it('should start with isPlanMode=false', () => {
      expect(useChatStore.getState().isPlanMode).toBe(false)
    })

    it('should start with toolApprovalMode=ask', () => {
      expect(useChatStore.getState().toolApprovalMode).toBe('ask')
    })
  })

  describe('setInputValue', () => {
    it('should set the input value', () => {
      useChatStore.getState().setInputValue('hello world')
      expect(useChatStore.getState().inputValue).toBe('hello world')
    })

    it('should clear the input value', () => {
      useChatStore.getState().setInputValue('hello')
      useChatStore.getState().setInputValue('')
      expect(useChatStore.getState().inputValue).toBe('')
    })
  })

  describe('appendMessage', () => {
    it('should add a user message to active tab', () => {
      useChatStore.getState().appendMessage({ role: 'user', content: 'Hello' })
      const tab = useChatStore.getState().tabs[0]
      expect(tab.messages).toHaveLength(1)
      expect(tab.messages[0].role).toBe('user')
      expect(tab.messages[0].content).toBe('Hello')
    })

    it('should add an assistant message', () => {
      useChatStore.getState().appendMessage({ role: 'assistant', content: 'Hi there' })
      const tab = useChatStore.getState().tabs[0]
      expect(tab.messages[0].role).toBe('assistant')
    })

    it('should append messages in order', () => {
      useChatStore.getState().appendMessage({ role: 'user', content: 'First' })
      useChatStore.getState().appendMessage({ role: 'assistant', content: 'Second' })
      const tab = useChatStore.getState().tabs[0]
      expect(tab.messages).toHaveLength(2)
      expect(tab.messages[0].content).toBe('First')
      expect(tab.messages[1].content).toBe('Second')
    })

    it('should auto-generate message id', () => {
      useChatStore.getState().appendMessage({ role: 'user', content: 'Test' })
      expect(useChatStore.getState().tabs[0].messages[0].id).toBeTruthy()
    })
  })

  describe('startStreaming', () => {
    it('should create an empty assistant message and set streaming', () => {
      useChatStore.getState().startStreaming()
      const tab = useChatStore.getState().tabs[0]
      expect(tab.isStreaming).toBe(true)
      expect(tab.currentAssistantMsgId).toBeTruthy()
      expect(tab.messages).toHaveLength(1)
      expect(tab.messages[0].role).toBe('assistant')
      expect(tab.messages[0].content).toBe('')
    })
  })

  describe('appendAssistantDelta', () => {
    it('should append delta to current assistant message', () => {
      useChatStore.getState().startStreaming()
      useChatStore.getState().appendAssistantDelta('Hello')
      useChatStore.getState().appendAssistantDelta(' World')
      const tab = useChatStore.getState().tabs[0]
      expect(tab.messages[0].content).toBe('Hello World')
    })
  })

  describe('stopStreaming', () => {
    it('should set isStreaming to false', () => {
      useChatStore.getState().startStreaming()
      useChatStore.getState().stopStreaming()
      expect(useChatStore.getState().tabs[0].isStreaming).toBe(false)
    })
  })

  describe('setBridgeConnected', () => {
    it('should set bridge connection state', () => {
      useChatStore.getState().setBridgeConnected(true)
      expect(useChatStore.getState().bridgeConnected).toBe(true)
    })

    it('should clear bridge connection state', () => {
      useChatStore.getState().setBridgeConnected(true)
      useChatStore.getState().setBridgeConnected(false)
      expect(useChatStore.getState().bridgeConnected).toBe(false)
    })
  })

  describe('setPlanMode', () => {
    it('should enable plan mode', () => {
      useChatStore.getState().setPlanMode(true)
      expect(useChatStore.getState().isPlanMode).toBe(true)
    })

    it('should disable plan mode', () => {
      useChatStore.getState().setPlanMode(true)
      useChatStore.getState().setPlanMode(false)
      expect(useChatStore.getState().isPlanMode).toBe(false)
    })
  })

  describe('togglePlanMode', () => {
    it('should toggle plan mode', () => {
      expect(useChatStore.getState().isPlanMode).toBe(false)
      useChatStore.getState().togglePlanMode()
      expect(useChatStore.getState().isPlanMode).toBe(true)
      useChatStore.getState().togglePlanMode()
      expect(useChatStore.getState().isPlanMode).toBe(false)
    })
  })

  describe('setToolApprovalMode', () => {
    it('should set approval mode to auto', () => {
      useChatStore.getState().setToolApprovalMode('auto')
      expect(useChatStore.getState().toolApprovalMode).toBe('auto')
    })

    it('should set approval mode to yolo', () => {
      useChatStore.getState().setToolApprovalMode('yolo')
      expect(useChatStore.getState().toolApprovalMode).toBe('yolo')
    })
  })

  describe('setConfig', () => {
    it('should set provider and model config', () => {
      useChatStore.getState().setConfig({
        currentProvider: 'openai',
        currentModel: 'gpt-4',
        providers: [],
      })
      expect(useChatStore.getState().currentProvider).toBe('openai')
      expect(useChatStore.getState().currentModel).toBe('gpt-4')
    })
  })

  describe('sendMessage', () => {
    it('should add user message and call bridgeSend', () => {
      useChatStore.getState().setInputValue('Hello world')
      const bridgeSend = vi.fn()
      useChatStore.getState().sendMessage(bridgeSend)

      const tab = useChatStore.getState().tabs[0]
      expect(tab.messages).toHaveLength(1)
      expect(tab.messages[0].role).toBe('user')
      expect(tab.messages[0].content).toBe('Hello world')
      expect(bridgeSend).toHaveBeenCalledWith('Hello world', undefined, undefined, useChatStore.getState().activeTabId)
    })

    it('should clear input value after sending', () => {
      useChatStore.getState().setInputValue('Hello')
      const bridgeSend = vi.fn()
      useChatStore.getState().sendMessage(bridgeSend)
      expect(useChatStore.getState().inputValue).toBe('')
    })

    it('should not send empty message', () => {
      useChatStore.getState().setInputValue('')
      const bridgeSend = vi.fn()
      useChatStore.getState().sendMessage(bridgeSend)
      expect(bridgeSend).not.toHaveBeenCalled()
    })

    it('should use overrideText when provided', () => {
      useChatStore.getState().setInputValue('original')
      const bridgeSend = vi.fn()
      useChatStore.getState().sendMessage(bridgeSend, undefined, 'override')
      expect(bridgeSend).toHaveBeenCalledWith('override', undefined, undefined, expect.any(String))
    })

    it('should set tab title from first message', () => {
      useChatStore.getState().setInputValue('My first question')
      const bridgeSend = vi.fn()
      useChatStore.getState().sendMessage(bridgeSend)
      expect(useChatStore.getState().tabs[0].title).toBe('My first question')
    })
  })

  describe('Tab operations', () => {
    it('should create a new tab', () => {
      const tabId = useChatStore.getState().createTab('Test Tab')
      expect(useChatStore.getState().tabs).toHaveLength(2)
      expect(useChatStore.getState().activeTabId).toBe(tabId)
    })

    it('should close a tab', () => {
      const tabId = useChatStore.getState().createTab('Tab 2')
      useChatStore.getState().closeTab(tabId)
      expect(useChatStore.getState().tabs).toHaveLength(1)
    })

    it('should not close the last tab', () => {
      const tabId = useChatStore.getState().tabs[0].id
      useChatStore.getState().closeTab(tabId)
      expect(useChatStore.getState().tabs).toHaveLength(1)
    })

    it('should set active tab', () => {
      const tabId = useChatStore.getState().createTab('Tab 2')
      const firstTabId = useChatStore.getState().tabs[0].id
      useChatStore.getState().setActiveTab(firstTabId)
      expect(useChatStore.getState().activeTabId).toBe(firstTabId)
    })

    it('should update tab title', () => {
      const tabId = useChatStore.getState().tabs[0].id
      useChatStore.getState().updateTabTitle(tabId, 'New Title')
      expect(useChatStore.getState().tabs[0].title).toBe('New Title')
    })
  })

  describe('setPendingApproval', () => {
    it('should set pending approval', () => {
      const approval = { toolId: 't1', toolName: 'query', args: { type: 'PIPE' } }
      useChatStore.getState().setPendingApproval(approval)
      expect(useChatStore.getState().tabs[0].pendingApproval).toEqual(approval)
    })

    it('should clear pending approval', () => {
      useChatStore.getState().setPendingApproval({ toolId: 't1', toolName: 'query' })
      useChatStore.getState().setPendingApproval(null)
      expect(useChatStore.getState().tabs[0].pendingApproval).toBeNull()
    })
  })

  describe('Streaming stats', () => {
    it('should set turn start timestamp', () => {
      useChatStore.getState().setTurnStart(12345)
      expect(useChatStore.getState().turnStartAt).toBe(12345)
    })

    it('should add turn tokens', () => {
      useChatStore.getState().addTurnTokens(100)
      expect(useChatStore.getState().turnTokens).toBe(100)
      expect(useChatStore.getState().sessionTokens).toBe(100)
      useChatStore.getState().addTurnTokens(50)
      expect(useChatStore.getState().turnTokens).toBe(150)
      expect(useChatStore.getState().sessionTokens).toBe(150)
    })

    it('should reset turn stats', () => {
      useChatStore.getState().addTurnTokens(100)
      useChatStore.getState().setTurnStart(12345)
      useChatStore.getState().resetTurnStats()
      expect(useChatStore.getState().turnStartAt).toBeNull()
      expect(useChatStore.getState().turnTokens).toBe(0)
    })
  })

  describe('setRetrying', () => {
    it('should set retrying state', () => {
      useChatStore.getState().setRetrying(true)
      expect(useChatStore.getState().isRetrying).toBe(true)
    })
  })

  describe('toggleSettings / toggleHistory / toggleCommandPalette', () => {
    it('should toggle showSettings', () => {
      expect(useChatStore.getState().showSettings).toBe(false)
      useChatStore.getState().toggleSettings()
      expect(useChatStore.getState().showSettings).toBe(true)
      useChatStore.getState().toggleSettings()
      expect(useChatStore.getState().showSettings).toBe(false)
    })

    it('should toggle showHistory', () => {
      expect(useChatStore.getState().showHistory).toBe(false)
      useChatStore.getState().toggleHistory()
      expect(useChatStore.getState().showHistory).toBe(true)
    })

    it('should toggle showCommandPalette', () => {
      expect(useChatStore.getState().showCommandPalette).toBe(false)
      useChatStore.getState().toggleCommandPalette()
      expect(useChatStore.getState().showCommandPalette).toBe(true)
    })
  })
})
