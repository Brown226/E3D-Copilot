using System;
using System.Collections.Generic;
using System.Linq;
using E3DCopilot.Core.Models;
using E3DCopilot.Core.Models.Geometry;

namespace E3DCopilot.Core.Services.DxfExport
{
    /// <summary>
    /// DXF 投影服务
    /// </summary>
    public class DxfProjectionService
    {
        /// <summary>
        /// 将3D点投影到2D平面
        /// </summary>
        public Point2D Project(Point3D point, ProjectionDirection direction)
        {
            switch (direction)
            {
                case ProjectionDirection.Top:
                    return new Point2D(point.X, point.Y);
                case ProjectionDirection.Front:
                case ProjectionDirection.North:
                    return new Point2D(point.X, point.Z);
                case ProjectionDirection.Side:
                case ProjectionDirection.East:
                    return new Point2D(point.Y, point.Z);
                case ProjectionDirection.South:
                    return new Point2D(-point.X, point.Z);
                case ProjectionDirection.West:
                    return new Point2D(-point.Y, point.Z);
                default:
                    return new Point2D(point.X, point.Y);
            }
        }

        /// <summary>
        /// 计算深度值
        /// </summary>
        public double CalculateDepth(Point3D point, ProjectionDirection direction)
        {
            switch (direction)
            {
                case ProjectionDirection.Top:
                    return -point.Z;
                case ProjectionDirection.Front:
                case ProjectionDirection.North:
                    return -point.Y;
                case ProjectionDirection.Side:
                case ProjectionDirection.East:
                    return -point.X;
                case ProjectionDirection.South:
                    return point.Y;
                case ProjectionDirection.West:
                    return point.X;
                default:
                    return -point.Z;
            }
        }

        /// <summary>
        /// 投影结构元素
        /// </summary>
        public List<ProjectedLine> ProjectElement(StructureElement element, ProjectionDirection direction)
        {
            switch (element.Type)
            {
                case StructureElementType.Sctn:
                    return ProjectSctn(element, direction);
                case StructureElementType.Stwl:
                    return ProjectStwl(element, direction);
                case StructureElementType.Frmw:
                    return ProjectFrmw(element, direction);
                default:
                    return ProjectGeneric(element, direction);
            }
        }

        private List<ProjectedLine> ProjectSctn(StructureElement element, ProjectionDirection direction)
        {
            var lines = new List<ProjectedLine>();
            var start2D = Project(element.StartPoint, direction);
            var end2D = Project(element.EndPoint, direction);
            double avgDepth = (CalculateDepth(element.StartPoint, direction) + CalculateDepth(element.EndPoint, direction)) / 2;

            double halfWidth = element.Width / 2;
            double halfHeight = element.Height / 2;

            var corners = new[]
            {
                new Point2D(start2D.X + halfWidth, start2D.Y + halfHeight),
                new Point2D(start2D.X - halfWidth, start2D.Y - halfHeight),
                new Point2D(end2D.X - halfWidth, end2D.Y - halfHeight),
                new Point2D(end2D.X + halfWidth, end2D.Y + halfHeight)
            };

            for (int i = 0; i < 4; i++)
            {
                lines.Add(new ProjectedLine
                {
                    Start = corners[i],
                    End = corners[(i + 1) % 4],
                    Layer = DxfStandards.LAYER_STRUCTURE,
                    Depth = avgDepth,
                    ElementId = element.Id
                });
            }

            return lines;
        }

        private List<ProjectedLine> ProjectStwl(StructureElement element, ProjectionDirection direction)
        {
            var lines = new List<ProjectedLine>();

            if (element.BoundaryPoints == null || element.BoundaryPoints.Count < 3)
                return lines;

            var projectedPoints = element.BoundaryPoints.Select(p => Project(p, direction)).ToList();
            double avgDepth = element.BoundaryPoints.Select(p => CalculateDepth(p, direction)).Average();

            for (int i = 0; i < projectedPoints.Count; i++)
            {
                lines.Add(new ProjectedLine
                {
                    Start = projectedPoints[i],
                    End = projectedPoints[(i + 1) % projectedPoints.Count],
                    Layer = DxfStandards.LAYER_STRUCTURE,
                    Depth = avgDepth,
                    ElementId = element.Id
                });
            }

            return lines;
        }

