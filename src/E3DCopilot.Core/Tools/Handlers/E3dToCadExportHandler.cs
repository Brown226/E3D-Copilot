using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Models.Geometry;
using E3DCopilot.Core.Services.Cad;
using E3DCopilot.Core.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// E3D → CAD 反向导出工具 — 把 E3D 元素导出为 DXF 文件
    ///
    /// 职责：读取 E3D 元素数据 → 转换为 CAD 实体 → 写入 DXF 文件
    /// 通过 IToolDispatcher 调用 query/get_attributes 获取 E3D 元素数据
    /// 使用 CadExportService + netDxf 库生成 DXF
    ///
    /// 当前限制：只支持 DXF 格式（netDxf 不支持写 DWG）
    /// </summary>
    public class E3dToCadExportHandler : IToolHandler
    {
        public string Name => "e3d_to_cad_export";
        public bool IsReadOnly => true;  // 只读 E3D，写文件不算改 E3D

        public string Description => @"把 E3D 元素（管道/设备/结构）导出为 DXF 文件。
支持按元素列表或按查询范围导出。

操作类型：
- preview: 预览导出映射（不生成文件）
- export_dxf: 导出为 DXF 文件
- export_dwg: 导出为 DWG（当前不支持，返回错误提示用 DXF）
- list_exportable: 列出范围内可导出的元素

导出参数：
- elements: E3D 元素 DBURI 数组（与 scope 二选一）
- scope: 查询范围（如 /ZONE-PIPE-01）
- element_type: 元素类型过滤（scope 模式用，如 PIPE/EQUI/STRU/ALL）
- output_path: 输出文件路径（.dxf）
- options: 导出选项（layer_prefix/include_text/projection/scale）

元素类型映射：
- PIPE → Line + DN标注，图层 PIPE-DN{DIA}
- EQUI → 矩形 + 位号标注，图层 EQUI-{TYPE}
- STRU → Line + 名称标注，图层 STRU-{TYPE}
- BRAN → Line + DN/SPRE标注，图层 BRAN-DN{DIA}
- SUPP → Circle + 类型标注，图层 SUPP";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""action"": {
      ""type"": ""string"",
      ""enum"": [""preview"", ""export_dxf"", ""export_dwg"", ""list_exportable""],
      ""description"": ""操作类型""
    },
    ""elements"": {
      ""type"": ""array"",
      ""items"": {""type"": ""string""},
      ""description"": ""E3D 元素 DBURI 数组（与 scope 二选一）""
    },
    ""scope"": {
      ""type"": ""string"",
      ""description"": ""查询范围（如 /ZONE-PIPE-01，与 elements 二选一）""
    },
    ""element_type"": {
      ""type"": ""string"",
      ""description"": ""元素类型过滤（scope 模式用，如 PIPE/EQUI/STRU/ALL）""
    },
    ""output_path"": {
      ""type"": ""string"",
      ""description"": ""输出文件路径（.dxf）""
    },
    ""options"": {
      ""type"": ""object"",
      ""properties"": {
        ""layer_prefix"": {""type"": ""string"", ""description"": ""图层名前缀，默认空""},
        ""include_text"": {""type"": ""boolean"", ""description"": ""是否包含标注文字，默认 true""},
        ""include_dimensions"": {""type"": ""boolean"", ""description"": ""是否包含尺寸标注，默认 true""},
        ""scale"": {""type"": ""number"", ""description"": ""比例，默认 1.0""},
        ""projection"": {
          ""type"": ""string"",
          ""enum"": [""plan"", ""elevation"", ""iso""],
          ""description"": ""投影方式，默认 plan（平面图）""
        }
      }
    }
  },
  ""required"": [""action""]
}";

        private readonly IToolDispatcher _dispatcher;
        private readonly CadExportService _exportService;

        public E3dToCadExportHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _exportService = new CadExportService();
        }

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                string action = json["action"]?.ToString()?.ToLower() ?? "";

                switch (action)
                {
                    case "preview":
                        return await HandlePreview(json);
                    case "export_dxf":
                        return await HandleExport(json, "dxf");
                    case "export_dwg":
                        return await HandleExport(json, "dwg");
                    case "list_exportable":
                        return await HandleListExportable(json);
                    default:
                        return ToolResult.Fail($"未知操作: {action}，支持: preview, export_dxf, export_dwg, list_exportable");
                }
            }
            catch (JsonException ex)
            {
                return ToolResult.Fail($"参数 JSON 解析错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"E3D→CAD 导出失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 预览导出映射（不生成文件）
        /// </summary>
        private async Task<ToolResult> HandlePreview(JObject json)
        {
            var elements = await ResolveElements(json);
            if (elements == null || elements.Count == 0)
                return ToolResult.Fail("未找到可导出的元素");

            var options = ParseOptions(json["options"]);
            var previewResult = _exportService.Preview(elements, options);

            if (!previewResult.Success)
                return ToolResult.Fail(previewResult.Error);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"📋 导出预览");
            sb.AppendLine($"  元素数: {previewResult.ElementCount}");
            sb.AppendLine($"  图层分布:");
            foreach (var kv in previewResult.LayerStats)
            {
                sb.AppendLine($"    {kv.Key}: {kv.Value} 个");
            }
            sb.AppendLine($"  投影: {options.Projection}");
            sb.AppendLine($"  比例: {options.Scale}");
            sb.AppendLine($"  包含标注: {options.IncludeText}");

            return ToolResult.Ok(sb.ToString(), new
            {
                elementCount = previewResult.ElementCount,
                layerStats = previewResult.LayerStats,
                projection = options.Projection,
                scale = options.Scale,
                includeText = options.IncludeText
            });
        }

        /// <summary>
        /// 导出为 DXF 或 DWG
        /// </summary>
        private async Task<ToolResult> HandleExport(JObject json, string format)
        {
            string outputPath = json["output_path"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputPath))
                return ToolResult.Fail("output_path 参数为空");

            var elements = await ResolveElements(json);
            if (elements == null || elements.Count == 0)
                return ToolResult.Fail("未找到可导出的元素");

            var options = ParseOptions(json["options"]);

            CadExportResult result;
            if (format == "dxf")
            {
                result = _exportService.ExportToDxf(elements, outputPath, options);
            }
            else
            {
                result = _exportService.ExportToDwg(elements, outputPath, options);
            }

            if (!result.Success)
                return ToolResult.Fail(result.Error);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"✅ 导出完成");
            sb.AppendLine($"  文件: {result.OutputPath}");
            sb.AppendLine($"  元素数: {result.ElementCount}");
            sb.AppendLine($"  图层分布:");
            foreach (var kv in result.LayerStats)
            {
                sb.AppendLine($"    {kv.Key}: {kv.Value} 个");
            }

            return ToolResult.Ok(sb.ToString(), new
            {
                outputPath = result.OutputPath,
                elementCount = result.ElementCount,
                layerStats = result.LayerStats,
                format
            });
        }

        /// <summary>
        /// 列出范围内可导出的元素
        /// </summary>
        private async Task<ToolResult> HandleListExportable(JObject json)
        {
            string scope = json["scope"]?.ToString();
            string elementType = json["element_type"]?.ToString() ?? "ALL";

            if (string.IsNullOrWhiteSpace(scope))
                return ToolResult.Fail("scope 参数为空（list_exportable 需要 scope 参数）");

            // 通过 dispatcher 调用 query 工具查询元素（返回 JSON 字符串）
            var queryArgs = JsonConvert.SerializeObject(new
            {
                type = elementType,
                scope = scope,
                limit = 500
            });

            string queryResultJson;
            try
            {
                queryResultJson = await _dispatcher.ExecuteAsync("query", queryArgs);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"查询元素失败: {ex.Message}");
            }

            // 解析查询结果（JSON 字符串）
            var elements = ExtractElementListFromQueryJson(queryResultJson);
            if (elements.Count == 0)
                return ToolResult.Fail($"在 {scope} 下未找到 {elementType} 类型的元素");

            // 按类型统计
            var typeStats = elements
                .GroupBy(e => e.Type ?? "UNKNOWN")
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"📋 可导出元素列表");
            sb.AppendLine($"  范围: {scope}");
            sb.AppendLine($"  类型过滤: {elementType}");
            sb.AppendLine($"  总数: {elements.Count}");
            sb.AppendLine($"  类型分布:");
            foreach (var kv in typeStats)
            {
                sb.AppendLine($"    {kv.Key}: {kv.Value} 个");
            }

            return ToolResult.Ok(sb.ToString(), new
            {
                scope,
                elementType,
                totalCount = elements.Count,
                typeStats
            });
        }

        #region 元素数据解析

        /// <summary>
        /// 从参数解析 E3D 元素列表（支持 elements 数组或 scope 查询）
        /// </summary>
        private async Task<List<E3DElementInfo>> ResolveElements(JObject json)
        {
            // 模式 A：直接传 elements 数组
            var elementsArray = json["elements"]?.ToObject<string[]>();
            if (elementsArray != null && elementsArray.Length > 0)
            {
                return await FetchElementDetails(elementsArray.ToList());
            }

            // 模式 B：按 scope 查询
            string scope = json["scope"]?.ToString();
            if (!string.IsNullOrWhiteSpace(scope))
            {
                string elementType = json["element_type"]?.ToString() ?? "ALL";
                return await QueryAndFetchElements(scope, elementType);
            }

            return null;
        }

        /// <summary>
        /// 按 scope 查询元素 DBURI 列表，再获取每个元素的详细属性
        /// </summary>
        private async Task<List<E3DElementInfo>> QueryAndFetchElements(string scope, string elementType)
        {
            var queryArgs = JsonConvert.SerializeObject(new
            {
                type = elementType,
                scope = scope,
                limit = 500
            });

            string queryResultJson;
            try
            {
                queryResultJson = await _dispatcher.ExecuteAsync("query", queryArgs);
            }
            catch
            {
                return new List<E3DElementInfo>();
            }

            var dburis = ExtractDburisFromQueryJson(queryResultJson);
            return await FetchElementDetails(dburis);
        }

        /// <summary>
        /// 获取每个元素的详细属性（位置、类型、属性值）
        /// </summary>
        private async Task<List<E3DElementInfo>> FetchElementDetails(List<string> dburis)
        {
            var elements = new List<E3DElementInfo>();

            foreach (var dburi in dburis.Take(500)) // 上限 500 个
            {
                try
                {
                    var attrArgs = JsonConvert.SerializeObject(new
                    {
                        element = dburi,
                        all = true
                    });

                    string attrResultJson = await _dispatcher.ExecuteAsync("get_attributes", attrArgs);
                    var elem = ParseElementFromAttributes(dburi, attrResultJson);
                    if (elem != null)
                        elements.Add(elem);
                }
                catch
                {
                    // 跳过无法读取的元素
                }
            }

            return elements;
        }

        /// <summary>
        /// 从 get_attributes 返回的 JSON 字符串解析元素信息
        /// </summary>
        private E3DElementInfo ParseElementFromAttributes(string dburi, string attrResultJson)
        {
            var elem = new E3DElementInfo
            {
                Dburi = dburi,
                Name = dburi.TrimStart('/')
            };

            if (string.IsNullOrWhiteSpace(attrResultJson))
                return elem;

            JObject attrJson;
            try
            {
                attrJson = JObject.Parse(attrResultJson);
            }
            catch
            {
                return elem;
            }

            // 遍历 JSON 属性，提取位置/类型/属性值
            foreach (var prop in attrJson.Properties())
            {
                string key = prop.Name;
                string val = prop.Value?.ToString() ?? "";
                elem.Attributes[key] = val;

                // 提取类型
                if (key.Equals("TYPE", StringComparison.OrdinalIgnoreCase))
                    elem.Type = val;

                // 提取位置（POSITION 属性格式：(X, Y, Z)）
                if (key.Equals("POSITION", StringComparison.OrdinalIgnoreCase) || key.Equals("POS", StringComparison.OrdinalIgnoreCase))
                {
                    elem.Position = Point3D.Parse(val);
                }

                // 提取终点位置（END_POSITION 属性）
                if (key.Equals("END_POSITION", StringComparison.OrdinalIgnoreCase) || key.Equals("ENDPOS", StringComparison.OrdinalIgnoreCase))
                {
                    elem.EndPosition = Point3D.Parse(val);
                }
            }

            // 如果 Type 为空，尝试从 DBURI 推断
            if (string.IsNullOrEmpty(elem.Type))
            {
                elem.Type = InferTypeFromDburi(dburi);
            }

            return elem;
        }

        /// <summary>
        /// 从 query 返回的 JSON 字符串提取 DBURI 列表
        /// 预期格式：{ "elements": [ {"name":"PIPE-001", "dburi":"/PIPE-001"}, ... ] }
        /// </summary>
        private List<string> ExtractDburisFromQueryJson(string queryResultJson)
        {
            var dburis = new List<string>();
            if (string.IsNullOrWhiteSpace(queryResultJson))
                return dburis;

            JObject json;
            try
            {
                json = JObject.Parse(queryResultJson);
            }
            catch
            {
                return dburis;
            }

            var elements = json["elements"] as JArray;
            if (elements == null)
                return dburis;

            foreach (var elem in elements)
            {
                try
                {
                    string dburi = elem["dburi"]?.ToString();
                    if (string.IsNullOrEmpty(dburi))
                    {
                        string name = elem["name"]?.ToString();
                        if (!string.IsNullOrEmpty(name))
                            dburi = "/" + name;
                    }
                    if (!string.IsNullOrEmpty(dburi))
                        dburis.Add(dburi);
                }
                catch { /* 跳过 */ }
            }

            return dburis;
        }

        /// <summary>
        /// 从 query 结果提取元素列表（用于 list_exportable 统计）
        /// </summary>
        private List<E3DElementInfo> ExtractElementListFromQueryJson(string queryResultJson)
        {
            var elements = new List<E3DElementInfo>();
            var dburis = ExtractDburisFromQueryJson(queryResultJson);

            foreach (var dburi in dburis)
            {
                elements.Add(new E3DElementInfo
                {
                    Dburi = dburi,
                    Name = dburi.TrimStart('/'),
                    Type = InferTypeFromDburi(dburi)
                });
            }

            return elements;
        }

        /// <summary>
        /// 从 DBURI 推断元素类型（/PIPE-001 → PIPE）
        /// </summary>
        private string InferTypeFromDburi(string dburi)
        {
            if (string.IsNullOrEmpty(dburi)) return "UNKNOWN";
            string name = dburi.TrimStart('/');
            int dash = name.IndexOf('-');
            if (dash > 0)
                return name.Substring(0, dash).ToUpperInvariant();
            return "UNKNOWN";
        }

        #endregion

        #region 选项解析

        private ExportOptions ParseOptions(JToken token)
        {
            var options = new ExportOptions();

            if (token == null || token.Type != JTokenType.Object)
                return options;

            var obj = (JObject)token;

            string prefix = obj["layer_prefix"]?.ToString();
            if (!string.IsNullOrEmpty(prefix))
                options.LayerPrefix = prefix;

            if (obj["include_text"]?.Type == JTokenType.Boolean)
                options.IncludeText = obj["include_text"].Value<bool>();

            if (obj["include_dimensions"]?.Type == JTokenType.Boolean)
                options.IncludeDimensions = obj["include_dimensions"].Value<bool>();

            if (obj["scale"]?.Type == JTokenType.Float || obj["scale"]?.Type == JTokenType.Integer)
                options.Scale = obj["scale"].Value<double>();

            string projection = obj["projection"]?.ToString();
            if (!string.IsNullOrEmpty(projection))
                options.Projection = projection;

            return options;
        }

        #endregion
    }
}
