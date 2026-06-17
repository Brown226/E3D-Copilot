export type Mode = 'plan' | 'act'

export interface StorageConfig {
  apiConfiguration?: any
  autoApprovalSettings?: any
  browserSettings?: any
  customInstructions?: string
  mcpServers?: any[]
}
export const OPENAI_REASONING_EFFORT_OPTIONS = ["low", "medium", "high"];
export function isOpenaiReasoningEffort(_value: any): boolean { return false }
