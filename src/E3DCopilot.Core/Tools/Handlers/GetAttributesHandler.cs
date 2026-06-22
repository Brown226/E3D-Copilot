using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// 读取指定元素的属性（C# 直接读取，比 PML 更快）
    /// 支持按属性名数组读取、all=true 读取全部常见属性、或默认读取四个基本属性。
    /// </summary>
    public class GetAttributesHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public GetAttributesHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public string Name => "get_attributes";
        public string Description => "Read attributes of a specific E3D element by name or DBURI (fast C# API). Prefer this over execute_pml for reading attributes.";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""element"": { ""type"": ""string"", ""description"": ""元素名称或 DBURI，例如 /MDS/FRAMES/VT18 或 VT18"" },
    ""attributes"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""要读取的属性名列表，如 [""NAME"", ""TYPE"", ""WTHK"", ""DESC""]"" },
    ""all"": { ""type"": ""boolean"", ""description"": ""是否读取全部常见属性集合（默认 false）"" }
  },
  ""required"": [""element""]
}";
        public bool IsReadOnly => true;

        /// <summary>
        /// 全部常见属性列表（对应 all=true）
        /// </summary>
        private static readonly string[] CommonAttributes = new[]
        {
            "NAME", "TYPE", "DESC", "OWNER",
            "WTHK", "LENG", "BORE", "SPEC",
            "SPRE", "SREF", "HEIG", "WIDT",
            "DPTH", "VOLU", "AREA",
            "POSX", "POSY", "POSZ"
        };

        /// <summary>
        /// 默认属性列表（未指定任何筛选条件时）
        /// </summary>
        private static readonly string[] DefaultAttributes = new[]
        {
            "NAME", "TYPE", "DESC", "OWNER"
        };

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                string element = json["element"]?.ToString();
                var attrArray = json["attributes"] as JArray;
                bool readAll = json["all"]?.Value<bool>() ?? false;

                if (string.IsNullOrEmpty(element))
                    return ToolResult.Fail("缺少 element 参数");

                // 确定要读取的属性列表
                string[] attrs;
                if (attrArray != null && attrArray.Count > 0)
                {
                    // 1. 如果 attributes 参数存在且非空，使用指定属性（转为大写，不区分大小写）
                    attrs = attrArray.Select(a => a.ToString().ToUpperInvariant()).Distinct().ToArray();
                }
                else if (readAll)
                {
                    // 2. 如果 all=true，读取全部常见属性
                    attrs = CommonAttributes;
                }
                else
                {
                    // 3. 默认读取四个基本属性
                    attrs = DefaultAttributes;
                }

                var attrResults = new JObject();

                foreach (var attr in attrs)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        string value = _dispatcher.GetAttribute(element, attr);
                        // 将值写入结果（允许空字符串，仅跳过 null）
                        attrResults[attr] = value ?? string.Empty;
                    }
                    catch
                    {
                        // 属性不存在、不可读或其它异常，跳过该属性
                    }
                }

                var result = new JObject
                {
                    ["element"] = element,
                    ["success"] = true,
                    ["count"] = attrResults.Count,
                    ["attributes"] = attrResults
                };

                // 使用缩进格式使 JSON 更清晰易读
                return ToolResult.Ok(result.ToString(Formatting.Indented), null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"Get attributes failed: {ex.Message}");
            }
        }
    }
}
