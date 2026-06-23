// Protobuf type stubs for E3DCopilot WebView2

export class AskResponseRequest {
	responseType?: string
	text?: string
	images?: string[]
	files?: string[]
	static create(data?: any): AskResponseRequest {
		const r = new AskResponseRequest()
		if (data) Object.assign(r, data)
		return r
	}
}

export class NewTaskRequest {
	text?: string
	images?: string[]
	files?: string[]
	static create(data?: any): NewTaskRequest {
		const r = new NewTaskRequest()
		if (data) Object.assign(r, data)
		return r
	}
}

export class GetTaskHistoryRequest {
	static create(_data?: any): GetTaskHistoryRequest { return new GetTaskHistoryRequest() }
}

export class TaskFavoriteRequest {
	static create(_data?: any): TaskFavoriteRequest { return new TaskFavoriteRequest() }
}
