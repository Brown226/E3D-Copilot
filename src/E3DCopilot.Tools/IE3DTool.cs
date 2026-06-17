using System.Threading.Tasks;

namespace E3DCopilot.Tools
{
    /// <summary>
    /// 工具分类（借鉴 Reasonix Tool Category）
    /// </summary>
    public enum ToolCategory
    {
        Query,
        Modify,
        Check,
        Calculate,
        Export,
        System
    }

    /// <summary>
    /// 工具统一接口（借鉴 Reasonix 的 Tool 抽象）
    /// </summary>
    public interface IE3DTool
    {
        /// <summary>工具名称（AI 调用时使用）</summary>
        string Name { get; }

        /// <summary>工具描述（用于 LLM 理解用途）</summary>
        string Description { get; }

        /// <summary>参数 JSON Schema</summary>
        string ParameterSchema { get; }

        /// <summary>是否为只读（查询型工具 = true）</summary>
        bool IsReadOnly { get; }

        /// <summary>工具分类</summary>
        ToolCategory Category { get; }

        /// <summary>执行工具</summary>
        Task<string> ExecuteAsync(string args);
    }
}
