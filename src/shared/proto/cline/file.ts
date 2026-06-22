// Protobuf type stubs for E3DCopilot WebView2

export class FileOperation {
  static create(_data?: any): FileOperation { return new FileOperation() }
}
export class FileReadRequest {
  static create(_data?: any): FileReadRequest { return new FileReadRequest() }
}
export class FileSearchRequest {
  query?: string
  static create(data?: { query?: string }): FileSearchRequest {
    const r = new FileSearchRequest(); if (data) r.query = data.query; return r
  }
}
export class FileSearchType {
  static create(_data?: any): FileSearchType { return new FileSearchType() }
}
export class RelativePathsRequest {
  static create(_data?: any): RelativePathsRequest { return new RelativePathsRequest() }
}

// enum RuleScope
export class RuleScope {
  static LOCAL = 0
  static GLOBAL = 1
  static REMOTE = 2
  static create(_data?: any): RuleScope { return new RuleScope() }
}

export class ToggleAgentsRuleRequest {
  filePath?: string
  enabled?: boolean
  static create(_data?: any): ToggleAgentsRuleRequest { return new ToggleAgentsRuleRequest() }
}
export class ToggleClineRuleRequest {
  filePath?: string
  enabled?: boolean
  static create(_data?: any): ToggleClineRuleRequest { return new ToggleClineRuleRequest() }
}
export class ToggleCursorRuleRequest {
  filePath?: string
  enabled?: boolean
  static create(_data?: any): ToggleCursorRuleRequest { return new ToggleCursorRuleRequest() }
}
export class ToggleSkillRequest {
  filePath?: string
  enabled?: boolean
  static create(_data?: any): ToggleSkillRequest { return new ToggleSkillRequest() }
}
export class ToggleWindsurfRuleRequest {
  filePath?: string
  enabled?: boolean
  static create(_data?: any): ToggleWindsurfRuleRequest { return new ToggleWindsurfRuleRequest() }
}
export class ToggleWorkflowRequest {
  filePath?: string
  enabled?: boolean
  static create(_data?: any): ToggleWorkflowRequest { return new ToggleWorkflowRequest() }
}
export class DeleteHookRequest {
  hookType?: string
  hookId?: string
  static create(_data?: any): DeleteHookRequest { return new DeleteHookRequest() }
}

export class HooksToggles {
  globalHooks: any[] = []
  workspaceHooks: any[] = []
  isWindows: boolean = false
  static create(_data?: any): HooksToggles { return new HooksToggles() }
}

export class ClineRulesToggles {
  toggles: { [key: string]: boolean } = {}
  static create(_data?: any): ClineRulesToggles { return new ClineRulesToggles() }
}

export class SkillInfo {
  name: string = ""
  description: string = ""
  path: string = ""
  enabled: boolean = false
  alwaysEnabled: boolean = false
  static create(_data?: any): SkillInfo { return new SkillInfo() }
}

export class RefreshedRules {
  globalClineRulesToggles?: ClineRulesToggles
  localClineRulesToggles?: ClineRulesToggles
  localCursorRulesToggles?: ClineRulesToggles
  localWindsurfRulesToggles?: ClineRulesToggles
  localAgentsRulesToggles?: ClineRulesToggles
  localWorkflowToggles?: ClineRulesToggles
  globalWorkflowToggles?: ClineRulesToggles
  globalSkillsToggles?: ClineRulesToggles
  localSkillsToggles?: ClineRulesToggles
  remoteRulesToggles?: ClineRulesToggles
  remoteWorkflowToggles?: ClineRulesToggles
  hooks?: HooksToggles
  skills?: SkillInfo[]
  static create(_data?: any): RefreshedRules { return new RefreshedRules() }
}
