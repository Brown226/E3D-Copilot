using System;
using System.Collections.Generic;
using System.Linq;
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
    /// AutoCAD 操作结果（用于写操作：SendCommand / CreateEntity / SetLayer 等）
    /// </summary>
    public class AutoCadOperationResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        /// <summary>创建的实体 Handle（CreateEntity/AddText/AddDimension 用）</summary>
        public string Handle { get; set; }
        /// <summary>附加数据（如图层列表、实体属性等）</summary>
        public Dictionary<string, object> Data { get; set; }

        public AutoCadOperationResult()
        {
            Data = new Dictionary<string, object>();
        }

        public static AutoCadOperationResult Ok(string handle = null)
        {
            return new AutoCadOperationResult { Success = true, Handle = handle };
        }

        public static AutoCadOperationResult Fail(string error)
        {
            return new AutoCadOperationResult { Success = false, Error = error };
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

        #region 写操作（P0 + P1）

        /// <summary>
        /// COM 调用重试包装（应对 AutoCAD 忙时的 RPC_E_SERVERCALL_RETRYLATER）
        /// </summary>
        private T WithRetry<T>(Func<T> action, string operationName)
        {
            const int maxRetries = 5;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return action();
                }
                catch (COMException ex) when (ex.HResult == unchecked((int)0x8001010A) && attempt < maxRetries)
                {
                    int waitMs = 500 * (attempt + 1);
                    System.Diagnostics.Debug.WriteLine($"AutoCAD 忙 ({operationName})，{waitMs}ms 后重试 ({attempt + 1}/{maxRetries})");
                    Thread.Sleep(waitMs);
                }
            }
            throw new COMException($"AutoCAD 持续忙碌，{operationName} 失败（重试 {maxRetries} 次后放弃）", unchecked((int)0x8001010A));
        }

        /// <summary>
        /// 确保已连接，否则返回失败结果
        /// </summary>
        private AutoCadOperationResult EnsureConnected(string operationName)
        {
            if (_status != AutoCadConnectionStatus.Connected || _activeDoc == null)
            {
                return AutoCadOperationResult.Fail($"未连接到 AutoCAD，无法执行 {operationName}（请先调用 Connect()）");
            }
            return null; // 已连接
        }

        #region P0: SendCommand / CreateEntity / AddText

        /// <summary>
        /// 发送 AutoCAD 命令行字符串
        /// 注意：命令字符串必须以空格或换行结尾，否则 AutoCAD 会等待更多输入
        /// 示例："_LINE 100,100 200,200 "（末尾空格表示回车）
        /// </summary>
        /// <param name="command">命令字符串（自动补全末尾空格）</param>
        /// <returns>操作结果</returns>
        public AutoCadOperationResult SendCommand(string command)
        {
            var connCheck = EnsureConnected("SendCommand");
            if (connCheck != null) return connCheck;

            if (string.IsNullOrWhiteSpace(command))
                return AutoCadOperationResult.Fail("命令字符串为空");

            try
            {
                // 命令字符串末尾必须有空格或换行（COM SendCommand 约定）
                if (!command.EndsWith(" ") && !command.EndsWith("\n") && !command.EndsWith("\r"))
                    command += " ";

                return WithRetry(() =>
                {
                    _activeDoc.SendCommand(command);
                    return AutoCadOperationResult.Ok();
                }, "SendCommand");
            }
            catch (Exception ex)
            {
                return AutoCadOperationResult.Fail($"发送命令失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建实体（Line / Circle / Polyline / Arc / BlockReference）
        /// </summary>
        /// <param name="entityType">实体类型（大小写不敏感）：Line / Circle / Polyline / Arc / Text</param>
        /// <param name="points">坐标点数组</param>
        /// <param name="layer">图层名（null 则用当前图层）</param>
        /// <param name="properties">附加属性（如 Circle 的 Radius、Arc 的 Radius/StartAngle/EndAngle）</param>
        /// <returns>操作结果，Handle 为创建的实体 Handle</returns>
        public AutoCadOperationResult CreateEntity(string entityType, List<Point3D> points,
                                                    string layer = null,
                                                    Dictionary<string, object> properties = null)
        {
            var connCheck = EnsureConnected("CreateEntity");
            if (connCheck != null) return connCheck;

            if (string.IsNullOrWhiteSpace(entityType))
                return AutoCadOperationResult.Fail("实体类型为空");
            if (points == null || points.Count == 0)
                return AutoCadOperationResult.Fail("坐标点列表为空");

            try
            {
                return WithRetry(() =>
                {
                    var modelSpace = _activeDoc.ModelSpace;
                    dynamic entity = null;
                    string typeUpper = entityType.ToUpperInvariant();

                    switch (typeUpper)
                    {
                        case "LINE":
                            if (points.Count < 2)
                                return AutoCadOperationResult.Fail("Line 需要 2 个点");
                            entity = modelSpace.AddLine(points[0].ToArray(), points[1].ToArray());
                            break;

                        case "CIRCLE":
                            if (points.Count < 1)
                                return AutoCadOperationResult.Fail("Circle 需要 1 个点（圆心）");
                            double radius = 50.0; // 默认半径
                            if (properties != null && properties.ContainsKey("Radius"))
                                radius = Convert.ToDouble(properties["Radius"]);
                            entity = modelSpace.AddCircle(points[0].ToArray(), radius);
                            break;

                        case "POLYLINE":
                            if (points.Count < 2)
                                return AutoCadOperationResult.Fail("Polyline 至少需要 2 个点");
                            // 使用 AddLightWeightPolyline（2D，扁平坐标 [x1,y1,x2,y2,...]）
                            var flatCoords = new List<double>();
                            foreach (var pt in points)
                            {
                                flatCoords.Add(pt.X);
                                flatCoords.Add(pt.Y);
                            }
                            entity = modelSpace.AddLightWeightPolyline(flatCoords.ToArray());
                            break;

                        case "ARC":
                            if (points.Count < 1)
                                return AutoCadOperationResult.Fail("Arc 需要 1 个点（圆心）");
                            double arcRadius = 50.0;
                            double startAngle = 0.0;
                            double endAngle = Math.PI;
                            if (properties != null)
                            {
                                if (properties.ContainsKey("Radius"))
                                    arcRadius = Convert.ToDouble(properties["Radius"]);
                                if (properties.ContainsKey("StartAngle"))
                                    startAngle = Convert.ToDouble(properties["StartAngle"]);
                                if (properties.ContainsKey("EndAngle"))
                                    endAngle = Convert.ToDouble(properties["EndAngle"]);
                            }
                            entity = modelSpace.AddArc(points[0].ToArray(), arcRadius, startAngle, endAngle);
                            break;

                        case "TEXT":
                            if (points.Count < 1)
                                return AutoCadOperationResult.Fail("Text 需要 1 个点（插入点）");
                            string textContent = properties != null && properties.ContainsKey("Text")
                                ? properties["Text"].ToString() : "";
                            double textHeight = properties != null && properties.ContainsKey("Height")
                                ? Convert.ToDouble(properties["Height"]) : 3.0;
                            entity = modelSpace.AddText(textContent, points[0].ToArray(), textHeight);
                            break;

                        default:
                            return AutoCadOperationResult.Fail($"不支持的实体类型: {entityType}（支持 Line/Circle/Polyline/Arc/Text）");
                    }

                    // 设置图层
                    if (!string.IsNullOrEmpty(layer) && entity != null)
                    {
                        entity.Layer = layer;
                    }

                    string handle = entity?.Handle;
                    return AutoCadOperationResult.Ok(handle);
                }, $"CreateEntity({entityType})");
            }
            catch (Exception ex)
            {
                return AutoCadOperationResult.Fail($"创建实体失败 ({entityType}): {ex.Message}");
            }
        }

        /// <summary>
        /// 添加文字
        /// </summary>
        /// <param name="position">插入点</param>
        /// <param name="content">文字内容</param>
        /// <param name="height">文字高度（mm）</param>
        /// <param name="layer">图层名（null 则用当前图层）</param>
        /// <param name="rotation">旋转角度（度，0=水平）</param>
        /// <returns>操作结果，Handle 为创建的文字实体 Handle</returns>
        public AutoCadOperationResult AddText(Point3D position, string content, double height,
                                              string layer = null, double rotation = 0)
        {
            var connCheck = EnsureConnected("AddText");
            if (connCheck != null) return connCheck;

            if (position == null)
                return AutoCadOperationResult.Fail("插入点为空");
            if (string.IsNullOrEmpty(content))
                return AutoCadOperationResult.Fail("文字内容为空");
            if (height <= 0)
                return AutoCadOperationResult.Fail($"文字高度必须大于 0，当前: {height}");

            try
            {
                return WithRetry(() =>
                {
                    var modelSpace = _activeDoc.ModelSpace;
                    var entity = modelSpace.AddText(content, position.ToArray(), height);

                    // 设置旋转角度（弧度）
                    if (Math.Abs(rotation) > 0.001)
                    {
                        entity.Rotation = rotation * Math.PI / 180.0;
                    }

                    if (!string.IsNullOrEmpty(layer))
                    {
                        entity.Layer = layer;
                    }

                    return AutoCadOperationResult.Ok(entity.Handle);
                }, "AddText");
            }
            catch (Exception ex)
            {
                return AutoCadOperationResult.Fail($"添加文字失败: {ex.Message}");
            }
        }

        #endregion

        #region P1: SetLayer / AddDimension / SaveDrawing / GetLayers

        /// <summary>
        /// 创建或修改图层
        /// </summary>
        /// <param name="name">图层名</param>
        /// <param name="color">ACI 颜色号（1-255，null 不修改）</param>
        /// <param name="linetype">线型名（null 不修改）</param>
        /// <param name="frozen">是否冻结（null 不修改）</param>
        /// <param name="locked">是否锁定（null 不修改）</param>
        /// <returns>操作结果</returns>
        public AutoCadOperationResult SetLayer(string name, int? color = null, string linetype = null,
                                               bool? frozen = null, bool? locked = null)
        {
            var connCheck = EnsureConnected("SetLayer");
            if (connCheck != null) return connCheck;

            if (string.IsNullOrWhiteSpace(name))
                return AutoCadOperationResult.Fail("图层名为空");

            // 保护系统图层
            if (name.Equals("0", StringComparison.Ordinal) ||
                name.Equals("DEFPOINTS", StringComparison.OrdinalIgnoreCase))
            {
                return AutoCadOperationResult.Fail($"不允许修改系统图层: {name}");
            }

            try
            {
                return WithRetry(() =>
                {
                    var layers = _activeDoc.Layers;
                    dynamic layer;

                    // 尝试获取已存在的图层，不存在则创建
                    try
                    {
                        layer = layers.Item(name);
                    }
                    catch
                    {
                        layer = layers.Add(name);
                    }

                    if (color.HasValue && color.Value >= 0 && color.Value <= 256)
                    {
                        layer.Color = color.Value;
                    }

                    if (!string.IsNullOrEmpty(linetype))
                    {
                        // 设置线型前需确保线型已加载
                        try { _activeDoc.Linetypes.Load(linetype, "acad.lin"); } catch { /* 已加载 */ }
                        layer.Linetype = linetype;
                    }

                    if (frozen.HasValue)
                    {
                        layer.Freeze = frozen.Value;
                    }

                    if (locked.HasValue)
                    {
                        layer.Lock = locked.Value;
                    }

                    return AutoCadOperationResult.Ok();
                }, $"SetLayer({name})");
            }
            catch (Exception ex)
            {
                return AutoCadOperationResult.Fail($"设置图层失败 ({name}): {ex.Message}");
            }
        }

        /// <summary>
        /// 添加尺寸标注
        /// </summary>
        /// <param name="dimType">标注类型：Aligned / Linear / Angular</param>
        /// <param name="start">起点</param>
        /// <param name="end">终点</param>
        /// <param name="dimLinePos">尺寸线位置点</param>
        /// <param name="layer">图层名（null 则用当前图层）</param>
        /// <returns>操作结果，Handle 为创建的标注实体 Handle</returns>
        public AutoCadOperationResult AddDimension(string dimType, Point3D start, Point3D end,
                                                   Point3D dimLinePos, string layer = null)
        {
            var connCheck = EnsureConnected("AddDimension");
            if (connCheck != null) return connCheck;

            if (start == null || end == null || dimLinePos == null)
                return AutoCadOperationResult.Fail("标注的起点/终点/尺寸线位置不能为空");

            if (string.IsNullOrWhiteSpace(dimType))
                dimType = "Aligned";

            try
            {
                return WithRetry(() =>
                {
                    var modelSpace = _activeDoc.ModelSpace;
                    dynamic entity;
                    string typeUpper = dimType.ToUpperInvariant();

                    switch (typeUpper)
                    {
                        case "ALIGNED":
                            // 对齐标注（沿两点连线方向）
                            entity = modelSpace.AddDimAligned(
                                start.ToArray(),
                                end.ToArray(),
                                dimLinePos.ToArray());
                            break;

                        case "LINEAR":
                            // 线性标注（水平或垂直）
                            // COM 自动化用 AddDimRotated（旋转角度决定方向）
                            double dx = end.X - start.X;
                            double dy = end.Y - start.Y;
                            double rotation = Math.Abs(dy) > Math.Abs(dx)
                                ? Math.PI / 2  // 垂直
                                : 0;           // 水平
                            entity = modelSpace.AddDimRotated(
                                start.ToArray(),
                                end.ToArray(),
                                dimLinePos.ToArray(),
                                rotation);
                            break;

                        case "ANGULAR":
                            // 角度标注（需要圆弧或两条线，简化为对齐标注）
                            entity = modelSpace.AddDimAligned(
                                start.ToArray(),
                                end.ToArray(),
                                dimLinePos.ToArray());
                            break;

                        default:
                            return AutoCadOperationResult.Fail($"不支持的标注类型: {dimType}（支持 Aligned/Linear/Angular）");
                    }

                    if (!string.IsNullOrEmpty(layer))
                    {
                        entity.Layer = layer;
                    }

                    return AutoCadOperationResult.Ok(entity.Handle);
                }, $"AddDimension({dimType})");
            }
            catch (Exception ex)
            {
                return AutoCadOperationResult.Fail($"添加标注失败 ({dimType}): {ex.Message}");
            }
        }

        /// <summary>
        /// 保存当前图纸
        /// </summary>
        /// <param name="path">保存路径（null 则保存到原路径）</param>
        /// <returns>操作结果</returns>
        public AutoCadOperationResult SaveDrawing(string path = null)
        {
            var connCheck = EnsureConnected("SaveDrawing");
            if (connCheck != null) return connCheck;

            try
            {
                return WithRetry(() =>
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        _activeDoc.Save();
                    }
                    else
                    {
                        // SaveAs 格式：ac2018_dwg = 61（AutoCAD 2018 DWG）
                        // 传 0 让 AutoCAD 根据文件扩展名自动判断
                        _activeDoc.SaveAs(path, 0);
                    }
                    return AutoCadOperationResult.Ok();
                }, "SaveDrawing");
            }
            catch (Exception ex)
            {
                return AutoCadOperationResult.Fail($"保存图纸失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取所有图层名
        /// </summary>
        /// <returns>操作结果，Data["Layers"] 为图层名列表</returns>
        public AutoCadOperationResult GetLayers()
        {
            var connCheck = EnsureConnected("GetLayers");
            if (connCheck != null) return connCheck;

            try
            {
                return WithRetry(() =>
                {
                    var layers = _activeDoc.Layers;
                    var names = new List<string>();
                    for (int i = 0; i < layers.Count; i++)
                    {
                        try { names.Add(layers.Item(i).Name); }
                        catch { /* 跳过无法读取的图层 */ }
                    }
                    var result = AutoCadOperationResult.Ok();
                    result.Data["Layers"] = names;
                    return result;
                }, "GetLayers");
            }
            catch (Exception ex)
            {
                return AutoCadOperationResult.Fail($"获取图层列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取实体属性
        /// </summary>
        /// <param name="handle">实体 Handle</param>
        /// <returns>操作结果，Data 包含 Layer/EntityType/Points 等</returns>
        public AutoCadOperationResult GetEntityProperties(string handle)
        {
            var connCheck = EnsureConnected("GetEntityProperties");
            if (connCheck != null) return connCheck;

            if (string.IsNullOrWhiteSpace(handle))
                return AutoCadOperationResult.Fail("实体 Handle 为空");

            try
            {
                return WithRetry(() =>
                {
                    var entity = _activeDoc.HandleToObject(handle);
                    if (entity == null)
                        return AutoCadOperationResult.Fail($"未找到 Handle 为 {handle} 的实体");

                    var info = ExtractEntityInfo(entity);
                    var result = AutoCadOperationResult.Ok(handle);
                    result.Data["Layer"] = info.Layer;
                    result.Data["EntityType"] = info.EntityType;
                    // 手动构建点列表（避免 dynamic 调度下 lambda 报错 CS1977）
                    var pointsList = new List<double[]>();
                    foreach (Point3D p in info.Points)
                    {
                        pointsList.Add(new double[] { p.X, p.Y, p.Z });
                    }
                    result.Data["Points"] = pointsList;
                    foreach (var kv in info.Properties)
                        result.Data[kv.Key] = kv.Value;
                    return result;
                }, "GetEntityProperties");
            }
            catch (Exception ex)
            {
                return AutoCadOperationResult.Fail($"获取实体属性失败: {ex.Message}");
            }
        }

        #endregion

        #endregion
    }
}
