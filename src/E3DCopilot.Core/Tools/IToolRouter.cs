using System.Threading.Tasks;

namespace E3DCopilot.Core.Tools
{
    /// <summary>
    /// 工具路由接口 — 将核心工具名路由到专用工具
    /// Core 层抽象，Tools 层的 ToolRouter 实现此接口
    /// </summary>
    public interface IToolRouter
    {
        /// <summary>路由核心工具调用到专用工具</summary>
        Task<(string toolName, string args)> RouteAsync(string coreToolName, string args);
    }
}
