using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Models.Building;
using E3DCopilot.Core.Models.Geometry;
using E3DCopilot.Core.Services.Cad;
using E3DCopilot.Core.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// CAD 图形导入工具 — 从 DWG 文件或坐标字符串导入建筑模型到 E3D
    /// 
    /// 职责：数据转换（文件/坐标 → 线段 → 建筑元素 → PML 脚本）
    /// 不负责 AutoCAD 运行时交互（那是 autocad 工具的事）
    /// </summary>
    public class CadImportHandler : IToolHandler
    {
        public string Name => "cad_import";

        bool IToolHandler.IsReadOnly => false;

        public string Description => @"从 CAD 图纸导入建筑模型到 E3D。
支持两种数据源：
- DWG/DXF 文件路径
- 坐标字符串（手动输入或从 AutoCAD 复制）

两个操作：
- parse: 解析并预览（不创建 E3D 元素）
- import: 解析并生成 PML 脚本，可选自动执行

注意：如需从运行中的 AutoCAD 实时获取选中对象，请使用 autocad 工具。";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""action"": {
      ""type"": ""string"",
      ""enum"": [""parse"", ""import""],
      ""description"": ""parse=预览解析结果, import=生成PML并可选执行""
    },
    ""file_path"": {
      ""type"": ""string"",
      ""description"": ""DWG/DXF 文件路径""
    },
    ""paths_string"": {
      ""type"": ""string"",
      ""description"": ""坐标字符串，格式: [(x1,y1,z1),(x2,y2,z2)],...""
    },
    ""owner"": {
      ""type"": ""string"",
      ""description"": ""父元素路径，默认 /IMPORT_ZONE""
    },
    ""wall_height"": {
      ""type"": ""number"",
      ""description"": ""默认墙高(mm)，默认 3000""
    },
    ""wall_thickness"": {
      ""type"": ""number"",
      ""description"": ""默认墙厚(mm)，默认 200""
    },
    ""auto_execute"": {
      ""type"": ""boolean"",
      ""description"": ""import 时是否自动执行 PML 脚本，默认 false""
    }
  },
  ""required"": [""action""]
}";

        private readonly TeighaCadParserService _parser;
        private readonly PmlScriptGenerator _pmlGenerator;

        public CadImportHandler()
        {
            _parser = new TeighaCadParserService();
            _pmlGenerator = new PmlScriptGenerator();
        }

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                string action = json["action"]?.ToString()?.ToLower() ?? "";

                switch (action)
                {
                    case "parse":
                        return HandleParse(json);
                    case "import":
                        return HandleImport(json);
                    default:
                        return ToolResult.Fail($"未知操作: {action}，支持: parse, import");
                }
            }
            catch (JsonException ex)
            {
                return ToolResult.Fail($"参数 JSON 解析错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"CAD 导入失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 解析预览（文件或坐标字符串）
        /// </summary>
        private ToolResult HandleParse(JObject json)
        {
            var (mergedSegments, parseError) = LoadSegments(json);
            if (mergedSegments == null && parseError == null)
                return ToolResult.Fail("需要提供 file_path 或 paths_string 参数");
            if (parseError != null) return parseError;

            // 墙高墙厚用于预览计算
            double wallHeight = json["wall_height"]?.Value<double>() ?? 3000;
            double wallThickness = json["wall_thickness"]?.Value<double>() ?? 200;
            var elements = ConvertSegmentsToElements(mergedSegments, wallHeight, wallThickness);

            var sb = new StringBuilder();
            sb.AppendLine($"📋 解析预览");
            sb.AppendLine(new string('=', 50));
            sb.AppendLine($"原始线段: {mergedSegments.Count} 条");
            sb.AppendLine($"将创建元素: {elements.Count} 个");
            sb.AppendLine($"墙高: {wallHeight}mm, 墙厚: {wallThickness}mm");
            sb.AppendLine();

            sb.AppendLine("元素详情:");
            for (int i = 0; i < elements.Count; i++)
            {
                var elem = elements[i];
                sb.Append($"  [{i + 1}] {elem.Type} - {elem.Points.Count} 个点");
                if (elem.Points.Count >= 2)
                {
                    double length = elem.Points[0].DistanceTo(elem.Points[1]);
                    sb.AppendLine($" ({length:F0}mm)");
                    sb.AppendLine($"       ({elem.Points[0].X:F0},{elem.Points[0].Y:F0}) → ({elem.Points[1].X:F0},{elem.Points[1].Y:F0})");
                }
                else
                {
                    sb.AppendLine();
                }
            }

            sb.AppendLine();
            sb.AppendLine("提示: 使用 action=import 生成 PML 脚本");

            return ToolResult.Ok(sb.ToString(), new
            {
                segmentCount = mergedSegments.Count,
                elementCount = elements.Count,
                wallHeight,
                wallThickness,
                elements = elements.Select(e => new
                {
                    type = e.Type.ToString(),
                    start = e.Points.Count > 0 ? new[] { e.Points[0].X, e.Points[0].Y, e.Points[0].Z } : null,
                    end = e.Points.Count > 1 ? new[] { e.Points[1].X, e.Points[1].Y, e.Points[1].Z } : null
                }).ToList()
            });
        }

        /// <summary>
        /// 导入（生成 PML 脚本，可选自动执行）
        /// </summary>
        private ToolResult HandleImport(JObject json)
        {
            var (mergedSegments, parseError) = LoadSegments(json);
            if (mergedSegments == null && parseError == null)
                return ToolResult.Fail("需要提供 file_path 或 paths_string 参数");
            if (parseError != null) return parseError;

            if (mergedSegments.Count == 0)
                return ToolResult.Fail("未找到有效的线段数据");

            // 获取参数
            string owner = json["owner"]?.ToString() ?? "/IMPORT_ZONE";
            double wallHeight = json["wall_height"]?.Value<double>() ?? 3000;
            double wallThickness = json["wall_thickness"]?.Value<double>() ?? 200;
            bool autoExecute = json["auto_execute"]?.Value<bool>() ?? false;

            var elements = ConvertSegmentsToElements(mergedSegments, wallHeight, wallThickness);
            string pmlScript = _pmlGenerator.GenerateBuildingScript(elements);

            var sb = new StringBuilder();
            sb.AppendLine($"✅ 导入准备完成");
            sb.AppendLine($"  - 线段数: {mergedSegments.Count}");
            sb.AppendLine($"  - 元素数: {elements.Count}");
            sb.AppendLine($"  - 墙高: {wallHeight}mm, 墙厚: {wallThickness}mm");
            sb.AppendLine();
            sb.AppendLine("生成的 PML 脚本:");
            sb.AppendLine("```pml");
            sb.AppendLine(pmlScript.Length > 1500 ? pmlScript.Substring(0, 1500) + "\n..." : pmlScript);
            sb.AppendLine("```");

            if (autoExecute)
            {
                sb.AppendLine();
                sb.AppendLine("⏳ auto_execute=true，PML 脚本已返回，请使用 execute_pml 工具执行");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("提示: 使用 execute_pml 工具执行生成的 PML 脚本，或设置 auto_execute=true 自动执行");
            }

            return ToolResult.Ok(sb.ToString(), new
            {
                segmentCount = mergedSegments.Count,
                elementCount = elements.Count,
                wallHeight,
                wallThickness,
                autoExecute,
                pmlScript,
                elements = elements.Select(e => new
                {
                    type = e.Type.ToString(),
                    pointCount = e.Points.Count,
                    properties = e.Properties
                }).ToList()
            });
        }

        /// <summary>
        /// 从参数加载线段（文件或坐标字符串）
        /// </summary>
        private (List<LineSegment>, ToolResult) LoadSegments(JObject json)
        {
            string filePath = json["file_path"]?.ToString();
            string pathsString = json["paths_string"]?.ToString();

            if (!string.IsNullOrEmpty(filePath))
            {
                var result = _parser.ParseFile(filePath);
                if (!result.Success)
                    return (null, ToolResult.Fail(result.Error));
                return (result.WallSegments, null);
            }

            if (!string.IsNullOrEmpty(pathsString))
            {
                var segments = TeighaCadParserService.ParsePathsString(pathsString);
                if (segments.Count == 0)
                    return (null, ToolResult.Fail("无法解析坐标字符串，请检查格式：[(x1,y1,z1),(x2,y2,z2)],..."));
                var merged = TeighaCadParserService.MergeCollinearSegments(segments);
                return (merged, null);
            }

            return (null, null);
        }

        /// <summary>
        /// 将线段转换为建筑元素
        /// </summary>
        private List<BuildingElement> ConvertSegmentsToElements(List<LineSegment> segments, double wallHeight, double wallThickness)
        {
            var elements = new List<BuildingElement>();

            foreach (var segment in segments)
            {
                if (segment.Length < 100) continue; // 忽略过短的线段

                var element = new BuildingElement
                {
                    Type = BuildingElementType.Wall,
                    Points = new List<Point3D> { segment.Start, segment.End }
                };

                element.Properties["Height"] = wallHeight;
                element.Properties["Thickness"] = wallThickness;
                element.Properties["Length"] = segment.Length;

                if (!string.IsNullOrEmpty(segment.Layer))
                    element.Properties["Layer"] = segment.Layer;

                double minX = Math.Min(segment.Start.X, segment.End.X) - wallThickness / 2;
                double minY = Math.Min(segment.Start.Y, segment.End.Y) - wallThickness / 2;
                double maxX = Math.Max(segment.Start.X, segment.End.X) + wallThickness / 2;
                double maxY = Math.Max(segment.Start.Y, segment.End.Y) + wallThickness / 2;

                element.BoundingBox = new BoundingBox(
                    new Point3D(minX, minY, 0),
                    new Point3D(maxX, maxY, wallHeight)
                );

                elements.Add(element);
            }

            return elements;
        }
    }
}
