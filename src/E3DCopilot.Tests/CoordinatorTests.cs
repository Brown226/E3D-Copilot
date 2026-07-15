using System;
using System.Threading.Tasks;
using E3DCopilot.Core.Agents;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Events;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class CoordinatorTests
    {
        [Test]
        public void AgentSpec_Defaults()
        {
            var spec = new AgentSpec { Name = "test" };
            Assert.That(spec.IsReadOnly, Is.True);
            Assert.That(spec.Name, Is.EqualTo("test"));
        }

        [Test]
        public void Register_AddsAgent()
        {
            var c = CreateCoordinator();
            c.Register(new AgentSpec { Name = "inspector", SystemPrompt = "Inspect things" });
            Assert.That(c.RegisteredAgentNames, Contains.Item("inspector"));
        }

        [Test]
        public void GetAgent_UnknownReturnsNull()
        {
            var c = CreateCoordinator();
            Assert.IsNull(c.GetAgent("nonexistent"));
        }

        [Test]
        public void GetAgent_ReturnsSpec()
        {
            var c = CreateCoordinator();
            var spec = new AgentSpec { Name = "designer", SystemPrompt = "Design" };
            c.Register(spec);
            var retrieved = c.GetAgent("designer");
            Assert.That(retrieved.SystemPrompt, Is.EqualTo("Design"));
        }

        [Test]
        public void LoadFromConfig_WithEmpty_DoesNotThrow()
        {
            var c = CreateCoordinator();
            Assert.DoesNotThrow(() => c.LoadFromConfig());
        }

        [Test]
        public void ClearCache_DoesNotThrow()
        {
            var c = CreateCoordinator();
            Assert.DoesNotThrow(() => c.ClearCache());
        }

        [Test]
        public void Register_DuplicateName_Overwrites()
        {
            var c = CreateCoordinator();
            c.Register(new AgentSpec { Name = "a", SystemPrompt = "First" });
            c.Register(new AgentSpec { Name = "a", SystemPrompt = "Second" });
            Assert.That(c.GetAgent("a").SystemPrompt, Is.EqualTo("Second"));
        }

        private static Coordinator CreateCoordinator()
        {
            var config = CopilotConfig.Load();
            var sink = new NullEventSink();
            var runner = new NullSubagentRunner();
            return new Coordinator(runner, sink, config);
        }

        /// <summary>无操作的 IEventSink 测试桩</summary>
        private class NullEventSink : IEventSink
        {
            public void Emit(CopilotEvent evt) { }
        }

        /// <summary>无操作的 SubagentRunner 测试桩（仅用于 Register/Cache 测试）</summary>
        private class NullSubagentRunner : SubagentRunner
        {
            public NullSubagentRunner()
                : base(new NullProvider(), new NullEventSink(),
                       new Core.Tools.ToolExecutor(new NullEventSink()),
                       CopilotConfig.Load(), null,
                       Core.Security.CommandPermissionController.CreateDefault())
            { }

        /// <summary>No-op provider that throws if ever called</summary>
            private class NullProvider : Core.Providers.ICopilotProvider
            {
                public string Name => "null";
                public Task<bool> HealthCheckAsync() => Task.FromResult(true);
                public Core.Providers.CopilotRequest PrepareRequest(string text, object tools) => null;
                public void SetConfig(CopilotConfig.ProviderConfig cfg) { }
                public Task StreamAsync(Core.Providers.CopilotRequest request, Action<Core.Providers.Chunk> onChunk, System.Threading.CancellationToken ct)
                {
                    onChunk(Core.Providers.Chunk.FromText(""));
                    return Task.CompletedTask;
                }
            }
        }
    }
}
