using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Agents;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Providers;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class SubagentEnhancementTests
    {
        class FakeProvider : ICopilotProvider
        {
            public string Name { get; set; } = "fake";
            public Task StreamAsync(CopilotRequest r, Action<Chunk> o, CancellationToken ct) => Task.CompletedTask;
            public Task<bool> HealthCheckAsync() => Task.FromResult(true);
        }

        [Test]
        public void Context_Defaults()
        {
            var ctx = new SubagentContext();
            Assert.AreEqual(SubagentMode.Executor, ctx.Mode);
            Assert.IsNull(ctx.PreferredProvider);
        }

        [Test]
        public void ResolveProvider_UsesPreferredProvider_ForHandoff()
        {
            ProviderRegistry.Instance.Register("fake", (pc, m) => new FakeProvider());
            var cfg = new CopilotConfig();
            cfg.Providers.Add(new CopilotConfig.ProviderConfig
            {
                Name = "fake",
                Kind = "fake",
                BaseUrl = "http://fake",
                Models = new List<string> { "x" }
            });
            var ctx = new SubagentContext { PreferredProvider = "fake/x" };
            var resolved = SubagentRunner.ResolveProvider(
                ctx, new VllmProvider("http://localhost:8000/v1", "x", ""), cfg);
            Assert.IsInstanceOf<FakeProvider>(resolved);
        }

        [Test]
        public void ResolveProvider_FallsBackToMainProvider()
        {
            var fb = new VllmProvider("http://localhost:8000/v1", "x", "");
            var resolved = SubagentRunner.ResolveProvider(new SubagentContext(), fb, new CopilotConfig());
            Assert.AreSame(fb, resolved);
        }
    }
}
