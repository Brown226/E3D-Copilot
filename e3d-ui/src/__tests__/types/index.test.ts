import { describe, it, expect } from 'vitest'
import { generateMessageId } from '../../types'

describe('generateMessageId', () => {
  it('should return a non-empty string', () => {
    const id = generateMessageId()
    expect(id).toBeTruthy()
    expect(typeof id).toBe('string')
  })

  it('should generate unique IDs', () => {
    const ids = new Set<string>()
    for (let i = 0; i < 100; i++) {
      ids.add(generateMessageId())
    }
    expect(ids.size).toBe(100)
  })

  it('should contain base36 characters', () => {
    const id = generateMessageId()
    // Date.now().toString(36) + random.toString(36).slice(2)
    expect(id).toMatch(/^[a-z0-9]+$/)
  })
})
