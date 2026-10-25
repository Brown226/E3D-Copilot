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

    /// <summary>
    /// 审批预览（可选实现）：工具需要用户审批时，为审批卡片提供富预览数据
    /// （如 write_file 的改动前后 diff）。AgentLoop 在发射审批事件前调用。
    /// </summary>
    public interface IApprovalPreview
    {
        /// <summary>返回随审批事件下发的预览数据（null = 无预览）</summary>
        object GetApprovalPreview(string args);
    }
}
