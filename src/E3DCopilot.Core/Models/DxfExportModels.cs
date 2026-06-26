using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using E3DCopilot.Core.Models.Geometry;

namespace E3DCopilot.Core.Models
{
    /// <summary>
    /// 投影方向枚举
    /// </summary>
    [DataContract]
    public enum ProjectionDirection
    {
        /// <summary>平面图（从上往下投影，取X,Y）</summary>
        [EnumMember] Top,
        /// <summary>北立面（从南往北看，取X,Z）</summary>
        [EnumMember] North,
        /// <summary>南立面（从北往南看，取X,Z）</summary>
        [EnumMember] South,
        /// <summary>东立面（从西往东看，取Y,Z）</summary>
        [EnumMember] East,
        /// <summary>西立面（从东往西看，取Y,Z）</summary>
        [EnumMember] West,
        /// <summary>正面（Front，取X,Z）</summary>
        [EnumMember] Front,
        /// <summary>侧面（Side，取Y,Z）</summary>
        [EnumMember] Side
    }

    /// <summary>
    /// 结构元素类型
    /// </summary>
    [DataContract]
    public enum StructureElementType
    {
        /// <summary>结构件（梁、柱等）</summary>
        [EnumMember] Sctn,
        /// <summary>墙</summary>
        [EnumMember] Stwl,
        /// <summary>框架</summary>
        [EnumMember] Frmw,
        /// <summary>通用截面</summary>
        [EnumMember] Gensec,
        /// <summary>结构</summary>
        [EnumMember] Stru,
        /// <summary>其他</summary>
        [EnumMember] Other
    }

    /// <summary>
    /// 投影后的2D点
    /// </summary>
    [DataContract]
    public class Point2D
    {
        [DataMember] public double X { get; set; }
        [DataMember] public double Y { get; set; }

        public Point2D() { }
        public Point2D(double x, double y) { X = x; Y = y; }

        public override string ToString() => $"({X:0.00}, {Y:0.00})";
    }

    /// <summary>
    /// 投影后的线段
    /// </summary>
    [DataContract]
    public class ProjectedLine
    {
        /// <summary>起点</summary>
        [DataMember] public Point2D Start { get; set; }
        /// <summary>终点</summary>
        [DataMember] public Point2D End { get; set; }
        /// <summary>所属图层</summary>
        [DataMember] public string Layer { get; set; }
        /// <summary>原始3D深度（用于消隐排序）</summary>
        [DataMember] public double Depth { get; set; }
        /// <summary>是否隐藏线</summary>
        [DataMember] public bool IsHidden { get; set; }
        /// <summary>关联的元素ID</summary>
        [DataMember] public string ElementId { get; set; }
        /// <summary>线型</summary>
        [DataMember] public string LineType { get; set; }
        /// <summary>颜色</summary>
        [DataMember] public int ColorIndex { get; set; }

        public ProjectedLine()
        {
            Start = new Point2D();
            End = new Point2D();
            Layer = string.Empty;
            LineType = string.Empty;
        }

        public ProjectedLine(Point2D start, Point2D end, string layer)
        {
            Start = start;
            End = end;
            Layer = layer;
            LineType = string.Empty;
        }

        /// <summary>线段长度</summary>
        public double Length => Math.Sqrt(Math.Pow(End.X - Start.X, 2) + Math.Pow(End.Y - Start.Y, 2));
    }

    /// <summary>
    /// 投影后的圆弧
    /// </summary>
    [DataContract]
    public class ProjectedArc
    {
        /// <summary>圆心</summary>
        [DataMember] public Point2D Center { get; set; }
        /// <summary>半径</summary>
        [DataMember] public double Radius { get; set; }
        /// <summary>起始角度（弧度）</summary>
        [DataMember] public double StartAngle { get; set; }
        /// <summary>终止角度（弧度）</summary>
        [DataMember] public double EndAngle { get; set; }
        /// <summary>所属图层</summary>
        [DataMember] public string Layer { get; set; }
        /// <summary>原始3D深度</summary>
        [DataMember] public double Depth { get; set; }
        /// <summary>是否隐藏线</summary>
        [DataMember] public bool IsHidden { get; set; }

