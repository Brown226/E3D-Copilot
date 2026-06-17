using System.Threading.Tasks;

namespace E3DCopilot.Core.Tools
{
    /// <summary>
    /// 工具调度接口 — Core 层的抽象，解除对 Tools 项目的反向依赖
    /// Tools 项目中的 ToolRegistry 实现此接口
    /// </summary>
    public interface IToolDispatcher
    {
        /// <summary>
        /// 按名称执行工具并返回结果字符串
        /// </summary>
        Task<string> ExecuteAsync(string name, string args);
    }
}
