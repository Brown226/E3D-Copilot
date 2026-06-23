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
        TurnDone,           // 轮次结束
        PlanModeChanged,    // Plan Mode 切换
        Error,              // 错误
        Retry,              // 重试
        AskUser             // AI 向用户提问（等待回答）
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

        public static CopilotEvent TextEvent(string text) =>
            new CopilotEvent { Kind = EventKind.Text, Text = text };

        public static CopilotEvent Reasoning(string text) =>
            new CopilotEvent { Kind = EventKind.Reasoning, Text = text };

        public static CopilotEvent Thinking(string text) =>
            new CopilotEvent { Kind = EventKind.Thinking, Text = text };

        public static CopilotEvent StreamDelta(string delta) =>
            new CopilotEvent { Kind = EventKind.StreamDelta, Text = delta };

        public static CopilotEvent StreamEnd(object usage = null) =>
            new CopilotEvent { Kind = EventKind.StreamEnd, Data = usage };

        public static CopilotEvent ToolStart(string toolId, string name, object args = null) =>
            new CopilotEvent { Kind = EventKind.ToolDispatch, ToolId = toolId, Text = name, Data = args };

        public static CopilotEvent ToolComplete(string toolId, object result) =>
            new CopilotEvent { Kind = EventKind.ToolResult, ToolId = toolId, Data = result };

        public static CopilotEvent ToolFail(string toolId, string error) =>
            new CopilotEvent { Kind = EventKind.ToolError, ToolId = toolId, Text = error };

        public static CopilotEvent ApprovalReq(string toolId, string description, object args = null) =>
            new CopilotEvent { Kind = EventKind.ApprovalRequest, ToolId = toolId, Text = description, Data = args };

        public static CopilotEvent Error(string message) =>
            new CopilotEvent { Kind = EventKind.Error, Text = message };

        public static CopilotEvent RetryEvent(string message, int attempt) =>
            new CopilotEvent { Kind = EventKind.Retry, Text = $"{message} (第 {attempt} 次尝试)" };

        public static CopilotEvent TurnDone() =>
            new CopilotEvent { Kind = EventKind.TurnDone };

        public static CopilotEvent Notice(string message) =>
            new CopilotEvent { Kind = EventKind.Notice, Text = message };
    }
}