        public ProjectedArc()
        {
            Center = new Point2D();
            Layer = string.Empty;
        }
    }

    /// <summary>
    /// 投影后的多段线
    /// </summary>
    [DataContract]
    public class ProjectedPolyline
    {
        /// <summary>顶点列表</summary>
        [DataMember] public List<Point2D> Vertices { get; set; }
        /// <summary>是否闭合</summary>
        [DataMember] public bool IsClosed { get; set; }
        /// <summary>所属图层</summary>
        [DataMember] public string Layer { get; set; }
        /// <summary>原始3D深度</summary>
        [DataMember] public double Depth { get; set; }
        /// <summary>是否隐藏线</summary>
        [DataMember] public bool IsHidden { get; set; }

        public ProjectedPolyline()
        {
            Vertices = new List<Point2D>();
            Layer = string.Empty;
        }
    }

    /// <summary>
    /// 尺寸标注信息
    /// </summary>
    [DataContract]
    public class DimensionInfo
    {
        /// <summary>标注类型</summary>
        [DataMember] public string Type { get; set; } // "linear", "aligned"
        /// <summary>起点</summary>
        [DataMember] public Point2D StartPoint { get; set; }
        /// <summary>终点</summary>
        [DataMember] public Point2D EndPoint { get; set; }
        /// <summary>标注位置</summary>
        [DataMember] public Point2D DimensionLinePoint { get; set; }
        /// <summary>标注文字</summary>
        [DataMember] public string Text { get; set; }
        /// <summary>所属图层</summary>
        [DataMember] public string Layer { get; set; }

        public DimensionInfo()
        {
            StartPoint = new Point2D();
            EndPoint = new Point2D();
            DimensionLinePoint = new Point2D();
            Layer = string.Empty;
            Text = string.Empty;
        }
    }

    /// <summary>
    /// 投影后的完整图纸
    /// </summary>
    [DataContract]
    public class ProjectedDrawing
    {
        /// <summary>线段集合</summary>
        [DataMember] public List<ProjectedLine> Lines { get; set; }
        /// <summary>圆弧集合</summary>
        [DataMember] public List<ProjectedArc> Arcs { get; set; }
        /// <summary>多段线集合</summary>
        [DataMember] public List<ProjectedPolyline> Polylines { get; set; }
        /// <summary>尺寸标注集合</summary>
        [DataMember] public List<DimensionInfo> Dimensions { get; set; }
        /// <summary>投影方向</summary>
        [DataMember] public ProjectionDirection Direction { get; set; }
        /// <summary>图名</summary>
        [DataMember] public string Title { get; set; }
        /// <summary>比例</summary>
        [DataMember] public string Scale { get; set; }

        public ProjectedDrawing()
        {
            Lines = new List<ProjectedLine>();
            Arcs = new List<ProjectedArc>();
            Polylines = new List<ProjectedPolyline>();
            Dimensions = new List<DimensionInfo>();
            Title = string.Empty;
            Scale = "1:100";
        }

        /// <summary>总实体数</summary>
        public int TotalEntities => Lines.Count + Arcs.Count + Polylines.Count;

        /// <summary>图层列表</summary>
        public List<string> LayerNames
        {
            get
            {
                var layers = new HashSet<string>();
                foreach (var line in Lines) layers.Add(line.Layer);
                foreach (var arc in Arcs) layers.Add(arc.Layer);
                foreach (var pl in Polylines) layers.Add(pl.Layer);
                return new List<string>(layers);
            }
        }

