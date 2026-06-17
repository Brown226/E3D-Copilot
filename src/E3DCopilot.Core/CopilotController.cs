using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Providers;
using E3DCopilot.Core.Security;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core
{
    /// <summary>
    /// 主控制器 — 中央调度器
    /// 聚合所有服务：会话管理 / AI 引擎 / 工具执行 / 权限控制 / 状态管理
    /// 对应 cline-chinese-main 的 Controller 类
    /// </summary>
    public class CopilotController : IDisposable
    {
        // ── 核心引擎 ──
        private AgentLoop _agent;
        private CopilotSession _session;

        // ── 聚合服务 ──
        public ICopilotProvider Provider { get; }
        public ToolExecutor Executor { get; }
        public CommandPermissionController Permission { get; }
        public CopilotConfig Config { get; }

        // ── 基础设施 ──
        private readonly IEventSink _sink;
        private CancellationTokenSource _cts;
        private bool _isRunning;

        // ── 审批管理 ──
        private readonly Dictionary<string, PendingApproval> _pendingApprovals
            = new Dictionary<string, PendingApproval>();

        // ── 事件 ──
        public event Action<CopilotEvent> OnEvent;
        public IEventSink EventSink => _sink;

        // ── 状态 ──
        public bool IsRunning => _isRunning;
        public CopilotSession Session => _session;
        public bool IsPlanMode => _session?.IsPlanMode ?? false;

        /// <summary>
        /// 完整构造函数
        /// </summary>
        public CopilotController(
            ICopilotProvider provider,
            ToolExecutor executor,
            CommandPermissionController permission,
            CopilotConfig config,
            IEventSink sink = null)
        {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Executor = executor ?? throw new ArgumentNullException(nameof(executor));
            Permission = permission ?? CommandPermissionController.CreateDefault();
            Config = config ?? CopilotConfig.Load();

            _session = new CopilotSession();

            // 桥接事件接收器
            var externalSink = sink;
            _sink = new BridgeEventSink(evt =>
            {
                externalSink?.Emit(evt);
                OnEvent?.Invoke(evt);
            });
        }

        /// <summary>
        /// 创建默认 Controller（快捷方式）
        /// </summary>
        public static CopilotController CreateDefault(
            IToolDispatcher dispatcher = null,
            IEventSink sink = null)
        {
            var config = CopilotConfig.Load();
            var provider = new VllmProvider(config.Llm.BaseUrl, config.Llm.Model);
            var executor = ToolExecutor.CreateDefault(dispatcher, sink);
            var permission = CommandPermissionController.CreateDefault();

            return new CopilotController(provider, executor, permission, config, sink);
        }

        /// <summary>
        /// 发送用户输入 → AgentLoop 处理
        /// </summary>
        public async Task SendAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;
            if (_isRunning) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();

            try
            {
                _agent = new AgentLoop(Provider, _sink, Executor, Permission, Config, this);
                await _agent.RunAsync(_session, input, _cts.Token);
            }
            finally
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// 取消当前操作
        /// </summary>
        public void Cancel()
        {
            _cts?.Cancel();
            _isRunning = false;
        }

        /// <summary>
        /// 切换 Plan Mode（只读规划模式）
        /// </summary>
        public void SetPlanMode(bool enabled)
        {
            if (_session != null)
                _session.IsPlanMode = enabled;

            _sink.Emit(new CopilotEvent
            {
                Kind = EventKind.PlanModeChanged,
                Text = enabled ? "Plan Mode 已启用" : "Plan Mode 已禁用"
            });
        }

        /// <summary>
        /// 注册待审批请求（AgentLoop 调用）
        /// </summary>
        public void RegisterApproval(PendingApproval approval)
        {
            if (approval == null) return;
            _pendingApprovals[approval.Id] = approval;

            _sink.Emit(new CopilotEvent
            {
                Kind = EventKind.ApprovalRequest,
                ToolId = approval.Id,
                Text = approval.ToolName,
                Data = new { approval.ToolName, approval.Args, approval.Description }
            });
        }

        /// <summary>
        /// 处理审批结果（UI 线程调用）
        /// </summary>
        public void Approve(string approvalId, bool allow, bool persist = false)
        {
            if (string.IsNullOrEmpty(approvalId)) return;

            if (_pendingApprovals.TryGetValue(approvalId, out var req))
            {
                req.Complete(allow, persist);  // 唤醒 TaskCompletionSource
                _pendingApprovals.Remove(approvalId);

                _sink.Emit(new CopilotEvent
                {
                    Kind = EventKind.Notice,
                    Text = allow ? "已批准" : "已拒绝"
                });
            }
            else
            {
                _sink.Emit(CopilotEvent.Error($"未找到审批请求: {approvalId}"));
            }
        }

        /// <summary>
        /// 新建会话
        /// </summary>
        public void NewSession()
        {
            _session = new CopilotSession();
            _pendingApprovals.Clear();
            _sink.Emit(CopilotEvent.Notice("已创建新会话"));
        }

        /// <summary>
        /// 获取当前会话摘要（用于状态栏显示）
        /// </summary>
        public string GetSessionSummary()
        {
            if (_session == null) return "无会话";
            return $"消息: {_session.Messages.Count} | 模式: {(_session.IsPlanMode ? "Plan" : "Act")}";
        }

        public void Dispose()
        {
            _cts?.Dispose();
            _cts = null;
            _pendingApprovals.Clear();
        }

        /// <summary>
        /// 桥接事件接收器
        /// </summary>
        private class BridgeEventSink : IEventSink
        {
            private readonly Action<CopilotEvent> _onEmit;
            public BridgeEventSink(Action<CopilotEvent> onEmit) => _onEmit = onEmit;
            public void Emit(CopilotEvent evt) => _onEmit?.Invoke(evt);
        }
    }
}
