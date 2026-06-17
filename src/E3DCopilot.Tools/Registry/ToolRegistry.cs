using System.Collections.Generic;
using System.Linq;

namespace E3DCopilot.Tools.Registry
{
    /// <summary>
    /// 工具注册表（借鉴 Reasonix Registry 设计）
    /// </summary>
    public class ToolRegistry
    {
        private readonly Dictionary<string, IE3DTool> _tools
            = new Dictionary<string, IE3DTool>();

        /// <summary>注册工具</summary>
        public void Register(IE3DTool tool)
        {
            _tools[tool.Name] = tool;
        }

        /// <summary>按名称获取工具</summary>
        public IE3DTool Get(string name)
        {
            _tools.TryGetValue(name, out var tool);
            return tool;
        }

        /// <summary>获取所有已注册工具</summary>
        public List<IE3DTool> GetAll() => _tools.Values.ToList();

        /// <summary>获取所有 Schema（用于 LLM tools 参数）</summary>
        public List<ToolSchema> GetSchemas()
        {
            return _tools.Values.Select(t => new ToolSchema
            {
                Name = t.Name,
                Description = t.Description,
                ParametersJson = t.ParameterSchema
            }).ToList();
        }

        /// <summary>核心工具列表（6 个）</summary>
        public static readonly string[] CoreToolNames =
        {
            "query", "modify", "check",
            "calculate", "export", "execute_pml"
        };

        /// <summary>是否为核心工具</summary>
        public static bool IsCoreTool(string name)
        {
            return System.Array.IndexOf(CoreToolNames, name) >= 0;
        }
    }

    /// <summary>
    /// 工具 Schema（用于 OpenAI tools 参数）
    /// </summary>
    public class ToolSchema
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ParametersJson { get; set; }
    }
}
