using System;
using System.Collections.Generic;
using E3DCopilot.Core.Config;
using ProviderConfig = E3DCopilot.Core.Config.CopilotConfig.ProviderConfig;

namespace E3DCopilot.Core.Providers
{
    /// <summary>
    /// Provider 工厂注册表（借鉴 Reasonix internal/provider/provider.go 的 Register + New 模式）。
    /// 按 Kind 路由到对应 ICopilotProvider 工厂，支持 vllm / qwen / deepseek / openai-compatible / anthropic。
    /// 密钥解析优先级：配置显式 ApiKey &gt; 环境变量 E3DCOPILOT_KEY_&lt;NAME&gt; &gt; 通用 OPENAI/ANTHROPIC_API_KEY（非硬编码）。
    /// </summary>
    public interface IProviderRegistry
    {
        /// <summary>注册一个 Kind 对应的工厂</summary>
        void Register(string kind, Func<ProviderConfig, string, ICopilotProvider> factory);

        /// <summary>是否已注册该 Kind</summary>
        bool IsRegistered(string kind);

        /// <summary>按配置与模型名创建 Provider 实例</summary>
        ICopilotProvider New(ProviderConfig cfg, string modelName);

        /// <summary>所有已注册的 Kind</summary>
        IEnumerable<string> RegisteredKinds { get; }
    }

    /// <summary>
    /// 默认注册表实现：单例，内置 OpenAI 兼容（vLLM/Qwen/DeepSeek 等）与 Anthropic 原生两种工厂。
    /// </summary>
    public class ProviderRegistry : IProviderRegistry
    {
        private static readonly ProviderRegistry _instance = new ProviderRegistry();

        /// <summary>全局单例</summary>
        public static ProviderRegistry Instance => _instance;

        private readonly Dictionary<string, Func<ProviderConfig, string, ICopilotProvider>> _factories =
            new Dictionary<string, Func<ProviderConfig, string, ICopilotProvider>>(StringComparer.OrdinalIgnoreCase);

        public ProviderRegistry()
        {
            RegisterBuiltIns();
        }

        private void RegisterBuiltIns()
        {
            // OpenAI 兼容实现（vLLM / Qwen / DeepSeek / MiniMax / 中间层均走此路径）
            Func<ProviderConfig, string, ICopilotProvider> openAiFactory =
                (cfg, model) => new VllmProvider(cfg.BaseUrl, model, ResolveApiKey(cfg));

            foreach (var kind in new[] { "openai", "openai-compatible", "vllm", "qwen", "deepseek", "minimax" })
            {
                Register(kind, openAiFactory);
            }

            // Anthropic 原生 Messages API（SSE 流式）
            Register("anthropic", (cfg, model) => new AnthropicProvider(cfg.BaseUrl, model, ResolveApiKey(cfg)));
        }

        public void Register(string kind, Func<ProviderConfig, string, ICopilotProvider> factory)
        {
            if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentNullException(nameof(kind));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _factories[kind] = factory;
        }

        public bool IsRegistered(string kind) =>
            !string.IsNullOrWhiteSpace(kind) && _factories.ContainsKey(kind);

        public ICopilotProvider New(ProviderConfig cfg, string modelName)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            var kind = string.IsNullOrWhiteSpace(cfg.Kind) ? "openai" : cfg.Kind;
            if (!_factories.TryGetValue(kind, out var factory))
            {
                throw new InvalidOperationException(
                    $"未注册的 Provider 类型: {kind}。已支持: {string.Join(", ", RegisteredKinds)}");
            }
            return factory(cfg, modelName);
        }

        public IEnumerable<string> RegisteredKinds => _factories.Keys;

        /// <summary>
        /// 密钥解析优先级：配置显式 ApiKey &gt; 环境变量 E3DCOPILOT_KEY_&lt;NAME&gt; &gt; 通用 OPENAI/ANTHROPIC_API_KEY。
        /// 绝不硬编码密钥。
        /// </summary>
        public static string ResolveApiKey(ProviderConfig cfg)
        {
            if (!string.IsNullOrEmpty(cfg.ApiKey)) return cfg.ApiKey;

            var name = (cfg.Name ?? "").ToUpperInvariant().Replace("-", "_").Replace(" ", "_");
            var env = Environment.GetEnvironmentVariable("E3DCOPILOT_KEY_" + name);
            if (!string.IsNullOrEmpty(env)) return env;

            if (string.Equals(cfg.Kind, "anthropic", StringComparison.OrdinalIgnoreCase))
                env = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            else
                env = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            return env ?? "";
        }
    }
}
