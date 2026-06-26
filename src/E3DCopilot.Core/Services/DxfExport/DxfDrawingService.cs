using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using E3DCopilot.Core.Models;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

namespace E3DCopilot.Core.Services.DxfExport
{
    /// <summary>
    /// DXF 绘图服务
    /// </summary>
    public class DxfDrawingService
    {
        private readonly DxfProjectionService _projectionService;

        public DxfDrawingService()
        {
            _projectionService = new DxfProjectionService();
        }

        /// <summary>
        /// 生成 DXF 文件
        /// </summary>
        public void GenerateDxf(ProjectedDrawing drawing, DxfExportOptions options)
        {
            var doc = new DxfDocument();

            SetupLayers(doc);
            AddGeometry(doc, drawing);
            AddDimensions(doc, drawing);
            AddTitleBlock(doc, drawing, options);

            string outputPath = options.OutputPath;
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            doc.Save(outputPath);
        }

        /// <summary>
        /// 从结构元素生成 DXF
        /// </summary>
        public void GenerateDxfFromElements(IEnumerable<StructureElement> elements, DxfExportOptions options)
        {
            var drawing = _projectionService.ProjectElements(elements, options.Direction, options);
            _projectionService.AddDimensions(drawing, options.ShowDimensions);
            GenerateDxf(drawing, options);
        }

        private void SetupLayers(DxfDocument doc)
        {
            var layers = DxfStandards.GetStandardLayers();
            foreach (var layer in layers)
            {
                if (!doc.Layers.Contains(layer.Name))
                {
                    doc.Layers.Add(layer);
                }
            }
        }

        private void AddGeometry(DxfDocument doc, ProjectedDrawing drawing)
        {
            foreach (var line in drawing.Lines)
            {
                var dxfLine = new Line(
                    new Vector2(line.Start.X, line.Start.Y),
                    new Vector2(line.End.X, line.End.Y));
                dxfLine.Layer = new Layer(line.Layer);
                doc.Entities.Add(dxfLine);
            }

            foreach (var arc in drawing.Arcs)
            {
                var dxfArc = new Arc(
                    new Vector2(arc.Center.X, arc.Center.Y),
                    arc.Radius,
                    arc.StartAngle * 180 / Math.PI,
                    arc.EndAngle * 180 / Math.PI);
                dxfArc.Layer = new Layer(arc.Layer);
                doc.Entities.Add(dxfArc);
            }
        }

        private void AddDimensions(DxfDocument doc, ProjectedDrawing drawing)
        {
            foreach (var dim in drawing.Dimensions)
            {
                try
                {
                    // 简化处理：使用文字代替尺寸标注
                    var text = new Text(
                        dim.Text ?? "",
                        new Vector2(dim.DimensionLinePoint.X, dim.DimensionLinePoint.Y),
                        DxfStandards.TEXT_HEIGHT_NORMAL);
                    text.Layer = new Layer(dim.Layer);
                    doc.Entities.Add(text);
                }
                catch
                {
                    // 忽略错误
                }
            }
        }

        private void AddTitleBlock(DxfDocument doc, ProjectedDrawing drawing, DxfExportOptions options)
        {
            var bbox = drawing.BoundingBox;

            // 创建标题栏模板
            var template = TitleBlockTemplate.CreateA3();
            template.DrawingName = options.Title ?? "结构图";
            template.Scale = options.Scale;
            template.Date = DateTime.Now.ToString("yyyy-MM-dd");

            // 计算图框位置（包围图形）
            double contentWidth = bbox.Width + 100;  // 内容宽度 + 边距
            double contentHeight = bbox.Height + 100;

            // 如果内容超过 A3，使用 A2
            if (contentWidth > 350 || contentHeight > 250)
            {
                template = TitleBlockTemplate.CreateA2();
            }

            // 计算图框左下角位置（居中）
            double frameX = bbox.Min.X - 50;
            double frameY = bbox.Min.Y - 50;

            // 绘制图框和标题栏
            template.DrawToDxf(doc, frameX, frameY);

            // 添加旧版简单标题栏（在图形右侧）
            AddSimpleTitleBlock(doc, drawing, options, bbox);
        }

        /// <summary>
        /// 添加简单标题栏（在图形右侧）
        /// </summary>
        private void AddSimpleTitleBlock(DxfDocument doc, ProjectedDrawing drawing, DxfExportOptions options, BoundingBox2D bbox)
        {
            double titleX = bbox.Max.X + 20;
            double titleY = bbox.Min.Y;
            double tbWidth = 180;
            double tbHeight = 40;

            var corners = new[]
            {
                new Vector2(titleX, titleY),
                new Vector2(titleX + tbWidth, titleY),
                new Vector2(titleX + tbWidth, titleY + tbHeight),
                new Vector2(titleX, titleY + tbHeight)
            };

            for (int i = 0; i < 4; i++)
            {
                var line = new Line(corners[i], corners[(i + 1) % 4]);
                line.Layer = new Layer(DxfStandards.LAYER_TITLE_BLOCK);
                doc.Entities.Add(line);
            }

            var titleText = new Text(
                options.Title ?? "结构图",
                new Vector2(titleX + 5, titleY + tbHeight - 10),
                DxfStandards.TEXT_HEIGHT_TITLE);
            titleText.Layer = new Layer(DxfStandards.LAYER_TEXT);
            doc.Entities.Add(titleText);

            var scaleText = new Text(
                "比例: " + options.Scale,
                new Vector2(titleX + 5, titleY + tbHeight - 20),
                DxfStandards.TEXT_HEIGHT_NORMAL);
            scaleText.Layer = new Layer(DxfStandards.LAYER_TEXT);
            doc.Entities.Add(scaleText);

            var dateText = new Text(
                "日期: " + DateTime.Now.ToString("yyyy-MM-dd"),
                new Vector2(titleX + 5, titleY + tbHeight - 30),
                DxfStandards.TEXT_HEIGHT_NORMAL);
            dateText.Layer = new Layer(DxfStandards.LAYER_TEXT);
            doc.Entities.Add(dateText);
        }
    }
}
