using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Tools.Bridge
{
    /// <summary>
    /// E3D 工具调度器 —— 实现 Core 层的 IToolDispatcher 接口
    /// 将 Core 的工具调用请求路由到 IE3DEnvironment 的真实/模拟实现
    /// 
    /// 这是 Core → Tools 依赖的关键桥梁：
    /// Core (DbQueryHandler) → IToolDispatcher → E3DToolDispatcher → IE3DEnvironment → E3D API
    /// </summary>
    public class E3DToolDispatcher : IToolDispatcher
    {
        private readonly IE3DEnvironment _env;

        public E3DToolDispatcher(IE3DEnvironment environment)
        {
            _env = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        /// <summary>
        /// 按名称执行工具并返回结果 JSON 字符串
        /// </summary>
        public Task<string> ExecuteAsync(string name, string args)
        {
            switch ((name ?? "").ToLower())
            {
                case "query":
                    return Task.FromResult(HandleQuery(args));

                case "modify":
                    return Task.FromResult(HandleModify(args));

                case "check":
                    return Task.FromResult(HandleCheck(args));

                case "calculate":
                    return Task.FromResult(HandleCalculate(args));

                case "export":
                    return Task.FromResult(HandleExport(args));

                case "execute_pml":
                    return Task.FromResult(HandleExecutePml(args));

                case "design":
                    return Task.FromResult(HandleDesign(args));

                case "piping":
                    return Task.FromResult(HandlePiping(args));

                case "geometry":
                    return Task.FromResult(HandleGeometry(args));

                default:
                    return Task.FromResult($"{{\"success\": false, \"error\": \"未知工具: {name}\"}}");
            }
        }

        /// <summary>
        /// 获取 E3D 当前选中元素名称（通过 CurrentElement.Element 静态属性）
        /// </summary>
        public string GetCurrentElementName()
        {
            try
            {
                return _env.GetCurrentElementName();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取 E3D 多选元素名称列表
        /// </summary>
        public List<string> GetSelectedElementNames()
        {
            try
            {
                return _env.GetSelectedElementNames();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// 读取指定元素的指定属性（C# 直接 API）
        /// </summary>
        public string GetAttribute(string element, string attribute)
        {
            try
            {
                return _env.GetAttribute(element, attribute);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 处理查询请求
        /// </summary>
        private string HandleQuery(string args)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                var type = json["type"]?.ToString() ?? "";
                var name = json["name"]?.ToString() ?? "";
                var scope = json["scope"]?.ToString() ?? "";
                var limit = json["limit"]?.Value<int>() ?? 50;

                var elements = _env.QueryElements(type, name, scope, limit);

                // 构建结果 JSON
                var resultArray = new JArray();
                foreach (var elem in elements)
                {
                    var obj = new JObject
                    {
                        ["name"] = elem.Name,
                        ["type"] = elem.Type,
                        ["dbUri"] = elem.DbUri
                    };

                    // 附加属性
                    if (elem.Attributes != null)
                    {
                        var attrs = new JObject();
                        foreach (var attr in elem.Attributes)
                        {
                            attrs[attr.Key] = attr.Value;
                        }
                        obj["attributes"] = attrs;
                    }

                    resultArray.Add(obj);
                }

                var result = new JObject
                {
                    ["success"] = true,
                    ["count"] = elements.Count,
                    ["elements"] = resultArray
                };

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }

        /// <summary>
        /// 处理修改请求 — 支持 dburi + attributes（主接口），兼容 element + attribute + value（旧接口）
        /// </summary>
        private string HandleModify(string args)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");

                // 主接口：dburi + attributes（键值对对象）
                var dburi = json["dburi"]?.ToString()
                    ?? json["element"]?.ToString();
                var attributes = json["attributes"] as JObject;

                // 兼容旧接口：element + attribute + value（单属性模式）
                if (attributes == null && json["attribute"] != null)
                {
                    attributes = new JObject
                    {
                        [json["attribute"]?.ToString()] = json["value"]?.ToString()
                    };
                }

                if (string.IsNullOrEmpty(dburi))
                {
                    return "{\"success\": false, \"error\": \"缺少 dburi 或 element 参数\"}";
                }

                if (attributes == null || attributes.Count == 0)
                {
                    return "{\"success\": false, \"error\": \"缺少 attributes 参数\"}";
                }

                // 逐属性设置
                var setList = new List<string>();
                foreach (var prop in attributes.Properties())
                {
                    _env.SetAttribute(dburi, prop.Name, prop.Value?.ToString());
                    setList.Add($"{prop.Name}={prop.Value}");
                }

                return $"{{\"success\": true, \"message\": \"已设置 {dburi} 的 {attributes.Count} 个属性: {string.Join(", ", setList)}\"}}";
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }

        /// <summary>
        /// 处理检查请求 — 支持多种检查类型:
        ///   exists              : 元素存在性检查
        ///   attribute           : 属性值验证（比较实际值与期望值）
        ///   attribute_complete  : 属性完整性检查（同 attribute，检查属性是否存在）
        ///   naming              : 命名规范检查（正则匹配）
        ///   name_consistency    : 命名一致性检查（同 naming）
        ///   clearance           : 碰撞/净距检查（占位）
        ///   distance            : 距离检查（同 clearance）
        ///   bore_consistency    : 管径一致性检查（占位）
        ///   change_status       : 变更状态检查（占位）
        ///   room_number         : 房间号检查（占位）
        /// </summary>
        private string HandleCheck(string args)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                string checkType = json["type"]?.ToString()?.ToLower() ?? "exists";

                // 兼容 element/dburi/target 多种参数名
                string element = json["element"]?.ToString()
                    ?? json["dburi"]?.ToString()
                    ?? json["target"]?.ToString();

                switch (checkType)
                {
                    case "exists":
                        return HandleCheckExists(json, element);
                    case "attribute":
                    case "attribute_complete":
                        return HandleCheckAttribute(json, element);
                    case "naming":
                    case "name_consistency":
                        return HandleCheckNaming(json, element);
                    case "clearance":
                    case "distance":
                        return HandleCheckClearance(json, element);
                    case "bore_consistency":
                        return HandleCheckBoreConsistency(json, element);
                    case "change_status":
                        return HandleCheckChangeStatus(json, element);
                    case "room_number":
                        return HandleCheckRoomNumber(json, element);
                    default:
                        return $"{{\"success\": false, \"error\": \"未知检查类型: {checkType}，支持: exists, attribute, attribute_complete, naming, name_consistency, clearance, distance, bore_consistency, change_status, room_number\"}}";
                }
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }

        /// <summary>
        /// 元素存在性检查
        /// </summary>
        private string HandleCheckExists(JObject json, string element)
        {
            bool exists = _env.CheckExists(element);
            var result = new JObject
            {
                ["success"] = true,
                ["type"] = "exists",
                ["element"] = element,
                ["exists"] = exists
            };
            return result.ToString();
        }

        /// <summary>
        /// 属性值验证
        /// </summary>
        private string HandleCheckAttribute(JObject json, string element)
        {
            string attributeName = json["attribute"]?.ToString()
                ?? json["attr"]?.ToString();
            string expectedValue = json["expected"]?.ToString()
                ?? json["value"]?.ToString();

            if (string.IsNullOrEmpty(attributeName))
            {
                return "{\"success\": false, \"error\": \"attribute 检查需要 attribute/attr 参数\"}";
            }

            // 首先检查元素是否存在
            if (!_env.CheckExists(element))
            {
                return $"{{\"success\": false, \"error\": \"元素 {element} 不存在\"}}";
            }

            string actualValue = _env.GetAttribute(element, attributeName);
            bool matches = expectedValue == null
                ? actualValue != null    // 没有期望值时只检查属性存在
                : string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase);

            var result = new JObject
            {
                ["success"] = true,
                ["type"] = "attribute",
                ["element"] = element,
                ["attribute"] = attributeName,
                ["expected"] = expectedValue ?? "(any)",
                ["actual"] = actualValue ?? "(null)",
                ["matches"] = matches,
                ["description"] = matches
                    ? $"属性 {attributeName} = {actualValue}，匹配期望值"
                    : $"属性 {attributeName} = {actualValue}，不匹配期望值 {expectedValue ?? "(any)"}"
            };
            return result.ToString();
        }

        /// <summary>
        /// 命名规范检查（正则匹配）
        /// </summary>
        private string HandleCheckNaming(JObject json, string element)
        {
            string pattern = json["pattern"]?.ToString();
            if (string.IsNullOrEmpty(pattern))
            {
                return "{\"success\": false, \"error\": \"naming 检查需要 pattern 参数（正则表达式）\"}";
            }

            try
            {
                var regex = new System.Text.RegularExpressions.Regex(
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // 检查元素本身的名字
                string elementName = json["elementName"]?.ToString()
                    ?? json["name"]?.ToString()
                    ?? element;

                bool matches = regex.IsMatch(elementName);

                var result = new JObject
                {
                    ["success"] = true,
                    ["type"] = "naming",
                    ["element"] = element,
                    ["elementName"] = elementName,
                    ["pattern"] = pattern,
                    ["matches"] = matches,
                    ["description"] = matches
                        ? $"名称 \"{elementName}\" 符合模式 \"{pattern}\""
                        : $"名称 \"{elementName}\" 不符合模式 \"{pattern}\""
                };
                return result.ToString();
            }
            catch (ArgumentException ex)
            {
                return $"{{\"success\": false, \"error\": \"正则表达式无效: {ex.Message}\"}}";
            }
        }

        /// <summary>
        /// 碰撞/净距检查（预留 — 需要 3D 几何计算，当前返回引导信息）
        /// </summary>
        private string HandleCheckClearance(JObject json, string element)
        {
            // Clearance check requires geometric computations (bounding boxes, nearest points)
            // which depend on E3D Geometry API (D3Point, D3Line, etc.)
            var result = new JObject
            {
                ["success"] = true,
                ["type"] = "clearance",
                ["element"] = element,
                ["implemented"] = false,
                ["message"] = "净距检查需要 3D 几何计算，当前尚未实现。可用 calculate 工具进行点/向量运算，或用 query 获取元素坐标后进行手动计算。",
                ["alternatives"] = new JArray
                {
                    "使用 calculate 工具计算两点距离",
                    "使用 query 工具获取元素坐标属性",
                    "通过 PML 执行 'CLASH' 命令"
                }
            };
            return result.ToString();
        }

        /// <summary>
        /// 管径一致性检查（占位 — 需要遍历管件，当前返回引导信息）
        /// </summary>
        private string HandleCheckBoreConsistency(JObject json, string element)
        {
            var result = new JObject
            {
                ["success"] = true,
                ["type"] = "bore_consistency",
                ["element"] = element,
                ["implemented"] = false,
                ["message"] = "管径一致性检查需要遍历管件的 BORE 属性，当前尚未实现。可通过 PML 执行检查。",
                ["alternatives"] = new JArray
                {
                    "使用 query 获取所有管件的 BORE 属性",
                    "通过 execute_pml 执行 PML 脚本检查"
                }
            };
            return result.ToString();
        }

        /// <summary>
        /// 变更状态检查（占位 — 需要 E3D 变更追踪 API，当前返回引导信息）
        /// </summary>
        private string HandleCheckChangeStatus(JObject json, string element)
        {
            var result = new JObject
            {
                ["success"] = true,
                ["type"] = "change_status",
                ["element"] = element,
                ["implemented"] = false,
                ["message"] = "变更状态检查需要 E3D 变更追踪功能，当前尚未实现。",
                ["alternatives"] = new JArray
                {
                    "通过 execute_pml 执行 PML 脚本检查变更状态"
                }
            };
            return result.ToString();
        }

        /// <summary>
        /// 房间号检查（占位 — 需要遍历设备并检查 ROOM_NO 属性，当前返回引导信息）
        /// </summary>
        private string HandleCheckRoomNumber(JObject json, string element)
        {
            var result = new JObject
            {
                ["success"] = true,
                ["type"] = "room_number",
                ["element"] = element,
                ["implemented"] = false,
                ["message"] = "房间号检查需要遍历设备的 ROOM_NO 属性，当前尚未实现。可通过 PML 执行检查。",
                ["alternatives"] = new JArray
                {
                    "使用 query 获取设备的 ROOM_NO 属性",
                    "通过 execute_pml 执行 PML 脚本检查"
                }
            };
            return result.ToString();
        }

        /// <summary>
        /// 处理计算请求（占位 — 实际计算由 Core 层 CalculateHandler 处理）
        /// 此方法不会被调用，因为 calculate 不经过 DispatcherBackedHandler。
        /// 保留仅用于向后兼容。
        /// </summary>
        private string HandleCalculate(string args)
        {
            return "{\"success\": true, \"message\": \"几何计算由 Core 层 CalculateHandler 处理（纯数学运算）。如需 E3D 元素几何计算，请使用 execute_pml。\"}";
        }

        /// <summary>
        /// 处理导出请求 — CSV / PML 导出
        /// 使用 IE3DEnvironment.QueryElements 获取数据，写入本地文件
        /// </summary>
        private string HandleExport(string args)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                string action = json["action"]?.ToString() ?? "export";
                string format = (json["format"]?.ToString() ?? "csv").ToLower();

                // 解析查询条件
                string queryParam = json["query"]?.ToString();
                string filePath = json["filePath"]?.ToString();

                // 解析 query 字符串为 elementType + namePattern
                string elementType = "";
                string namePattern = "";
                if (!string.IsNullOrEmpty(queryParam))
                {
                    // 支持格式: "type=PIPE,name=*" 或直接作为 name pattern
                    if (queryParam.Contains("="))
                    {
                        var parts = queryParam.Split(',');
                        foreach (var part in parts)
                        {
                            var kv = part.Split('=');
                            if (kv.Length == 2)
                            {
                                string key = kv[0].Trim().ToLower();
                                string val = kv[1].Trim();
                                if (key == "type")
                                    elementType = val;
                                else if (key == "name")
                                    namePattern = val;
                            }
                        }
                    }
                    else
                    {
                        // 纯文本作为 name pattern
                        namePattern = queryParam;
                    }
                }

                // 如果没有指定文件路径，生成默认路径
                if (string.IsNullOrEmpty(filePath))
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    filePath = System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        $"E3DExport_{timestamp}.{format}");
                }

                // 查询元素
                var elements = _env.QueryElements(elementType, namePattern, "", 1000);

                // 格式化输出
                string content;
                if (format == "pml")
                {
                    content = GeneratePmlScript(elements);
                }
                else
                {
                    content = GenerateCsv(elements);
                }

                // 写入文件
                System.IO.File.WriteAllText(filePath, content, Encoding.UTF8);

                var result = new JObject
                {
                    ["success"] = true,
                    ["message"] = $"成功导出 {elements.Count} 个元素到 {filePath}",
                    ["filePath"] = filePath,
                    ["count"] = elements.Count,
                    ["format"] = format
                };
                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }

        /// <summary>
        /// 生成 CSV 格式
        /// </summary>
        private string GenerateCsv(List<ElementInfo> elements)
        {
            if (elements == null || elements.Count == 0)
                return "NAME,TYPE,DBURI\n";

            // 收集所有属性键
            var allKeys = new HashSet<string> { "NAME", "TYPE" };
            foreach (var elem in elements)
            {
                if (elem.Attributes != null)
                {
                    foreach (var key in elem.Attributes.Keys)
                        allKeys.Add(key);
                }
            }

            var keys = allKeys.ToList();
            var sb = new StringBuilder();

            // 写表头
            sb.AppendLine(string.Join(",", keys.Select(k => CsvEscape(k))));

            // 写数据行
            foreach (var elem in elements)
            {
                var values = new List<string>();
                foreach (var key in keys)
                {
                    if (key == "NAME")
                        values.Add(CsvEscape(elem.Name));
                    else if (key == "TYPE")
                        values.Add(CsvEscape(elem.Type));
                    else
                        values.Add(CsvEscape(
                            elem.Attributes?.ContainsKey(key) == true ? elem.Attributes[key] : ""));
                }
                sb.AppendLine(string.Join(",", values));
            }

            return sb.ToString();
        }

        /// <summary>
        /// 生成 PML 注释脚本
        /// </summary>
        private string GeneratePmlScript(List<ElementInfo> elements)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"! Export generated by E3D Copilot");
            sb.AppendLine($"! Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"! Count: {elements.Count}");
            sb.AppendLine();

            foreach (var elem in elements)
            {
                sb.AppendLine($"!! Element: {elem.Name} ({elem.Type}) [{elem.DbUri}]");
                if (elem.Attributes != null)
                {
                    foreach (var attr in elem.Attributes)
                    {
                        sb.AppendLine($"!   {attr.Key} = {attr.Value}");
                    }
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// CSV 转义（含逗号/引号/换行的字段用双引号包裹）
        /// </summary>
        private string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        /// <summary>
        /// 处理 PML 执行请求
        /// </summary>
        private string HandleExecutePml(string args)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                // 优先读取 script（与 PmlCommandHandler Schema 一致），兼容旧参数 command
                var command = json["script"]?.ToString()
                    ?? json["command"]?.ToString();

                if (string.IsNullOrEmpty(command))
                {
                    return "{\"success\": false, \"error\": \"缺少 script 参数\"}";
                }

                string result = _env.ExecutePml(command);

                var jResult = new JObject
                {
                    ["success"] = true,
                    ["result"] = result
                };
                return jResult.ToString();
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }

        // ==================== Design Handler ====================

        /// <summary>
        /// 处理设计建模请求 — 创建/删除 Equipment/Component
        /// </summary>
        private string HandleDesign(string args)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                string action = json["action"]?.ToString()?.ToLower() ?? "";

                switch (action)
                {
                    case "create_equipment":
                    case "create_component":
                    {
                        string parent = json["parent"]?.ToString();
                        string name = json["name"]?.ToString();
                        string type = json["type"]?.ToString()
                            ?? (action == "create_equipment" ? "EQUIPMENT" : "COMPONENT");
                        string attrs = json["attributes"]?.ToString();

                        if (string.IsNullOrEmpty(parent))
                            return "{\"success\": false, \"error\": \"缺少 parent 参数\"}";

                        return _env.CreateElement(parent, name, type, attrs);
                    }

                    case "delete_element":
                    {
                        string element = json["element"]?.ToString();
                        if (string.IsNullOrEmpty(element))
                            return "{\"success\": false, \"error\": \"缺少 element 参数\"}";

                        bool ok = _env.DeleteElement(element);
                        return $"{{\"success\": {ok.ToString().ToLower()}, \"element\": \"{element}\", \"deleted\": {ok.ToString().ToLower()}}}";
                    }

                    case "set_position":
                    {
                        string element = json["element"]?.ToString();
                        double? x = json["x"]?.Value<double>();
                        double? y = json["y"]?.Value<double>();
                        double? z = json["z"]?.Value<double>();

                        if (string.IsNullOrEmpty(element))
                            return "{\"success\": false, \"error\": \"缺少 element 参数\"}";
                        if (x == null || y == null || z == null)
                            return "{\"success\": false, \"error\": \"缺少 x/y/z 坐标参数\"}";

                        // 使用 PML 设置位置
                        string pml = $"$P var = DB ELEMENT '{element.Replace("'", "''")}'";
                        pml += $" ; $P var.X = {x.Value}";
                        pml += $" ; $P var.Y = {y.Value}";
                        pml += $" ; $P var.Z = {z.Value}";
                        _env.ExecutePml(pml);

                        return $"{{\"success\": true, \"element\": \"{element}\", \"x\": {x}, \"y\": {y}, \"z\": {z}}}";
                    }

                    default:
                        return $"{{\"success\": false, \"error\": \"未知 design 操作: {action}，支持: create_equipment, create_component, delete_element, set_position\"}}";
                }
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }

        // ==================== Piping Handler ====================

        /// <summary>
        /// 处理管道操作请求 — 创建 Pipe/Branch/Fitment，设置规格
        /// </summary>
        private string HandlePiping(string args)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                string action = json["action"]?.ToString()?.ToLower() ?? "";

                switch (action)
                {
                    case "create_pipe":
                    case "create_branch":
                    {
                        string parent = action == "create_pipe"
                            ? json["parent"]?.ToString()
                            : json["pipe"]?.ToString();
                        string name = json["name"]?.ToString();
                        string type = action == "create_pipe" ? "PIPE" : "BRANCH";
                        string attrs = json["attributes"]?.ToString();

                        if (string.IsNullOrEmpty(parent))
                            return $"{{\"success\": false, \"error\": \"缺少 {(action == "create_pipe" ? "parent" : "pipe")} 参数\"}}";

                        return _env.CreateElement(parent, name, type, attrs);
                    }

                    case "add_fitment":
                    {
                        string pipe = json["pipe"]?.ToString();
                        string fitmentType = json["fitmentType"]?.ToString() ?? "FLANGE";
                        string name = json["name"]?.ToString() ?? (fitmentType + "-" + DateTime.Now.Ticks);
                        string attrs = json["attributes"]?.ToString();

                        if (string.IsNullOrEmpty(pipe))
                            return "{\"success\": false, \"error\": \"缺少 pipe 参数\"}";

                        return _env.CreateElement(pipe, name, fitmentType, attrs);
                    }

                    case "set_spec":
                    {
                        string pipe = json["pipe"]?.ToString();
                        string spec = json["spec"]?.ToString();

                        if (string.IsNullOrEmpty(pipe))
                            return "{\"success\": false, \"error\": \"缺少 pipe 参数\"}";
                        if (string.IsNullOrEmpty(spec))
                            return "{\"success\": false, \"error\": \"缺少 spec 参数\"}";

                        _env.SetAttribute(pipe, "SPEC", spec);
                        return $"{{\"success\": true, \"pipe\": \"{pipe}\", \"spec\": \"{spec}\"}}";
                    }

                    default:
                        return $"{{\"success\": false, \"error\": \"未知 piping 操作: {action}，支持: create_pipe, create_branch, add_fitment, set_spec\"}}";
                }
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }

        // ==================== Geometry Handler ====================

        /// <summary>
        /// 处理几何查询请求 — 获取元素位置/方向，计算元素间距离
        /// </summary>
        private string HandleGeometry(string args)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                string action = json["action"]?.ToString()?.ToLower() ?? "";

                switch (action)
                {
                    case "get_position":
                    {
                        string element = json["element"]?.ToString();
                        if (string.IsNullOrEmpty(element))
                            return "{\"success\": false, \"error\": \"缺少 element 参数\"}";

                        string posStr = _env.GetAttribute(element, "POSITION");
                        string xStr = _env.GetAttribute(element, "X");
                        string yStr = _env.GetAttribute(element, "Y");
                        string zStr = _env.GetAttribute(element, "Z");

                        var result = new JObject
                        {
                            ["success"] = true,
                            ["type"] = "get_position",
                            ["element"] = element,
                            ["position"] = posStr ?? $"{xStr ?? "?"}, {yStr ?? "?"}, {zStr ?? "?"}",
                            ["x"] = xStr,
                            ["y"] = yStr,
                            ["z"] = zStr
                        };
                        return result.ToString();
                    }

                    case "get_orientation":
                    {
                        string element = json["element"]?.ToString();
                        if (string.IsNullOrEmpty(element))
                            return "{\"success\": false, \"error\": \"缺少 element 参数\"}";

                        string orientStr = _env.GetAttribute(element, "ORIENTATION");
                        string orientX = _env.GetAttribute(element, "ORIENTX");
                        string orientY = _env.GetAttribute(element, "ORIENTY");
                        string orientZ = _env.GetAttribute(element, "ORIENTZ");

                        var result = new JObject
                        {
                            ["success"] = true,
                            ["type"] = "get_orientation",
                            ["element"] = element,
                            ["orientation"] = orientStr ?? $"{orientX ?? "?"}, {orientY ?? "?"}, {orientZ ?? "?"}",
                            ["orientX"] = orientX,
                            ["orientY"] = orientY,
                            ["orientZ"] = orientZ
                        };
                        return result.ToString();
                    }

                    case "distance_between":
                    {
                        string elem1 = json["element1"]?.ToString();
                        string elem2 = json["element2"]?.ToString();

                        if (string.IsNullOrEmpty(elem1) || string.IsNullOrEmpty(elem2))
                            return "{\"success\": false, \"error\": \"需要 element1 和 element2 参数\"}";

                        // 读取坐标
                        double x1 = DoubleParse(_env.GetAttribute(elem1, "X"));
                        double y1 = DoubleParse(_env.GetAttribute(elem1, "Y"));
                        double z1 = DoubleParse(_env.GetAttribute(elem1, "Z"));
                        double x2 = DoubleParse(_env.GetAttribute(elem2, "X"));
                        double y2 = DoubleParse(_env.GetAttribute(elem2, "Y"));
                        double z2 = DoubleParse(_env.GetAttribute(elem2, "Z"));

                        double dx = x2 - x1, dy = y2 - y1, dz = z2 - z1;
                        double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                        var result = new JObject
                        {
                            ["success"] = true,
                            ["type"] = "distance_between",
                            ["element1"] = elem1,
                            ["element2"] = elem2,
                            ["distance_mm"] = Math.Round(distance, 3),
                            ["distance_m"] = Math.Round(distance / 1000.0, 6),
                            ["delta"] = new JObject
                            {
                                ["dx"] = Math.Round(dx, 3),
                                ["dy"] = Math.Round(dy, 3),
                                ["dz"] = Math.Round(dz, 3)
                            }
                        };
                        return result.ToString();
                    }

                    case "bounding_box":
                    {
                        string element = json["element"]?.ToString();
                        if (string.IsNullOrEmpty(element))
                            return "{\"success\": false, \"error\": \"缺少 element 参数\"}";

                        // 读取边界框属性（如果元素支持）
                        var min = new JObject
                        {
                            ["x"] = _env.GetAttribute(element, "MINX") ?? "(N/A)",
                            ["y"] = _env.GetAttribute(element, "MINY") ?? "(N/A)",
                            ["z"] = _env.GetAttribute(element, "MINZ") ?? "(N/A)"
                        };
                        var max = new JObject
                        {
                            ["x"] = _env.GetAttribute(element, "MAXX") ?? "(N/A)",
                            ["y"] = _env.GetAttribute(element, "MAXY") ?? "(N/A)",
                            ["z"] = _env.GetAttribute(element, "MAXZ") ?? "(N/A)"
                        };

                        var result = new JObject
                        {
                            ["success"] = true,
                            ["type"] = "bounding_box",
                            ["element"] = element,
                            ["min"] = min,
                            ["max"] = max,
                            ["note"] = "部分元素类型不支持边界框属性，返回 N/A"
                        };
                        return result.ToString();
                    }

                    default:
                        return $"{{\"success\": false, \"error\": \"未知 geometry 操作: {action}，支持: get_position, get_orientation, distance_between, bounding_box\"}}";
                }
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }

        /// <summary>
        /// 安全解析 double（从属性字符串解析）
        /// </summary>
        private static double DoubleParse(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            double.TryParse(value.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double result);
            return result;
        }
    }
}
