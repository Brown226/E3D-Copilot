export interface AutoApprovalSettings {
  enabled: boolean
  actions: Record<string, boolean>
  maxRequests?: number
}

export const DEFAULT_AUTO_APPROVAL_SETTINGS: AutoApprovalSettings = {
  enabled: false,
  actions: {
    read: true,
    write: false,
    command: false,
    browser: false,
    mcp: false,
  },
}
