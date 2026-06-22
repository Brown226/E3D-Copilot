// Protobuf type stubs for E3DCopilot WebView2

export class UserOrganization {
  static create(_data?: any): UserOrganization { return new UserOrganization() }
}
export class UsageTransaction {
  static create(_data?: any): UsageTransaction { return new UsageTransaction() }
}
export class PaymentTransaction {
  static create(_data?: any): PaymentTransaction { return new PaymentTransaction() }
}
export class UserInfo {
  static create(_data?: any): UserInfo { return new UserInfo() }
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

export class TrackWorktreeViewOpenedRequest {
  static create(): TrackWorktreeViewOpenedRequest { return new TrackWorktreeViewOpenedRequest() }
}
export class Worktree {
  static create(_data?: any): Worktree { return new Worktree() }
}
