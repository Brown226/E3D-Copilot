using System.Collections.Generic;
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
        public const string AskRequest = "ask_request";  //（对齐 Reasonix AskRequest）
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

        // === CAD 导入相关 ===
        public const string UserCadImport = "user:cad_import";
        public const string CadProgress = "cad:progress";
        public const string CadResult = "cad:result";
        public const string CadPreview = "cad:preview";
        public const string CadConfirmBatch = "cad:confirm_batch";
        public const string CadCancel = "cad:cancel";
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
    /// 用户回答（响应 ask / ask_user，新协议对齐 Reasonix AnswerQuestion）
    /// </summary>
    public class AskResponsePayload
    {
        /// <summary>Ask 批次 ID（新协议）</summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>问题 ID（旧协议兼容）</summary>
        [JsonProperty("questionId")]
        public string QuestionId { get; set; } = "";
        
        /// <summary>旧协议：单个回答字符串</summary>
        [JsonProperty("answer")]
        public string Answer { get; set; } = "";

        /// <summary>新协议：结构化回答列表 [{questionId, selected:[]}]</summary>
        [JsonProperty("answers")]
        public List<AskAnswerItem> Answers { get; set; }
    }

    /// <summary>
    /// 单个问题的回答（新协议，对齐 Reasonix QuestionAnswer）
    /// </summary>
    public class AskAnswerItem
    {
        [JsonProperty("questionId")]
        public string QuestionId { get; set; }
        
        [JsonProperty("selected")]
        public List<string> Selected { get; set; }
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
        
        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public string Error { get; set; }
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

        [JsonProperty("contextWindow")]
        public int ContextWindow { get; set; } = 0;

        [JsonProperty("visionModels")]
        public string[] VisionModels { get; set; } = new string[0];
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

        [JsonProperty("contextWindow")]
        public int ContextWindow { get; set; } = 0;

        [JsonProperty("visionModels")]
        public string[] VisionModels { get; set; } = new string[0];
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

    #region CAD 导入消息

    /// <summary>
    /// CAD 导入请求
    /// </summary>
    public class CadImportPayload
    {
        [JsonProperty("action")]
        public string Action { get; set; } = "import"; // import, preview, parse_file, parse_paths

        [JsonProperty("filePath")]
        public string FilePath { get; set; }

        [JsonProperty("pathsString")]
        public string PathsString { get; set; }

        [JsonProperty("owner")]
        public string Owner { get; set; } = "/IMPORT_ZONE";

        [JsonProperty("wallHeight")]
        public double WallHeight { get; set; } = 3000;

        [JsonProperty("wallThickness")]
        public double WallThickness { get; set; } = 200;

        [JsonProperty("autoName")]
        public bool AutoName { get; set; } = true;
    }

    /// <summary>
    /// CAD 解析进度
    /// </summary>
    public class CadProgressPayload
    {
        [JsonProperty("phase")]
        public string Phase { get; set; } = ""; // parsing, classifying, creating

        [JsonProperty("current")]
        public int Current { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("percentage")]
        public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;

        [JsonProperty("message")]
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// CAD 导入结果
    /// </summary>
    public class CadResultPayload
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("segmentCount")]
        public int SegmentCount { get; set; }

        [JsonProperty("elementCount")]
        public int ElementCount { get; set; }

        [JsonProperty("pmlScript")]
        public string PmlScript { get; set; }

        [JsonProperty("elements")]
        public object[] Elements { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }
    }

    /// <summary>
    /// CAD 预览结果
    /// </summary>
    public class CadPreviewPayload
    {
        [JsonProperty("segmentCount")]
        public int SegmentCount { get; set; }

        [JsonProperty("elementCount")]
        public int ElementCount { get; set; }

        [JsonProperty("wallHeight")]
        public double WallHeight { get; set; }

        [JsonProperty("wallThickness")]
        public double WallThickness { get; set; }

        [JsonProperty("preview")]
        public object[] Preview { get; set; }
    }

    /// <summary>
    /// 批量确认请求
    /// </summary>
    public class CadConfirmBatchPayload
    {
        [JsonProperty("elements")]
        public object[] Elements { get; set; }

        [JsonProperty("owner")]
        public string Owner { get; set; } = "/IMPORT_ZONE";

        [JsonProperty("execute")]
        public bool Execute { get; set; } = true;
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
