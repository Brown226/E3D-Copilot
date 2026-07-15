using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Logging;
using E3DCopilot.Core.Providers;
using E3DCopilot.Core.Security;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core.Agents
{
    /// <summary>
    /// 子代理运行器 — 复用 AgentLoop 作为独立员工
    /// 每个子代理 = 一个 AgentLoop 实例 + 独立 CopilotSession
    /// 对齐 Reasonix SubagentStore + SubagentRun
    /// </summary>
    public class SubagentRunner
    {
        private readonly ICopilotProvider _provider;
        private readonly IEventSink _sink;
        private readonly ToolExecutor _executor;
        private readonly CopilotConfig _config;
        private readonly CopilotController _controller;
        private readonly CommandPermissionController _permission;

        /// <summary>最大子代理嵌套深度</summary>
        public const int MaxSubagentDepth = 2;

        /// <summary>当前嵌套深度（由 SubagentDispatchHandler 传入）</summary>
        public int CurrentDepth { get; set; } = 0;

        public SubagentRunner(
            ICopilotProvider provider,
            IEventSink sink,
            ToolExecutor executor,
            CopilotConfig config,
            CopilotController controller,
            CommandPermissionController permission)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _sink = sink;
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _config = config ?? CopilotConfig.Load();
            _controller = controller;
            _permission = permission ?? CommandPermissionController.CreateDefault();
        }

        /// <summary>
        /// 运行一个子代理任务
        /// </summary>
        /// <param name="ctx">子代理上下文</param>
        /// <param name="task">任务描述（用户输入）</param>
        /// <param name="ct">取消令牌</param>
        public virtual async Task<AgentResult> RunAsync(SubagentContext ctx, string task, CancellationToken ct)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (string.IsNullOrWhiteSpace(task)) throw new ArgumentException("Task must not be empty", nameof(task));

            // 仅只读模式 — 过滤掉写工具
            ToolExecutor subExecutor;
            if (ctx.IsReadOnly)
            {
                subExecutor = _executor.FilterReadOnly();
            }
            else
            {
                subExecutor = _executor;
            }

            // 子代理内禁用 dispatch_subagent（防递归）
            subExecutor.RemoveHandler("dispatch_subagent");

            // 使用 TaggedSink 给每条事件打 AgentName 标签
            var taggedSink = new TaggedSink(_sink, ctx.Name);

            // 注入子代理专长提示词
            string systemPrompt = ctx.SystemPrompt ??
                "You are a specialized sub-agent. Complete the assigned task using the available tools. " +
                "Report back with your findings. You may only use read-only tools.";

            ctx.Session.AddSystemMessage(systemPrompt);

            // 构造 AgentLoop 实例（复用现有循环逻辑）
            // C2：按子代理模式 / 指定 Provider 选择模型（双模型 / handoff）
            var loop = new AgentLoop(
                ResolveProvider(ctx, _provider, _config),
                taggedSink,
                subExecutor,
                _permission,
                _config,
                _controller,
                skillManager: null,
                toolPolicy: null);

            // 注入当前深度（防无限嵌套）
            loop.SubagentDepth = CurrentDepth + 1;

            // 运行子代理
            await loop.RunAsync(ctx.Session, task, images: null, ct);

            // 取最后一条助手回复作为输出
            string output = ctx.Session.LastAssistantText() ?? "(no output)";

            return new AgentResult
            {
                Output = output,
                Success = true
            };
        }

        /// <summary>
        /// C2：解析子代理应使用的 Provider。
        /// 优先 PreferredProvider（handoff 专长切换），否则退回主 provider（双模型 MVP 复用主模型）。
        /// </summary>
        public static ICopilotProvider ResolveProvider(SubagentContext ctx, ICopilotProvider fallback, CopilotConfig config)
        {
            if (ctx == null) return fallback;
            if (!string.IsNullOrEmpty(ctx.PreferredProvider))
            {
                var (pc, model) = (config ?? CopilotConfig.Load()).ResolveModel(ctx.PreferredProvider);
                if (pc != null)
                {
                    try { return ProviderRegistry.Instance.New(pc, model); }
                    catch (InvalidOperationException) { }
                }
            }
            return fallback;
        }
    }

    /// <summary>
    /// 带 AgentName 标签的事件 Sink 包装器
    /// </summary>
    public class TaggedSink : IEventSink
    {
        private readonly IEventSink _inner;
        private readonly string _agentName;

        public TaggedSink(IEventSink inner, string agentName)
        {
            _inner = inner;
            _agentName = agentName;
        }

        public void Emit(CopilotEvent evt)
        {
            if (evt != null)
            {
                evt.AgentName = _agentName;
            }
            _inner?.Emit(evt);
        }
    }
}
