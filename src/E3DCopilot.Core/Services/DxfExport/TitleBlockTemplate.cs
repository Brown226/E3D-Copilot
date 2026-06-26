using System;
using System.Collections.Generic;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

namespace E3DCopilot.Core.Services.DxfExport
{
    /// <summary>
    /// 标题栏模板
    /// </summary>
    public class TitleBlockTemplate
    {
        /// <summary>
        /// 图框尺寸
        /// </summary>
        public double Width { get; set; }
        public double Height { get; set; }

        /// <summary>
        /// 标题栏位置（相对于图框右下角）
        /// </summary>
        public double TitleBlockWidth { get; set; }
        public double TitleBlockHeight { get; set; }

        /// <summary>
        /// 边距
        /// </summary>
        public double LeftMargin { get; set; }
        public double RightMargin { get; set; }
        public double TopMargin { get; set; }
        public double BottomMargin { get; set; }

        /// <summary>
        /// 图纸信息
        /// </summary>
        public string ProjectName { get; set; }
        public string DrawingName { get; set; }
        public string DrawingNumber { get; set; }
        public string Scale { get; set; }
        public string Date { get; set; }
        public string Designer { get; set; }
        public string Reviewer { get; set; }
        public string Company { get; set; }

        public TitleBlockTemplate()
        {
            // A3 默认尺寸
            Width = 420;
            Height = 297;
            TitleBlockWidth = 180;
            TitleBlockHeight = 40;
            LeftMargin = 25;
            RightMargin = 10;
            TopMargin = 10;
            BottomMargin = 10;

            Date = DateTime.Now.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// 创建 A4 模板
        /// </summary>
        public static TitleBlockTemplate CreateA4()
        {
            return new TitleBlockTemplate
            {
                Width = 297,
                Height = 210,
                TitleBlockWidth = 140,
                TitleBlockHeight = 32,
                LeftMargin = 25,
                RightMargin = 5,
                TopMargin = 5,
                BottomMargin = 5
            };
        }

        /// <summary>
        /// 创建 A3 模板
        /// </summary>
        public static TitleBlockTemplate CreateA3()
        {
            return new TitleBlockTemplate
            {
                Width = 420,
                Height = 297,
                TitleBlockWidth = 180,
                TitleBlockHeight = 40,
                LeftMargin = 25,
                RightMargin = 10,
                TopMargin = 10,
                BottomMargin = 10
            };
        }

        /// <summary>
        /// 创建 A2 模板
        /// </summary>
        public static TitleBlockTemplate CreateA2()
        {
            return new TitleBlockTemplate
            {
                Width = 594,
                Height = 420,
                TitleBlockWidth = 200,
                TitleBlockHeight = 48,
                LeftMargin = 25,
                RightMargin = 10,
                TopMargin = 10,
                BottomMargin = 10
            };
        }

        /// <summary>
        /// 绘制图框和标题栏到 DXF 文档
        /// </summary>
        public void DrawToDxf(DxfDocument doc, double offsetX, double offsetY)
        {
            // 绘制外框
            DrawOuterFrame(doc, offsetX, offsetY);

            // 绘制内框（图框边界）
            DrawInnerFrame(doc, offsetX, offsetY);

            // 绘制标题栏
            DrawTitleBlock(doc, offsetX, offsetY);
        }

        /// <summary>
        /// 绘制外框
        /// </summary>
        private void DrawOuterFrame(DxfDocument doc, double offsetX, double offsetY)
        {
            var corners = new[]
            {
                new Vector2(offsetX, offsetY),
                new Vector2(offsetX + Width, offsetY),
                new Vector2(offsetX + Width, offsetY + Height),
                new Vector2(offsetX, offsetY + Height)
            };

            for (int i = 0; i < 4; i++)
            {
                var line = new Line(corners[i], corners[(i + 1) % 4]);
                line.Layer = new Layer(DxfStandards.LAYER_FRAME);
                doc.Entities.Add(line);
            }
        }

        /// <summary>
        /// 绘制内框
        /// </summary>
        private void DrawInnerFrame(DxfDocument doc, double offsetX, double offsetY)
        {
            double innerX = offsetX + LeftMargin;
            double innerY = offsetY + BottomMargin;
            double innerWidth = Width - LeftMargin - RightMargin;
            double innerHeight = Height - TopMargin - BottomMargin;

            var corners = new[]
            {
                new Vector2(innerX, innerY),
                new Vector2(innerX + innerWidth, innerY),
                new Vector2(innerX + innerWidth, innerY + innerHeight),
                new Vector2(innerX, innerY + innerHeight)
            };

            for (int i = 0; i < 4; i++)
            {
                var line = new Line(corners[i], corners[(i + 1) % 4]);
                line.Layer = new Layer(DxfStandards.LAYER_FRAME);
                doc.Entities.Add(line);
            }
        }

        /// <summary>
        /// 绘制标题栏
        /// </summary>
        private void DrawTitleBlock(DxfDocument doc, double offsetX, double offsetY)
        {
            double tbX = offsetX + Width - RightMargin - TitleBlockWidth;
            double tbY = offsetY + BottomMargin;

            // 标题栏外框
            var corners = new[]
            {
                new Vector2(tbX, tbY),
                new Vector2(tbX + TitleBlockWidth, tbY),
                new Vector2(tbX + TitleBlockWidth, tbY + TitleBlockHeight),
                new Vector2(tbX, tbY + TitleBlockHeight)
            };

            for (int i = 0; i < 4; i++)
            {
                var line = new Line(corners[i], corners[(i + 1) % 4]);
                line.Layer = new Layer(DxfStandards.LAYER_TITLE_BLOCK);
                doc.Entities.Add(line);
            }

            // 绘制分隔线
            double rowHeight = TitleBlockHeight / 4;

            // 横向分隔线
            for (int i = 1; i < 4; i++)
            {
                var line = new Line(
                    new Vector2(tbX, tbY + i * rowHeight),
                    new Vector2(tbX + TitleBlockWidth, tbY + i * rowHeight));
                line.Layer = new Layer(DxfStandards.LAYER_TITLE_BLOCK);
                doc.Entities.Add(line);
            }

            // 纵向分隔线
            double col1Width = TitleBlockWidth * 0.15;
            double col2Width = TitleBlockWidth * 0.35;

            var vLine1 = new Line(
                new Vector2(tbX + col1Width, tbY),
                new Vector2(tbX + col1Width, tbY + TitleBlockHeight));
            vLine1.Layer = new Layer(DxfStandards.LAYER_TITLE_BLOCK);
            doc.Entities.Add(vLine1);

            var vLine2 = new Line(
                new Vector2(tbX + col1Width + col2Width, tbY),
                new Vector2(tbX + col1Width + col2Width, tbY + TitleBlockHeight));
            vLine2.Layer = new Layer(DxfStandards.LAYER_TITLE_BLOCK);
            doc.Entities.Add(vLine2);

            // 添加文字
            AddTitleBlockText(doc, tbX, tbY, col1Width, col2Width, rowHeight);
        }

        /// <summary>
        /// 添加标题栏文字
        /// </summary>
        private void AddTitleBlockText(DxfDocument doc, double tbX, double tbY, double col1Width, double col2Width, double rowHeight)
        {
            double textOffsetX = 2;
            double textOffsetY = rowHeight / 2 - 1;

            // 第一行：项目名称
            AddText(doc, "项目", tbX + textOffsetX, tbY + 3 * rowHeight + textOffsetY, DxfStandards.TEXT_HEIGHT_SMALL);
            AddText(doc, ProjectName ?? "", tbX + col1Width + textOffsetX, tbY + 3 * rowHeight + textOffsetY, DxfStandards.TEXT_HEIGHT_NORMAL);

            // 第二行：图名和图号
            AddText(doc, "图名", tbX + textOffsetX, tbY + 2 * rowHeight + textOffsetY, DxfStandards.TEXT_HEIGHT_SMALL);
            AddText(doc, DrawingName ?? "", tbX + col1Width + textOffsetX, tbY + 2 * rowHeight + textOffsetY, DxfStandards.TEXT_HEIGHT_NORMAL);
            AddText(doc, "图号", tbX + col1Width + col2Width + textOffsetX, tbY + 2 * rowHeight + textOffsetY, DxfStandards.TEXT_HEIGHT_SMALL);
            AddText(doc, DrawingNumber ?? "", tbX + col1Width + col2Width + 30 + textOffsetX, tbY + 2 * rowHeight + textOffsetY, DxfStandards.TEXT_HEIGHT_NORMAL);

            // 第三行：比例和日期
            AddText(doc, "比例", tbX + textOffsetX, tbY + rowHeight + textOffsetY, DxfStandards.TEXT_HEIGHT_SMALL);
            AddText(doc, Scale ?? "1:100", tbX + col1Width + textOffsetX, tbY + rowHeight + textOffsetY, DxfStandards.TEXT_HEIGHT_NORMAL);
            AddText(doc, "日期", tbX + col1Width + col2Width + textOffsetX, tbY + rowHeight + textOffsetY, DxfStandards.TEXT_HEIGHT_SMALL);
            AddText(doc, Date ?? "", tbX + col1Width + col2Width + 30 + textOffsetX, tbY + rowHeight + textOffsetY, DxfStandards.TEXT_HEIGHT_NORMAL);

            // 第四行：设计人和审核人
            AddText(doc, "设计", tbX + textOffsetX, tbY + textOffsetY, DxfStandards.TEXT_HEIGHT_SMALL);
            AddText(doc, Designer ?? "", tbX + col1Width + textOffsetX, tbY + textOffsetY, DxfStandards.TEXT_HEIGHT_NORMAL);
            AddText(doc, "审核", tbX + col1Width + col2Width + textOffsetX, tbY + textOffsetY, DxfStandards.TEXT_HEIGHT_SMALL);
            AddText(doc, Reviewer ?? "", tbX + col1Width + col2Width + 30 + textOffsetX, tbY + textOffsetY, DxfStandards.TEXT_HEIGHT_NORMAL);
        }

        /// <summary>
        /// 添加文字辅助方法
        /// </summary>
        private void AddText(DxfDocument doc, string text, double x, double y, double height)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var dxfText = new Text(text, new Vector2(x, y), height);
            dxfText.Layer = new Layer(DxfStandards.LAYER_TEXT);
            doc.Entities.Add(dxfText);
        }
    }
}