        /// <summary>包围盒</summary>
        public BoundingBox2D BoundingBox
        {
            get
            {
                var bbox = new BoundingBox2D();
                foreach (var line in Lines)
                {
                    bbox.ExpandToInclude(line.Start);
                    bbox.ExpandToInclude(line.End);
                }
                foreach (var arc in Arcs)
                {
                    bbox.ExpandToInclude(new Point2D(arc.Center.X - arc.Radius, arc.Center.Y - arc.Radius));
                    bbox.ExpandToInclude(new Point2D(arc.Center.X + arc.Radius, arc.Center.Y + arc.Radius));
                }
                return bbox;
            }
        }
    }

    /// <summary>
    /// 2D包围盒
    /// </summary>
    [DataContract]
    public class BoundingBox2D
    {
        [DataMember] public Point2D Min { get; set; }
        [DataMember] public Point2D Max { get; set; }

        public BoundingBox2D()
        {
            Min = new Point2D(double.MaxValue, double.MaxValue);
            Max = new Point2D(double.MinValue, double.MinValue);
        }

        public void ExpandToInclude(Point2D point)
        {
            Min.X = Math.Min(Min.X, point.X);
            Min.Y = Math.Min(Min.Y, point.Y);
            Max.X = Math.Max(Max.X, point.X);
            Max.Y = Math.Max(Max.Y, point.Y);
        }

        public double Width => Max.X - Min.X;
        public double Height => Max.Y - Min.Y;
        public Point2D Center => new Point2D((Min.X + Max.X) / 2, (Min.Y + Max.Y) / 2);
    }

    /// <summary>
    /// DXF导出选项
    /// </summary>
    [DataContract]
    public class DxfExportOptions
    {
        /// <summary>输出文件路径</summary>
        [DataMember] public string OutputPath { get; set; }
        /// <summary>投影方向</summary>
        [DataMember] public ProjectionDirection Direction { get; set; }
        /// <summary>是否显示尺寸标注</summary>
        [DataMember] public bool ShowDimensions { get; set; }
        /// <summary>是否显示隐藏线</summary>
        [DataMember] public bool ShowHiddenLines { get; set; }
        /// <summary>比例</summary>
        [DataMember] public string Scale { get; set; }
        /// <summary>是否添加标题栏</summary>
        [DataMember] public bool TitleBlock { get; set; }
        /// <summary>图名</summary>
        [DataMember] public string Title { get; set; }

        public DxfExportOptions()
        {
            OutputPath = string.Empty;
            Direction = ProjectionDirection.Top;
            ShowDimensions = true;
            ShowHiddenLines = true;
            Scale = "1:100";
            TitleBlock = true;
            Title = "结构图";
        }
    }

    /// <summary>
    /// 结构元素几何信息（从E3D提取）
    /// </summary>
    [DataContract]
    public class StructureElement
    {
        /// <summary>元素ID</summary>
        [DataMember] public string Id { get; set; }
        /// <summary>元素名称</summary>
        [DataMember] public string Name { get; set; }
        /// <summary>元素类型</summary>
        [DataMember] public StructureElementType Type { get; set; }
        /// <summary>起点（SCTN用）</summary>
        [DataMember] public Point3D StartPoint { get; set; }
        /// <summary>终点（SCTN用）</summary>
        [DataMember] public Point3D EndPoint { get; set; }
        /// <summary>截面宽度</summary>
        [DataMember] public double Width { get; set; }
        /// <summary>截面高度</summary>
        [DataMember] public double Height { get; set; }
        /// <summary>边界点（STWL用）</summary>
        [DataMember] public List<Point3D> BoundaryPoints { get; set; }
        /// <summary>包围盒</summary>
        [DataMember] public BoundingBox BoundingBox { get; set; }
        /// <summary>原始DbElement引用</summary>
        [DataMember] public string DbRef { get; set; }

        public StructureElement()
        {
            Id = string.Empty;
            Name = string.Empty;
            StartPoint = new Point3D();
            EndPoint = new Point3D();
            BoundaryPoints = new List<Point3D>();
            BoundingBox = new BoundingBox();
            DbRef = string.Empty;
        }
    }
}
