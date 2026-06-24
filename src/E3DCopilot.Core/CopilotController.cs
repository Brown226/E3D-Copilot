using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Memory;
using E3DCopilot.Core.Providers;
using E3DCopilot.Core.Security;
using E3DCopilot.Core.Skills;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core
{
    /// <summary>
    /// Main controller — central dispatcher
    /// Aggregates all services: session management / AI engine / tool execution / permission control / state management
    /// Corresponds to cline-chinese-main's Controller class
    /// </summary>
    public class CopilotController : IDisposable
    {
        // ── Core engine ──
        private AgentLoop _agent;
        private CopilotSession _session;

        // ── 多 Tab 会话管理 ──
        private readonly ConcurrentDictionary<string, CopilotSession> _tabSessions
            = new ConcurrentDictionary<string, CopilotSession>();
        private string _activeTabId = "";

        /// <summary>
        /// 获取或创建指定 tab 的 session
        /// </summary>
        public CopilotSession GetOrCreateTabSession(string tabId)
        {
            if (string.IsNullOrEmpty(tabId))
                return _session;
            return _tabSessions.GetOrAdd(tabId, _ => new CopilotSession());
        }

        /// <summary>
        /// 设置当前活跃 tab
        /// </summary>
        public void SetActiveTab(string tabId)
        {
            if (string.IsNullOrEmpty(tabId)) return;
            _activeTabId = tabId;
            // 切换 _session 到对应 tab 的 session
            _session = GetOrCreateTabSession(tabId);
        }

        /// <summary>
        /// 移除指定 tab 的 session
        /// </summary>
        public void RemoveTabSession(string tabId)
        {
            _tabSessions.TryRemove(tabId, out _);
        }

        /// <summary>
        /// 获取所有 tab session 的摘要信息（供历史面板使用）
        /// </summary>
        public List<TabSessionInfo> GetTabSessionInfos()
        {
            var list = new List<TabSessionInfo>();
            foreach (var kvp in _tabSessions)
            {
                list.Add(new TabSessionInfo
                {
                    TabId = kvp.Key,
                    SessionId = kvp.Value.SessionId,
                    MessageCount = kvp.Value.Messages?.Count ?? 0,
                    IsPlanMode = kvp.Value.IsPlanMode
                });
            }
            return list;
        }

        // ── Aggregated services ──
        private ICopilotProvider _provider;
        public ICopilotProvider Provider => _provider;
        public ToolExecutor Executor { get; }
        public CommandPermissionController Permission { get; }
        public CopilotConfig Config { get; }
        public SkillManager Skills { get; }
        public MemoryManager Memory { get; }
        
        // ── Current model ──
        public string CurrentModelName { get; private set; }

        // ── Storm Breaker 状态（跨 turn 持久化，注入 AgentLoop）──
        public string StormSig { get; set; } = "";
        public int StormCount { get; set; } = 0;

        // ── Tool approval mode: "ask" | "auto" | "yolo" ──
        public string ToolApprovalMode { get; private set; } = "auto";

    // ── Infrastructure ──
    private readonly IEventSink _sink;
    private CancellationTokenSource _cts;
    private volatile bool _isRunning;
    private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        // ── Steer Queue — 中途干预（用户运行中注入引导消息） ──
        public readonly ConcurrentQueue<string> SteerQueue = new ConcurrentQueue<string>();

        public void EnqueueSteer(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                SteerQueue.Enqueue(message.Trim());
                _sink?.Emit(CopilotEvent.Notice($"Steer queued: {message}"));
            }
        }

        public bool HasPendingSteer => !SteerQueue.IsEmpty;

        // ── Approval management ──
        private readonly Dictionary<string, PendingApproval> _pendingApprovals
            = new Dictionary<string, PendingApproval>();

        // ── Events ──
        public event Action<CopilotEvent> OnEvent;
        public IEventSink EventSink => _sink;

        // ── State ──
        public bool IsRunning => _isRunning;
        public CopilotSession Session => _session;
        public bool IsPlanMode => _session?.IsPlanMode ?? false;
        public string ActiveTabId => _activeTabId;

        /// <summary>
        /// Full constructor
        /// </summary>
        public CopilotController(
            ICopilotProvider provider,
            ToolExecutor executor,
            CommandPermissionController permission,
            CopilotConfig config,
            IEventSink sink = null)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Executor = executor ?? throw new ArgumentNullException(nameof(executor));
            Permission = permission ?? CommandPermissionController.CreateDefault();
            Config = config ?? CopilotConfig.Load();

            // 初始化技能管理器
            var skillStatePath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "E3DCopilot", "skills-state.json");
            Skills = new SkillManager(skillStatePath);

            // 注册 run_skill 工具（需要 SkillManager，在 ToolExecutor.CreateDefault 之后）
            if (Executor.GetHandler("run_skill") == null)
                Executor.Register(new Tools.Handlers.RunSkillHandler(Skills, _sink));

            // 初始化记忆管理器
            Memory = new MemoryManager();

            // 注册 memory 工具（需要 MemoryManager，在 CreateDefault 之后）
            if (Executor.GetHandler("memory") == null)
                Executor.Register(new Tools.Handlers.MemoryHandler(Memory, _sink));

            _session = new CopilotSession();

            // Bridge event sink
            var externalSink = sink;
            _sink = new BridgeEventSink(evt =>
            {
                externalSink?.Emit(evt);
                OnEvent?.Invoke(evt);
            });
        }

        /// <summary>
        /// Create default Controller (shortcut)
        /// </summary>
        public static CopilotController CreateDefault(
            IToolDispatcher dispatcher = null,
            IEventSink sink = null,
            IToolRouter router = null)
        {
            var config = CopilotConfig.Load();
            
            // Resolve default model (supports provider/model format)
            var (providerConfig, modelName) = config.ResolveModel(config.DefaultModel);
            
            // Create corresponding provider based on Provider type
            ICopilotProvider provider;
            if (providerConfig.Kind == "anthropic")
            {
                // TODO: implement AnthropicProvider
                throw new NotSupportedException("Anthropic Provider not yet implemented");
            }
            else
            {
                // Default to OpenAI-compatible VllmProvider
                provider = new VllmProvider(
                    providerConfig.BaseUrl,
                    modelName,
                    providerConfig.ApiKey
                );
            }
            
            var executor = ToolExecutor.CreateDefault(dispatcher, sink, router);
            var permission = CommandPermissionController.CreateDefault();

            return new CopilotController(provider, executor, permission, config, sink);
        }

        /// <summary>
        /// Send user input → AgentLoop processing
        /// 使用 SemaphoreSlim 串行化，彻底消除旧 finally 与新请求的竞态
        /// </summary>
        public async Task SendAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;

            // 取消上一个任务（仅发 Cancel 信号，不阻塞）
            if (_isRunning)
            {
                try { _cts?.Cancel(); } catch { }
                try { _sink?.Emit(CopilotEvent.TurnDone()); } catch { }
            }

            // 等待上一个任务完全结束（finally 执行完毕后才继续）
            await _sendLock.WaitAsync();

            try
            {
                _isRunning = true;
                _cts = new CancellationTokenSource();

                _agent = new AgentLoop(Provider, _sink, Executor, Permission, Config, this, skillManager: Skills,
                    stormSig: StormSig, stormCount: StormCount);
                await _agent.RunAsync(_session, input, _cts.Token);
                // 回写 Storm Breaker 状态（AgentLoop 内部可能已更新）
                StormSig = _agent.StormSig;
                StormCount = _agent.StormCount;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Controller] SendAsync exception: {ex}");
                try { _sink?.Emit(CopilotEvent.TurnDone()); } catch { }
            }
            finally
            {
                _isRunning = false;
                _sendLock.Release();
            }
        }

        /// <summary>
        /// Cancel current operation
        /// </summary>
        public void Cancel()
        {
            _cts?.Cancel();
            _isRunning = false;
        }

        /// <summary>
        /// 切换 LLM Provider（运行时动态切换）
        /// </summary>
        public void SwitchProvider(ICopilotProvider newProvider, string modelRef = null)
        {
            if (newProvider == null) throw new ArgumentNullException(nameof(newProvider));
            if (_isRunning) throw new InvalidOperationException("Cannot switch provider while a task is running");
            _provider = newProvider;
            CurrentModelName = modelRef; // 记录当前模型引用（如 "mimo/mimo-v2.5"）
            _sink.Emit(CopilotEvent.Notice($"Provider switched to {newProvider.Name ?? "unknown"}"));
        }

        /// <summary>
        /// Toggle Plan Mode (read-only planning mode)
        /// </summary>
        public void SetPlanMode(bool enabled)
        {
            if (_session != null)
                _session.IsPlanMode = enabled;

            _sink.Emit(new CopilotEvent
            {
                Kind = EventKind.PlanModeChanged,
                Text = enabled ? "Plan Mode enabled" : "Plan Mode disabled"
            });
        }

        /// <summary>
        /// Set tool approval mode: "ask" (all tools need approval),
        /// "auto" (read-only auto, write needs approval), "yolo" (all auto)
        /// </summary>
        public void SetApprovalMode(string mode)
        {
            if (string.IsNullOrEmpty(mode)) return;
            mode = mode.ToLowerInvariant();
            if (mode != "ask" && mode != "auto" && mode != "yolo") return;

            ToolApprovalMode = mode;
            _sink.Emit(CopilotEvent.Notice($"Tool approval mode: {mode}"));
        }

        /// <summary>
        /// Register pending approval (called by AgentLoop)
        /// </summary>
        public void RegisterApproval(PendingApproval approval)
        {
            if (approval == null) return;
            _pendingApprovals[approval.Id] = approval;

            // 审批超时：5分钟后自动拒绝
            var timeout = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ =>
            {
                if (_pendingApprovals.TryGetValue(approval.Id, out var timedOut))
                {
                    timedOut.Complete(false, false);
                    _pendingApprovals.Remove(approval.Id);
                    _sink.Emit(CopilotEvent.Notice($"Approval timed out: {approval.ToolName}"));
                }
            });

            _sink.Emit(new CopilotEvent
            {
                Kind = EventKind.ApprovalRequest,
                ToolId = approval.Id,
                Text = approval.ToolName,
                Data = new { approval.ToolName, approval.Args, approval.Description }
            });
        }

        /// <summary>
        /// Handle approval result (called by UI thread)
        /// </summary>
        public void Approve(string approvalId, bool allow, bool persist = false)
        {
            if (string.IsNullOrEmpty(approvalId)) return;

            if (_pendingApprovals.TryGetValue(approvalId, out var req))
            {
                req.Complete(allow, persist);  // Unblock TaskCompletionSource
                _pendingApprovals.Remove(approvalId);

                _sink.Emit(new CopilotEvent
                {
                    Kind = EventKind.Notice,
                    Text = allow ? "Approved" : "Rejected"
                });
            }
            else
            {
                _sink.Emit(CopilotEvent.Error($"Approval request not found: {approvalId}"));
            }
        }

        /// <summary>
        /// New session
        /// </summary>
        public void NewSession()
        {
            // 完成所有待处理的审批（避免旧 AgentLoop 永久挂起）
            foreach (var kvp in _pendingApprovals)
            {
                try { kvp.Value.Complete(false, false); } catch { }
            }
            _pendingApprovals.Clear();
            _session = new CopilotSession();
            _sink.Emit(CopilotEvent.Notice("New session created"));
        }

        /// <summary>
        /// Get current session summary (for status bar display)
        /// </summary>
        public string GetSessionSummary()
        {
            if (_session == null) return "No session";
            return $"Messages: {_session.Messages.Count} | Mode: {(_session.IsPlanMode ? "Plan" : "Act")}";
        }

        public void Dispose()
        {
            try
            {
                _cts?.Cancel();  // Cancel first, then dispose
            }
            catch
            {
                // Ignore cancellation exception
            }
            
            try
            {
                _cts?.Dispose();
            }
            catch
            {
                // Ignore dispose exception
            }
            
            _cts = null;
            // 拒绝所有待处理的审批
            foreach (var kvp in _pendingApprovals)
            {
                try { kvp.Value.Complete(false, false); } catch { }
            }
            _pendingApprovals.Clear();
        }

        /// <summary>
        /// Bridge event sink
        /// </summary>
        private class BridgeEventSink : IEventSink
        {
            private readonly Action<CopilotEvent> _onEmit;
            public BridgeEventSink(Action<CopilotEvent> onEmit) => _onEmit = onEmit;
            public void Emit(CopilotEvent evt) => _onEmit?.Invoke(evt);
        }
    }

    /// <summary>
    /// Tab 会话摘要信息（供历史面板使用）
    /// </summary>
    public class TabSessionInfo
    {
        public string TabId { get; set; }
        public string SessionId { get; set; }
        public int MessageCount { get; set; }
        public bool IsPlanMode { get; set; }
    }
}
