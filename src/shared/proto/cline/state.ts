// Protobuf type stubs for E3DCopilot WebView2

export class ClineState {
	static create(_data?: any): ClineState { return new ClineState() }
}

export class PlanActMode {
	static PLAN = "plan"
	static ACT = "act"
}
export class TogglePlanActModeRequest {
	mode?: string
	chatContent?: { message?: string; images?: string[]; files?: string[] }
	static create(data?: any): TogglePlanActModeRequest {
		const r = new TogglePlanActModeRequest()
		if (data) Object.assign(r, data)
		return r
	}
}
export class UpdateSettingsRequest {
	static create(_data?: any): UpdateSettingsRequest { return new UpdateSettingsRequest() }
}
export class McpDisplayMode {}

export enum TelemetrySettingEnum {
	UNSET = 0,
	ENABLED = 1,
	DISABLED = 2,
}
export class TelemetrySettingRequest {
	setting?: TelemetrySettingEnum
	static create(data?: { setting?: TelemetrySettingEnum }): TelemetrySettingRequest {
		const r = new TelemetrySettingRequest()
		if (data) r.setting = data.setting
		return r
	}
}

export class OnboardingModel {
	id: string = ""
	name: string = ""
	score: number = 0
	latency: number = 0
	badge: string = ""
	group: string = ""
	info?: {
		contextWindow: number
		supportsImages: boolean
		supportsPromptCache: boolean
		inputPrice: number
		outputPrice: number
		tiers: any[]
	}
	static create(_data?: any): OnboardingModel { return new OnboardingModel() }
}

export class OnboardingModelGroup {
	models: OnboardingModel[] = []
	static create(_data?: any): OnboardingModelGroup { return new OnboardingModelGroup() }
}

export class TerminalProfile {
	static create(_data?: any): TerminalProfile { return new TerminalProfile() }
}

export class ResetStateRequest {
	static create(_data?: any): ResetStateRequest { return new ResetStateRequest() }
}
