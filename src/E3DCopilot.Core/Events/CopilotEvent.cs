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
        Text,               // AI 回答文本（流式）
        Message,            // 完整消息（流结束）
        ToolDispatch,       // 工具调用开始
        ToolResult,         // 工具执行结果
        ToolProgress,       // 工具执行进度（长时操作）
        Usage,              // Token 用量
        Notice,             // 通知/警告
        ApprovalRequest,    // 审批请求
        TurnDone,           // 轮次结束
        PlanModeChanged,    // Plan Mode 切换
        Error,              // 错误
        Retry               // 重试
    }

    /// <summary>
    /// 统一事件，贯穿 AgentLoop → Controller → UI
    /// </summary>
    public class CopilotEvent
    {
        public EventKind Kind { get; set; }
        public string Text { get; set; }
        public object Data { get; set; }

        public static CopilotEvent TextEvent(string text) =>
            new CopilotEvent { Kind = EventKind.Text, Text = text };

        public static CopilotEvent Reasoning(string text) =>
            new CopilotEvent { Kind = EventKind.Reasoning, Text = text };

        public static CopilotEvent Error(string message) =>
            new CopilotEvent { Kind = EventKind.Error, Text = "❌ " + message };

        public static CopilotEvent RetryEvent(string message, int attempt) =>
            new CopilotEvent { Kind = EventKind.Retry, Text = $"⏳ {message} (第 {attempt} 次尝试)" };

        public static CopilotEvent TurnDone() =>
            new CopilotEvent { Kind = EventKind.TurnDone };

        public static CopilotEvent Notice(string message) =>
            new CopilotEvent { Kind = EventKind.Notice, Text = message };
    }
}
