// Protobuf type stubs for E3DCopilot WebView2

export class ClineState {
  static create(_data?: any): ClineState { return new ClineState() }
}

export class PlanActMode {}
export class TogglePlanActModeRequest {
  planActMode?: boolean
  chatContent?: string
  static create(data?: { planActMode?: boolean; chatContent?: string }): TogglePlanActModeRequest {
    const r = new TogglePlanActModeRequest()
    if (data) { r.planActMode = data.planActMode; r.chatContent = data.chatContent }
    return r
  }
}
export class UpdateSettingsRequest {
  static create(_data?: any): UpdateSettingsRequest { return new UpdateSettingsRequest() }
}
export class McpDisplayMode {}
export class OnboardingModelGroup {}
export class TerminalProfile {}
export class ResetStateRequest { static create(_data?: any): ResetStateRequest { return new ResetStateRequest() } }
