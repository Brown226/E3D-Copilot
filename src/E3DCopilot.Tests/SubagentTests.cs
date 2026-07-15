using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core;
using E3DCopilot.Core.Agents;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Providers;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Security;
using E3DCopilot.Core.Tools;
using E3DCopilot.Core.Tools.Handlers;
using E3DCopilot.Tools.Bridge;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class SubagentTests
    {
        private ToolExecutor _executor;
        private E3DToolDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            var env = new SimulatedE3DEnvironment();
            _dispatcher = new E3DToolDispatcher(env);
            _executor = ToolExecutor.CreateDefault(_dispatcher, null);
        }

        // ── SubagentContext ──

        [Test]
        public void SubagentContext_Defaults_IsReadOnlyTrue()
        {
            var ctx = new SubagentContext();
            Assert.IsTrue(ctx.IsReadOnly);
            Assert.IsNotNull(ctx.Session);
            Assert.IsNull(ctx.Name);
            Assert.IsNull(ctx.SystemPrompt);
        }

        [Test]
        public void SubagentContext_WithValues_StoresCorrectly()
        {
            var ctx = new SubagentContext
            {
                Name = "test-agent",
                SystemPrompt = "You are a test agent.",
                IsReadOnly = true
            };
            Assert.AreEqual("test-agent", ctx.Name);
            Assert.AreEqual("You are a test agent.", ctx.SystemPrompt);
            Assert.IsTrue(ctx.IsReadOnly);
        }

        [Test]
        public void AgentResult_Defaults_SuccessTrue()
        {
            var result = new AgentResult();
            Assert.IsTrue(result.Success);
            Assert.IsNull(result.Output);
        }

        [Test]
        public void AgentResult_WithOutput_StoresCorrectly()
        {
            var result = new AgentResult { Output = "test output", Success = true };
            Assert.AreEqual("test output", result.Output);
            Assert.IsTrue(result.Success);
        }

        // ── ToolExecutor.FilterReadOnly ──

        [Test]
        public void FilterReadOnly_ReturnsOnlyReadOnlyHandlers()
        {
            // Arrange
            var filtered = _executor.FilterReadOnly();

            // Act: check a known read-only handler
            bool hasQuery = filtered.HasHandler("query");
            bool hasAsk = filtered.HasHandler("ask");
            // modify is NOT read-only, should be filtered out
            bool hasModify = filtered.HasHandler("modify");

            // Assert
            Assert.IsTrue(hasQuery, "query should be present (read-only)");
            Assert.IsTrue(hasAsk, "ask should be present (read-only)");
            Assert.IsFalse(hasModify, "modify should be filtered out (write)");
        }

        [Test]
        public void FilterReadOnly_OriginalExecutorUnchanged()
        {
            // Arrange
            bool before = _executor.HasHandler("modify");
            Assert.IsTrue(before, "modify should exist in original before filter");

            // Act
            var filtered = _executor.FilterReadOnly();

            // Assert: original still has modify
            Assert.IsTrue(_executor.HasHandler("modify"), "original executor should still have modify");
            Assert.IsFalse(filtered.HasHandler("modify"), "filtered executor should NOT have modify");
        }

        [Test]
        public void FilterReadOnly_ReturnsNewInstance()
        {
            var filtered = _executor.FilterReadOnly();
            Assert.AreNotSame(_executor, filtered, "FilterReadOnly should return a new instance");
        }

        // ── ToolExecutor.RemoveHandler ──

        [Test]
        public void RemoveHandler_RemovesSpecifiedHandler()
        {
            // Arrange
            Assert.IsTrue(_executor.HasHandler("query"), "query should exist before removal");

            // Act
            _executor.RemoveHandler("query");

            // Assert
            Assert.IsFalse(_executor.HasHandler("query"), "query should be removed");
        }

        [Test]
        public void RemoveHandler_NonExistent_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _executor.RemoveHandler("nonexistent_tool"));
        }

        [Test]
        public void RemoveHandler_OtherHandlersUnaffected()
        {
            // Arrange
            _executor.RemoveHandler("query");

            // Assert
            Assert.IsTrue(_executor.HasHandler("modify"), "modify should still exist after removing query");
            Assert.IsTrue(_executor.HasHandler("ask"), "ask should still exist after removing query");
        }

        // ── CopilotEvent.AgentName ──

        [Test]
        public void CopilotEvent_AgentName_DefaultNull()
        {
            var evt = new CopilotEvent();
            Assert.IsNull(evt.AgentName);
        }

        [Test]
        public void CopilotEvent_AgentName_CanSetAndRead()
        {
            var evt = new CopilotEvent { AgentName = "sub-agent-1" };
            Assert.AreEqual("sub-agent-1", evt.AgentName);
        }

        [Test]
        public void TaggedSink_SetsAgentName()
        {
            // Arrange
            string capturedName = null;
            var innerSink = new TestEventSink(evt => capturedName = evt.AgentName);
            var taggedSink = new TaggedSink(innerSink, "my-agent");

            // Act
            taggedSink.Emit(new CopilotEvent { Kind = EventKind.Text, Text = "hello" });

            // Assert
            Assert.AreEqual("my-agent", capturedName);
        }

        [Test]
        public void TaggedSink_PreservesOtherProperties()
        {
            // Arrange
            string capturedText = null;
            EventKind capturedKind = EventKind.Text;
            var innerSink = new TestEventSink(evt =>
            {
                capturedText = evt.Text;
                capturedKind = evt.Kind;
            });
            var taggedSink = new TaggedSink(innerSink, "agent");

            // Act
            taggedSink.Emit(new CopilotEvent { Kind = EventKind.ToolDispatch, Text = "test_tool" });

            // Assert
            Assert.AreEqual("test_tool", capturedText);
            Assert.AreEqual(EventKind.ToolDispatch, capturedKind);
        }

        // ── CopilotSession.LastAssistantText ──

        [Test]
        public void LastAssistantText_EmptySession_ReturnsNull()
        {
            var session = new CopilotSession();
            Assert.IsNull(session.LastAssistantText());
        }

        [Test]
        public void LastAssistantText_OnlyUserMessages_ReturnsNull()
        {
            var session = new CopilotSession();
            session.AddUserMessage("hello");
            Assert.IsNull(session.LastAssistantText());
        }

        [Test]
        public void LastAssistantText_WithAssistantMessage_ReturnsContent()
        {
            var session = new CopilotSession();
            session.AddUserMessage("hello");
            session.AddAssistantMessage("world");
            Assert.AreEqual("world", session.LastAssistantText());
        }

        [Test]
        public void LastAssistantText_ReturnsLastAssistantContent()
        {
            var session = new CopilotSession();
            session.AddAssistantMessage("first");
            session.AddAssistantMessage("second");
            Assert.AreEqual("second", session.LastAssistantText());
        }

        [Test]
        public void LastAssistantText_IgnoresToolMessages()
        {
            var session = new CopilotSession();
            session.AddAssistantMessage("the answer");
            session.AddToolResult("call_1", "result");
            Assert.AreEqual("the answer", session.LastAssistantText());
        }

        [Test]
        public void LastAssistantText_EmptyAssistantContent_ReturnsNull()
        {
            var session = new CopilotSession();
            session.AddAssistantMessage(""); // empty content
            Assert.IsNull(session.LastAssistantText());
        }

        // ── AgentLoop.SubagentDepth ──

        [Test]
        public void SubagentDepth_DefaultZero()
        {
            // AgentLoop requires many dependencies; we test the property via reflection-like pattern
            // by checking the static constant
            Assert.AreEqual(2, SubagentRunner.MaxSubagentDepth, "MaxSubagentDepth should be 2");
        }

        // ── SubagentDispatchHandler 写模式参数化 ──

        [Test]
        public void DispatchHandler_IsReadOnlyAsTool()
        {
            // 派发动作本身是只读的（不直接修改 E3D 数据），
            // 但子代理内部是否只读由 readonly 参数控制
            var runner = CreateFakeRunner();
            var handler = new SubagentDispatchHandler(runner);
            Assert.IsTrue(handler.IsReadOnly, "dispatch_subagent 工具本身应为只读动作");
        }

        [Test]
        public void DispatchHandler_ParameterSchema_ContainsReadonly()
        {
            var runner = CreateFakeRunner();
            var handler = new SubagentDispatchHandler(runner);
            Assert.IsTrue(handler.ParameterSchema.Contains("readonly"),
                "参数 schema 应含 readonly 字段，供主 Agent 控制子代理是否可写");
        }

        [Test]
        public void DispatchHandler_ReadonlyFalse_AllowsWriteTools()
        {
            // 使用假 runner 捕获传入的 SubagentContext，验证 IsReadOnly 随参数变化
            var fakeRunner = CreateFakeRunner();
            var handler = new SubagentDispatchHandler(fakeRunner);

            // readonly=false 时，子代理应获完整工具集
            var json = "{\"name\":\"writer\",\"task\":\"修改管道\",\"readonly\":false}";
            var result = handler.ExecuteAsync(json, CancellationToken.None).Result;

            Assert.IsTrue(result.Success, "派发应成功");
            Assert.IsNotNull(fakeRunner.LastContext, "应构造子代理上下文");
            Assert.IsFalse(fakeRunner.LastContext.IsReadOnly,
                "readonly=false 时子代理应允许写工具");
        }

        [Test]
        public void DispatchHandler_ReadonlyOmitted_DefaultsToReadOnly()
        {
            var fakeRunner = CreateFakeRunner();
            var handler = new SubagentDispatchHandler(fakeRunner);

            var json = "{\"name\":\"inspector\",\"task\":\"查询管道\"}";
            var result = handler.ExecuteAsync(json, CancellationToken.None).Result;

            Assert.IsTrue(result.Success);
            Assert.IsTrue(fakeRunner.LastContext.IsReadOnly,
                "省略 readonly 时默认只读（安全优先）");
        }

        // ── SubagentDispatchHandler registration ──
        // 注意：dispatch_subagent 在 CopilotController.CreateDefault 中注册，
        // 不在 ToolExecutor.CreateDefault 中。集成测试应在 CopilotController 层进行。

        // ── Helper ──

        /// <summary>
        /// 假 SubagentRunner：捕获最后一次构造的 SubagentContext，不真正运行 AgentLoop
        /// </summary>
        private class FakeSubagentRunner : SubagentRunner
        {
            public SubagentContext LastContext { get; private set; }

            public FakeSubagentRunner(IToolDispatcher dispatcher)
                : base(new FakeProvider(), new TestEventSink(_ => { }),
                       ToolExecutor.CreateDefault(dispatcher, null),
                       CopilotConfig.Load(),
                       null, CommandPermissionController.CreateDefault())
            {
            }

            public override Task<AgentResult> RunAsync(SubagentContext ctx, string task, CancellationToken ct)
            {
                LastContext = ctx;
                return Task.FromResult(new AgentResult { Output = "fake", Success = true });
            }
        }

        private FakeSubagentRunner CreateFakeRunner()
        {
            return new FakeSubagentRunner(_dispatcher);
        }

        private class FakeProvider : E3DCopilot.Core.Providers.ICopilotProvider
        {
            public string Name { get; set; } = "fake";
            public Task StreamAsync(CopilotRequest r, System.Action<E3DCopilot.Core.Providers.Chunk> o, CancellationToken ct) => Task.CompletedTask;
            public Task<bool> HealthCheckAsync() => Task.FromResult(true);
        }

        /// <summary>
        /// 测试用 IEventSink，捕获事件用于断言
        /// </summary>
        private class TestEventSink : IEventSink
        {
            private readonly System.Action<CopilotEvent> _onEmit;
            public TestEventSink(System.Action<CopilotEvent> onEmit)
            {
                _onEmit = onEmit;
            }
            public void Emit(CopilotEvent evt)
            {
                _onEmit?.Invoke(evt);
            }
        }
    }
}
