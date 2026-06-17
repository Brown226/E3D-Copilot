using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Tools.Registry
{
    /// <summary>
    /// 工具注册表 — 管理所有 IE3DTool 的注册、发现、执行
    /// C# 侧工具注册中心，与 Core 层的 ToolExecutor 配合使用
    /// 实现 IToolDispatcher 接口，供 Core 层调用
    /// </summary>
    public class ToolRegistry : IToolDispatcher
    {
        private readonly Dictionary<string, IE3DTool> _tools
            = new Dictionary<string, IE3DTool>();

        /// <summary>注册工具</summary>
        public void Register(IE3DTool tool)
        {
            if (tool == null)
                throw new ArgumentNullException(nameof(tool));
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

        /// <summary>
        /// 执行工具（Handler 层调用）
        /// </summary>
        public async Task<string> ExecuteAsync(string name, string args)
        {
            if (!_tools.TryGetValue(name, out var tool))
                return $"错误: 未找到工具 {name}";

            try
            {
                var result = await tool.ExecuteAsync(args);
                return result ?? "(空结果)";
            }
            catch (Exception ex)
            {
                return $"错误: {ex.Message}";
            }
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
            return Array.IndexOf(CoreToolNames, name) >= 0;
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
