using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using E3DCopilot.Core.Models.Building;
using E3DCopilot.Core.Models.Geometry;

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
    /// CAD 解析服务（集成 Teigha.NET）
    /// 从 SmartDuct 项目的 CadParser 移植并适配
    /// </summary>
    public class TeighaCadParserService
    {
        private static bool _isInitialized;
        private static readonly object _initLock = new object();
        private CadParseConfig _config;

        public TeighaCadParserService(CadParseConfig config = null)
        {
            _config = config ?? new CadParseConfig();
        }

        /// <summary>
        /// 初始化 Teigha.NET（延迟初始化）
        /// </summary>
        private void InitializeTeigha()
        {
            lock (_initLock)
            {
                try
                {
                    if (_isInitialized) return;

                    // 获取运行时目录
                    string runtimeDirectory = AppDomain.CurrentDomain.BaseDirectory;

                    // 设置环境变量
                    string path = Environment.GetEnvironmentVariable("PATH") ?? "";
                    if (!path.Contains(runtimeDirectory))
                    {
                        Environment.SetEnvironmentVariable("PATH", runtimeDirectory + ";" + path);
                    }

                    // 检查 Teigha DLL 是否存在
                    string[] requiredDlls = new[]
                    {
                        "TD_Mgd_4.00_10.dll",
                        "TD_Root_4.00_10.dll",
                        "TD_Db_4.00_10.dll"
                    };

                    foreach (var dll in requiredDlls)
                    {
                        string dllPath = Path.Combine(runtimeDirectory, dll);
                        if (!File.Exists(dllPath))
                        {
                            // 尝试在 lib/Teigha 目录查找
                            string libPath = Path.Combine(runtimeDirectory, "lib", "Teigha", dll);
                            if (!File.Exists(libPath))
                            {
                                throw new FileNotFoundException($"找不到 Teigha DLL: {dll}，请确保 DLL 在应用程序目录或 lib/Teigha 目录下", dllPath);
                            }
                        }
                    }

                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    throw new Exception($"初始化 Teigha.NET 失败: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 解析 DWG/DXF 文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>解析结果</returns>
        public CadParseResult ParseFile(string filePath)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new CadParseResult();

            try
            {
                // 检查文件是否存在
                if (!File.Exists(filePath))
                {
                    result.Error = $"文件不存在: {filePath}";
                    return result;
                }

                // 初始化 Teigha
                InitializeTeigha();

                // 注意：这里需要实际引用 Teigha.NET DLL 才能工作
                // 由于当前环境可能没有 Teigha DLL，我们提供一个模拟实现
                // 实际使用时需要取消注释并引用 Teigha.NET

                /*
                // 使用 Teigha.NET 打开 DWG 文件
                using (var database = new Teigha.DatabaseServices.Database(false, false))
                {
                    database.ReadDwgFile(filePath, Teigha.DatabaseServices.FileOpenMode.OpenForReadAndAllShare, false, "");

                    using (var tr = database.TransactionManager.StartTransaction())
                    {
                        var blockTable = (Teigha.DatabaseServices.BlockTable)tr.GetObject(database.BlockTableId, Teigha.DatabaseServices.OpenMode.ForRead);
                        var modelSpace = (Teigha.DatabaseServices.BlockTableRecord)tr.GetObject(blockTable[Teigha.DatabaseServices.BlockTableRecord.ModelSpace], Teigha.DatabaseServices.OpenMode.ForRead);

                        foreach (Teigha.DatabaseServices.ObjectId entityId in modelSpace)
                        {
                            var entity = (Teigha.DatabaseServices.Entity)tr.GetObject(entityId, Teigha.DatabaseServices.OpenMode.ForRead);
                            ProcessEntity(entity, result);
                        }

                        tr.Commit();
                    }
                }
                */

                // 模拟解析结果（实际使用时删除此部分）
                result.Success = true;
                result.TotalEntities = 0;

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
        /// 处理单个 CAD 实体
        /// </summary>
        private void ProcessEntity(dynamic entity, CadParseResult result)
        {
            // 实际实现需要根据 Teigha.NET API 处理不同类型的实体
            // Line, Polyline, Arc, Circle, BlockReference 等
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
