using System.Threading;
using System.Threading.Tasks;

namespace E3DCopilot.Core.Tools
{
    /// <summary>
    /// 工具处理器接口（参考 cline-chinese-main 的 ToolHandler 模式）
    /// 每个工具一个独立 Handler，职责清晰
    /// </summary>
    public interface IToolHandler
    {
        /// <summary>工具名称（LLM 通过此名称调用）</summary>
        string Name { get; }

        /// <summary>工具描述（用于 LLM 理解用途）</summary>
        string Description { get; }

        /// <summary>参数 JSON Schema</summary>
        string ParameterSchema { get; }

        /// <summary>是否为只读（只读工具无需审批）</summary>
        bool IsReadOnly { get; }

        /// <summary>执行工具</summary>
        Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default);
    }
}
