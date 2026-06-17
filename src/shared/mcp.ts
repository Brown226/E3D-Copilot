export interface McpServer {
  name: string
  config: any
  status: 'connected' | 'connecting' | 'disconnected'
  tools?: any[]
}

export interface McpMarketplaceCatalog {
  servers: McpServer[]
}

export type McpViewTab = 'installed' | 'marketplace'
export const DEFAULT_MCP_TIMEOUT_SECONDS = 60;