        private List<ProjectedLine> ProjectFrmw(StructureElement element, ProjectionDirection direction)
        {
            var lines = new List<ProjectedLine>();
            var start2D = Project(element.StartPoint, direction);
            var end2D = Project(element.EndPoint, direction);
            double avgDepth = (CalculateDepth(element.StartPoint, direction) + CalculateDepth(element.EndPoint, direction)) / 2;

            lines.Add(new ProjectedLine
            {
                Start = start2D,
                End = end2D,
                Layer = DxfStandards.LAYER_CENTERLINE,
                Depth = avgDepth,
                ElementId = element.Id
            });

            return lines;
        }

        private List<ProjectedLine> ProjectGeneric(StructureElement element, ProjectionDirection direction)
        {
            var lines = new List<ProjectedLine>();
            var bbox = element.BoundingBox;

            if (bbox == null)
                return lines;

            var corners3D = new[]
            {
                new Point3D(bbox.Min.X, bbox.Min.Y, bbox.Min.Z),
                new Point3D(bbox.Max.X, bbox.Min.Y, bbox.Min.Z),
                new Point3D(bbox.Max.X, bbox.Max.Y, bbox.Min.Z),
                new Point3D(bbox.Min.X, bbox.Max.Y, bbox.Min.Z)
            };

            var projected = corners3D.Select(c => Project(c, direction)).ToArray();
            double avgDepth = corners3D.Select(c => CalculateDepth(c, direction)).Average();

            double minX = projected.Min(p => p.X);
            double maxX = projected.Max(p => p.X);
            double minY = projected.Min(p => p.Y);
            double maxY = projected.Max(p => p.Y);

            var rect = new[]
            {
                new Point2D(minX, minY),
                new Point2D(maxX, minY),
                new Point2D(maxX, maxY),
                new Point2D(minX, maxY)
            };

            for (int i = 0; i < 4; i++)
            {
                lines.Add(new ProjectedLine
                {
                    Start = rect[i],
                    End = rect[(i + 1) % 4],
                    Layer = DxfStandards.LAYER_STRUCTURE,
                    Depth = avgDepth,
                    ElementId = element.Id
                });
            }

            return lines;
        }

        /// <summary>
        /// 投影多个元素
        /// </summary>
        public ProjectedDrawing ProjectElements(IEnumerable<StructureElement> elements, ProjectionDirection direction, DxfExportOptions options)
        {
            var drawing = new ProjectedDrawing
            {
                Direction = direction,
                Title = options.Title,
                Scale = options.Scale
            };

            var allLines = new List<ProjectedLine>();
            foreach (var element in elements)
            {
                var lines = ProjectElement(element, direction);
                allLines.AddRange(lines);
            }

            allLines = allLines.OrderByDescending(l => l.Depth).ToList();

            if (options.ShowHiddenLines)
            {
                ApplyHiddenLineProcessing(allLines);
            }

            drawing.Lines = allLines;
            return drawing;
        }

        private void ApplyHiddenLineProcessing(List<ProjectedLine> lines)
        {
            if (lines.Count < 2)
                return;

            // 按深度分组
            var depthGroups = lines.GroupBy(l => Math.Round(l.Depth, 0))
                                   .OrderBy(g => g.Key)
                                   .ToList();

            if (depthGroups.Count < 2)
                return;

            // 最前面的组保持实线，后面的组标记为隐藏线
            for (int i = 1; i < depthGroups.Count; i++)
            {
                foreach (var line in depthGroups[i])
                {
                    if (line.Layer == DxfStandards.LAYER_STRUCTURE)
                    {
                        line.IsHidden = true;
                        line.Layer = DxfStandards.LAYER_STRUCTURE_HIDDEN;
                    }
                }
            }

            // 检测并处理线段重叠（简化版遮挡检测）
            DetectOverlappingLines(lines);
        }

