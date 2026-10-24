using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace E3DCopilot.Core.Logging
{
    /// <summary>
    /// 对话执行轨迹 — 顶层结构
    /// 记录一次完整对话的全部执行信息，供 AI 诊断分析
    /// </summary>
    public class ConversationTrace
    {
        /// <summary>会话 ID</summary>
        [JsonProperty("sessionId")]
        public string SessionId { get; set; }

        /// <summary>对话开始时间</summary>
        [JsonProperty("startTime")]
        public DateTime StartTime { get; set; }

        /// <summary>对话结束时间</summary>
        [JsonProperty("endTime")]
        public DateTime EndTime { get; set; }

        /// <summary>用户输入（原始问题）</summary>
        [JsonProperty("userInput")]
        public string UserInput { get; set; }

        /// <summary>使用的模型</summary>
        [JsonProperty("model")]
        public string Model { get; set; }

        /// <summary>总步骤数</summary>
        [JsonProperty("totalSteps")]
        public int TotalSteps { get; set; }

        /// <summary>总 Token 消耗</summary>
        [JsonProperty("totalTokens")]
        public TraceTokenUsage TotalTokens { get; set; } = new TraceTokenUsage();

        /// <summary>最终结果: success / error / cancelled / max_steps</summary>
        [JsonProperty("outcome")]
        public string Outcome { get; set; }

        /// <summary>各步骤详情</summary>
        [JsonProperty("steps")]
        public List<TraceStep> Steps { get; set; } = new List<TraceStep>();

        /// <summary>全局错误列表</summary>
        [JsonProperty("errors")]
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>系统级事件（压缩/恢复/循环守卫等）</summary>
        [JsonProperty("systemEvents")]
        public List<string> SystemEvents { get; set; } = new List<string>();
    }

    /// <summary>
    /// 单步执行记录
    /// </summary>
    public class TraceStep
    {
        /// <summary>步骤编号（从 1 开始）</summary>
        [JsonProperty("step")]
        public int Step { get; set; }

        /// <summary>步骤开始时间</summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        /// <summary>LLM 思考链（reasoning_content）</summary>
        [JsonProperty("reasoning", NullValueHandling = NullValueHandling.Ignore)]
        public string Reasoning { get; set; }

        /// <summary>LLM 输出文本</summary>
        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }

        /// <summary>本步骤的工具调用列表</summary>
        [JsonProperty("toolCalls")]
        public List<TraceToolCall> ToolCalls { get; set; } = new List<TraceToolCall>();

        /// <summary>本步骤 Token 消耗</summary>
        [JsonProperty("tokens", NullValueHandling = NullValueHandling.Ignore)]
        public TraceTokenUsage Tokens { get; set; }

        /// <summary>本步骤的系统事件/通知</summary>
        [JsonProperty("events", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Events { get; set; }

        /// <summary>本步骤耗时（毫秒）</summary>
        [JsonProperty("durationMs")]
        public long DurationMs { get; set; }

        /// <summary>其中 LLM 调用耗时（毫秒）</summary>
        [JsonProperty("llmMs")]
        public long LlmMs { get; set; }

        /// <summary>LLM 首 token 延迟 TTFT（毫秒），未收到任何增量时为 -1</summary>
        [JsonProperty("ttftMs")]
        public long TtftMs { get; set; } = -1;

        /// <summary>其中审批等待耗时（毫秒）</summary>
        [JsonProperty("approvalMs")]
        public long ApprovalMs { get; set; }

        /// <summary>本步骤的请求上下文规模（诊断"越跑越慢"用）</summary>
        [JsonProperty("context", NullValueHandling = NullValueHandling.Ignore)]
        public TraceContextSize Context { get; set; }
    }

    /// <summary>
    /// 请求上下文规模 — 用于诊断 prompt 膨胀
    /// </summary>
    public class TraceContextSize
    {
        /// <summary>历史消息条数</summary>
        [JsonProperty("messages")]
        public int Messages { get; set; }

        /// <summary>暴露给 LLM 的工具数量</summary>
        [JsonProperty("tools")]
        public int Tools { get; set; }

        /// <summary>工具 schema 序列化字符数</summary>
        [JsonProperty("toolSchemaChars")]
        public int ToolSchemaChars { get; set; }

        /// <summary>系统提示词字符数</summary>
        [JsonProperty("systemPromptChars")]
        public int SystemPromptChars { get; set; }

        /// <summary>全部消息内容字符数</summary>
        [JsonProperty("messageChars")]
        public int MessageChars { get; set; }
    }

    /// <summary>
    /// 工具调用详情
    /// </summary>
    public class TraceToolCall
    {
        /// <summary>调用 ID</summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>工具名称</summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>调用参数（JSON 字符串）</summary>
        [JsonProperty("arguments")]
        public string Arguments { get; set; }

        /// <summary>执行结果</summary>
        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public string Result { get; set; }

        /// <summary>是否成功</summary>
        [JsonProperty("success")]
        public bool Success { get; set; }

        /// <summary>执行耗时（毫秒）</summary>
        [JsonProperty("durationMs")]
        public long DurationMs { get; set; }

        /// <summary>错误信息（失败时）</summary>
        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public string Error { get; set; }

        /// <summary>审批等待耗时（毫秒），未走审批时为 -1</summary>
        [JsonProperty("approvalMs")]
        public long ApprovalMs { get; set; } = -1;

        /// <summary>审批结果: allow / reject / timeout / auto</summary>
        [JsonProperty("approval", NullValueHandling = NullValueHandling.Ignore)]
        public string Approval { get; set; }

        /// <summary>参数与结果被截断前的原始长度，用于判断"是不是内容太大"</summary>
        [JsonProperty("argsLen")]
        public int ArgsLen { get; set; }

        /// <summary>工具返回内容的原始长度</summary>
        [JsonProperty("resultLen")]
        public int ResultLen { get; set; }
    }

    /// <summary>
    /// Token 用量统计
    /// </summary>
    public class TraceTokenUsage
    {
        [JsonProperty("prompt")]
        public int Prompt { get; set; }

        [JsonProperty("completion")]
        public int Completion { get; set; }

        [JsonProperty("total")]
        public int Total => Prompt + Completion;
    }
}
