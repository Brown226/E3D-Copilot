using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Events;
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

            // 分派事件（传原始 toolName 作为 coreToolName，effectiveName 作为实际执行的工具名）
            _sink?.Emit(CopilotEvent.ToolStart(Guid.NewGuid().ToString("N"), effectiveName, args, toolName));

            var sw = Stopwatch.StartNew();

            try
            {
                var result = await handler.ExecuteAsync(args, ct);
                sw.Stop();
                result.DurationMs = sw.ElapsedMilliseconds;

                // 结果事件（传递 result.Data 作为 meta，供前端渲染）
                if (result.Success)
                    _sink?.Emit(CopilotEvent.ToolComplete(
                        Guid.NewGuid().ToString("N"), result.Text, result.Data));
                else
                    _sink?.Emit(CopilotEvent.ToolFail(
                        Guid.NewGuid().ToString("N"), result.Error));

                return result;
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                _sink?.Emit(CopilotEvent.Notice($"工具 {toolName} 已取消"));
                return ToolResult.Fail("已取消");
            }
            catch (Exception ex)
            {
                sw.Stop();
                var result = ToolResult.Fail($"工具执行异常: {ex.Message}");
                _sink?.Emit(CopilotEvent.ToolFail(Guid.NewGuid().ToString("N"), result.Error));
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
                "query", "Query E3D elements by type (PIPE/EQUI/STRU), name pattern, scope",
                @"{""type"":""object"",""properties"":{""type"":{""type"":""string"",""description"":""Element type like PIPE/EQUI/STRU/BRAN""},""name"":{""type"":""string"",""description"":""Name pattern, supports *""},""scope"":{""type"":""string"",""description"":""Scope DBURI""},""limit"":{""type"":""integer"",""description"":""Max results""}},""required"":[""type""]}",
                true));
            executor.Register(new DispatcherBackedHandler(dispatcher,
                "modify", "Modify E3D element attribute values (single or batch). Query first.",
                @"{""type"":""object"",""properties"":{""dburi"":{""type"":""string"",""description"":""Target element DBURI""},""attributes"":{""type"":""object"",""description"":""Key-value pairs to modify""},""preview"":{""type"":""boolean"",""description"":""Preview only""}},""required"":[""dburi"",""attributes""]}",
                false));
            executor.Register(new DispatcherBackedHandler(dispatcher,
                "check", "Check and validate: existence, attribute, naming, clearance",
                @"{""type"":""object"",""properties"":{""type"":{""type"":""string"",""enum"":[""exists"",""attribute"",""naming"",""clearance""],""description"":""Check type""},""element"":{""type"":""string"",""description"":""Target element name or DBURI""},""attribute"":{""type"":""string"",""description"":""Attribute name""},""expected"":{""type"":""string"",""description"":""Expected value""},""pattern"":{""type"":""string"",""description"":""Naming regex""}},""required"":[""type"",""element""]}",
                true));
            executor.Register(new DispatcherBackedHandler(dispatcher,
                "export", "Import/Export: export element list to Excel/CSV, generate PML script",
                @"{""type"":""object"",""properties"":{""action"":{""type"":""string"",""enum"":[""export"",""import"",""generate_pml""]},""format"":{""type"":""string"",""enum"":[""csv"",""excel"",""pml""]},""filePath"":{""type"":""string"",""description"":""File path""}},""required"":[""action"",""format""]}",
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
            executor.Register(new SearchKnowledgeHandler(sink));

            // 接入可选路由器
            executor.Router = router;

            return executor;
        }
    }
}
