using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Logging;
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
    ""layers"": {
      ""type"": ""array"",
      ""items"": { ""type"": ""string"" },
      ""description"": ""参与导入的图层名列表。强烈建议指定（如 [\""土建\"",\""C1 柱墙剖线\""]），否则轴线/标注/文字图层也会被当成构件。不填=全部图层""
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
    },
    ""specifications"": {
      ""type"": ""object"",
      ""description"": ""各元素类型→规格名的覆盖映射，键为 Wall/Column/Beam，值为规格名（如 /MyWall-SPEC）。默认使用 /Concrete_*-SPEC""
    },
    ""database"": {
      ""type"": ""string"",
      ""description"": ""目标 DESIGN 数据库名（可选）。提供时脚本先 NEW DB 进入该库，避免当前非设计库根导致 NEW SITE 失败""
    }
  },
  ""required"": [""action""]
}";

        private readonly ICadParser _parser;
        private readonly PmlScriptGenerator _pmlGenerator;
        private readonly IToolDispatcher _dispatcher;

        public CadImportHandler(IToolDispatcher dispatcher = null, ICadParser parser = null)
        {
            _parser = parser ?? CreateDefaultParser();
            _pmlGenerator = new PmlScriptGenerator();
            _dispatcher = dispatcher;
        }

        /// <summary>
        /// 选择解析引擎：配置了 sidecar 路径则用它（读取全量真实几何），
        /// 否则回退到进程内 Teigha（按图层名关键词筛选）。
        /// </summary>
        private static ICadParser CreateDefaultParser()
        {
            var sidecar = new SidecarCadParserService();
            if (!string.IsNullOrEmpty(sidecar.SidecarPath) && System.IO.File.Exists(sidecar.SidecarPath))
                return sidecar;

            // 静默回退是陷阱：旧解析器只认 6 个图层名关键词，几何覆盖不全。
            // 部署后如果 sidecar 没带上，结果会「看着正常、实际全错」，所以必须留痕。
            CopilotLogger.Warn(
                "未找到 dwg-sidecar，已回退到进程内 Teigha 解析器（按图层名关键词筛选，几何覆盖不全）。" +
                "部署形态请把 dwg-sidecar.exe 放到 <E3D目录>/tools/ 下，或设置环境变量 E3D_DWG_SIDECAR。");
            return new TeighaCadParserService();
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
        /// 导入（生成 PML 脚本，支持自动执行）
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
            string owner = json["owner"]?.ToString();
            double wallHeight = json["wall_height"]?.Value<double>() ?? 3000;
            double wallThickness = json["wall_thickness"]?.Value<double>() ?? 200;
            bool autoExecute = json["auto_execute"]?.Value<bool>() ?? true;
            var specifications = ParseSpecifications(json);
            string database = json["database"]?.ToString();

            var elements = ConvertSegmentsToElements(mergedSegments, wallHeight, wallThickness);
            string pmlScript = _pmlGenerator.GenerateBuildingScript(elements, specifications: specifications, databaseName: database, owner: owner);

            // auto_execute: 直接通过 IToolDispatcher 执行 PML
            if (autoExecute && _dispatcher != null)
            {
                try
                {
                    var execArgs = JsonConvert.SerializeObject(new { script = pmlScript });
                    var execResult = _dispatcher.ExecuteAsync("execute_pml", execArgs).GetAwaiter().GetResult();

                    // execute_pml 失败时返回以 "Error:" 开头的字符串（约定见
                    // RealE3DEnvironment.ExecutePmlDirect），不抛异常。之前这里不看返回值，
                    // 一律报 executed=true，导致 PML 整段脚本第一行就崩了，
                    // 结果却显示「已创建 317 个元素」——属于静默假成功，最误导人。
                    bool execFailed = execResult != null &&
                                      execResult.TrimStart().StartsWith("Error:", StringComparison.OrdinalIgnoreCase);

                    if (execFailed)
                    {
                        var sbFail = new StringBuilder();
                        sbFail.AppendLine("❌ CAD→E3D 导入执行失败，E3D 中没有创建任何元素");
                        sbFail.AppendLine($"  线段数: {mergedSegments.Count}");
                        sbFail.AppendLine($"  脚本中本应创建: {elements.Count} 个元素");
                        sbFail.AppendLine($"  落位: {(string.IsNullOrEmpty(owner) ? "新建 SITE/ZONE" : owner)}");
                        sbFail.AppendLine();
                        sbFail.AppendLine($"PML 错误: {execResult}");
                        sbFail.AppendLine();
                        sbFail.AppendLine("完整 PML 脚本已生成，可修正后用 execute_pml 重新执行。");

                        // 用 Fail 让上层（EvidenceLedger / 前端卡片）明确知道这一步失败了，
                        // 而不是像以前那样按 Ok + executed=true 报成功。
                        return ToolResult.Fail(sbFail.ToString());
                    }

                    // 创建批次入撤销栈：undo_create 可一键批量删除本轮建的所有元素
                    if (_pmlGenerator.LastCreatedElementNames.Count > 0)
                        UndoRedoManager.Instance.RecordCreation("cad_import",
                            $"CAD 导入（{elements.Count} 元素，落位 {owner ?? "新建 SITE/ZONE"}）",
                            _pmlGenerator.LastCreatedElementNames);

                    var sbExec = new StringBuilder();
                    sbExec.AppendLine($"✅ CAD→E3D 导入已执行完成");
                    sbExec.AppendLine($"  线段数: {mergedSegments.Count}");
                    sbExec.AppendLine($"  已创建元素: {elements.Count} 个");
                    sbExec.AppendLine($"  墙高: {wallHeight}mm, 墙厚: {wallThickness}mm");
                    sbExec.AppendLine($"  落位: {(string.IsNullOrEmpty(owner) ? "新建 SITE/ZONE" : owner)}");
                    sbExec.AppendLine();
                    sbExec.AppendLine($"PML 执行结果: {execResult}");

                    return ToolResult.Ok(sbExec.ToString(), new
                    {
                        segmentCount = mergedSegments.Count,
                        elementCount = elements.Count,
                        wallHeight,
                        wallThickness,
                        owner,
                        executed = true,
                        pmlScript
                    });
                }
                catch (Exception ex)
                {
                    return ToolResult.Fail($"PML 执行失败: {ex.Message}\n\n完整 PML 脚本已生成（{elements.Count} 个元素），可用 execute_pml 手动执行。");
                }
            }

            // 不自动执行：返回完整脚本（不截断）
            var sb = new StringBuilder();
            sb.AppendLine($"✅ 导入准备完成");
            sb.AppendLine($"  - 线段数: {mergedSegments.Count}");
            sb.AppendLine($"  - 元素数: {elements.Count}");
            sb.AppendLine($"  - 墙高: {wallHeight}mm, 墙厚: {wallThickness}mm");
            sb.AppendLine($"  - 落位: {(string.IsNullOrEmpty(owner) ? "新建 SITE/ZONE" : owner)}");
            sb.AppendLine();
            sb.AppendLine("生成的 PML 脚本:");
            sb.AppendLine("```pml");
            sb.AppendLine(pmlScript);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("💡 使用 execute_pml 工具执行生成的 PML 脚本即可在 E3D 中创建元素");

            return ToolResult.Ok(sb.ToString(), new
            {
                segmentCount = mergedSegments.Count,
                elementCount = elements.Count,
                wallHeight,
                wallThickness,
                owner,
                executed = false,
                pmlScript
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
                // sidecar 支持按图层过滤；Teigha 走自己的关键词表，忽略此参数
                var sidecarParser = _parser as SidecarCadParserService;
                if (sidecarParser != null)
                {
                    var layersToken = json["layers"] as JArray;
                    if (layersToken != null && layersToken.Count > 0)
                    {
                        sidecarParser.Layers = layersToken
                            .Select(t => t?.ToString())
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();
                    }
                }

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
                // 过短的线段是图框线/标注引线，建出来会是碎片墙
                if (segment.Length < MinSegmentLength) continue;

                var element = new BuildingElement
                {
                    Type = ClassifyByLayer(segment.Layer),
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

        /// <summary>线段短于此值(mm)不建模——多为图框、标注引线、填充碎片。</summary>
        private const double MinSegmentLength = 100;

        /// <summary>
        /// 按图层名判定构件类型。
        ///
        /// 之前一律建成 Wall，导致柱、梁被当成墙，演示时无法向评委解释模型。
        /// 这里只做关键词判定，判不出来就按墙处理（结构图里线段绝大多数是墙）。
        /// </summary>
        private static BuildingElementType ClassifyByLayer(string layer)
        {
            if (string.IsNullOrEmpty(layer)) return BuildingElementType.Wall;

            if (ContainsAny(layer, "COLUMN", "COL", "柱"))
                return BuildingElementType.Column;
            if (ContainsAny(layer, "BEAM", "梁"))
                return BuildingElementType.Beam;
            if (ContainsAny(layer, "WALL", "墙"))
                return BuildingElementType.Wall;

            return BuildingElementType.Wall;
        }

        private static bool ContainsAny(string text, params string[] keywords)
        {
            foreach (var k in keywords)
            {
                if (text.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 从参数 JSON 解析规格名覆盖映射（元素类型名 → 规格名）
        /// </summary>
        private Dictionary<BuildingElementType, string> ParseSpecifications(JObject json)
        {
            var dict = new Dictionary<BuildingElementType, string>();
            if (json["specifications"] is JObject specObj)
            {
                foreach (var prop in specObj.Properties())
                {
                    if (Enum.TryParse<BuildingElementType>(prop.Name, true, out var t) && !string.IsNullOrEmpty(prop.Value?.ToString()))
                        dict[t] = prop.Value.ToString();
                }
            }
            return dict;
        }
    }
}
