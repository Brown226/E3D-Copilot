using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using E3DCopilot.Core.Models.Geometry;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

namespace E3DCopilot.Core.Services.Cad
{
    /// <summary>
    /// E3D 元素信息（导出用）
    /// </summary>
    public class E3DElementInfo
    {
        public string Dburi { get; set; }
        public string Type { get; set; }       // PIPE/EQUI/STRU/BRAN/SUPP
        public string Name { get; set; }
        public Point3D Position { get; set; }
        public Point3D EndPosition { get; set; }  // 管道/线段用
        public Dictionary<string, string> Attributes { get; set; }

        public E3DElementInfo()
        {
            Attributes = new Dictionary<string, string>();
            Position = new Point3D();
            EndPosition = new Point3D();
        }
    }

    /// <summary>
    /// 导出选项
    /// </summary>
    public class ExportOptions
    {
        public string LayerPrefix { get; set; } = "";
        public bool IncludeText { get; set; } = true;
        public bool IncludeDimensions { get; set; } = true;
        public double Scale { get; set; } = 1.0;
        /// <summary>投影方式：plan(平面) / elevation(立面) / iso(等轴测)</summary>
        public string Projection { get; set; } = "plan";
    }

    /// <summary>
    /// 导出结果
    /// </summary>
    public class CadExportResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public int ElementCount { get; set; }
        public Dictionary<string, int> LayerStats { get; set; }
        public string OutputPath { get; set; }

        public CadExportResult()
        {
            LayerStats = new Dictionary<string, int>();
        }

        public static CadExportResult Ok(string path, int count, Dictionary<string, int> stats)
        {
            return new CadExportResult { Success = true, OutputPath = path, ElementCount = count, LayerStats = stats };
        }

