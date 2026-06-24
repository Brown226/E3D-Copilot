using Newtonsoft.Json;

namespace E3DCopilot.Core.Messaging
{
    /// <summary>
    /// 前后端消息契约定义
    /// 所有消息类型、常量、数据结构在此统一定义
    /// </summary>
    
    #region 消息类型常量
    
    public static class MessageTypes
    {
        // === 前端 → 后端 ===
        public const string UserMessage = "user:message";
        public const string UserCancel = "user:cancel";
        public const string UserNewSession = "user:new_session";
        public const string UserApprove = "user:approve";
        public const string UserAskResponse = "user:ask_response";
        public const string UserSetPlanMode = "user:set_plan_mode";
        public const string UserSetApprovalMode = "user:set_approval_mode";
        public const string UserCloseTab = "tab:close";
        public const string UserSteer = "user:steer";  // 中途干预：用户运行中注入引导消息
        public const string Ping = "ping";

        // === Provider / Model 管理（参考 Reasonix） ===
        public const string ModelsList = "models:list";
        public const string ModelSwitch = "model:switch";
        public const string ProvidersList = "providers:list";
        public const string ProviderSave = "provider:save";
        public const string ProviderDelete = "provider:delete";
        public const string ProviderFetchModels = "provider:fetch_models";
        public const string ProviderSetKey = "provider:set_key";

        // === Skills 管理 ===
        public const string SkillsList = "skills:list";
        public const string SkillsToggle = "skills:toggle";
        public const string SkillsAddSource = "skills:add_source";
        public const string SkillsRemoveSource = "skills:remove_source";
        public const string SkillsRefresh = "skills:refresh";

        // === Memory 管理 ===
        public const string MemoryList = "memory:list";
        public const string MemorySave = "memory:save";
        public const string MemoryDelete = "memory:delete";

        // === Settings 管理 ===
        public const string SettingsSave = "settings:save";

        // === Sessions 管理 ===
        public const string SessionsList = "sessions:list";
        public const string SessionsDelete = "sessions:delete";

        // === 后端 → 前端 ===
        public const string Pong = "pong";
        public const string LlmStreamDelta = "llm:stream:delta";
        public const string LlmStreamEnd = "llm:stream:end";
        public const string LlmThinking = "llm:thinking";
        public const string ToolDispatch = "tool:dispatch";
        public const string ToolResult = "tool:result";
        public const string ToolError = "tool:error";
        public const string ToolApproval = "tool:approval";
        public const string AskUser = "ask_user";
        public const string Notice = "notice";
        public const string Error = "error";
        public const string HostReady = "host:ready";
        public const string ConfigSync = "config:sync";
        public const string ModelsListResult = "models:list:result";
        public const string ProvidersListResult = "providers:list:result";
        public const string ProviderFetchResult = "provider:fetch_models:result";
        public const string TurnDone = "turn:done";

        // === 新增事件类型（补全前后端事件覆盖） ===
        public const string LlmTurnStarted = "llm:turn_started";
        public const string LlmUsage = "llm:usage";
        public const string LlmRetry = "llm:retry";
        public const string ToolProgress = "tool:progress";
    }
    
    #endregion
    
    #region 前端 → 后端消息
    
    /// <summary>
    /// 用户消息（聊天输入）
    /// </summary>
    public class UserMessagePayload
    {
        [JsonProperty("text")]
        public string Text { get; set; } = "";
        
        [JsonProperty("images")]
        public string[] Images { get; set; }
        
        [JsonProperty("files")]
        public string[] Files { get; set; }

        [JsonProperty("tabId")]
        public string TabId { get; set; }
    }
    
    /// <summary>
    /// 审批响应
    /// </summary>
    public class ApprovalPayload
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";
        
