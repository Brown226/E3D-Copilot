using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Tools.Registry
{
    /// <summary>
    /// 核心工具 → 专用工具 参数路由器（精简版）
    /// 只路由到确实已注册的 Handler，避免虚设路由。
    /// 当前仅路由 query 中带 attributes 参数时到 get_attributes（GetAttributesHandler 已注册），
    /// 其余情况直接回退到核心工具。
    /// </summary>
    public class ToolRouter : IToolRouter
    {
        // 已注册的 Handler 名称集合（与 ToolExecutor.CreateDefault 保持一致）
        private static readonly System.Collections.Generic.HashSet<string> RegisteredHandlers
            = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "query", "modify", "check", "calculate", "export", "execute_pml",
            "get_attributes", "design", "piping", "geometry",
            "ask_user", "task", "todo_write", "read_file", "write_file", "search_knowledge",
            "grep", "glob", "memory"
        };

        /// <summary>
        /// 路由核心工具的调用到具体的专用工具
        /// 只有当路由目标确实已注册时才路由，否则回退到核心工具
        /// </summary>
        public Task<(string toolName, string args)> RouteAsync(string coreToolName, string args)
        {
            var (routedName, routedArgs) = DoRoute(coreToolName, args);

            // 只有路由目标已注册时才生效，否则回退到核心工具
            if (!string.IsNullOrEmpty(routedName)
                && routedName != coreToolName
                && RegisteredHandlers.Contains(routedName))
            {
                return Task.FromResult((routedName, routedArgs ?? args));
            }

            return Task.FromResult((coreToolName, args));
        }

        private (string toolName, string args) DoRoute(string coreToolName, string args)
        {
            if (string.IsNullOrWhiteSpace(args))
                return (coreToolName, args);

            try
            {
                switch (coreToolName)
                {
                    case "query":
                        return RouteQuery(args);
                    case "modify":
                        return RouteModify(args);
                    case "check":
                        return RouteCheck(args);
                    case "calculate":
                        return RouteCalculate(args);
                    default:
                        return (coreToolName, args);
                }
            }
            catch
            {
                // JSON 解析失败时直接回退
                return (coreToolName, args);
            }
        }

        /// <summary>
        /// query 路由：仅当有 attributes 参数时路由到 get_attributes
        /// </summary>
        private (string toolName, string args) RouteQuery(string args)
        {
            var json = JObject.Parse(args);

            // 有 attributes 参数 → 路由到 get_attributes（已注册）
            if (json["attributes"] != null)
                return ("get_attributes", args);

            // 其余情况直接走 query
            return ("query", args);
        }

        /// <summary>
        /// modify 路由：统一走 modify（不细分专用工具）
        /// </summary>
        private (string toolName, string args) RouteModify(string args)
        {
            return ("modify", args);
        }

        /// <summary>
        /// check 路由：统一走 check（不细分专用工具）
        /// </summary>
        private (string toolName, string args) RouteCheck(string args)
        {
            return ("check", args);
        }

        /// <summary>
        /// calculate 路由：纯数学运算走 calculate，E3D 元素相关走 execute_pml
        /// </summary>
        private (string toolName, string args) RouteCalculate(string args)
        {
            var json = JObject.Parse(args);
            string type = json["type"]?.Value<string>() ?? "";

            // 如果参数包含元素名（element/element1），走 execute_pml
            if (json["element"] != null || json["element1"] != null)
                return ("execute_pml", args);

            // 纯数学运算走 calculate
            if (type == "distance" || type == "angle" || type == "midpoint"
                || type == "vector" || type == "magnitude"
                || type == "dot_product" || type == "cross_product")
                return ("calculate", args);

            // 默认走 execute_pml
            return ("execute_pml", args);
        }
    }
}