        public static CadExportResult Fail(string error)
        {
            return new CadExportResult { Success = false, Error = error };
        }
    }

    /// <summary>
    /// CAD 导出服务 — 把 E3D 元素导出为 DXF 文件
    /// 使用 netDxf 库（项目已引用 NuGet 包 netDxf 2023.11.10）
    /// </summary>
    public class CadExportService
    {
        private const double TextHeight = 3.0;        // 标注文字高度
        private const double TextOffset = 200.0;       // 标注偏移量

        /// <summary>
        /// 导出元素到 DXF 文件
        /// </summary>
        /// <param name="elements">E3D 元素列表</param>
        /// <param name="outputPath">输出文件路径（.dxf）</param>
        /// <param name="options">导出选项</param>
        /// <returns>导出结果</returns>
        public CadExportResult ExportToDxf(List<E3DElementInfo> elements, string outputPath, ExportOptions options = null)
        {
            if (elements == null || elements.Count == 0)
                return CadExportResult.Fail("元素列表为空");

            if (string.IsNullOrWhiteSpace(outputPath))
                return CadExportResult.Fail("输出路径为空");

            if (!outputPath.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
                return CadExportResult.Fail("输出路径必须以 .dxf 结尾（当前只支持 DXF 格式）");

            if (options == null)
                options = new ExportOptions();

            try
            {
                var doc = new DxfDocument();
                var layerStats = new Dictionary<string, int>();

                foreach (var elem in elements)
                {
                    var (layerName, entities) = ConvertElementToDxfEntities(elem, options);
                    if (entities == null || entities.Count == 0) continue;

                    // 统计图层
                    if (!layerStats.ContainsKey(layerName))
                        layerStats[layerName] = 0;
                    layerStats[layerName]++;

                    // 确保图层存在并添加实体
                    var layer = GetOrCreateLayer(doc, layerName, elem.Type);
                    foreach (var entity in entities)
                    {
                        entity.Layer = layer;
                        doc.Entities.Add(entity);
                    }
                }

                // 确保输出目录存在
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                doc.Save(outputPath);

                return CadExportResult.Ok(outputPath, elements.Count, layerStats);
            }
            catch (Exception ex)
            {
                return CadExportResult.Fail($"导出 DXF 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 导出元素到 DWG 文件
        /// 注意：netDxf 不支持写 DWG，需要 Teigha.NET 或 AutoCAD COM
        /// 当前返回"不支持"，建议用 DXF
        /// </summary>
        public CadExportResult ExportToDwg(List<E3DElementInfo> elements, string outputPath, ExportOptions options = null)
        {
            return CadExportResult.Fail("当前版本不支持直接导出 DWG（netDxf 库限制），请用 .dxf 格式导出，然后在 AutoCAD 中另存为 DWG");
        }

        /// <summary>
        /// 预览导出映射（不生成文件）
        /// </summary>
        public CadExportResult Preview(List<E3DElementInfo> elements, ExportOptions options = null)
        {
            if (elements == null || elements.Count == 0)
                return CadExportResult.Fail("元素列表为空");

            if (options == null)
                options = new ExportOptions();

            var layerStats = new Dictionary<string, int>();
            foreach (var elem in elements)
            {
                var (layerName, _) = ConvertElementToDxfEntities(elem, options);
                if (!layerStats.ContainsKey(layerName))
                    layerStats[layerName] = 0;
                layerStats[layerName]++;
            }

            return new CadExportResult
            {
                Success = true,
                ElementCount = elements.Count,
                LayerStats = layerStats
            };
        }

        /// <summary>
        /// 将 E3D 元素转换为 DXF 实体列表
        /// </summary>
        private (string layerName, List<EntityObject> entities) ConvertElementToDxfEntities(
            E3DElementInfo elem, ExportOptions options)
        {
            var entities = new List<EntityObject>();
            string prefix = options.LayerPrefix ?? "";
            string typeUpper = (elem.Type ?? "").ToUpperInvariant();
            string layerName;

            switch (typeUpper)
            {
                case "PIPE":
                    string dia = GetAttr(elem, "DIA", "0");
                    layerName = $"{prefix}PIPE-DN{dia}";
                    entities.AddRange(ConvertPipeToEntities(elem, options));
                    break;

                case "EQUI":
                    string etype = GetAttr(elem, "TYPE", "GENERIC");
                    layerName = $"{prefix}EQUI-{etype}";
                    entities.AddRange(ConvertEquipmentToEntities(elem, options));
                    break;

                case "STRU":
                    string stype = GetAttr(elem, "TYPE", "BEAM");
                    layerName = $"{prefix}STRU-{stype}";
                    entities.AddRange(ConvertStructureToEntities(elem, options));
                    break;

                case "BRAN":
                    string bdia = GetAttr(elem, "DIA", "0");
                    layerName = $"{prefix}BRAN-DN{bdia}";
                    entities.AddRange(ConvertBranchToEntities(elem, options));
                    break;

                case "SUPP":
                    layerName = $"{prefix}SUPP";
                    entities.AddRange(ConvertSupportToEntities(elem, options));
                    break;

                default:
                    layerName = $"{prefix}OTHER";
                    entities.AddRange(ConvertGenericToEntities(elem, options));
                    break;
            }

            return (layerName, entities);
        }

        #region 按元素类型转换

        private List<EntityObject> ConvertPipeToEntities(E3DElementInfo elem, ExportOptions options)
        {
            var result = new List<EntityObject>();
            var (startN, endN) = ProjectTo2D(elem.Position, elem.EndPosition, options.Projection, options.Scale);

            if (startN.HasValue && endN.HasValue)
            {
                var start2d = startN.Value;
                var end2d = endN.Value;
                result.Add(new Line(start2d, end2d));

                if (options.IncludeText)
                {
                    var midPt = new Vector2((start2d.X + end2d.X) / 2, (start2d.Y + end2d.Y) / 2 + TextOffset);
                    string dia = GetAttr(elem, "DIA", "?");
                    string label = $"DN{dia} / {elem.Name}";
                    result.Add(new Text(label, midPt, TextHeight));
                }
            }
            return result;
        }

        private List<EntityObject> ConvertEquipmentToEntities(E3DElementInfo elem, ExportOptions options)
        {
            var result = new List<EntityObject>();
            var (centerN, _) = ProjectTo2D(elem.Position, null, options.Projection, options.Scale);

            if (centerN.HasValue)
            {
                var center = centerN.Value;
                // 设备用 500x500 的矩形表示
                double size = 500 * options.Scale;
                var p1 = new Vector2(center.X - size / 2, center.Y - size / 2);
                var p2 = new Vector2(center.X + size / 2, center.Y - size / 2);
                var p3 = new Vector2(center.X + size / 2, center.Y + size / 2);
                var p4 = new Vector2(center.X - size / 2, center.Y + size / 2);
                // 用 4 条 Line 画矩形（避免 LwPolyline 在不同 netDxf 版本的命名差异）
                result.Add(new Line(p1, p2));
                result.Add(new Line(p2, p3));
                result.Add(new Line(p3, p4));
                result.Add(new Line(p4, p1));

                if (options.IncludeText)
                {
                    string pno = GetAttr(elem, "PNO", "");
                    string desc = GetAttr(elem, "DESC", "");
                    string label = string.IsNullOrEmpty(pno) ? elem.Name : $"{pno} / {desc}";
                    result.Add(new Text(label, new Vector2(center.X, center.Y + size / 2 + TextOffset), TextHeight));
                }
            }
            return result;
        }

        private List<EntityObject> ConvertStructureToEntities(E3DElementInfo elem, ExportOptions options)
        {
            var result = new List<EntityObject>();
            var (startN, endN) = ProjectTo2D(elem.Position, elem.EndPosition, options.Projection, options.Scale);

            if (startN.HasValue && endN.HasValue)
            {
                var start2d = startN.Value;
                var end2d = endN.Value;
                result.Add(new Line(start2d, end2d));

                if (options.IncludeText)
                {
                    result.Add(new Text(elem.Name, new Vector2(start2d.X, start2d.Y + TextOffset), TextHeight));
                }
            }
            return result;
        }

        private List<EntityObject> ConvertBranchToEntities(E3DElementInfo elem, ExportOptions options)
        {
            var result = new List<EntityObject>();
            var (startN, endN) = ProjectTo2D(elem.Position, elem.EndPosition, options.Projection, options.Scale);

            if (startN.HasValue && endN.HasValue)
            {
                var start2d = startN.Value;
                var end2d = endN.Value;
                result.Add(new Line(start2d, end2d));

                if (options.IncludeText)
                {
                    var midPt = new Vector2((start2d.X + end2d.X) / 2, (start2d.Y + end2d.Y) / 2 + TextOffset);
                    string dia = GetAttr(elem, "DIA", "?");
                    string spre = GetAttr(elem, "SPRE", "");
                    result.Add(new Text($"DN{dia} / {spre}", midPt, TextHeight));
                }
            }
            return result;
        }

        private List<EntityObject> ConvertSupportToEntities(E3DElementInfo elem, ExportOptions options)
        {
            var result = new List<EntityObject>();
            var (centerN, _) = ProjectTo2D(elem.Position, null, options.Projection, options.Scale);

            if (centerN.HasValue)
            {
                var center = centerN.Value;
                double radius = 100 * options.Scale;
                result.Add(new Circle(center, radius));

                if (options.IncludeText)
                {
                    string stype = GetAttr(elem, "TYPE", "SUPP");
                    result.Add(new Text(stype, new Vector2(center.X, center.Y + radius + TextOffset), TextHeight));
                }
            }
            return result;
        }

        private List<EntityObject> ConvertGenericToEntities(E3DElementInfo elem, ExportOptions options)
        {
            var result = new List<EntityObject>();
            var (centerN, _) = ProjectTo2D(elem.Position, null, options.Projection, options.Scale);

            if (centerN.HasValue && options.IncludeText)
            {
                result.Add(new Text(elem.Name ?? "UNKNOWN", centerN.Value, TextHeight));
            }
            return result;
        }

        #endregion

        #region 坐标投影

        /// <summary>
        /// 3D 坐标投影到 2D
        /// </summary>
        private (Vector2? start, Vector2? end) ProjectTo2D(Point3D start3d, Point3D end3d, string projection, double scale)
        {
            if (start3d == null) return (null, null);

            Vector2 start = ProjectPoint(start3d, projection, scale);
            Vector2? end = null;
            if (end3d != null)
            {
                end = ProjectPoint(end3d, projection, scale);
            }
            return (start, end);
        }

        private Vector2 ProjectPoint(Point3D pt, string projection, double scale)
        {
            // pt 不可能为 null（调用前已检查）
            string proj = (projection ?? "plan").ToLowerInvariant();

            switch (proj)
            {
                case "elevation":
                    // 立面投影：X→X, Z→Y
                    return new Vector2(pt.X * scale, pt.Z * scale);

                case "iso":
                    // 等轴测投影（简化版，30度角）
                    double isoX = (pt.X - pt.Y) * Math.Cos(Math.PI / 6) * scale;
                    double isoY = (pt.X + pt.Y) * Math.Sin(Math.PI / 6) * scale + pt.Z * scale;
                    return new Vector2(isoX, isoY);

                case "plan":
                default:
                    // 平面投影：X→X, Y→Y
                    return new Vector2(pt.X * scale, pt.Y * scale);
            }
        }

        #endregion

        #region 辅助方法

        private string GetAttr(E3DElementInfo elem, string key, string defaultValue = "")
        {
            if (elem?.Attributes != null && elem.Attributes.TryGetValue(key, out string val) && !string.IsNullOrEmpty(val))
                return val;
            return defaultValue;
        }

        private Layer GetOrCreateLayer(DxfDocument doc, string name, string elementType)
        {
            var existing = doc.Layers.Items.FirstOrDefault(l => l.Name == name);
            if (existing != null) return existing;

            var layer = new Layer(name);
            // 按元素类型设置颜色（ACI）
            layer.Color = GetLayerColor(elementType);
            doc.Layers.Add(layer);
            return layer;
        }

        private AciColor GetLayerColor(string elementType)
        {
            string typeUpper = (elementType ?? "").ToUpperInvariant();
            switch (typeUpper)
            {
                case "PIPE": return AciColor.Red;         // 1 红
                case "EQUI": return AciColor.Yellow;      // 2 黄
                case "STRU": return AciColor.Green;       // 3 绿
                case "BRAN": return AciColor.Blue;        // 5 蓝
                case "SUPP": return AciColor.Magenta;     // 6 紫
                default: return new AciColor(7);          // 7 白
            }
        }

        #endregion
    }
}
