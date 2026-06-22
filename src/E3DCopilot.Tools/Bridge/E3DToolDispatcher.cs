using System;
using System.Collections.Generic;
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
        /// 处理修改请求
        /// </summary>
        private string HandleModify(string args)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                var element = json["element"]?.ToString();
                var attribute = json["attribute"]?.ToString();
                var value = json["value"]?.ToString();

                if (string.IsNullOrEmpty(element) || string.IsNullOrEmpty(attribute))
                {
                    return "{\"success\": false, \"error\": \"缺少 element 或 attribute 参数\"}";
                }

                _env.SetAttribute(element, attribute, value);

                return $"{{\"success\": true, \"message\": \"已设置 {element}.{attribute} = {value}\"}}";
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }

        /// <summary>
        /// 处理检查请求
        /// </summary>
        private string HandleCheck(string args)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                var element = json["element"]?.ToString();

                if (string.IsNullOrEmpty(element))
                {
                    return "{\"success\": false, \"error\": \"缺少 element 参数\"}";
                }

                bool exists = _env.CheckExists(element);

                return $"{{\"success\": true, \"exists\": {exists.ToString().ToLower()}}}";
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }

        /// <summary>
        /// 处理计算请求（占位）
        /// </summary>
        private string HandleCalculate(string args)
        {
            return "{\"success\": true, \"message\": \"计算工具尚未实现\"}";
        }

        /// <summary>
        /// 处理导出请求（占位）
        /// </summary>
        private string HandleExport(string args)
        {
            return "{\"success\": true, \"message\": \"导出工具尚未实现\"}";
        }

        /// <summary>
        /// 处理 PML 执行请求
        /// </summary>
        private string HandleExecutePml(string args)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                var command = json["command"]?.ToString();

                if (string.IsNullOrEmpty(command))
                {
                    return "{\"success\": false, \"error\": \"缺少 command 参数\"}";
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
    }
}
