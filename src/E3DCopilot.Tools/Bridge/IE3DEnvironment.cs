using System.Collections.Generic;

namespace E3DCopilot.Tools.Bridge
{
    /// <summary>
    /// E3D 环境抽象接口
    /// 隔离 E3D API 调用，使 Tools 层可测试
    /// 真实环境：RealE3DEnvironment（调用 Aveva.* DLL）
    /// 测试环境：SimulatedE3DEnvironment（返回模拟数据）
    /// </summary>
    public interface IE3DEnvironment
    {
        /// <summary>查询元素（按类型和名称模式）</summary>
        List<ElementInfo> QueryElements(string elementType, string namePattern, string scope, int limit);

        /// <summary>读取元素属性</summary>
        string GetAttribute(string elementName, string attributeName);

        /// <summary>写入元素属性</summary>
        void SetAttribute(string elementName, string attributeName, string value);

        /// <summary>检查元素是否存在</summary>
        bool CheckExists(string elementName);

        /// <summary>执行 PML 命令</summary>
        string ExecutePml(string pmlCommand);

        /// <summary>获取当前元素名称</summary>
        string GetCurrentElementName();

        /// <summary>获取多选元素名称列表</summary>
        List<string> GetSelectedElementNames();

        // 新增：Design / Piping 操作

        /// <summary>创建子元素（Equipment/Pipe/Component/Branch 等）</summary>
        string CreateElement(string parentElement, string name, string elementType, string attributesJson);

        /// <summary>删除元素</summary>
        bool DeleteElement(string elementName);
    }

    /// <summary>
    /// 元素信息（查询结果）
    /// </summary>
    public class ElementInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string DbUri { get; set; }
        public Dictionary<string, string> Attributes { get; set; }

        public ElementInfo()
        {
            Attributes = new Dictionary<string, string>();
        }
    }
}
