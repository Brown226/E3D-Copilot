using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Logging;
using E3DCopilot.Core.Tools.Handlers;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools
{
    /// <summary>
    /// 工具执行器 — 核心调度器
    /// 管理所有 IToolHandler 的注册、发现、调度、执行
    /// 对应 cline-chinese-main 的 ToolExecutor + ToolExecutorCoordinator
    /// </summary>
    public class ToolExecutor
    {
        private readonly Dictionary<string, IToolHandler> _handlers;
        private readonly IEventSink _sink;
        private IToolDispatcher _dispatcher;

        /// <summary>
        /// 可选工具路由器 — 将核心工具路由到专用工具
        /// </summary>
        public IToolRouter Router { get; set; }

        public ToolExecutor(IEventSink sink)
        {
            _sink = sink;
            _handlers = new Dictionary<string, IToolHandler>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 注册处理器
        /// </summary>
        public void Register(IToolHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _handlers[handler.Name] = handler;
            _sink?.Emit(CopilotEvent.Notice($"已注册工具: {handler.Name}"));
        }

        /// <summary>
        /// 批量注册处理器
        /// </summary>
        public void RegisterAll(IEnumerable<IToolHandler> handlers)
        {
            foreach (var h in handlers)
                Register(h);
        }

        /// <summary>
        /// 获取当前 E3D 选中元素名称（供 AgentLoop 构建上下文）
        /// </summary>
        public string GetCurrentElementName()
        {
            return _dispatcher?.GetCurrentElementName();
        }

        /// <summary>
        /// 获取 E3D 多选元素名称列表（供 AgentLoop 构建多元素上下文）
        /// </summary>
        public List<string> GetSelectedElementNames()
        {
            return _dispatcher?.GetSelectedElementNames() ?? new List<string>();
        }

        /// <summary>
        /// 获取所有已注册的工具
        /// </summary>
        public IReadOnlyCollection<IToolHandler> GetAllHandlers() => _handlers.Values;

        /// <summary>
        /// 根据名称获取工具
        /// </summary>
        public IToolHandler GetHandler(string name)
        {
            _handlers.TryGetValue(name, out var handler);
            return handler;
        }

        /// <summary>
        /// 检查工具是否存在
        /// </summary>
        public bool HasHandler(string name) => _handlers.ContainsKey(name);

        /// <summary>
        /// 执行工具（完整流程：路由 → 校验 → 分派 → 执行 → 结果）
        /// </summary>
        public async Task<ToolResult> ExecuteAsync(string toolName, string args,
            CancellationToken ct = default)
        {
            // 第一步：尝试路由 — 将核心工具名映射到专用工具
            string effectiveName = toolName;
            string effectiveArgs = args;
            if (Router != null)
            {
                var (routedName, routedArgs) = await Router.RouteAsync(toolName, args);
                if (!string.IsNullOrEmpty(routedName) && routedName != toolName)
                {
                    // 有专用工具匹配，尝试查找对应 Handler
                    if (_handlers.TryGetValue(routedName, out var _))
                    {
                        effectiveName = routedName;
                        effectiveArgs = routedArgs ?? args;
                    }
                    // 专用工具没有注册 Handler，用回核心工具
                }
            }

            if (!_handlers.TryGetValue(effectiveName, out var handler))
            {
                return ToolResult.Fail($"未知工具: {toolName}");
            }

            // ponytail: inline JSON parse check (replaced ToolValidator which always got null requiredParams)
            if (!string.IsNullOrWhiteSpace(args))
            {
                try { JObject.Parse(args); }
                catch { return ToolResult.Fail($"Invalid JSON args for {toolName}"); }
            }

            // 生成统一的 toolId，用于关联 Start/Complete/Fail 事件
            var toolId = Guid.NewGuid().ToString("N");

            // 分派事件（传原始 toolName 作为 coreToolName，effectiveName 作为实际执行的工具名）
            _sink?.Emit(CopilotEvent.ToolStart(toolId, effectiveName, args, toolName));
            CopilotLogger.Info("ToolExecutor: {0} (id={1}), args={2}", effectiveName, toolId.Substring(0, 8), args?.Substring(0, Math.Min(200, args?.Length ?? 0)));

            var sw = Stopwatch.StartNew();

            // ── 超时配置：默认 60 秒，PML 执行 120 秒 ──
            int timeoutMs = (toolName == "execute_pml" || effectiveName == "execute_pml") ? 120000 : 60000;
            var timeoutCts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            // ── Timer-based progress for long-running tools ──
            System.Threading.Timer progressTimer = null;
            System.Threading.Timer timeoutTimer = null;
            if (handler.IsReadOnly == false || toolName == "export" || toolName == "execute_pml"
                || toolName == "query" || toolName == "modify")
            {
                // 对写入工具和耗时查询工具启动进度定时器（3秒间隔）
                progressTimer = new System.Threading.Timer(_ =>
                {
                    if (sw.IsRunning && sw.ElapsedMilliseconds > 2000)
                    {
                        _sink?.Emit(CopilotEvent.ToolProgressEvent(toolId,
                            $"执行中... {sw.ElapsedMilliseconds}ms", sw.ElapsedMilliseconds));
                    }
                }, null, 3000, 3000);

                // 超时定时器（触发取消）
                timeoutTimer = new System.Threading.Timer(_ =>
                {
                    if (sw.IsRunning && !ct.IsCancellationRequested)
                    {
                        CopilotLogger.Warn("ToolExecutor: {0} 执行超时 ({1}ms)", effectiveName, timeoutMs);
                        _sink?.Emit(CopilotEvent.ToolProgressEvent(toolId,
                            $"⚠ 执行超时 ({timeoutMs/1000}秒)，正在取消...", sw.ElapsedMilliseconds));
                        timeoutCts.Cancel();
                    }
                }, null, timeoutMs, Timeout.Infinite);
            }

            try
            {
                var result = await handler.ExecuteAsync(args, linkedCts.Token);
                sw.Stop();
                progressTimer?.Dispose();
                timeoutTimer?.Dispose();
                timeoutCts.Dispose();
                linkedCts.Dispose();
                result.DurationMs = sw.ElapsedMilliseconds;

                // 结果事件（使用相同 toolId 关联）
                CopilotLogger.Info("ToolExecutor: {0} 完成, success={1}, duration={2}ms", effectiveName, result.Success, result.DurationMs);
                if (result.Success)
                    _sink?.Emit(CopilotEvent.ToolComplete(toolId, result.Text, result.Data));
                else
                {
                    CopilotLogger.Warn("ToolExecutor: {0} 失败: {1}", effectiveName, result.Error);
                    _sink?.Emit(CopilotEvent.ToolFail(toolId, result.Error));
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                progressTimer?.Dispose();
                timeoutTimer?.Dispose();
                timeoutCts.Dispose();
                linkedCts.Dispose();
                string reason = ct.IsCancellationRequested ? "用户取消" : $"执行超时 ({timeoutMs/1000}秒)";
                _sink?.Emit(CopilotEvent.Notice($"工具 {toolName} 已取消: {reason}"));
                return ToolResult.Fail(reason);
            }
            catch (Exception ex)
            {
                sw.Stop();
                progressTimer?.Dispose();
                timeoutTimer?.Dispose();
                timeoutCts.Dispose();
                linkedCts.Dispose();
                var result = ToolResult.Fail($"工具执行异常: {ex.Message}");
                CopilotLogger.Error(ex, "ToolExecutor: {0} 异常", effectiveName);
                _sink?.Emit(CopilotEvent.ToolFail(toolId, result.Error));
                return result;
            }
        }

        /// <summary>
        /// 初始化工具集
        /// </summary>
        /// <param name="dispatcher">E3D 工具调度器</param>
        /// <param name="sink">事件接收器</param>
        /// <param name="router">可选工具路由器（由上层传入，避免 Core→Tools 反向依赖）</param>
        public static ToolExecutor CreateDefault(IToolDispatcher dispatcher, IEventSink sink,
            IToolRouter router = null)
        {
            var executor = new ToolExecutor(sink);
            executor._dispatcher = dispatcher;

            // 7 个透传 Handler：直接转发到 IToolDispatcher，零业务逻辑
            executor.Register(new DispatcherBackedHandler(dispatcher,
                "query", "Query E3D elements by type (PIPE/EQUI/STRU/BRAN), name pattern, scope. 按类型/名称/范围查询元素列表。不要用于读取单个元素的属性——用 get_attributes。",
                @"{""type"":""object"",""properties"":{""type"":{""type"":""string"",""description"":""Element type like PIPE/EQUI/STRU/BRAN""},""name"":{""type"":""string"",""description"":""Name pattern, supports * wildcard""},""scope"":{""type"":""string"",""description"":""Scope DBURI, e.g. ZONE-01 or CE for current element""},""limit"":{""type"":""integer"",""description"":""Max results (default 50)""}},""required"":[""type""]}",
                true));
            executor.Register(new DispatcherBackedHandler(dispatcher,
                "modify", "Modify E3D element attribute values (single or batch). Query first. 修改元素属性，支持单属性和批量。",
                @"{""type"":""object"",""properties"":{""dburi"":{""type"":""string"",""description"":""Target element name or DBURI""},""attributes"":{""type"":""object"",""description"":""Key-value pairs to modify, e.g. {\""WTHK\"": \""SCH40\""}""},""element"":{""type"":""string"",""description"":""Alias for dburi (backward compatible)""},""attribute"":{""type"":""string"",""description"":""Single attribute name (backward compatible, use with value)""},""value"":{""type"":""string"",""description"":""Single attribute value (backward compatible, use with attribute)""},""preview"":{""type"":""boolean"",""description"":""Preview only, don't actually modify""}},""required"":[""dburi""]}",
                false));
            executor.Register(new DispatcherBackedHandler(dispatcher,
                "check", "Check and validate: existence, attribute completeness, naming, clearance, bore consistency, change status, room number",
                @"{""type"":""object"",""properties"":{""type"":{""type"":""string"",""enum"":[""exists"",""attribute"",""attribute_complete"",""naming"",""name_consistency"",""clearance"",""distance"",""bore_consistency"",""change_status"",""room_number""],""description"":""Check type""},""element"":{""type"":""string"",""description"":""Target element name or DBURI""},""target"":{""type"":""string"",""description"":""Alias for element""},""attribute"":{""type"":""string"",""description"":""Attribute name to check""},""expected"":{""type"":""string"",""description"":""Expected value for attribute check""},""pattern"":{""type"":""string"",""description"":""Naming regex pattern""}},""required"":[""type""]}",
                true));
            executor.Register(new DispatcherBackedHandler(dispatcher,
                "export", "Export element list to Excel/CSV, or generate PML script. 导出元素列表或生成 PML 脚本。",
                @"{""type"":""object"",""properties"":{""action"":{""type"":""string"",""enum"":[""export"",""generate_pml""]},""format"":{""type"":""string"",""enum"":[""csv"",""excel"",""pml""]},""filePath"":{""type"":""string"",""description"":""File path""}},""required"":[""action"",""format""]}",
                false));
            executor.Register(new DispatcherBackedHandler(dispatcher,
                "design", "Create/modify equipment and structural elements",
                @"{""type"":""object"",""properties"":{""action"":{""type"":""string"",""enum"":[""create"",""modify"",""delete""],""description"":""Operation type""},""type"":{""type"":""string"",""description"":""Element type like EQUI/STRU""},""name"":{""type"":""string"",""description"":""Element name""},""attributes"":{""type"":""object"",""description"":""Attributes to set""}},""required"":[""action"",""type""]}",
                false));
            executor.Register(new DispatcherBackedHandler(dispatcher,
                "piping", "Create/modify piping elements (PIPE/BRAN/FTUB/BEND/TEE)",
                @"{""type"":""object"",""properties"":{""action"":{""type"":""string"",""enum"":[""create"",""modify""],""description"":""Operation type""},""name"":{""type"":""string"",""description"":""Pipe/Branch name""},""attributes"":{""type"":""object"",""description"":""Attributes to set""}},""required"":[""action""]}",
                false));
            executor.Register(new DispatcherBackedHandler(dispatcher,
                "geometry", "Spatial geometry queries: position, orientation, bounding box",
                @"{""type"":""object"",""properties"":{""action"":{""type"":""string"",""enum"":[""position"",""orientation"",""bbox""],""description"":""Query type""},""element"":{""type"":""string"",""description"":""Element name""}},""required"":[""action"",""element""]}",
                true));

            // 有实质逻辑的 Handler
            executor.Register(new PmlCommandHandler(dispatcher));
            executor.Register(new GetAttributesHandler(dispatcher));
            executor.Register(new CalculateHandler());

            // 元能力工具（不依赖 IToolDispatcher）
            executor.Register(new AskUserHandler(sink));
            executor.Register(new TaskHandler(sink));
            executor.Register(new ReadFileHandler(sink));
            executor.Register(new WriteFileHandler(sink));

            // 文件搜索工具（纯读操作，不依赖 IToolDispatcher）
            executor.Register(new GrepHandler(sink));
            executor.Register(new GlobHandler(sink));

            // 结构化任务追踪（替代 task 工具的升级版）
            executor.Register(new TodoWriteHandler(sink));

            // 元能力工具：知识库搜索（memory 和 run_skill 在 CopilotController 中注册）
            executor.Register(new SearchKnowledgeHandler(sink));

            // E3D 操作辅助工具（需要 IToolDispatcher）
            executor.Register(new UndoRedoHandler(dispatcher, sink));
            executor.Register(new ReportHandler(dispatcher));
            executor.Register(new CompareHandler(dispatcher));
            executor.Register(new HierarchyHandler(dispatcher));
            executor.Register(new BatchHandler(dispatcher, sink));

            // ISO出图相关工具（集成CNPE.IC.ISO功能）
            executor.Register(new IsoDrawingHandler(dispatcher));
            executor.Register(new MaterialQueryHandler(dispatcher));
            executor.Register(new PipeInfoHandler(dispatcher));

            // CAD 工具（文件/坐标导入 + AutoCAD 运行时交互）
            executor.Register(new CadImportHandler());   // cad_import
            executor.Register(new AutoCadHandler());     // autocad

            // 接入可选路由器
            executor.Router = router;

            return executor;
        }
    }
}
