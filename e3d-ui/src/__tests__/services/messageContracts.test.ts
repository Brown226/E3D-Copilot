import { describe, it, expect } from 'vitest'
import {
  MessageTypes,
  isMessageType,
  createMessage,
  createUserMessage,
  createApproval,
  createAskResponse,
} from '../../services/messageContracts'

describe('MessageTypes', () => {
  it('should have correct user message types', () => {
    expect(MessageTypes.UserMessage).toBe('user:message')
    expect(MessageTypes.UserCancel).toBe('user:cancel')
    expect(MessageTypes.UserNewSession).toBe('user:new_session')
    expect(MessageTypes.UserApprove).toBe('user:approve')
    expect(MessageTypes.UserAskResponse).toBe('user:ask_response')
    expect(MessageTypes.Ping).toBe('ping')
  })

  it('should have correct backend message types', () => {
    expect(MessageTypes.LlmStreamDelta).toBe('llm:stream:delta')
    expect(MessageTypes.LlmStreamEnd).toBe('llm:stream:end')
    expect(MessageTypes.LlmThinking).toBe('llm:thinking')
    expect(MessageTypes.ToolDispatch).toBe('tool:dispatch')
    expect(MessageTypes.ToolResult).toBe('tool:result')
    expect(MessageTypes.ToolError).toBe('tool:error')
    expect(MessageTypes.ToolApproval).toBe('tool:approval')
    expect(MessageTypes.HostReady).toBe('host:ready')
    expect(MessageTypes.ConfigSync).toBe('config:sync')
    expect(MessageTypes.TurnDone).toBe('turn:done')
  })

  it('should have correct provider/model management types', () => {
    expect(MessageTypes.ModelsList).toBe('models:list')
    expect(MessageTypes.ModelSwitch).toBe('model:switch')
    expect(MessageTypes.ProvidersList).toBe('providers:list')
    expect(MessageTypes.ProviderSave).toBe('provider:save')
    expect(MessageTypes.ProviderDelete).toBe('provider:delete')
    expect(MessageTypes.ProviderFetchModels).toBe('provider:fetch_models')
    expect(MessageTypes.ProviderSetKey).toBe('provider:set_key')
  })

  it('should have correct skills management types', () => {
    expect(MessageTypes.SkillsList).toBe('skills:list')
    expect(MessageTypes.SkillsToggle).toBe('skills:toggle')
    expect(MessageTypes.SkillsAddSource).toBe('skills:add_source')
    expect(MessageTypes.SkillsRemoveSource).toBe('skills:remove_source')
    expect(MessageTypes.SkillsRefresh).toBe('skills:refresh')
  })

  it('should have correct extended event types', () => {
    expect(MessageTypes.LlmTurnStarted).toBe('llm:turn_started')
    expect(MessageTypes.LlmUsage).toBe('llm:usage')
    expect(MessageTypes.LlmRetry).toBe('llm:retry')
    expect(MessageTypes.ToolProgress).toBe('tool:progress')
  })
})

describe('isMessageType', () => {
  it('should return true for valid message types', () => {
    expect(isMessageType('user:message')).toBe(true)
    expect(isMessageType('llm:stream:delta')).toBe(true)
    expect(isMessageType('ping')).toBe(true)
    expect(isMessageType('host:ready')).toBe(true)
  })

  it('should return false for invalid message types', () => {
    expect(isMessageType('unknown:type')).toBe(false)
    expect(isMessageType('')).toBe(false)
    expect(isMessageType('random')).toBe(false)
  })
})

describe('createMessage', () => {
  it('should create a message with type and payload', () => {
    const msg = createMessage(MessageTypes.UserMessage, { text: 'hello' })
    expect(msg.type).toBe('user:message')
    expect(msg.payload).toEqual({ text: 'hello' })
  })

  it('should create a message with complex payload', () => {
    const msg = createMessage(MessageTypes.ToolDispatch, {
      id: 't1',
      name: 'query',
      args: { type: 'PIPE' },
    })
    expect(msg.type).toBe('tool:dispatch')
    expect(msg.payload.id).toBe('t1')
    expect(msg.payload.name).toBe('query')
  })
})

describe('createUserMessage', () => {
  it('should create user message with text only', () => {
    const msg = createUserMessage('hello world')
    expect(msg.type).toBe('user:message')
    expect(msg.payload.text).toBe('hello world')
    expect(msg.payload.images).toBeUndefined()
    expect(msg.payload.files).toBeUndefined()
  })

  it('should create user message with images', () => {
    const msg = createUserMessage('check this', ['data:image/png;base64,...'])
    expect(msg.payload.images).toEqual(['data:image/png;base64,...'])
  })

  it('should create user message with files', () => {
    const msg = createUserMessage('process', undefined, ['file:///path/to/file.txt'])
    expect(msg.payload.files).toEqual(['file:///path/to/file.txt'])
  })
})

describe('createApproval', () => {
  it('should create approval with allow=true', () => {
    const msg = createApproval('tool_123', true)
    expect(msg.type).toBe('user:approve')
    expect(msg.payload.id).toBe('tool_123')
    expect(msg.payload.allow).toBe(true)
  })

  it('should create approval with allow=false', () => {
    const msg = createApproval('tool_456', false)
    expect(msg.payload.allow).toBe(false)
  })
})

describe('createAskResponse', () => {
  it('should create ask response', () => {
    const msg = createAskResponse('q_001', 'Yes, proceed')
    expect(msg.type).toBe('user:ask_response')
    expect(msg.payload.questionId).toBe('q_001')
    expect(msg.payload.answer).toBe('Yes, proceed')
  })
})
