using System.Collections.Generic;
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

        /// <summary>
        /// 获取 E3D 当前选中元素的名称（对应 !!ce）
        /// </summary>
        string GetCurrentElementName();

        /// <summary>
        /// 获取 E3D 多选元素名称列表
        /// </summary>
        List<string> GetSelectedElementNames();

        /// <summary>
        /// 读取指定元素的指定属性（C# 直接 API）
        /// </summary>
        string GetAttribute(string element, string attribute);
    }
}
