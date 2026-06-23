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
        public const string Ping = "ping";
        
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
    
    #endregion
    
    #region 后端 → 前端消息
    
    /// <summary>
    /// LLM 流式输出增量
    /// </summary>
    public class LlmStreamDeltaPayload
    {
        [JsonProperty("delta")]
        public string Delta { get; set; } = "";
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
