export interface HistoryItem {
  id: string
  label: string
  ts: number
  tokens: number
  messageCount: number
  task?: string
  reduced?: boolean
  deleted?: boolean
}
