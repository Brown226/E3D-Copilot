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
    /// AutoCAD 运行时交互工具 — 连接运行中的 AutoCAD 实例，获取选中对象并导入 E3D
    /// 
    /// 职责：AutoCAD ↔ E3D 的实时数据桥接
    /// 不负责文件解析（那是 cad_import 工具的事）
    /// 不管理 AutoCAD 进程启停（用户自行管理）
    /// </summary>
    public class AutoCadHandler : IToolHandler
    {
        public string Name => "autocad";
        public bool IsReadOnly => false;

        public string Description => @"连接运行中的 AutoCAD，获取图纸中的图形对象并导入 E3D。
前置条件：AutoCAD 已启动并打开了目标图纸。

操作流程：
- status: 检查 AutoCAD 是否运行、是否已连接
- connect: 连接到 AutoCAD 实例
- list_objects: 列出当前图纸中的对象（可按图层过滤）
- get_selection: 获取用户在 AutoCAD 中框选的对象（预览）
- import_selection: 获取选中对象并生成 PML 导入 E3D

典型用法：
1. 先用 status 确认 AutoCAD 在运行
2. 用 connect 建立连接
3. 让用户在 AutoCAD 中选中图形
4. 用 get_selection 预览，或 import_selection 直接导入";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""action"": {
      ""type"": ""string"",
      ""enum"": [""status"", ""connect"", ""list_objects"", ""get_selection"", ""import_selection""],
      ""description"": ""操作类型""
    },
    ""layer_filter"": {
      ""type"": ""array"",
      ""items"": {""type"": ""string""},
      ""description"": ""图层过滤器（可选）""
    },
    ""wall_height"": {
      ""type"": ""number"",
      ""description"": ""默认墙高(mm)，默认 3000""
    },
    ""wall_thickness"": {
      ""type"": ""number"",
      ""description"": ""默认墙厚(mm)，默认 200""
    }
  },
  ""required"": [""action""]
}";

        private readonly AutoCadComService _autoCadService;
        private readonly PmlScriptGenerator _pmlGenerator;

        public AutoCadHandler()
        {
            _autoCadService = new AutoCadComService();
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
                    case "status":
                        return HandleStatus();
                    case "connect":
                        return HandleConnect();
                    case "list_objects":
                        return HandleListObjects(json);
                    case "get_selection":
                        return HandleGetSelection(json);
                    case "import_selection":
                        return HandleImportSelection(json);
                    default:
                        return ToolResult.Fail($"未知操作: {action}，支持: status, connect, list_objects, get_selection, import_selection");
                }
            }
            catch (JsonException ex)
            {
                return ToolResult.Fail($"参数 JSON 解析错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"AutoCAD 操作失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查 AutoCAD 状态
        /// </summary>
        private ToolResult HandleStatus()
        {
            bool isRunning = AutoCadComService.IsAutoCadRunning();
            bool isConnected = _autoCadService.Status == AutoCadConnectionStatus.Connected;

            var sb = new StringBuilder();

            if (!isRunning)
            {
                sb.AppendLine("❌ AutoCAD 未运行");
                sb.AppendLine("请先启动 AutoCAD 并打开目标图纸，然后使用 connect 操作连接");
                return ToolResult.Ok(sb.ToString(), new
                {
                    isRunning = false,
                    isConnected = false,
                    status = "not_running"
                });
            }

            if (isConnected)
            {
                sb.AppendLine("✅ AutoCAD 已连接");
                sb.AppendLine($"  当前图纸: {_autoCadService.ActiveDocumentName}");
                sb.AppendLine("可以使用 get_selection 或 import_selection 操作");
            }
            else
            {
                sb.AppendLine("⚠️ AutoCAD 正在运行，但尚未连接");
                sb.AppendLine("请使用 connect 操作建立连接");
            }

            return ToolResult.Ok(sb.ToString(), new
            {
                isRunning = true,
                isConnected,
                drawingName = isConnected ? _autoCadService.ActiveDocumentName : null,
                status = isConnected ? "connected" : "disconnected"
            });
        }

        /// <summary>
        /// 连接到 AutoCAD
        /// </summary>
        private ToolResult HandleConnect()
        {
            // 检查是否已连接
            if (_autoCadService.Status == AutoCadConnectionStatus.Connected)
            {
                return ToolResult.Ok(
                    $"✅ 已经连接到 AutoCAD\n当前图纸: {_autoCadService.ActiveDocumentName}",
                    new { alreadyConnected = true, drawingName = _autoCadService.ActiveDocumentName }
                );
            }

            // 检查 AutoCAD 是否运行
            if (!AutoCadComService.IsAutoCadRunning())
            {
                return ToolResult.Fail(
                    "AutoCAD 未运行，请先启动 AutoCAD 并打开目标图纸。\n" +
                    "提示：手动打开 AutoCAD 后再试 connect"
                );
            }

            bool connected = _autoCadService.Connect();
            if (connected)
            {
                var sb = new StringBuilder();
                sb.AppendLine("✅ 已成功连接到 AutoCAD");
                sb.AppendLine($"  当前图纸: {_autoCadService.ActiveDocumentName}");
                sb.AppendLine();
                sb.AppendLine("现在可以：");
                sb.AppendLine("  - 在 AutoCAD 中选中图形，然后用 import_selection 导入");
                sb.AppendLine("  - 用 list_objects 查看图纸中的所有对象");

                return ToolResult.Ok(sb.ToString(), new
                {
                    connected = true,
                    drawingName = _autoCadService.ActiveDocumentName
                });
            }
            else
            {
                return ToolResult.Fail("连接 AutoCAD 失败，请确保 AutoCAD 正在运行并打开了图纸");
            }
        }

        /// <summary>
        /// 列出图纸中的对象
        /// </summary>
        private ToolResult HandleListObjects(JObject json)
        {
            EnsureConnected();

            var layerFilter = json["layer_filter"]?.ToObject<List<string>>();
            var result = _autoCadService.GetAllModelSpaceObjects(layerFilter);

            if (!result.Success)
                return ToolResult.Fail(result.Error);

            var sb = new StringBuilder();
            sb.AppendLine($"📋 图纸对象列表");
            sb.AppendLine($"  图纸: {result.DrawingName}");
            sb.AppendLine($"  总对象数: {result.TotalEntities}");
            sb.AppendLine($"  有效线段: {result.Segments.Count} 条");
            sb.AppendLine();

            // 按图层分组统计
            var layerGroups = result.Segments.GroupBy(s => s.Layer ?? "默认").OrderByDescending(g => g.Count());
            sb.AppendLine("按图层统计:");
            foreach (var group in layerGroups)
            {
                sb.AppendLine($"  {group.Key}: {group.Count()} 条线段");
            }

            sb.AppendLine();
            sb.AppendLine("提示: 使用 get_selection 获取用户选中的对象，或 import_selection 直接导入");

            return ToolResult.Ok(sb.ToString(), new
            {
                drawingName = result.DrawingName,
                totalEntities = result.TotalEntities,
                segmentCount = result.Segments.Count,
                layers = layerGroups.Select(g => new { name = g.Key, count = g.Count() }).ToList()
            });
        }

        /// <summary>
        /// 获取用户选中的对象（预览）
        /// </summary>
        private ToolResult HandleGetSelection(JObject json)
        {
            EnsureConnected();

            var layerFilter = json["layer_filter"]?.ToObject<List<string>>();
            var result = _autoCadService.GetSelectedObjects();

            if (!result.Success)
                return ToolResult.Fail(result.Error);

            if (result.TotalEntities == 0)
                return ToolResult.Fail("未选择任何对象，请先在 AutoCAD 中选择要导入的图形");

            // 应用图层过滤
            var segments = result.Segments;
            if (layerFilter != null && layerFilter.Count > 0)
            {
                segments = segments.Where(s => layerFilter.Contains(s.Layer)).ToList();
            }

            var sb = new StringBuilder();
            sb.AppendLine($"📋 选中对象预览");
            sb.AppendLine($"  图纸: {result.DrawingName}");
            sb.AppendLine($"  原始对象: {result.TotalEntities} 个");
            sb.AppendLine($"  有效线段: {segments.Count} 条");
            sb.AppendLine();

            if (segments.Count > 0)
            {
                // 按图层分组
                var layerGroups = segments.GroupBy(s => s.Layer ?? "默认").OrderByDescending(g => g.Count());
                sb.AppendLine("按图层:");
                foreach (var group in layerGroups)
                {
                    sb.AppendLine($"  {group.Key}: {group.Count()} 条");
                }

                sb.AppendLine();
                sb.AppendLine("前 20 条线段:");
                for (int i = 0; i < Math.Min(20, segments.Count); i++)
                {
                    var seg = segments[i];
                    sb.AppendLine($"  [{i + 1}] ({seg.Start.X:F0},{seg.Start.Y:F0}) → ({seg.End.X:F0},{seg.End.Y:F0}) [{seg.Layer}]");
                }
                if (segments.Count > 20)
                    sb.AppendLine($"  ... 还有 {segments.Count - 20} 条");
            }

            sb.AppendLine();
            sb.AppendLine("提示: 确认无误后使用 import_selection 导入到 E3D");

            return ToolResult.Ok(sb.ToString(), new
            {
                drawingName = result.DrawingName,
                entityCount = result.TotalEntities,
                segmentCount = segments.Count,
                segments = segments.Take(50).Select(s => new
                {
                    start = new[] { s.Start.X, s.Start.Y, s.Start.Z },
                    end = new[] { s.End.X, s.End.Y, s.End.Z },
                    length = s.Length,
                    layer = s.Layer
                }).ToList()
            });
        }

        /// <summary>
        /// 获取选中对象并导入到 E3D（生成 PML）
        /// </summary>
        private ToolResult HandleImportSelection(JObject json)
        {
            EnsureConnected();

            var layerFilter = json["layer_filter"]?.ToObject<List<string>>();
            double wallHeight = json["wall_height"]?.Value<double>() ?? 3000;
            double wallThickness = json["wall_thickness"]?.Value<double>() ?? 200;

            // 获取选中对象
            var extractResult = _autoCadService.GetSelectedObjects();

            if (!extractResult.Success)
                return ToolResult.Fail(extractResult.Error);

            if (extractResult.Segments.Count == 0)
                return ToolResult.Fail("未找到有效的线段，请在 AutoCAD 中选择直线、多段线等图形");

            // 应用图层过滤
            var segments = extractResult.Segments;
            if (layerFilter != null && layerFilter.Count > 0)
            {
                segments = segments.Where(s => layerFilter.Contains(s.Layer)).ToList();
            }

            if (segments.Count == 0)
                return ToolResult.Fail("过滤后无有效线段，请检查 layer_filter 参数");

            // 合并共线线段
            segments = TeighaCadParserService.MergeCollinearSegments(segments);

            // 转换为建筑元素
            var elements = ConvertSegmentsToElements(segments, wallHeight, wallThickness);

            // 生成 PML 脚本
            string pmlScript = _pmlGenerator.GenerateBuildingScript(elements);

            var sb = new StringBuilder();
            sb.AppendLine($"✅ 从 AutoCAD 导入准备完成");
            sb.AppendLine($"  图纸: {extractResult.DrawingName}");
            sb.AppendLine($"  原始对象: {extractResult.TotalEntities} 个");
            sb.AppendLine($"  有效线段: {segments.Count} 条");
            sb.AppendLine($"  将创建元素: {elements.Count} 个");
            sb.AppendLine($"  墙高: {wallHeight}mm, 墙厚: {wallThickness}mm");
            sb.AppendLine();
            sb.AppendLine("生成的 PML 脚本:");
            sb.AppendLine("```pml");
            sb.AppendLine(pmlScript.Length > 1500 ? pmlScript.Substring(0, 1500) + "\n..." : pmlScript);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("💡 使用 execute_pml 工具执行生成的 PML 脚本即可在 E3D 中创建元素");

            return ToolResult.Ok(sb.ToString(), new
            {
                drawingName = extractResult.DrawingName,
                entityCount = extractResult.TotalEntities,
                segmentCount = segments.Count,
                elementCount = elements.Count,
                wallHeight,
                wallThickness,
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
        /// 确保已连接到 AutoCAD
        /// </summary>
        private void EnsureConnected()
        {
            if (_autoCadService.Status != AutoCadConnectionStatus.Connected)
                throw new InvalidOperationException("未连接到 AutoCAD，请先使用 connect 操作连接");
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
