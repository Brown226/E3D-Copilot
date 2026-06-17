using System;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Providers;
using E3DCopilot.Core.Security;

namespace E3DCopilot.Core
{
    /// <summary>
    /// 会话管理 + 事件分发 + 审批流 + Plan Mode
    /// transport-agnostic，可被 WinForms / 测试共享
    /// </summary>
    public class CopilotController : IDisposable
    {
        private AgentLoop _agent;
        private CopilotSession _session;
        private readonly IEventSink _sink;
        private readonly ToolPolicy _policy;
        private readonly PermissionGate _gate;
        private readonly CopilotConfig _config;
        private readonly ICopilotProvider _provider;

        private CancellationTokenSource _cts;
        private bool _isRunning;

        /// <summary>UI 订阅此事件接收 CopilotEvent</summary>
        public event Action<CopilotEvent> OnEvent;

        /// <summary>事件接收器（AgentLoop 传入）</summary>
        public IEventSink EventSink => _sink;

        /// <summary>是否正在运行</summary>
        public bool IsRunning => _isRunning;

        /// <summary>当前会话</summary>
        public CopilotSession Session => _session;

        /// <summary>是否 Plan Mode</summary>
        public bool IsPlanMode => _session?.IsPlanMode ?? false;

        public CopilotController(ICopilotProvider provider, IEventSink sink,
            ToolPolicy policy, PermissionGate gate, CopilotConfig config)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _policy = policy ?? new ToolPolicy();
            _gate = gate ?? new PermissionGate(_policy);
            _config = config ?? CopilotConfig.Load();

            _session = new CopilotSession();

            // 创建桥接 sink：外部 sink + 内部 event 转发
            var externalSink = sink;
            _sink = new BridgeEventSink(evt =>
            {
                externalSink?.Emit(evt);
                OnEvent?.Invoke(evt);
            });
        }

        /// <summary>
        /// 创建默认 Controller（方便快速启动，不传 sink 时用事件订阅）
        /// </summary>
        public static CopilotController CreateDefault(IEventSink sink = null)
        {
            var config = CopilotConfig.Load();
            var provider = new VllmProvider(config.Llm.BaseUrl, config.Llm.Model);
            var policy = new ToolPolicy();
            policy.ApplyPreset(ToolPreset.Confirm);
            var gate = new PermissionGate(policy);

            return new CopilotController(provider, sink, policy, gate, config);
        }

        /// <summary>
        /// 桥接事件接收器：将 Emit 转发到回调
        /// </summary>
        private class BridgeEventSink : IEventSink
        {
            private readonly Action<CopilotEvent> _onEmit;
            public BridgeEventSink(Action<CopilotEvent> onEmit)
            {
                _onEmit = onEmit;
            }
            public void Emit(CopilotEvent evt)
            {
                _onEmit(evt);
            }
        }

        /// <summary>
        /// 发送用户输入到 AgentLoop
        /// </summary>
        public async Task SendAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;
            if (_isRunning) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();

            try
            {
                _agent = new AgentLoop(_provider, _sink, _policy, _gate, _config);
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
        /// 切换 Plan Mode
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
        /// 处理审批结果（UI 线程调用）
        /// </summary>
        public void Approve(string approvalId, bool allow, bool persist = false)
        {
            // Phase 1c: 从 pending 表中查找并 complete
            _sink.Emit(new CopilotEvent
            {
                Kind = EventKind.Notice,
                Text = allow ? "已批准" : "已拒绝"
            });
        }

        /// <summary>
        /// 新建会话
        /// </summary>
        public void NewSession()
        {
            _session = new CopilotSession();
            _sink.Emit(CopilotEvent.Notice("已创建新会话"));
        }

        public void Dispose()
        {
            _cts?.Dispose();
            _cts = null;
        }
    }
}
