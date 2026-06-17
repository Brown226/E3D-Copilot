export function getApiMetrics(messages: any[]): { totalTokens: number; totalCost: number; totalRequests: number } {
  return { totalTokens: 0, totalCost: 0, totalRequests: 0 }
}

export function getLastApiReqTotalTokens(messages: any[]): number | null {
  return null
}