        [JsonProperty("allow")]
        public bool Allow { get; set; }
    }
    
    /// <summary>
    /// 用户回答（响应 ask_user）
    /// </summary>
    public class AskResponsePayload
    {
        [JsonProperty("questionId")]
        public string QuestionId { get; set; } = "";
        
        [JsonProperty("answer")]
        public string Answer { get; set; } = "";
    }

    /// <summary>
    /// Plan/Act 模式切换请求
    /// </summary>
    public class SetPlanModePayload
    {
        [JsonProperty("mode")]
        public string Mode { get; set; } = "act"; // "plan" | "act"
    }

    /// <summary>
    /// Plan Mode 状态变更通知
    /// </summary>
    public class PlanModeChangedPayload
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }
    }
    
    #endregion
    
    #region 后端 → 前端消息
    
    /// <summary>
    /// LLM 流式输出增量
    /// </summary>
    public class LlmStreamDeltaPayload
    {
        [JsonProperty("delta")]
        public string Delta { get; set; } = "";

        [JsonProperty("tabId")]
        public string TabId { get; set; }
    }
    
    /// <summary>
    /// LLM 流式输出结束
    /// </summary>
    public class LlmStreamEndPayload
    {
        [JsonProperty("usage")]
        public object Usage { get; set; }
    }
    
    /// <summary>
    /// 工具调度
    /// </summary>
    public class ToolDispatchPayload
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";
        
        [JsonProperty("name")]
        public string Name { get; set; } = "";
        
        [JsonProperty("args")]
        public object Args { get; set; }

        [JsonProperty("tabId")]
        public string TabId { get; set; }
    }
    
    /// <summary>
    /// 工具执行结果
    /// </summary>
    public class ToolResultPayload
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";
        
        [JsonProperty("result")]
        public string Result { get; set; }
        
        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("tabId")]
        public string TabId { get; set; }
    }
    
    /// <summary>
    /// 审批请求
    /// </summary>
    public class ApprovalRequestPayload
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";
        
        [JsonProperty("name")]
        public string Name { get; set; } = "";
        
        [JsonProperty("args")]
        public string Args { get; set; }
        
        [JsonProperty("description")]
        public string Description { get; set; } = "";
    }
    
    /// <summary>
    /// 询问用户
    /// </summary>
    public class AskUserPayload
    {
        [JsonProperty("questionId")]
        public string QuestionId { get; set; } = "";
        
        [JsonProperty("question")]
        public string Question { get; set; } = "";
        
        [JsonProperty("data")]
        public object Data { get; set; }
    }
    
    /// <summary>
    /// 通用通知
    /// </summary>
    public class NoticePayload
    {
        [JsonProperty("text")]
        public string Text { get; set; } = "";
    }
    
    /// <summary>
    /// 错误消息
    /// </summary>
    public class ErrorPayload
    {
        [JsonProperty("message")]
        public string Message { get; set; } = "";
    }
    
    /// <summary>
    /// 宿主就绪
    /// </summary>
    public class HostReadyPayload
    {
        [JsonProperty("version")]
        public string Version { get; set; } = "";
        
        [JsonProperty("platform")]
        public string Platform { get; set; } = "";
        
        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }
    }
    
    /// <summary>
    /// 配置同步
    /// </summary>
    public class ConfigSyncPayload
    {
        [JsonProperty("provider")]
        public string Provider { get; set; } = "";

        [JsonProperty("model")]
        public string Model { get; set; } = "";

        [JsonProperty("baseUrl")]
        public string BaseUrl { get; set; } = "";

        [JsonProperty("apiKey")]
        public string ApiKey { get; set; } = "";

        [JsonProperty("mode")]
        public string Mode { get; set; } = "";

        // === 多 provider 模式（参考 Reasonix） ===
        [JsonProperty("currentProvider")]
        public string CurrentProvider { get; set; } = "";

        [JsonProperty("currentModel")]
        public string CurrentModel { get; set; } = "";

        [JsonProperty("providers")]
        public ProviderInfo[] Providers { get; set; }
    }

    /// <summary>
    /// 单个 provider 信息（前后端传输格式）
    /// </summary>
    public class ProviderInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; } = "openai";

        [JsonProperty("baseUrl")]
        public string BaseUrl { get; set; }

        [JsonProperty("apiKey")]
        public string ApiKey { get; set; } = "";

        [JsonProperty("keySet")]
        public bool KeySet { get; set; }

        [JsonProperty("models")]
        public string[] Models { get; set; } = new string[0];

        [JsonProperty("default")]
        public string Default { get; set; } = "";

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("builtIn")]
        public bool BuiltIn { get; set; } = false;
    }

    /// <summary>
    /// 单个模型信息（前后端传输格式）
    /// </summary>
    public class ModelInfo
    {
        [JsonProperty("ref")]
        public string Ref { get; set; }      // "provider/model"

        [JsonProperty("provider")]
        public string Provider { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("current")]
        public bool Current { get; set; }
    }

    /// <summary>
    /// 切换模型请求（"provider/model"）
    /// </summary>
    public class ModelSwitchPayload
    {
        [JsonProperty("ref")]
        public string Ref { get; set; }
    }

    /// <summary>
    /// Provider 列表/模型列表响应
    /// </summary>
    public class ModelsListResultPayload
    {
        [JsonProperty("models")]
        public ModelInfo[] Models { get; set; } = new ModelInfo[0];

        [JsonProperty("currentProvider")]
        public string CurrentProvider { get; set; }

        [JsonProperty("currentModel")]
        public string CurrentModel { get; set; }
    }

    public class ProvidersListResultPayload
    {
        [JsonProperty("providers")]
        public ProviderInfo[] Providers { get; set; } = new ProviderInfo[0];

        [JsonProperty("currentProvider")]
        public string CurrentProvider { get; set; }

        [JsonProperty("currentModel")]
        public string CurrentModel { get; set; }
    }

    public class ProviderFetchResultPayload
    {
        [JsonProperty("providerName")]
        public string ProviderName { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("models")]
        public string[] Models { get; set; } = new string[0];

        [JsonProperty("error")]
        public string Error { get; set; }
    }

    public class ProviderSavePayload
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; } = "openai";

        [JsonProperty("baseUrl")]
        public string BaseUrl { get; set; }

        [JsonProperty("apiKey")]
        public string ApiKey { get; set; }

        [JsonProperty("models")]
        public string[] Models { get; set; } = new string[0];

        [JsonProperty("default")]
        public string Default { get; set; }

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("builtIn")]
        public bool BuiltIn { get; set; } = false;
    }

    public class ProviderDeletePayload
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class ProviderSetKeyPayload
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("apiKey")]
        public string ApiKey { get; set; }
    }

    #endregion
    
    #region 消息信封
    
    /// <summary>
    /// 消息信封（统一格式）
    /// </summary>
    public class CopilotMessage<T>
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "";
        
        [JsonProperty("payload")]
        public T Payload { get; set; }
    }
    
    #endregion
}
