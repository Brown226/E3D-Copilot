using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace E3DCopilot.Core.Providers
{
    /// <summary>
    /// 消息角色
    /// </summary>
    public enum MessageRole
    {
        System,
        User,
        Assistant,
        Tool
    }

    /// <summary>
    /// 对话消息
    /// </summary>
    public class ChatMessage
    {
        public MessageRole Role { get; set; }
        public string Content { get; set; }
        public List<ToolCall> ToolCalls { get; set; }
        public string ToolCallId { get; set; }
        
        /// <summary>
        /// 多模态支持：图片数组（base64 格式）
        /// </summary>
        public string[] Images { get; set; }

        public ChatMessage() { }

        public ChatMessage(MessageRole role, string content)
        {
            Role = role;
            Content = content;
        }
    }

    /// <summary>
    /// 流式 Chunk 类型
    /// </summary>
    public enum ChunkType
    {
        Reasoning,
        Text,
        ToolCall,
        Usage
    }

    /// <summary>
    /// 流式数据块（net48 回调模式，不用 IAsyncEnumerable）
    /// </summary>
    public class Chunk
    {
        public ChunkType Type { get; set; }
        public string Content { get; set; }
        public ToolCall ToolCall { get; set; }
        public UsageData UsageData { get; set; }

        public static Chunk FromText(string text) =>
            new Chunk { Type = ChunkType.Text, Content = text };

        public static Chunk FromReasoning(string text) =>
            new Chunk { Type = ChunkType.Reasoning, Content = text };

        public static Chunk FromToolCall(ToolCall call) =>
            new Chunk { Type = ChunkType.ToolCall, ToolCall = call };

        public static Chunk FromUsage(int completionTokens, int promptTokens) =>
            new Chunk { Type = ChunkType.Usage, UsageData = new UsageData
            {
                CompletionTokens = completionTokens,
                PromptTokens = promptTokens
            }};
    }

    /// <summary>
    /// 工具调用描述
    /// </summary>
    public class ToolCall
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; }
        public string Arguments { get; set; } // JSON string
    }

    /// <summary>
    /// Token 用量
    /// </summary>
    public class UsageData
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens => PromptTokens + CompletionTokens;
    }

    /// <summary>
    /// 请求参数
    /// </summary>
    public class CopilotRequest
    {
        public string Model { get; set; }
        public List<ChatMessage> Messages { get; set; }
        public List<ToolSchema> Tools { get; set; }
        public double Temperature { get; set; } = 0.1;
        public int MaxTokens { get; set; } = 8192;
    }

    /// <summary>
    /// 工具 Schema（OpenAI 兼容格式）
    /// </summary>
    public class ToolSchema
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ParametersJson { get; set; } // JSON Schema string
    }

    /// <summary>
    /// LLM Provider 接口（net48 兼容：回调模式）
    /// </summary>
    public interface ICopilotProvider
    {
        /// <summary>
        /// Provider 名称标识
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 流式调用 LLM，通过 onChunk 回调逐块返回
        /// </summary>
        Task StreamAsync(
            CopilotRequest request,
            Action<Chunk> onChunk,
            CancellationToken ct);

        /// <summary>
        /// 健康检查 — 检测 Provider 是否可用
        /// </summary>
        Task<bool> HealthCheckAsync();
    }
}
