using System.Collections.Generic;
using E3DCopilot.Core.Providers;

namespace E3DCopilot.Core
{
    /// <summary>
    /// 单次会话上下文
    /// 包含对话历史、工具结果、审批记录
    /// </summary>
    public class CopilotSession
    {
        public string SessionId { get; } = System.Guid.NewGuid().ToString("N");
        public List<ChatMessage> Messages { get; } = new List<ChatMessage>();
        public int TokenCount { get; set; }
        public bool IsPlanMode { get; set; }

        /// <summary>
        /// 追加用户消息
        /// </summary>
        public void AddUserMessage(string content)
        {
            Messages.Add(new ChatMessage(MessageRole.User, content));
        }

        /// <summary>
        /// 追加助手消息
        /// </summary>
        public void AddAssistantMessage(string content, List<ToolCall> calls = null)
        {
            var msg = new ChatMessage(MessageRole.Assistant, content);
            if (calls != null && calls.Count > 0)
                msg.ToolCalls = calls;
            Messages.Add(msg);
        }

        /// <summary>
        /// 追加工具结果消息
        /// </summary>
        public void AddToolResult(string toolCallId, string result)
        {
            Messages.Add(new ChatMessage
            {
                Role = MessageRole.Tool,
                Content = result,
                ToolCallId = toolCallId
            });
        }

        /// <summary>
        /// 获取最近 N 条消息用于 LLM 上下文
        /// </summary>
        public List<ChatMessage> GetRecentMessages(int count = 20)
        {
            if (Messages.Count <= count)
                return Messages;

            return Messages.GetRange(Messages.Count - count, count);
        }
    }
}
