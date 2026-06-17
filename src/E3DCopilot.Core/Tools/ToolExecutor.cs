using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Tools.Handlers;

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
        /// 执行工具（完整流程：校验 → 分派 → 执行 → 结果）
        /// </summary>
        public async Task<ToolResult> ExecuteAsync(string toolName, string args,
            CancellationToken ct = default)
        {
            if (!_handlers.TryGetValue(toolName, out var handler))
            {
                return ToolResult.Fail($"未知工具: {toolName}");
            }

            // 参数校验
            var validation = ToolValidator.Validate(toolName, args, null);
            if (!validation.IsValid)
            {
                return ToolResult.Fail(validation.Error);
            }

            // 分派事件
            _sink?.Emit(CopilotEvent.ToolStart(Guid.NewGuid().ToString("N"), toolName, args));

            var sw = Stopwatch.StartNew();

            try
            {
                var result = await handler.ExecuteAsync(args, ct);
                sw.Stop();
                result.DurationMs = sw.ElapsedMilliseconds;

                // 结果事件
                if (result.Success)
                    _sink?.Emit(CopilotEvent.ToolComplete(
                        Guid.NewGuid().ToString("N"), result.Text));
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
        /// 初始化工具集（接受 IToolDispatcher 而非 ToolRegistry）
        /// </summary>
        public static ToolExecutor CreateDefault(IToolDispatcher dispatcher, IEventSink sink)
        {
            var executor = new ToolExecutor(sink);

            executor.Register(new DbQueryHandler(dispatcher));
            executor.Register(new ModifyHandler(dispatcher));
            executor.Register(new PmlCommandHandler(dispatcher));
            executor.Register(new CheckHandler(dispatcher));
            executor.Register(new CalculateHandler(dispatcher));
            executor.Register(new ExportHandler(dispatcher));

            return executor;
        }
    }
}
