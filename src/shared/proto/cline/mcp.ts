export class McpServer {}
export class McpTool {}
export class McpServers {
	servers: McpServer[] = []
	static create(data?: any): McpServers {
		const r = new McpServers()
		if (data) Object.assign(r, data)
		return r
	}
}
export class AddRemoteMcpServerRequest {
	static create(data?: any): AddRemoteMcpServerRequest {
		const r = new AddRemoteMcpServerRequest()
		if (data) Object.assign(r, data)
		return r
	}
}
export class ToggleToolAutoApproveRequest {
	static create(data?: any): ToggleToolAutoApproveRequest {
		const r = new ToggleToolAutoApproveRequest()
		if (data) Object.assign(r, data)
		return r
	}
}
export class ToggleMcpServerRequest {
	static create(data?: any): ToggleMcpServerRequest {
		const r = new ToggleMcpServerRequest()
		if (data) Object.assign(r, data)
		return r
	}
}
export class UpdateMcpTimeoutRequest {
	static create(data?: any): UpdateMcpTimeoutRequest {
		const r = new UpdateMcpTimeoutRequest()
		if (data) Object.assign(r, data)
		return r
	}
}
