using E3DCopilot.Core.Events;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class CopilotEventTests
    {
        [Test]
        public void TextEvent_CreatesCorrectEvent()
        {
            var evt = CopilotEvent.TextEvent("hello");
            Assert.AreEqual(EventKind.Text, evt.Kind);
            Assert.AreEqual("hello", evt.Text);
            Assert.Greater(evt.Timestamp, 0);
        }

        [Test]
        public void Reasoning_CreatesCorrectEvent()
        {
            var evt = CopilotEvent.Reasoning("thinking about it");
            Assert.AreEqual(EventKind.Reasoning, evt.Kind);
            Assert.AreEqual("thinking about it", evt.Text);
        }

        [Test]
        public void Thinking_CreatesCorrectEvent()
        {
            var evt = CopilotEvent.Thinking("deep thought");
            Assert.AreEqual(EventKind.Thinking, evt.Kind);
            Assert.AreEqual("deep thought", evt.Text);
        }

        [Test]
        public void StreamDelta_CreatesCorrectEvent()
        {
            var evt = CopilotEvent.StreamDelta("chunk");
            Assert.AreEqual(EventKind.StreamDelta, evt.Kind);
            Assert.AreEqual("chunk", evt.Text);
        }

        [Test]
        public void StreamEnd_CreatesCorrectEvent()
        {
            var usage = new { total = 100 };
            var evt = CopilotEvent.StreamEnd(usage);
            Assert.AreEqual(EventKind.StreamEnd, evt.Kind);
            Assert.IsNotNull(evt.Data);
        }

        [Test]
        public void ToolStart_CreatesCorrectEvent()
        {
            var evt = CopilotEvent.ToolStart("t1", "query", new { type = "PIPE" }, "query");
            Assert.AreEqual(EventKind.ToolDispatch, evt.Kind);
            Assert.AreEqual("t1", evt.ToolId);
            Assert.AreEqual("query", evt.Text);
            Assert.AreEqual("query", evt.CoreToolName);
            Assert.IsNotNull(evt.Data);
        }

        [Test]
        public void ToolComplete_CreatesCorrectEvent()
        {
            var evt = CopilotEvent.ToolComplete("t1", "result data", new { meta = true });
            Assert.AreEqual(EventKind.ToolResult, evt.Kind);
            Assert.AreEqual("t1", evt.ToolId);
            Assert.AreEqual("result data", evt.Data);
            Assert.IsNotNull(evt.Meta);
        }

        [Test]
        public void ToolFail_CreatesCorrectEvent()
        {
            var evt = CopilotEvent.ToolFail("t1", "some error");
            Assert.AreEqual(EventKind.ToolError, evt.Kind);
            Assert.AreEqual("t1", evt.ToolId);
            Assert.AreEqual("some error", evt.Text);
        }

        [Test]
        public void ApprovalReq_CreatesCorrectEvent()
        {
            var evt = CopilotEvent.ApprovalReq("t1", "确认修改", new { element = "PIPE-001" });
            Assert.AreEqual(EventKind.ApprovalRequest, evt.Kind);
            Assert.AreEqual("t1", evt.ToolId);
            Assert.AreEqual("确认修改", evt.Text);
            Assert.IsNotNull(evt.Data);
        }

        [Test]
        public void Error_CreatesCorrectEvent()
        {
            var evt = CopilotEvent.Error("connection failed");
            Assert.AreEqual(EventKind.Error, evt.Kind);
            Assert.AreEqual("connection failed", evt.Text);
        }

        [Test]
        public void RetryEvent_IncludesAttemptNumber()
        {
            var evt = CopilotEvent.RetryEvent("timeout", 3);
            Assert.AreEqual(EventKind.Retry, evt.Kind);
            Assert.IsTrue(evt.Text.Contains("3"));
            Assert.IsTrue(evt.Text.Contains("timeout"));
        }

        [Test]
        public void TurnDone_CreatesCorrectEvent()
        {
            var evt = CopilotEvent.TurnDone();
            Assert.AreEqual(EventKind.TurnDone, evt.Kind);
        }

        [Test]
        public void Notice_CreatesCorrectEvent()
        {
            var evt = CopilotEvent.Notice("registered tool: query");
            Assert.AreEqual(EventKind.Notice, evt.Kind);
            Assert.AreEqual("registered tool: query", evt.Text);
        }

        [Test]
        public void Timestamp_IsRecentTime()
        {
            long before = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var evt = new CopilotEvent { Kind = EventKind.Text };
            long after = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            Assert.GreaterOrEqual(evt.Timestamp, before);
            Assert.LessOrEqual(evt.Timestamp, after);
        }
    }
}
