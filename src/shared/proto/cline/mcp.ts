export class McpServer {}
export class McpTool {}
export class McpServers {
  servers: McpServer[] = []
  static create(_data?: any): McpServers { return new McpServers() }
}
export class AddRemoteMcpServerRequest { static create(_data?: any): AddRemoteMcpServerRequest { return new AddRemoteMcpServerRequest() } }
export class ToggleToolAutoApproveRequest { static create(_data?: any): ToggleToolAutoApproveRequest { return new ToggleToolAutoApproveRequest() } }
export class ToggleMcpServerRequest { static create(_data?: any): ToggleMcpServerRequest { return new ToggleMcpServerRequest() } }
