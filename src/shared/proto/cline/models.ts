// Protobuf type stubs for E3DCopilot WebView2

export class OpenRouterModelInfo {
  maxTokens?: number
  contextWindow?: number
  supportsImages?: boolean
  supportsPromptCache: boolean = false
  inputPrice?: number
  outputPrice?: number
  cacheWritesPrice?: number
  cacheReadsPrice?: number
  description?: string
  thinkingConfig?: any
  tiers?: any[]
  static create(_data?: any): OpenRouterModelInfo { return new OpenRouterModelInfo() }
}

export class OpenRouterCompatibleModelInfo {
  models: { [key: string]: OpenRouterModelInfo } = {}
  static create(_data?: any): OpenRouterCompatibleModelInfo { return new OpenRouterCompatibleModelInfo() }
}

export class ShengSuanYunModelInfo {
  [key: string]: any
  static create(_data?: any): ShengSuanYunModelInfo { return new ShengSuanYunModelInfo() }
}

export class UpdateApiConfigurationRequest {
  static create(_data?: any): UpdateApiConfigurationRequest { return new UpdateApiConfigurationRequest() }
}

export class ApiFormat {}

export class ClineRecommendedModel {
  id: string = ""
  name: string = ""
  description: string = ""
  tags: string[] = []
  static create(_data?: any): ClineRecommendedModel { return new ClineRecommendedModel() }
}

export class ClineRecommendedModelsResponse {
  recommended: ClineRecommendedModel[] = []
  free: ClineRecommendedModel[] = []
  static create(_data?: any): ClineRecommendedModelsResponse { return new ClineRecommendedModelsResponse() }
}

export class OpenAiModelsRequest {
  static create(_data?: any): OpenAiModelsRequest { return new OpenAiModelsRequest() }
}

export class LanguageModelChatSelector {
  vendor?: string
  family?: string
  version?: string
  id?: string
  static create(_data?: any): LanguageModelChatSelector { return new LanguageModelChatSelector() }
}
