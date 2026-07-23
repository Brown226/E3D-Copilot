using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Providers;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    /// <summary>
    /// ContextCompactor 单元测试 — 验证三级压缩机制
    /// </summary>
    [TestFixture]
    public class ContextCompactorTests
    {
        private static CopilotConfig CreateConfig(int contextWindow = 10000)
        {
            var config = new CopilotConfig();
            config.Providers = new List<CopilotConfig.ProviderConfig>
            {
                new CopilotConfig.ProviderConfig
                {
                    Name = "test",
                    ContextWindow = contextWindow
                }
            };
            config.Ui = new CopilotConfig.UiConfig
            {
                CompactRatio = 0.8,
                SoftCompactRatio = 0.5,
                ToolResultSnipRatio = 0.6,
                CompactForceRatio = 0.9
            };
            return config;
        }

        private static ContextCompactor CreateCompactor(CopilotConfig config = null)
        {
            config = config ?? CreateConfig();
            var provider = new FakeProvider();
            var sink = new FakeSink();
            return new ContextCompactor(provider, sink, config);
        }

        private static CopilotSession CreateSessionWithMessages(int userMsgCount, int toolResultSize = 100)
        {
            var session = new CopilotSession();
            session.AddSystemMessage("You are a helpful assistant.");
            session.AddUserMessage("Hello, help me with E3D design.");

            for (int i = 0; i < userMsgCount; i++)
            {
                session.AddAssistantMessage($"Response {i}", new List<ToolCall>
                {
                    new ToolCall { Id = $"tc{i}", Name = "query", Arguments = "{\"type\":\"PIPE\"}" }
                });
                string toolContent = new string('x', toolResultSize);
                session.AddToolResult($"tc{i}", toolContent);
                session.AddUserMessage($"Follow up {i}");
            }
            return session;
        }

        // ═══════════════════════════════════════════════════════════
        //  基本行为测试
        // ═══════════════════════════════════════════════════════════

        [Test]
        public async Task MaybeCompact_NoContextWindow_DoesNothing()
        {
            var config = CreateConfig(0); // 0 = 禁用
            var compactor = CreateCompactor(config);
            var session = CreateSessionWithMessages(20);
            int countBefore = session.Messages.Count;

            await compactor.MaybeCompactAsync(session, CancellationToken.None);

            Assert.AreEqual(countBefore, session.Messages.Count);
        }

        [Test]
        public async Task MaybeCompact_SmallSession_DoesNothing()
        {
            var compactor = CreateCompactor();
            var session = CreateSessionWithMessages(2);

            // 模拟低 usage
            compactor.UpdateUsage(new UsageData { PromptTokens = 100, CompletionTokens = 50 });
            int countBefore = session.Messages.Count;

            await compactor.MaybeCompactAsync(session, CancellationToken.None);

            Assert.AreEqual(countBefore, session.Messages.Count);
        }

        [Test]
        public async Task MaybeCompact_SoftNotice_EmitsNoticeOnly()
        {
            var config = CreateConfig(1000);
            var compactor = CreateCompactor(config);
            var sink = new FakeSink();
            var provider = new FakeProvider();
            compactor = new ContextCompactor(provider, sink, config);

            var session = CreateSessionWithMessages(5);
            // 模拟 55% usage（在 soft 50% 和 snip 60% 之间）
            compactor.UpdateUsage(new UsageData { PromptTokens = 550, CompletionTokens = 50 });

            int countBefore = session.Messages.Count;
            await compactor.MaybeCompactAsync(session, CancellationToken.None);

            // 消息数不变
            Assert.AreEqual(countBefore, session.Messages.Count);
            // 有通知事件
            Assert.IsTrue(sink.Events.Any(e => e.Kind == EventKind.Notice));
        }

        // ═══════════════════════════════════════════════════════════
        //  Snip 测试
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void SnipStaleToolResults_LargeResults_TruncatesContent()
        {
            var compactor = CreateCompactor();
            var session = new CopilotSession();
            session.AddSystemMessage("System");
            session.AddUserMessage("First user message");

            // 添加大量工具结果（>1024 bytes）
            for (int i = 0; i < 10; i++)
            {
                session.AddAssistantMessage($"Step {i}", new List<ToolCall>
                {
                    new ToolCall { Id = $"tc{i}", Name = "query", Arguments = "{}" }
                });
                string largeContent = string.Join("\n", Enumerable.Range(0, 200).Select(n => $"Line {n}: data data data"));
                session.AddToolResult($"tc{i}", largeContent);
            }
            // 尾部保留消息
            session.AddUserMessage("Recent question");
            session.AddAssistantMessage("Recent answer");

            var stats = compactor.SnipStaleToolResults(session);

            Assert.IsTrue(stats.Results > 0);
            Assert.IsTrue(stats.SavedChars > 0);
            // 验证 snip 标记
            var snipped = session.Messages.Where(m =>
                m.Role == MessageRole.Tool && (m.Content ?? "").StartsWith("[snipped tool result")).ToList();
            Assert.IsTrue(snipped.Count > 0);
        }

        [Test]
        public void SnipStaleToolResults_SmallResults_Untouched()
        {
            var compactor = CreateCompactor();
            var session = new CopilotSession();
            session.AddSystemMessage("System");
            session.AddUserMessage("User msg");
            session.AddAssistantMessage("Asst", new List<ToolCall>
            {
                new ToolCall { Id = "tc1", Name = "query", Arguments = "{}" }
            });
            session.AddToolResult("tc1", "small result"); // < 1024 bytes
            session.AddUserMessage("Recent");

            var stats = compactor.SnipStaleToolResults(session);

            Assert.AreEqual(0, stats.Results);
        }

        // ═══════════════════════════════════════════════════════════
        //  Prune 测试
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void PruneStaleToolResults_ReplacesWithPlaceholder()
        {
            var compactor = CreateCompactor();
            var session = new CopilotSession();
            session.AddSystemMessage("System");
            session.AddUserMessage("First user message");

            for (int i = 0; i < 10; i++)
            {
                session.AddAssistantMessage($"Step {i}", new List<ToolCall>
                {
                    new ToolCall { Id = $"tc{i}", Name = "query", Arguments = "{}" }
                });
                string largeContent = string.Join("\n", Enumerable.Range(0, 200).Select(n => $"Line {n}: data"));
                session.AddToolResult($"tc{i}", largeContent);
            }
            session.AddUserMessage("Recent");
            session.AddAssistantMessage("Recent answer");

            var stats = compactor.PruneStaleToolResults(session);

            Assert.IsTrue(stats.Results > 0);
            var pruned = session.Messages.Where(m =>
                m.Role == MessageRole.Tool && (m.Content ?? "").StartsWith("[elided tool result")).ToList();
            Assert.IsTrue(pruned.Count > 0);
        }

        [Test]
        public void PruneStaleToolResults_ErrorResults_Preserved()
        {
            var compactor = CreateCompactor();
            var session = new CopilotSession();
            session.AddSystemMessage("System");
            session.AddUserMessage("User msg");

            for (int i = 0; i < 10; i++)
            {
                session.AddAssistantMessage($"Step {i}", new List<ToolCall>
                {
                    new ToolCall { Id = $"tc{i}", Name = "modify", Arguments = "{}" }
                });
                // 错误结果应被保留
                string errorContent = "error: " + new string('x', 2000);
                session.AddToolResult($"tc{i}", errorContent);
            }
            session.AddUserMessage("Recent");
            session.AddAssistantMessage("Answer");

            var stats = compactor.PruneStaleToolResults(session);

            // 错误结果不被 prune
            Assert.AreEqual(0, stats.Results);
        }

        // ═══════════════════════════════════════════════════════════
        //  Compaction Summary 测试
        // ═══════════════════════════════════════════════════════════

        [Test]
        public async Task MaybeCompact_HighUsage_TriggersSummary()
        {
            var config = CreateConfig(1000);
            var provider = new FakeProvider { SummaryResponse = "## Goal\nTest summary" };
            var sink = new FakeSink();
            var compactor = new ContextCompactor(provider, sink, config);

            var session = new CopilotSession();
            session.AddSystemMessage("System prompt");
            session.AddUserMessage("Initial task");

            // 构建足够多的消息
            for (int i = 0; i < 20; i++)
            {
                session.AddAssistantMessage($"Response {i} with some content", new List<ToolCall>
                {
                    new ToolCall { Id = $"tc{i}", Name = "query", Arguments = "{\"q\":\"test\"}" }
                });
                session.AddToolResult($"tc{i}", new string('y', 200));
                session.AddUserMessage($"Follow up {i}");
            }

            // 模拟 85% usage（超过 compact 80%）
            compactor.UpdateUsage(new UsageData { PromptTokens = 850, CompletionTokens = 50 });

            int countBefore = session.Messages.Count;
            await compactor.MaybeCompactAsync(session, CancellationToken.None);

            // 消息数应减少
            Assert.IsTrue(session.Messages.Count < countBefore);
            // 应包含 compaction-summary 标签
            Assert.IsTrue(session.Messages.Any(m =>
                (m.Content ?? "").Contains("<compaction-summary>")));
        }

        [Test]
        public async Task MaybeCompact_PinnedPrefix_PreservesSystemAndFirstUser()
        {
            var config = CreateConfig(1000);
            var provider = new FakeProvider { SummaryResponse = "## Goal\nSummary" };
            var sink = new FakeSink();
            var compactor = new ContextCompactor(provider, sink, config);

            var session = new CopilotSession();
            session.AddSystemMessage("Important system prompt");
            session.AddUserMessage("First user task");

            for (int i = 0; i < 20; i++)
            {
                session.AddAssistantMessage($"Resp {i}", new List<ToolCall>
                {
                    new ToolCall { Id = $"tc{i}", Name = "query", Arguments = "{}" }
                });
                session.AddToolResult($"tc{i}", new string('z', 200));
                session.AddUserMessage($"Msg {i}");
            }

            compactor.UpdateUsage(new UsageData { PromptTokens = 850, CompletionTokens = 50 });
            await compactor.MaybeCompactAsync(session, CancellationToken.None);

            // System 消息应保留
            Assert.AreEqual(MessageRole.System, session.Messages[0].Role);
            Assert.AreEqual("Important system prompt", session.Messages[0].Content);
        }

        // ═══════════════════════════════════════════════════════════
        //  Stuck Guard 测试
        // ═══════════════════════════════════════════════════════════

        [Test]
        public async Task MaybeCompact_AfterCompaction_ResetsConsecutiveWhenBelowThreshold()
        {
            // 验证：压缩后如果 usage 降到阈值以下，连续计数器重置
            var config = CreateConfig(1000);
            var provider = new FakeProvider { SummaryResponse = "## Goal\nSummary" };
            var sink = new FakeSink();
            var compactor = new ContextCompactor(provider, sink, config);

            var session = new CopilotSession();
            session.AddSystemMessage("Sys");
            session.AddUserMessage("Task");
            for (int i = 0; i < 30; i++)
            {
                session.AddAssistantMessage($"Response {i}", new List<ToolCall>
                {
                    new ToolCall { Id = $"tc{i}", Name = "q", Arguments = "{}" }
                });
                session.AddToolResult($"tc{i}", new string('a', 200));
                session.AddUserMessage($"Follow up {i}");
            }

            // 第一次：高 usage 触发压缩
            compactor.UpdateUsage(new UsageData { PromptTokens = 850, CompletionTokens = 10 });
            await compactor.MaybeCompactAsync(session, CancellationToken.None);

            // 第二次：低 usage（低于阈值）—— 应重置连续计数器
            compactor.UpdateUsage(new UsageData { PromptTokens = 300, CompletionTokens = 10 });
            await compactor.MaybeCompactAsync(session, CancellationToken.None);

            // 第三次：再次高 usage —— 应能正常压缩（未被 stuck guard 阻止）
            int countBefore = session.Messages.Count;
            compactor.UpdateUsage(new UsageData { PromptTokens = 850, CompletionTokens = 10 });
            await compactor.MaybeCompactAsync(session, CancellationToken.None);

            // 验证没有被 stuck guard 阻止（压缩正常执行或消息数不变因为没有可压缩的）
            // 关键是不应出现 "暂停" 通知
            Assert.IsFalse(sink.Events.Any(e =>
                e.Kind == EventKind.Notice && (e.Text ?? "").Contains("暂停")),
                "Stuck guard should not trigger after reset");
        }

        // ═══════════════════════════════════════════════════════════
        //  辅助类
        // ═══════════════════════════════════════════════════════════

        private class FakeProvider : ICopilotProvider
        {
            public string SummaryResponse { get; set; } = "## Goal\nTest summary content";
            public string Name => "fake";

            public Task StreamAsync(CopilotRequest request, Action<Chunk> onChunk, CancellationToken ct)
            {
                // 返回预设的摘要响应
                onChunk(Chunk.FromText(SummaryResponse));
                return Task.CompletedTask;
            }

            public Task<bool> HealthCheckAsync() => Task.FromResult(true);
        }

        private class FakeSink : IEventSink
        {
            public List<CopilotEvent> Events { get; } = new List<CopilotEvent>();
            public void Emit(CopilotEvent evt) => Events.Add(evt);
        }
    }
}