        /// <summary>
        /// 检测重叠线段（简化版）
        /// 如果两条线段几乎重合且深度不同，后面的标记为隐藏
        /// </summary>
        private void DetectOverlappingLines(List<ProjectedLine> lines)
        {
            double tolerance = 1.0; // 1mm 容差

            for (int i = 0; i < lines.Count; i++)
            {
                for (int j = i + 1; j < lines.Count; j++)
                {
                    var line1 = lines[i];
                    var line2 = lines[j];

                    // 检查是否几乎重合
                    if (AreLinesOverlapping(line1, line2, tolerance))
                    {
                        // 深度大的（后面的）标记为隐藏
                        if (line1.Depth > line2.Depth && line1.Layer == DxfStandards.LAYER_STRUCTURE)
                        {
                            line1.IsHidden = true;
                            line1.Layer = DxfStandards.LAYER_STRUCTURE_HIDDEN;
                        }
                        else if (line2.Depth > line1.Depth && line2.Layer == DxfStandards.LAYER_STRUCTURE)
                        {
                            line2.IsHidden = true;
                            line2.Layer = DxfStandards.LAYER_STRUCTURE_HIDDEN;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 判断两条线段是否重叠
        /// </summary>
        private bool AreLinesOverlapping(ProjectedLine line1, ProjectedLine line2, double tolerance)
        {
            // 检查端点是否接近
            bool startMatch = Distance(line1.Start, line2.Start) < tolerance || 
                             Distance(line1.Start, line2.End) < tolerance;
            bool endMatch = Distance(line1.End, line2.Start) < tolerance || 
                           Distance(line1.End, line2.End) < tolerance;

            // 如果两个端点都匹配，认为重叠
            if (startMatch && endMatch)
                return true;

            // 检查一条线是否完全在另一条线上（共线且重叠）
            if (AreCollinear(line1, line2, tolerance))
            {
                // 检查投影范围是否重叠
                if (DoRangesOverlap(line1, line2))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 计算两点距离
        /// </summary>
        private double Distance(Point2D a, Point2D b)
        {
            return Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
        }

        /// <summary>
        /// 判断两条线段是否共线
        /// </summary>
        private bool AreCollinear(ProjectedLine line1, ProjectedLine line2, double tolerance)
        {
            // 计算线段1的方向向量
            double dx1 = line1.End.X - line1.Start.X;
            double dy1 = line1.End.Y - line1.Start.Y;

            // 计算线段2的方向向量
            double dx2 = line2.End.X - line2.Start.X;
            double dy2 = line2.End.Y - line2.Start.Y;

            // 检查是否平行（叉积接近0）
            double cross = dx1 * dy2 - dy1 * dx2;
            if (Math.Abs(cross) > tolerance * 100) // 允许一定误差
                return false;

            // 检查线段2的起点是否在线段1所在的直线上
            double dx = line2.Start.X - line1.Start.X;
            double dy = line2.Start.Y - line1.Start.Y;
            double cross2 = dx1 * dy - dy1 * dx;

            return Math.Abs(cross2) < tolerance * 100;
        }

        /// <summary>
        /// 判断两条线段的投影范围是否重叠
        /// </summary>
        private bool DoRangesOverlap(ProjectedLine line1, ProjectedLine line2)
        {
            // 获取线段1的范围
            double minX1 = Math.Min(line1.Start.X, line1.End.X);
            double maxX1 = Math.Max(line1.Start.X, line1.End.X);
            double minY1 = Math.Min(line1.Start.Y, line1.End.Y);
            double maxY1 = Math.Max(line1.Start.Y, line1.End.Y);

            // 获取线段2的范围
            double minX2 = Math.Min(line2.Start.X, line2.End.X);
            double maxX2 = Math.Max(line2.Start.X, line2.End.X);
            double minY2 = Math.Min(line2.Start.Y, line2.End.Y);
            double maxY2 = Math.Max(line2.Start.Y, line2.End.Y);

            // 检查X和Y范围是否都重叠
            bool xOverlap = maxX1 >= minX2 && maxX2 >= minX1;
            bool yOverlap = maxY1 >= minY2 && maxY2 >= minY1;

            return xOverlap && yOverlap;
        }

        /// <summary>
        /// 添加尺寸标注
        /// </summary>
        public void AddDimensions(ProjectedDrawing drawing, bool showDimensions)
        {
            if (!showDimensions || drawing.Lines.Count == 0)
                return;

            var bbox = drawing.BoundingBox;

            // 添加总尺寸标注
            AddOverallDimensions(drawing, bbox);

            // 添加元素间尺寸标注
            AddElementDimensions(drawing);

            // 添加标高标注
            AddElevationDimensions(drawing, bbox);
        }

        /// <summary>
        /// 添加总尺寸标注
        /// </summary>
        private void AddOverallDimensions(ProjectedDrawing drawing, BoundingBox2D bbox)
        {
            double offset = 20;

            // 水平总尺寸
            drawing.Dimensions.Add(new DimensionInfo
            {
                Type = "linear",
                StartPoint = new Point2D(bbox.Min.X, bbox.Min.Y - offset),
                EndPoint = new Point2D(bbox.Max.X, bbox.Min.Y - offset),
                DimensionLinePoint = new Point2D((bbox.Min.X + bbox.Max.X) / 2, bbox.Min.Y - offset - 5),
                Text = string.Format("{0:F2}m", bbox.Width / 1000),
                Layer = DxfStandards.LAYER_DIMENSION
            });

            // 垂直总尺寸
            drawing.Dimensions.Add(new DimensionInfo
            {
                Type = "linear",
                StartPoint = new Point2D(bbox.Min.X - offset, bbox.Min.Y),
                EndPoint = new Point2D(bbox.Min.X - offset, bbox.Max.Y),
                DimensionLinePoint = new Point2D(bbox.Min.X - offset - 5, (bbox.Min.Y + bbox.Max.Y) / 2),
                Text = string.Format("{0:F2}m", bbox.Height / 1000),
                Layer = DxfStandards.LAYER_DIMENSION
            });
        }

        /// <summary>
        /// 添加元素间尺寸标注
        /// </summary>
        private void AddElementDimensions(ProjectedDrawing drawing)
        {
            // 按元素分组获取线段
            var elementGroups = drawing.Lines
                .Where(l => !string.IsNullOrEmpty(l.ElementId))
                .GroupBy(l => l.ElementId)
                .ToList();

            if (elementGroups.Count < 2)
                return;

            // 计算每个元素的中心点
            var centers = new List<Tuple<string, Point2D, double>>();
            foreach (var group in elementGroups)
            {
                var lines = group.ToList();
                double minX = lines.Min(l => Math.Min(l.Start.X, l.End.X));
                double maxX = lines.Max(l => Math.Max(l.Start.X, l.End.X));
                double minY = lines.Min(l => Math.Min(l.Start.Y, l.End.Y));
                double maxY = lines.Max(l => Math.Max(l.Start.Y, l.End.Y));

                var center = new Point2D((minX + maxX) / 2, (minY + maxY) / 2);
                double depth = lines.Average(l => l.Depth);

                centers.Add(Tuple.Create(group.Key, center, depth));
            }

            // 按X坐标排序，添加间距标注
            centers = centers.OrderBy(c => c.Item2.X).ToList();
            for (int i = 0; i < centers.Count - 1; i++)
            {
                var current = centers[i];
                var next = centers[i + 1];

                double distance = Math.Abs(next.Item2.X - current.Item2.X);
                if (distance > 100) // 只标注大于100mm的间距
                {
                    drawing.Dimensions.Add(new DimensionInfo
                    {
                        Type = "aligned",
                        StartPoint = current.Item2,
                        EndPoint = next.Item2,
                        DimensionLinePoint = new Point2D(
                            (current.Item2.X + next.Item2.X) / 2,
                            Math.Min(current.Item2.Y, next.Item2.Y) - 10),
                        Text = string.Format("{0:F0}", distance),
                        Layer = DxfStandards.LAYER_DIMENSION
                    });
                }
            }
        }

        /// <summary>
        /// 添加标高标注
        /// </summary>
        private void AddElevationDimensions(ProjectedDrawing drawing, BoundingBox2D bbox)
        {
            // 在图纸右上角添加标高信息
            drawing.Dimensions.Add(new DimensionInfo
            {
                Type = "text",
                StartPoint = new Point2D(bbox.Max.X + 10, bbox.Max.Y),
                EndPoint = new Point2D(bbox.Max.X + 10, bbox.Max.Y),
                DimensionLinePoint = new Point2D(bbox.Max.X + 10, bbox.Max.Y),
                Text = string.Format("标高: {0:F2}m", bbox.Min.Y / 1000),
                Layer = DxfStandards.LAYER_TEXT
            });
        }
    }
}
