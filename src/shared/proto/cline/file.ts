// Protobuf type stubs for E3DCopilot WebView2

export class FileOperation {}
export class FileReadRequest {}
export class FileSearchRequest {
  query?: string
  static create(data?: { query?: string }): FileSearchRequest {
    const r = new FileSearchRequest(); if (data) r.query = data.query; return r
  }
}
export class FileSearchType {}
export class RelativePathsRequest {
  static create(_data?: any): RelativePathsRequest { return new RelativePathsRequest() }
}
export class RuleScope {}
export class ToggleAgentsRuleRequest {}
export class ToggleClineRuleRequest {}
export class ToggleCursorRuleRequest {}
export class ToggleSkillRequest {}
export class ToggleWindsurfRuleRequest {}
export class ToggleWorkflowRequest {}
export class DeleteHookRequest {}
export class HooksToggles {}
