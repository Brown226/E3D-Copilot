using System.Collections.Generic;

namespace E3DCopilot.Core.Events
{
    /// <summary>
    /// 事件类型枚举（借鉴 Reasonix Event Stream 设计）
    /// </summary>
    public enum EventKind
    {
        TurnStarted,        // 轮次开始
        Reasoning,          // AI 思考过程（流式）
        Thinking,           // AI 推理过程（单独展示）
        Text,               // AI 回答文本（完整消息）
        StreamDelta,        // 流式文本片段
        StreamEnd,          // 流式结束
        Message,            // 完整消息（流结束）
        ToolDispatch,       // 工具调用开始
        ToolResult,         // 工具执行结果
        ToolError,          // 工具执行错误
        ToolProgress,       // 工具执行进度（长时操作）
        Usage,              // Token 用量
        Notice,             // 通知/警告
        ApprovalRequest,    // 审批请求
        AskRequest,         // AI 向用户提问（对齐 Reasonix AskRequest）
        TurnDone,           // 轮次结束
        PlanModeChanged,    // Plan Mode 切换
        Error,              // 错误
        Retry,              // 重试
    }

    /// <summary>
    /// 统一事件，贯穿 AgentLoop → Controller → UI
    /// </summary>
    public class CopilotEvent
    {
        public EventKind Kind { get; set; }
        public string Text { get; set; }
        public string ToolId { get; set; }
        public object Data { get; set; }

        /// <summary>原始核心工具名（路由前），供前端渲染分组用</summary>
        public string CoreToolName { get; set; }

        /// <summary>结构化元数据（最小安全方案：供前端渲染，不影响 LLM 的 Text）</summary>
        public object Meta { get; set; }

        /// <summary>
        /// AskRequest 事件专用：结构化提问数据
        /// 对齐 Reasonix event.Ask{ID, Questions}
        /// </summary>
        public Ask Ask { get; set; }

        /// <summary>事件时间戳（毫秒），用于前端计算耗时</summary>
        public long Timestamp { get; set; } = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public static CopilotEvent TextEvent(string text) =>
            new CopilotEvent { Kind = EventKind.Text, Text = text };

        public static CopilotEvent Reasoning(string text) =>
            new CopilotEvent { Kind = EventKind.Reasoning, Text = text };

        public static CopilotEvent Thinking(string text) =>
            new CopilotEvent { Kind = EventKind.Thinking, Text = text };

        public static CopilotEvent StreamDelta(string delta) =>
            new CopilotEvent { Kind = EventKind.StreamDelta, Text = delta };

        public static CopilotEvent StreamEnd(object usage = null, string errorMessage = null) =>
            new CopilotEvent { Kind = EventKind.StreamEnd, Data = usage, Text = errorMessage };

        public static CopilotEvent ToolStart(string toolId, string name, object args = null, string coreToolName = null) =>
            new CopilotEvent { Kind = EventKind.ToolDispatch, ToolId = toolId, Text = name, Data = args, CoreToolName = coreToolName };

        public static CopilotEvent ToolComplete(string toolId, object result, object meta = null) =>
            new CopilotEvent { Kind = EventKind.ToolResult, ToolId = toolId, Data = result, Meta = meta };

        public static CopilotEvent ToolFail(string toolId, string error) =>
            new CopilotEvent { Kind = EventKind.ToolError, ToolId = toolId, Text = error };

        public static CopilotEvent ToolProgressEvent(string toolId, string text, long elapsedMs) =>
            new CopilotEvent { Kind = EventKind.ToolProgress, ToolId = toolId, Text = text, Data = new { elapsedMs } };

        public static CopilotEvent ApprovalReq(string toolId, string description, object args = null) =>
            new CopilotEvent { Kind = EventKind.ApprovalRequest, ToolId = toolId, Text = description, Data = args };

        /// <summary>
        /// 创建 AskRequest 事件（对齐 Reasonix AskRequest event）
        /// </summary>
        public static CopilotEvent AskRequestEvent(Ask ask) =>
            new CopilotEvent { Kind = EventKind.AskRequest, Ask = ask };

        public static CopilotEvent Error(string message) =>
            new CopilotEvent { Kind = EventKind.Error, Text = message };

        public static CopilotEvent RetryEvent(string message, int attempt) =>
            new CopilotEvent { Kind = EventKind.Retry, Text = $"{message} (第 {attempt} 次尝试)" };

        public static CopilotEvent TurnDone() =>
            new CopilotEvent { Kind = EventKind.TurnDone };

        public static CopilotEvent Notice(string message) =>
            new CopilotEvent { Kind = EventKind.Notice, Text = message };
    }

    // ═══════════════════════════════════════════════════════════
    //  Ask 相关数据结构（对齐 Reasonix event 包）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// AskOption — 选项定义（对齐 Reasonix event.AskOption）
    /// </summary>
    public class AskOption
    {
        /// <summary>选项文本（简洁）</summary>
        public string Label { get; set; }

        /// <summary>可选的单行解释说明</summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// AskQuestion — 单个提问（对齐 Reasonix event.AskQuestion）
    /// </summary>
    public class AskQuestion
    {
        /// <summary>稳定 ID（q1, q2...），回答时关联</summary>
        public string Id { get; set; }

        /// <summary>短标签（Tab 标题）</summary>
        public string Header { get; set; }

        /// <summary>问题文本</summary>
        public string Prompt { get; set; }

        /// <summary>选项列表（2-4 个）</summary>
        public List<AskOption> Options { get; set; }

        /// <summary>是否允许多选</summary>
        public bool Multi { get; set; }
    }

    /// <summary>
    /// Ask — 一批提问的包装（对齐 Reasonix event.Ask）
    /// 包含唯一 ID（用于 controller.AnswerQuestion）和提问列表
    /// </summary>
    public class Ask
    {
        /// <summary>本次 Ask 的唯一 ID（用于应答关联）</summary>
        public string Id { get; set; }

        /// <summary>提问列表（1-4 个）</summary>
        public List<AskQuestion> Questions { get; set; }
    }

    /// <summary>
    /// AskAnswer — 用户对单个 AskQuestion 的回答（对齐 Reasonix event.AskAnswer）
    /// </summary>
    public class AskAnswer
    {
        /// <summary>关联的 question id（如 "q1"）</summary>
        public string QuestionId { get; set; }

        /// <summary>用户选择的选项 label(s)</summary>
        public List<string> Selected { get; set; }
    }
}
