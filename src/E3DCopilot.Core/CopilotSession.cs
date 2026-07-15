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

        /// <summary>会话持久化路径（JSONL 文件路径，首次保存时由 SessionStore 赋值）</summary>
        public string SessionPath { get; set; }

        // ── 对话分支 / Checkpoint（E6 对话分支回退）──
        private readonly List<SessionSnapshot> _snapshots = new List<SessionSnapshot>();

        /// <summary>保存当前会话快照（用于回退到此处）</summary>
        public void Checkpoint()
        {
            _snapshots.Add(new SessionSnapshot
            {
                MessageCount = Messages.Count,
                Timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        /// <summary>回退到指定消息数位置</summary>
        public bool Rollback(int messageCount)
        {
            if (messageCount < 0 || messageCount >= Messages.Count)
                return false;
            Messages.RemoveRange(messageCount, Messages.Count - messageCount);
            return true;
        }

        /// <summary>回退到最近一个 checkpoint</summary>
        public bool RollbackToLastCheckpoint()
        {
            if (_snapshots.Count == 0) return false;
            var last = _snapshots[_snapshots.Count - 1];
            _snapshots.RemoveAt(_snapshots.Count - 1);
            return Rollback(last.MessageCount);
        }

        /// <summary>获取所有 checkpoints</summary>
        public IReadOnlyList<SessionSnapshot> GetCheckpoints() => _snapshots.AsReadOnly();

        /// <summary>会话快照</summary>
        public class SessionSnapshot
        {
            public int MessageCount { get; set; }
            public long Timestamp { get; set; }
        }

        /// <summary>
        /// 追加用户消息
        /// </summary>
        public void AddUserMessage(string content, string[] images = null)
        {
            var msg = new ChatMessage(MessageRole.User, content);
            if (images != null && images.Length > 0)
            {
                msg.Images = images;
            }
            Messages.Add(msg);
        }

        /// <summary>
        /// 追加助手消息
        /// </summary>
        public void AddAssistantMessage(string content, List<ToolCall> calls = null, string reasoning = null)
        {
            var msg = new ChatMessage(MessageRole.Assistant, content);
            if (calls != null && calls.Count > 0)
                msg.ToolCalls = calls;
            if (!string.IsNullOrEmpty(reasoning))
                msg.ReasoningContent = reasoning;
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
        /// 追加系统消息（用于 Grace Round nudge / 压缩摘要注入）
        /// </summary>
        public void AddSystemMessage(string content)
        {
            Messages.Add(new ChatMessage(MessageRole.System, content));
        }

        /// <summary>
        /// 获取最近 N 条消息用于 LLM 上下文
        /// 返回副本（.ToList()），防止 BuildRequest 中的 RemoveAt 意外修改会话历史
        /// </summary>
        public List<ChatMessage> GetRecentMessages(int count = 20)
        {
            if (Messages.Count <= count)
                return new List<ChatMessage>(Messages);

            return new List<ChatMessage>(Messages.GetRange(Messages.Count - count, count));
        }

        /// <summary>
        /// 获取最后一条助手回复的文本内容（子代理结果提取用）
        /// </summary>
        public string LastAssistantText()
        {
            for (int i = Messages.Count - 1; i >= 0; i--)
            {
                if (Messages[i].Role == MessageRole.Assistant && !string.IsNullOrEmpty(Messages[i].Content))
                    return Messages[i].Content;
            }
            return null;
        }
    }
}
