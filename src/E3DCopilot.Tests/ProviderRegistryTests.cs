using System.Collections.Generic;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Providers;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class ProviderRegistryTests
    {
        [Test]
        public void BuiltInKinds_AreRegistered()
        {
            var reg = ProviderRegistry.Instance;
            foreach (var k in new[] { "openai", "openai-compatible", "vllm", "qwen", "deepseek", "minimax", "anthropic" })
                Assert.IsTrue(reg.IsRegistered(k), "未注册的 Kind: " + k);
        }

        [Test]
        public void New_OpenAiCompatible_ReturnsVllmProvider()
        {
            var cfg = new CopilotConfig.ProviderConfig
            {
                Name = "local",
                Kind = "openai-compatible",
                BaseUrl = "http://localhost:8000/v1",
                Models = new List<string> { "Qwen3" }
            };
            var p = ProviderRegistry.Instance.New(cfg, "Qwen3");
            Assert.IsInstanceOf<VllmProvider>(p);
            Assert.AreEqual("vllm", p.Name);
        }

        [Test]
        public void New_Anthropic_ReturnsAnthropicProvider()
        {
            var cfg = new CopilotConfig.ProviderConfig
            {
                Name = "claude",
                Kind = "anthropic",
                BaseUrl = "https://api.anthropic.com/v1",
                Models = new List<string> { "claude-sonnet-4-0" }
            };
            var p = ProviderRegistry.Instance.New(cfg, "claude-sonnet-4-0");
            Assert.IsInstanceOf<AnthropicProvider>(p);
            Assert.AreEqual("anthropic", p.Name);
        }

        [Test]
        public void New_UnknownKind_Throws()
        {
            var cfg = new CopilotConfig.ProviderConfig { Name = "x", Kind = "unknown-kind" };
            Assert.Throws<System.InvalidOperationException>(() => ProviderRegistry.Instance.New(cfg, "m"));
        }

        [Test]
        public void ResolveApiKey_PrefersConfig()
        {
            var cfg = new CopilotConfig.ProviderConfig { Name = "local", ApiKey = "cfg-secret" };
            Assert.AreEqual("cfg-secret", ProviderRegistry.ResolveApiKey(cfg));
        }

        [Test]
        public void ResolveApiKey_FallsBackToEnv()
        {
            var cfg = new CopilotConfig.ProviderConfig { Name = "openai", Kind = "openai" };
            var prev = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            System.Environment.SetEnvironmentVariable("OPENAI_API_KEY", "env-secret");
            try
            {
                Assert.AreEqual("env-secret", ProviderRegistry.ResolveApiKey(cfg));
            }
            finally
            {
                System.Environment.SetEnvironmentVariable("OPENAI_API_KEY", prev);
            }
        }
    }
}
