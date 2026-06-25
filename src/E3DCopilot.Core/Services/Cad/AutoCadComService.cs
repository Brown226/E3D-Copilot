using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using E3DCopilot.Core.Models.Geometry;
using E3DCopilot.Core.Models.Building;

namespace E3DCopilot.Core.Services.Cad
{
    /// <summary>
    /// AutoCAD 连接状态
    /// </summary>
    public enum AutoCadConnectionStatus
    {
        /// <summary>
        /// 未连接
        /// </summary>
        Disconnected,
        /// <summary>
        /// 已连接
        /// </summary>
        Connected,
        /// <summary>
        /// 连接失败
        /// </summary>
        Error
    }

    /// <summary>
    /// AutoCAD 实体信息
    /// </summary>
    public class AutoCadEntityInfo
    {
        public string Handle { get; set; }
        public string EntityType { get; set; }
        public string Layer { get; set; }
        public List<Point3D> Points { get; set; }
        public Dictionary<string, object> Properties { get; set; }

        public AutoCadEntityInfo()
        {
            Points = new List<Point3D>();
            Properties = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// AutoCAD 提取结果
    /// </summary>
    public class AutoCadExtractResult
    {
        public bool Success { get; set; }
        public List<AutoCadEntityInfo> Entities { get; set; }
        public List<LineSegment> Segments { get; set; }
        public int TotalEntities { get; set; }
        public string Error { get; set; }
        public string DrawingName { get; set; }

        public AutoCadExtractResult()
        {
            Entities = new List<AutoCadEntityInfo>();
            Segments = new List<LineSegment>();
        }
    }

    /// <summary>
    /// AutoCAD COM 自动化服务
    /// 通过 COM 接口连接运行中的 AutoCAD 应用程序
    /// </summary>
    public class AutoCadComService
    {
        private dynamic _acadApp;
        private dynamic _activeDoc;
        private AutoCadConnectionStatus _status = AutoCadConnectionStatus.Disconnected;

        /// <summary>
        /// 连接状态
        /// </summary>
        public AutoCadConnectionStatus Status => _status;

        /// <summary>
        /// 当前活动文档名称
        /// </summary>
        public string ActiveDocumentName => _activeDoc?.Name;

        /// <summary>
        /// 连接到 AutoCAD
        /// </summary>
        /// <returns>是否连接成功</returns>
        public bool Connect()
        {
            try
            {
                // 尝试获取正在运行的 AutoCAD 实例
                _acadApp = Marshal.GetActiveObject("AutoCAD.Application");
                _activeDoc = _acadApp.ActiveDocument;
                _status = AutoCadConnectionStatus.Connected;
                return true;
            }
            catch (COMException)
            {
                // AutoCAD 未运行
                _status = AutoCadConnectionStatus.Error;
                return false;
            }
            catch (Exception)
            {
                _status = AutoCadConnectionStatus.Error;
                return false;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            _activeDoc = null;
            _acadApp = null;
            _status = AutoCadConnectionStatus.Disconnected;
        }

        /// <summary>
        /// 获取用户选择的对象
        /// </summary>
        /// <returns>提取结果</returns>
        public AutoCadExtractResult GetSelectedObjects()
        {
            var result = new AutoCadExtractResult();

            if (_status != AutoCadConnectionStatus.Connected || _activeDoc == null)
            {
                result.Error = "未连接到 AutoCAD，请先调用 Connect()";
                return result;
            }

            try
            {
                // 获取编辑器
                var editor = _activeDoc.Editor;

                // 提示用户选择对象
                var promptResult = editor.GetSelection();

                if (promptResult.Status != 0) // 0 = OK
                {
                    result.Error = "用户取消选择或未选择任何对象";
                    return result;
                }

                // 获取选择集
                var selectionSet = promptResult.Value;
                result.TotalEntities = selectionSet.Count;

                // 遍历选中的对象
                for (int i = 0; i < selectionSet.Count; i++)
                {
                    try
                    {
                        var selectedObj = selectionSet.Item(i);
                        var entity = selectedObj.ObjectId.GetObject(0); // 0 = OpenMode.ForRead

                        var entityInfo = ExtractEntityInfo(entity);
                        if (entityInfo != null)
                        {
                            result.Entities.Add(entityInfo);

                            // 提取线段
                            if (entityInfo.Points.Count >= 2)
                            {
                                for (int j = 0; j < entityInfo.Points.Count - 1; j++)
                                {
                                    result.Segments.Add(new LineSegment(
                                        entityInfo.Points[j],
                                        entityInfo.Points[j + 1],
                                        entityInfo.Layer
                                    ));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // 跳过无法处理的实体
                        System.Diagnostics.Debug.WriteLine($"提取实体失败: {ex.Message}");
                    }
                }

                result.Success = true;
                result.DrawingName = _activeDoc.Name;
            }
            catch (Exception ex)
            {
                result.Error = $"获取选择对象失败: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 获取模型空间中的所有对象
        /// </summary>
        /// <param name="layerFilter">图层过滤器（可选）</param>
        /// <returns>提取结果</returns>
        public AutoCadExtractResult GetAllModelSpaceObjects(List<string> layerFilter = null)
        {
            var result = new AutoCadExtractResult();

            if (_status != AutoCadConnectionStatus.Connected || _activeDoc == null)
            {
                result.Error = "未连接到 AutoCAD";
                return result;
            }

            try
            {
                var database = _activeDoc.Database;
                var modelSpace = database.ModelSpace;

                result.TotalEntities = modelSpace.Count;

                for (int i = 0; i < modelSpace.Count; i++)
                {
                    try
                    {
                        var entity = modelSpace.Item(i);

                        // 图层过滤
                        if (layerFilter != null && layerFilter.Count > 0)
                        {
                            if (!layerFilter.Contains(entity.Layer))
                                continue;
                        }

                        var entityInfo = ExtractEntityInfo(entity);
                        if (entityInfo != null)
                        {
                            result.Entities.Add(entityInfo);

                            if (entityInfo.Points.Count >= 2)
                            {
                                for (int j = 0; j < entityInfo.Points.Count - 1; j++)
                                {
                                    result.Segments.Add(new LineSegment(
                                        entityInfo.Points[j],
                                        entityInfo.Points[j + 1],
                                        entityInfo.Layer
                                    ));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"提取实体失败: {ex.Message}");
                    }
                }

                result.Success = true;
                result.DrawingName = _activeDoc.Name;
            }
            catch (Exception ex)
            {
                result.Error = $"获取模型空间对象失败: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 提取实体信息
        /// </summary>
        private AutoCadEntityInfo ExtractEntityInfo(dynamic entity)
        {
            var info = new AutoCadEntityInfo
            {
                Handle = entity.Handle,
                EntityType = entity.EntityName,
                Layer = entity.Layer
            };

            switch (entity.EntityName)
            {
                case "AcDbLine":
                    var line = entity;
                    info.Points.Add(new Point3D(line.StartPoint[0], line.StartPoint[1], line.StartPoint[2]));
                    info.Points.Add(new Point3D(line.EndPoint[0], line.EndPoint[1], line.EndPoint[2]));
                    info.Properties["Length"] = line.Length;
                    break;

                case "AcDbPolyline":
                    var pl = entity;
                    for (int i = 0; i < pl.NumberOfVertices; i++)
                    {
                        var pt = pl.GetPointAt(i);
                        info.Points.Add(new Point3D(pt[0], pt[1], 0));
                    }
                    if (pl.Closed && info.Points.Count > 0)
                    {
                        info.Points.Add(info.Points[0]); // 闭合
                    }
                    info.Properties["Closed"] = pl.Closed;
                    break;

                case "AcDbArc":
                    var arc = entity;
                    // 将弧线离散化为线段
                    int segments = 10;
                    double startAngle = arc.StartAngle;
                    double endAngle = arc.EndAngle;
                    if (endAngle < startAngle) endAngle += 2 * Math.PI;
                    double angleStep = (endAngle - startAngle) / segments;

                    for (int i = 0; i <= segments; i++)
                    {
                        double angle = startAngle + i * angleStep;
                        double x = arc.Center[0] + arc.Radius * Math.Cos(angle);
                        double y = arc.Center[1] + arc.Radius * Math.Sin(angle);
                        double z = arc.Center[2];
                        info.Points.Add(new Point3D(x, y, z));
                    }
                    info.Properties["Radius"] = arc.Radius;
                    break;

                case "AcDbCircle":
                    var circle = entity;
                    // 将圆离散化为线段
                    int circleSegments = 20;
                    for (int i = 0; i <= circleSegments; i++)
                    {
                        double angle = 2 * Math.PI * i / circleSegments;
                        double x = circle.Center[0] + circle.Radius * Math.Cos(angle);
                        double y = circle.Center[1] + circle.Radius * Math.Sin(angle);
                        double z = circle.Center[2];
                        info.Points.Add(new Point3D(x, y, z));
                    }
                    info.Properties["Radius"] = circle.Radius;
                    break;

                case "AcDbBlockReference":
                    var blockRef = entity;
                    info.Points.Add(new Point3D(blockRef.InsertionPoint[0], blockRef.InsertionPoint[1], blockRef.InsertionPoint[2]));
                    info.Properties["BlockName"] = blockRef.Name;
                    info.Properties["Rotation"] = blockRef.Rotation;
                    break;

                default:
                    // 不支持的实体类型，跳过
                    return null;
            }

            return info;
        }

        /// <summary>
        /// 检查 AutoCAD 是否正在运行
        /// </summary>
        public static bool IsAutoCadRunning()
        {
            try
            {
                var app = Marshal.GetActiveObject("AutoCAD.Application");
                Marshal.ReleaseComObject(app);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
