using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using E3DCopilot.Core.Models.Geometry;
using E3DCopilot.Core.Models.Building;

namespace E3DCopilot.Core.Services.Cad
{
    #region COM MessageFilter（解决 RPC_E_SERVERCALL_RETRYLATER）

    /// <summary>
    /// COM IMessageFilter 实现 — 当 AutoCAD 忙时自动等待重试
    /// 解决 0x8001010A (RPC_E_SERVERCALL_RETRYLATER) 错误
    /// </summary>
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("00000016-0000-0000-C000-000000000046")]
    internal interface IMessageFilter
    {
        [PreserveSig]
        int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);

        [PreserveSig]
        int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);

        [PreserveSig]
        int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
    }

    /// <summary>
    /// 自动重试的 MessageFilter：遇到 SERVERCALL_RETRYLATER 时等待后重试
    /// </summary>
    internal class RetryMessageFilter : IMessageFilter
    {
        private const int SERVERCALL_RETRYLATER = 2;
        private const int PENDINGTYPE_NESTED = 2;

        public int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo)
        {
            return 0; // SERVERCALL_ISHANDLED
        }

        public int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
        {
            if (dwRejectType == SERVERCALL_RETRYLATER)
            {
                // AutoCAD 忙，等待 200ms 后重试（返回 -1 则取消调用）
                // 超过 30 秒则放弃
                if (dwTickCount < 30000)
                    return 200;
            }
            return -1; // 取消调用
        }

        public int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType)
        {
            return 2; // PENDINGMSG_WAITDEFPROCESS
        }
    }

    #endregion
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
        private static bool _messageFilterRegistered;

        [DllImport("ole32.dll")]
        private static extern int CoRegisterMessageFilter(IMessageFilter newFilter, out IMessageFilter oldFilter);

        /// <summary>
        /// 连接状态
        /// </summary>
        public AutoCadConnectionStatus Status => _status;

        /// <summary>
        /// 当前活动文档名称
        /// </summary>
        public string ActiveDocumentName => _activeDoc?.Name;

        /// <summary>
        /// 当前活动文档的完整磁盘路径（Document.FullName）
        /// </summary>
        public string ActiveDocumentPath
        {
            get
            {
                try { return _activeDoc?.FullName; }
                catch { return null; }
            }
        }

        /// <summary>
        /// 连接到 AutoCAD
        /// </summary>
        /// <returns>是否连接成功</returns>
        public bool Connect()
        {
            try
            {
                // 注册 COM MessageFilter（解决 AutoCAD 忙时的 RPC_E_SERVERCALL_RETRYLATER）
                RegisterMessageFilter();

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
        /// 注册 COM MessageFilter（进程级单例，只需注册一次）
        /// </summary>
        private static void RegisterMessageFilter()
        {
            if (_messageFilterRegistered) return;
            try
            {
                CoRegisterMessageFilter(new RetryMessageFilter(), out _);
                _messageFilterRegistered = true;
            }
            catch
            {
                // 注册失败不影响主流程（只是没有自动重试能力）
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
        /// 获取用户选择的对象（通过 COM SelectionSets API）
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

            const string setName = "E3DCopilot_SelSet";
            dynamic selectionSet = null;

            try
            {
                // COM 自动化：使用 SelectionSets 集合获取用户屏幕选择
                var selectionSets = _activeDoc.SelectionSets;

                // 清理同名选择集（防止重复创建报错）
                try
                {
                    var existing = selectionSets.Item(setName);
                    existing.Delete();
                }
                catch { /* 不存在则忽略 */ }

                selectionSet = selectionSets.Add(setName);

                // 提示用户在 AutoCAD 中框选对象（阻塞直到用户完成选择）
                selectionSet.SelectOnScreen();

                int count = selectionSet.Count;
                result.TotalEntities = count;

                if (count == 0)
                {
                    result.Error = "用户未选择任何对象";
                    return result;
                }

                // 遍历选中的对象
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        var entity = selectionSet.Item(i);

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
            finally
            {
                // 清理选择集
                try { selectionSet?.Delete(); } catch { }
            }

            return result;
        }

        /// <summary>
        /// 获取模型空间中的所有对象（带重试机制，应对 AutoCAD 忙时拒绝 COM 调用）
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

            // 带重试的 COM 调用（应对 RPC_E_SERVERCALL_RETRYLATER）
            const int maxRetries = 5;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return DoGetAllModelSpaceObjects(layerFilter);
                }
                catch (COMException ex) when (ex.HResult == unchecked((int)0x8001010A) && attempt < maxRetries)
                {
                    // AutoCAD 忙，等待后重试
                    int waitMs = 500 * (attempt + 1);
                    System.Diagnostics.Debug.WriteLine($"AutoCAD 忙 (RPC_E_SERVERCALL_RETRYLATER)，{waitMs}ms 后重试 ({attempt + 1}/{maxRetries})");
                    Thread.Sleep(waitMs);
                }
                catch (Exception ex)
                {
                    result.Error = $"获取模型空间对象失败: {ex.Message}";
                    return result;
                }
            }

            result.Error = "获取模型空间对象失败: AutoCAD 持续忙碌，请确保 AutoCAD 没有弹出对话框或正在执行命令，然后重试";
            return result;
        }

        /// <summary>
        /// 实际执行 ModelSpace 遍历
        /// </summary>
        private AutoCadExtractResult DoGetAllModelSpaceObjects(List<string> layerFilter)
        {
            var result = new AutoCadExtractResult();

            // COM 自动化：直接通过 Document.ModelSpace 访问（标准 COM 路径）
            var modelSpace = _activeDoc.ModelSpace;

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
                case "AcDb2dPolyline":
                case "AcDb3dPolyline":
                    var pl = entity;
                    // COM 自动化：通过 Coordinates 属性获取顶点坐标（返回 double 数组）
                    try
                    {
                        var coords = (double[])pl.Coordinates;
                        // 2D 多段线：每 2 个值为一个顶点 (x, y)
                        for (int i = 0; i + 1 < coords.Length; i += 2)
                        {
                            info.Points.Add(new Point3D(coords[i], coords[i + 1], 0));
                        }
                    }
                    catch
                    {
                        // 3D 多段线或 Coordinates 不可用时，尝试逐顶点获取
                        int nVerts = pl.NumberOfVertices;
                        for (int i = 0; i < nVerts; i++)
                        {
                            try
                            {
                                var pt = pl.Coordinate(i);
                                info.Points.Add(new Point3D(pt[0], pt[1], pt.Length > 2 ? pt[2] : 0));
                            }
                            catch { break; }
                        }
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
