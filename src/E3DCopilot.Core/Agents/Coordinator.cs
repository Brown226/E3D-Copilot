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
    /// 多 Agent Coordinator — Agent 注册表 + 调度 + Handoff
    /// E7：为未来多 Agent 架构统一管理所有专长 Agent
    /// </summary>
    public class Coordinator
    {
        private readonly Dictionary<string, AgentSpec> _agents =
            new Dictionary<string, AgentSpec>(StringComparer.OrdinalIgnoreCase);

        private readonly SubagentRunner _runner;
        private readonly IEventSink _sink;
        private readonly CopilotConfig _config;
        private readonly Dictionary<string, AgentResult> _cache =
            new Dictionary<string, AgentResult>(StringComparer.OrdinalIgnoreCase);

        public Coordinator(SubagentRunner runner, IEventSink sink, CopilotConfig config)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _sink = sink;
            _config = config ?? CopilotConfig.Load();
        }

        /// <summary>注册专长 Agent</summary>
        public void Register(AgentSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            _agents[spec.Name] = spec;
            _sink?.Emit(CopilotEvent.Notice($"Coordinator: 注册专长 Agent '{spec.Name}'"));
        }

        /// <summary>从 CopilotConfig.SpecializedAgents 加载预置 Agent</summary>
        public void LoadFromConfig()
        {
            var specialized = _config.SpecializedAgents;
            if (specialized == null) return;

            foreach (var sa in specialized)
            {
                Register(new AgentSpec
                {
                    Name = sa.Name,
                    SystemPrompt = sa.SystemPrompt,
                    IsReadOnly = sa.ReadOnly,
                    DefaultProvider = sa.DefaultProvider
                });
            }
        }

        /// <summary>Handoff — 派发到专长 Agent</summary>
        public Task<AgentResult> HandoffAsync(string agentName, string context, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (!_agents.TryGetValue(agentName, out var spec))
            {
                return Task.FromResult(new AgentResult
                {
                    Success = false,
                    Output = $"未知 Agent: {agentName}"
                });
            }

            // 缓存命中
            string cacheKey = $"{agentName}:{context.GetHashCode()}";
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                _sink?.Emit(CopilotEvent.Notice($"Coordinator: 使用缓存结果 '{agentName}'"));
                return Task.FromResult(cached);
            }

            return HandoffInternalAsync(spec, context, cacheKey, ct);
        }

        private async Task<AgentResult> HandoffInternalAsync(
            AgentSpec spec, string context, string cacheKey, CancellationToken ct)
        {
            var ctx = new SubagentContext
            {
                Name = spec.Name,
                SystemPrompt = spec.SystemPrompt,
                IsReadOnly = spec.IsReadOnly,
                Mode = SubagentMode.Executor,
                Session = new CopilotSession(),
                PreferredProvider = spec.DefaultProvider
            };

            var result = await _runner.RunAsync(ctx, context, ct);
            _cache[cacheKey] = result;
            return result;
        }

        /// <summary>获取所有已注册 Agent 名称</summary>
        public IReadOnlyCollection<string> RegisteredAgentNames => _agents.Keys;

        /// <summary>获取 Agent 规格</summary>
        public AgentSpec GetAgent(string name) =>
            _agents.TryGetValue(name, out var s) ? s : null;

        /// <summary>清空结果缓存</summary>
        public void ClearCache() => _cache.Clear();
    }

    /// <summary>
    /// 专长 Agent 规格
    /// </summary>
    public class AgentSpec
    {
        public string Name { get; set; }
        public string SystemPrompt { get; set; }
        public bool IsReadOnly { get; set; } = true;
        public string DefaultProvider { get; set; }
    }
}
