using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Tools.Registry
{
    /// <summary>
    /// 核心工具 → 专用工具 参数路由器
    /// AI 调用 6 个核心工具，Router 根据参数自动分发到 41 个专用工具
    /// </summary>
    public class ToolRouter
    {
        private readonly ToolRegistry _registry;

        public ToolRouter(ToolRegistry registry)
        {
            _registry = registry;
        }

        /// <summary>
        /// 路由核心工具的调用到具体的专用工具
        /// </summary>
        public string Route(string coreToolName, string args)
        {
            switch (coreToolName)
            {
                case "query": return RouteQuery(args);
                case "modify": return RouteModify(args);
                case "check": return RouteCheck(args);
                case "calculate": return RouteCalculate(args);
                case "export": return RouteExport(args);
                case "execute_pml": return RouteExecutePml(args);
                default: return $"未知核心工具: {coreToolName}";
            }
        }

        private string RouteQuery(string args)
        {
            var json = JObject.Parse(args);
            string type = json["type"]?.Value<string>() ?? "";
            string pattern = json["pattern"]?.Value<string>();
            string scope = json["scope"]?.Value<string>();

            // scope=CE + 无type → GetCurrentElementTool
            if (scope == "CE" && string.IsNullOrEmpty(type))
                return Dispatch("get_current_element", args);

            // target + 无filter → GetElementInfoTool
            if (!string.IsNullOrEmpty(json["target"]?.Value<string>()))
                return Dispatch("get_element_info", args);

            // type=POSITION → GetPositionTool
            if (type == "POSITION")
                return Dispatch("get_position", args);

            // type=NOZZ → GetNozzleInfoTool
            if (type == "NOZZ")
                return Dispatch("get_nozzle_info", args);

            // type=BRAN → GetPipeBranchesTool
            if (type == "BRAN")
                return Dispatch("get_pipe_branches", args);

            // 带 filter → 复杂查询
            if (!string.IsNullOrEmpty(json["filter"]?.Value<string>()))
                return Dispatch("query_elements", args);

            // 默认 → 按类型分发
            switch (type)
            {
                case "PIPE": return Dispatch("query_pipes", args);
                case "EQUI": return Dispatch("query_equipment", args);
                case "ALL": return Dispatch("get_hierarchy", args);
                default: return Dispatch("query_elements", args);
            }
        }

        private string RouteModify(string args)
        {
            var json = JObject.Parse(args);
            string target = json["target"]?.Value<string>();
            string attribute = json["attribute"]?.Value<string>();

            // 批量修改（有type+filter）
            if (!string.IsNullOrEmpty(json["type"]?.Value<string>())
                && !string.IsNullOrEmpty(json["filter"]?.Value<string>()))
            {
                if (attribute == "NAME")
                    return Dispatch("batch_rename", args);
                if (attribute == "SPEC" || attribute == "WTHK")
                    return Dispatch("modify_pipe_spec", args);
                return Dispatch("batch_set_attribute", args);
            }

            // 单个元素修改
            if (attribute == "TYPE")
                return Dispatch("create_equipment", args);
            return Dispatch("set_attribute", args);
        }

        private string RouteCheck(string args)
        {
            var json = JObject.Parse(args);
            string type = json["type"]?.Value<string>() ?? "";

            switch (type)
            {
                case "exists": return Dispatch("check_exists", args);
                case "attribute_complete": return Dispatch("check_attribute_complete", args);
                case "room_number": return Dispatch("check_room_number", args);
                case "name_consistency": return Dispatch("check_name_consistency", args);
                case "distance": return Dispatch("check_distance", args);
                case "bore_consistency": return Dispatch("check_bore_consistency", args);
                case "change_status": return Dispatch("get_change_status", args);
                default: return Dispatch("check_exists", args);
            }
        }

        private string RouteCalculate(string args)
        {
            var json = JObject.Parse(args);
            string type = json["type"]?.Value<string>() ?? "";

            switch (type)
            {
                case "distance":
                case "angle":
                case "midpoint":
                    return Dispatch("calculate_distance", args);
                case "orientation":
                    return Dispatch("get_orientation_wrt", args);
                case "ppoint":
                    return Dispatch("get_ppoint_info", args);
                case "route_length":
                    return Dispatch("get_route_info", args);
                default:
                    // 复杂几何通过 execute_pml 生成 PML 脚本
                    return Dispatch("execute_pml", $"{{ \"script\": \"/* {type} 计算 */\" }}");
            }
        }

        private string RouteExport(string args)
        {
            var json = JObject.Parse(args);
            string direction = json["direction"]?.Value<string>() ?? "";
            string format = json["format"]?.Value<string>() ?? "";

            if (direction == "export")
            {
                if (format == "report")
                    return Dispatch("generate_report", args);
                return Dispatch("export_to_excel", args);
            }

            if (direction == "import")
            {
                if (format == "merge")
                    return Dispatch("merge_excel", args);
                return Dispatch("import_excel", args);
            }

            return Dispatch("export_to_excel", args);
        }

        private string RouteExecutePml(string args)
        {
            return Dispatch("execute_pml", args);
        }

        private string Dispatch(string toolName, string args)
        {
            var tool = _registry.Get(toolName);
            if (tool != null)
            {
                // 同步执行（Phase 1b 改为异步）
                var task = tool.ExecuteAsync(args);
                task.Wait();
                return task.Result;
            }

            // 工具未实现 → 给出模拟回复
            return $"[模拟] {toolName} 已执行，参数: {args}";
        }
    }
}
