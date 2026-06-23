using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Tools.Registry
{
    /// <summary>
    /// 核心工具 → 专用工具 参数路由器
    /// AI 调用 6 个核心工具，Router 根据参数自动分发到 41 个专用工具
    /// 实现 IToolRouter 接口供 Core 层调用
    /// </summary>
    public class ToolRouter : IToolRouter
    {
        /// <summary>
        /// 路由核心工具的调用到具体的专用工具
        /// 异步版本，返回 (toolName, args) 供上层执行
        /// </summary>
        public Task<(string toolName, string args)> RouteAsync(string coreToolName, string args)
        {
            switch (coreToolName)
            {
                case "query": return Task.FromResult(RouteQuery(args));
                case "modify": return Task.FromResult(RouteModify(args));
                case "check": return Task.FromResult(RouteCheck(args));
                case "calculate": return Task.FromResult(RouteCalculate(args));
                case "export": return Task.FromResult(RouteExport(args));
                case "execute_pml": return Task.FromResult(RouteExecutePml(args));
                default: return Task.FromResult((coreToolName, args));
            }
        }

        private (string toolName, string args) RouteQuery(string args)
        {
            var json = JObject.Parse(args);
            string type = json["type"]?.Value<string>() ?? "";
            string scope = json["scope"]?.Value<string>();

            // scope=CE + 无type → GetCurrentElement
            if (scope == "CE" && string.IsNullOrEmpty(type))
                return ("get_current_element", args);

            // target + 无filter → GetElementInfo
            if (!string.IsNullOrEmpty(json["target"]?.Value<string>()))
                return ("get_element_info", args);

            // type=POSITION
            if (type == "POSITION")
                return ("get_position", args);

            // type=NOZZ
            if (type == "NOZZ")
                return ("get_nozzle_info", args);

            // type=BRAN
            if (type == "BRAN")
                return ("get_pipe_branches", args);

            // 带 filter → 复杂查询
            if (!string.IsNullOrEmpty(json["filter"]?.Value<string>()))
                return ("query_elements", args);

            // 有 attributes 参数 → 获取属性
            if (json["attributes"] != null || json["name"] != null)
                return ("get_attributes", args);

            // 默认 → 按类型分发
            switch (type)
            {
                case "PIPE": return ("query_pipes", args);
                case "EQUI": return ("query_equipment", args);
                case "ALL":  return ("get_hierarchy", args);
                default:     return ("query_elements", args);
            }
        }

        private (string toolName, string args) RouteModify(string args)
        {
            var json = JObject.Parse(args);

            // 批量修改（有type+filter）
            if (!string.IsNullOrEmpty(json["type"]?.Value<string>())
                && !string.IsNullOrEmpty(json["filter"]?.Value<string>()))
            {
                string attr = json["attribute"]?.Value<string>() ?? "";
                if (attr == "NAME")
                    return ("batch_rename", args);
                if (attr == "SPEC" || attr == "WTHK")
                    return ("modify_pipe_spec", args);
                return ("batch_set_attribute", args);
            }

            // 单个元素修改
            string attribute = json["attribute"]?.Value<string>();
            if (attribute == "TYPE")
                return ("create_equipment", args);
            return ("set_attribute", args);
        }

        private (string toolName, string args) RouteCheck(string args)
        {
            var json = JObject.Parse(args);
            string type = json["type"]?.Value<string>() ?? "";

            switch (type)
            {
                case "exists":              return ("check_exists", args);
                case "attribute_complete":  return ("check_attribute_complete", args);
                case "room_number":         return ("check_room_number", args);
                case "name_consistency":    return ("check_name_consistency", args);
                case "distance":            return ("check_distance", args);
                case "bore_consistency":    return ("check_bore_consistency", args);
                case "change_status":       return ("get_change_status", args);
                default:                    return ("check_exists", args);
            }
        }

        private (string toolName, string args) RouteCalculate(string args)
        {
            var json = JObject.Parse(args);
            string type = json["type"]?.Value<string>() ?? "";

            switch (type)
            {
                case "distance":
                case "angle":
                case "midpoint":
                    return ("calculate_distance", args);
                case "orientation":
                    return ("get_orientation_wrt", args);
                case "ppoint":
                    return ("get_ppoint_info", args);
                case "route_length":
                    return ("get_route_info", args);
                default:
                    return ("execute_pml", $"{{\"script\":\"/* {type} via PML */\"}}");
            }
        }

        private (string toolName, string args) RouteExport(string args)
        {
            var json = JObject.Parse(args);
            string direction = json["direction"]?.Value<string>() ?? "";
            string format = json["format"]?.Value<string>() ?? "";

            if (direction == "export")
            {
                if (format == "report")
                    return ("generate_report", args);
                return ("export_to_excel", args);
            }

            if (direction == "import")
            {
                if (format == "merge")
                    return ("merge_excel", args);
                return ("import_excel", args);
            }

            return ("export_to_excel", args);
        }

        private (string toolName, string args) RouteExecutePml(string args)
        {
            return ("execute_pml", args);
        }
    }
}
