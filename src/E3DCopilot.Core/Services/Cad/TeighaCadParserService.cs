using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using E3DCopilot.Core.Models.Building;
using E3DCopilot.Core.Models.Geometry;
using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace E3DCopilot.Core.Services.Cad
{
    /// <summary>
    /// CAD 解析配置
    /// </summary>
    public class CadParseConfig
    {
        /// <summary>
        /// 墙体图层名称列表
        /// </summary>
        public List<string> WallLayers { get; set; } = new List<string>
        {
            "土建", "墙体", "WALL", "A-WALL", "S-WALL", "结构"
        };

        /// <summary>
        /// 门窗图层名称列表
        /// </summary>
        public List<string> DoorWindowLayers { get; set; } = new List<string>
        {
            "门窗", "WINDOW", "DOOR", "A-DOOR", "A-WIND"
        };

        /// <summary>
        /// 设备图层名称列表
        /// </summary>
        public List<string> EquipmentLayers { get; set; } = new List<string>
        {
            "设备", "EQUIPMENT", "MEP", "HVAC"
        };

        /// <summary>
        /// 默认墙高（mm）
        /// </summary>
        public double DefaultWallHeight { get; set; } = 3000;

        /// <summary>
        /// 默认墙厚（mm）
        /// </summary>
        public double DefaultWallThickness { get; set; } = 200;

        /// <summary>
        /// 最小线段长度（mm）
        /// </summary>
        public double MinSegmentLength { get; set; } = 100;

        /// <summary>
        /// 图层匹配容差
        /// </summary>
        public double LayerMatchTolerance { get; set; } = 0.1;
    }

    /// <summary>
    /// CAD 解析服务（集成 Teigha.NET 4.00）
    /// 支持离线解析 DWG/DXF 文件，无需打开 AutoCAD
    /// </summary>
    public class TeighaCadParserService
    {
        private static Teigha.Runtime.Services _teighaServices;
        private static readonly object _initLock = new object();
        private CadParseConfig _config;

        public TeighaCadParserService(CadParseConfig config = null)
        {
            _config = config ?? new CadParseConfig();
        }

        /// <summary>
        /// 初始化 Teigha.NET 运行时（进程级单例）
        /// </summary>
        private static void EnsureTeighaInitialized()
        {
            lock (_initLock)
            {
                if (_teighaServices != null) return;

                // 将 Teigha DLL 目录加入 PATH（原生 DLL 加载需要）
                string runtimeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string teighaDir = Path.Combine(runtimeDirectory, "Teigha");
                if (!Directory.Exists(teighaDir))
                    teighaDir = Path.Combine(runtimeDirectory, "lib", "Teigha");

                if (Directory.Exists(teighaDir))
                {
                    string path = Environment.GetEnvironmentVariable("PATH") ?? "";
                    if (!path.Contains(teighaDir))
                        Environment.SetEnvironmentVariable("PATH", teighaDir + ";" + path);
                }

                // 初始化 Teigha 运行时服务（必须保持存活直到进程结束）
                _teighaServices = new Teigha.Runtime.Services();
            }
        }

        /// <summary>
        /// 解析 DWG/DXF 文件（离线，无需 AutoCAD）
        /// </summary>
        /// <param name="filePath">DWG/DXF 文件完整路径</param>
        /// <returns>解析结果，包含线段、图层等信息</returns>
        public CadParseResult ParseFile(string filePath)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new CadParseResult();

            try
            {
                if (!File.Exists(filePath))
                {
                    result.Error = $"文件不存在: {filePath}";
                    return result;
                }

                EnsureTeighaInitialized();

                // 使用 Teigha 打开 DWG/DXF 文件
                using (var database = new Database(false, false))
                {
                    database.ReadDwgFile(filePath, FileShare.Read, true, "");

                    // 获取 ModelSpace
                    using (var blockTable = (BlockTable)database.BlockTableId.Open(OpenMode.ForRead))
                    {
                        var modelSpaceId = blockTable[BlockTableRecord.ModelSpace];
                        using (var modelSpace = (BlockTableRecord)modelSpaceId.Open(OpenMode.ForRead))
                        {
                            foreach (ObjectId entityId in modelSpace)
                            {
                                using (var entity = (Entity)entityId.Open(OpenMode.ForRead))
                                {
                                    ProcessEntity(entity, result);
                                }
                            }
                        }
                    }
                }

                result.Success = true;
                result.TotalEntities = result.Entities.Count;

                stopwatch.Stop();
                result.ParseDuration = stopwatch.Elapsed;
            }
            catch (Exception ex)
            {
                result.Error = $"解析失败: {ex.Message}";
                stopwatch.Stop();
                result.ParseDuration = stopwatch.Elapsed;
            }

            return result;
        }

        /// <summary>
        /// 处理单个 CAD 实体，提取线段数据
        /// </summary>
        private void ProcessEntity(Entity entity, CadParseResult result)
        {
            string layer = entity.Layer ?? "0";

            // 记录实体信息
            result.Entities.Add(new CadEntityInfo
            {
                EntityType = entity.GetRXClass().Name,
                Layer = layer
            });

            // 记录图层信息（去重）
            if (!result.Layers.Any(l => l.Name == layer))
            {
                result.Layers.Add(new CadLayerInfo { Name = layer });
            }

            switch (entity)
            {
                case Line line:
                    AddSegmentIfValid(
                        new Point3D(line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z),
                        new Point3D(line.EndPoint.X, line.EndPoint.Y, line.EndPoint.Z),
                        layer, result);
                    break;

                case Polyline pl:
                    ProcessLightweightPolyline(pl, layer, result);
                    break;

                case Polyline2d pl2d:
                    ProcessPolyline2d(pl2d, layer, result);
                    break;

                case Arc arc:
                    ProcessArc(arc, layer, result);
                    break;

                case Circle circle:
                    ProcessCircle(circle, layer, result);
                    break;

                case BlockReference blockRef:
                    ProcessBlockReference(blockRef, layer, result);
                    break;
            }
        }

        /// <summary>处理轻量多段线（LWPOLYLINE）</summary>
        private void ProcessLightweightPolyline(Polyline pl, string layer, CadParseResult result)
        {
            int n = pl.NumberOfVertices;
            for (int i = 0; i < n - 1; i++)
            {
                var p1 = pl.GetPoint3dAt(i);
                var p2 = pl.GetPoint3dAt(i + 1);
                AddSegmentIfValid(
                    new Point3D(p1.X, p1.Y, p1.Z),
                    new Point3D(p2.X, p2.Y, p2.Z),
                    layer, result);
            }
            // 闭合多段线：连接最后一个点到第一个点
            if (pl.Closed && n > 2)
            {
                var pLast = pl.GetPoint3dAt(n - 1);
                var pFirst = pl.GetPoint3dAt(0);
                AddSegmentIfValid(
                    new Point3D(pLast.X, pLast.Y, pLast.Z),
                    new Point3D(pFirst.X, pFirst.Y, pFirst.Z),
                    layer, result);
            }
        }

        /// <summary>处理 2D 多段线（POLYLINE）</summary>
        private void ProcessPolyline2d(Polyline2d pl2d, string layer, CadParseResult result)
        {
            var points = new List<Point3D>();
            foreach (ObjectId vId in pl2d)
            {
                using (var vertex = (Vertex2d)vId.Open(OpenMode.ForRead))
                {
                    var pos = vertex.Position;
                    points.Add(new Point3D(pos.X, pos.Y, pos.Z));
                }
            }

            for (int i = 0; i < points.Count - 1; i++)
                AddSegmentIfValid(points[i], points[i + 1], layer, result);

            if (pl2d.Closed && points.Count > 2)
                AddSegmentIfValid(points[points.Count - 1], points[0], layer, result);
        }

        /// <summary>处理圆弧（离散化为线段）</summary>
        private void ProcessArc(Arc arc, string layer, CadParseResult result)
        {
            int segments = 16;
            double startAngle = arc.StartAngle;
            double endAngle = arc.EndAngle;
            if (endAngle < startAngle) endAngle += 2 * Math.PI;
            double angleStep = (endAngle - startAngle) / segments;

            Point3D prev = null;
            for (int i = 0; i <= segments; i++)
            {
                double angle = startAngle + i * angleStep;
                double x = arc.Center.X + arc.Radius * Math.Cos(angle);
                double y = arc.Center.Y + arc.Radius * Math.Sin(angle);
                double z = arc.Center.Z;
                var pt = new Point3D(x, y, z);
                if (prev != null)
                    AddSegmentIfValid(prev, pt, layer, result);
                prev = pt;
            }
        }

        /// <summary>处理圆（离散化为线段）</summary>
        private void ProcessCircle(Circle circle, string layer, CadParseResult result)
        {
            int segments = 24;
            Point3D prev = null;
            for (int i = 0; i <= segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                double x = circle.Center.X + circle.Radius * Math.Cos(angle);
                double y = circle.Center.Y + circle.Radius * Math.Sin(angle);
                double z = circle.Center.Z;
                var pt = new Point3D(x, y, z);
                if (prev != null)
                    AddSegmentIfValid(prev, pt, layer, result);
                prev = pt;
            }
        }

        /// <summary>处理块参照（展开内部实体）</summary>
        private void ProcessBlockReference(BlockReference blockRef, string layer, CadParseResult result)
        {
            try
            {
                // 展开块参照获取内部实体
                var exploded = new Teigha.DatabaseServices.DBObjectCollection();
                blockRef.Explode(exploded);

                foreach (DBObject obj in exploded)
                {
                    if (obj is Entity innerEntity)
                    {
                        ProcessEntity(innerEntity, result);
                    }
                    obj.Dispose();
                }
            }
            catch
            {
                // 块展开失败时跳过
            }
        }

        /// <summary>如果线段长度满足最小要求，添加到结果中</summary>
        private void AddSegmentIfValid(Point3D start, Point3D end, string layer, CadParseResult result)
        {
            if (start.DistanceTo(end) >= _config.MinSegmentLength)
            {
                result.WallSegments.Add(new LineSegment(start, end, layer));
            }
        }

        /// <summary>
        /// 从坐标字符串解析线段
        /// 格式：[(x1,y1,z1),(x2,y2,z2)],[(x3,y3,z3),(x4,y4,z4)],...
        /// </summary>
        /// <param name="pathsString">坐标字符串</param>
        /// <returns>线段列表</returns>
        public static List<LineSegment> ParsePathsString(string pathsString)
        {
            var segments = new List<LineSegment>();

            if (string.IsNullOrWhiteSpace(pathsString))
                return segments;

            try
            {
                // 移除空格
                pathsString = pathsString.Replace(" ", "");

                // 按 ],[ 分割线段
                var segmentStrings = pathsString.Split(new[] { "],[" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var segStr in segmentStrings)
                {
                    // 清理字符串
                    var clean = segStr.Trim('[', ']');

                    // 分割两个点
                    var pointStrings = clean.Split(new[] { "),(" }, StringSplitOptions.RemoveEmptyEntries);

                    if (pointStrings.Length >= 2)
                    {
                        var startStr = pointStrings[0].Trim('(', ')');
                        var endStr = pointStrings[1].Trim('(', ')');

                        var startCoords = startStr.Split(',');
                        var endCoords = endStr.Split(',');

                        if (startCoords.Length >= 2 && endCoords.Length >= 2)
                        {
                            double.TryParse(startCoords[0], out double x1);
                            double.TryParse(startCoords[1], out double y1);
                            double.TryParse(startCoords.Length > 2 ? startCoords[2] : "0", out double z1);

                            double.TryParse(endCoords[0], out double x2);
                            double.TryParse(endCoords[1], out double y2);
                            double.TryParse(endCoords.Length > 2 ? endCoords[2] : "0", out double z2);

                            segments.Add(new LineSegment(
                                new Point3D(x1, y1, z1),
                                new Point3D(x2, y2, z2)
                            ));
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 解析失败返回空列表
            }

            return segments;
        }

        /// <summary>
        /// 合并共线线段
        /// </summary>
        /// <param name="segments">原始线段列表</param>
        /// <param name="tolerance">角度容差（度）</param>
        /// <returns>合并后的线段列表</returns>
        public static List<LineSegment> MergeCollinearSegments(List<LineSegment> segments, double tolerance = 5.0)
        {
            if (segments == null || segments.Count <= 1)
                return segments ?? new List<LineSegment>();

            var result = new List<LineSegment>();
            var used = new bool[segments.Count];

            for (int i = 0; i < segments.Count; i++)
            {
                if (used[i]) continue;

                var current = segments[i];
                bool merged = true;

                while (merged)
                {
                    merged = false;
                    for (int j = i + 1; j < segments.Count; j++)
                        {
                        if (used[j]) continue;

                        if (AreCollinear(current, segments[j], tolerance))
                        {
                            // 合并线段
                            current = MergeTwoSegments(current, segments[j]);
                            used[j] = true;
                            merged = true;
                        }
                    }
                }

                result.Add(current);
            }

            return result;
        }

        /// <summary>
        /// 判断两条线段是否共线
        /// </summary>
        private static bool AreCollinear(LineSegment a, LineSegment b, double angleTolerance)
        {
            // 计算两条线段的方向向量
            var dir1 = new Point3D(a.End.X - a.Start.X, a.End.Y - a.Start.Y, a.End.Z - a.Start.Z);
            var dir2 = new Point3D(b.End.X - b.Start.X, b.End.Y - b.Start.Y, b.End.Z - b.Start.Z);

            // 计算长度
            double len1 = Math.Sqrt(dir1.X * dir1.X + dir1.Y * dir1.Y + dir1.Z * dir1.Z);
            double len2 = Math.Sqrt(dir2.X * dir2.X + dir2.Y * dir2.Y + dir2.Z * dir2.Z);

            if (len1 < 0.001 || len2 < 0.001)
                return false;

            // 归一化
            dir1 = new Point3D(dir1.X / len1, dir1.Y / len1, dir1.Z / len1);
            dir2 = new Point3D(dir2.X / len2, dir2.Y / len2, dir2.Z / len2);

            // 计算点积
            double dot = dir1.X * dir2.X + dir1.Y * dir2.Y + dir1.Z * dir2.Z;

            // 计算角度（弧度）
            double angle = Math.Acos(Math.Min(1, Math.Abs(dot)));

            // 转换为度
            double angleDeg = angle * 180 / Math.PI;

            return angleDeg < angleTolerance;
        }

        /// <summary>
        /// 合并两条共线线段
        /// </summary>
        private static LineSegment MergeTwoSegments(LineSegment a, LineSegment b)
        {
            // 找到最远的两个端点
            var points = new[] { a.Start, a.End, b.Start, b.End };

            double maxDist = 0;
            Point3D p1 = a.Start, p2 = a.End;

            for (int i = 0; i < 4; i++)
            {
                for (int j = i + 1; j < 4; j++)
                {
                    double dist = points[i].DistanceTo(points[j]);
                    if (dist > maxDist)
                    {
                        maxDist = dist;
                        p1 = points[i];
                        p2 = points[j];
                    }
                }
            }

            return new LineSegment(p1, p2, a.Layer);
        }
    }
}
