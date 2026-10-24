using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Logging;
using E3DCopilot.Core.Tools.Handlers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    /// <summary>
    /// ConversationTracer 新增诊断字段 + trace_analyze 工具的验证。
    ///
    /// 背景：大王反馈"执行不丝滑、总是卡顿"，但原有 trace 只记录总耗时，
    /// 无法区分「等模型 / 等审批 / 等工具」，导致优化必然改错地方。
    /// 这些用例锁定新字段确实被采集、且分析工具能正确归因。
    /// </summary>
    [TestFixture]
    public class TraceDiagnosticsTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "e3dcopilot-trace-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
        }

        private ConversationTracer NewTracer() => new ConversationTracer(_dir, true, 7);

        [Test]
        public void RecordContext_WritesToolAndMessageSizes()
        {
            var t = NewTracer();
            t.BeginTurn("sess-ctx", "测试输入", "model-x");
            t.BeginStep(0);
            t.RecordContext(12, 30, 34532, 3711, 20000);
            t.EndTurn("success");

            var step = LatestStep();
            var ctx = step["context"];
            Assert.IsNotNull(ctx, "context 字段应被记录");
            Assert.AreEqual(12, (int)ctx["messages"]);
            Assert.AreEqual(30, (int)ctx["tools"]);
            Assert.AreEqual(34532, (int)ctx["toolSchemaChars"]);
            Assert.AreEqual(3711, (int)ctx["systemPromptChars"]);
            Assert.AreEqual(20000, (int)ctx["messageChars"]);
        }

        [Test]
        public void BeginEndLlmCall_AccumulatesLlmMs()
        {
            var t = NewTracer();
            t.BeginTurn("sess-llm", "测试", "model-x");
            t.BeginStep(0);
            t.BeginLlmCall();
            Thread.Sleep(60);
            t.EndLlmCall();
            t.EndTurn("success");

            var step = LatestStep();
            Assert.GreaterOrEqual((long)step["llmMs"], 50,
                "llmMs 应记录到实际等待时长");
        }

        [Test]
        public void MarkFirstToken_RecordsTtftOnce()
        {
            var t = NewTracer();
            t.BeginTurn("sess-ttft", "测试", "model-x");
            t.BeginStep(0);
            t.BeginLlmCall();
            Thread.Sleep(40);
            t.MarkFirstToken();
            long first = (long)LatestStepUnfinishedTtft(t);
            Thread.Sleep(40);
            t.MarkFirstToken(); // 第二次应被忽略
            t.EndLlmCall();
            t.EndTurn("success");

            var step = LatestStep();
            Assert.GreaterOrEqual(first, 30, "首 token 延迟应被记录");
            Assert.Less((long)step["ttftMs"], first + 30, "TTFT 只记录第一次");
        }

        [Test]
        public void RecordApproval_AttachesToToolCall()
        {
            var t = NewTracer();
            t.BeginTurn("sess-appr", "测试", "model-x");
            t.BeginStep(0);
            t.RecordToolStart("call-1", "design_workflow", "{\"a\":1}");
            t.RecordApproval("call-1", 120000, "timeout");
            t.RecordToolEnd("call-1", null, false, 0, "审批超时");
            t.EndTurn("cancelled");

            var step = LatestStep();
            Assert.AreEqual(120000, (long)step["approvalMs"], "步骤级审批耗时应被累计");

            var tool = (JObject)step["toolCalls"][0];
            Assert.AreEqual(120000, (long)tool["approvalMs"], "审批耗时应挂到具体工具上");
            Assert.AreEqual("timeout", (string)tool["approval"]);
        }

        [Test]
        public void RecordToolStart_KeepsOriginalLengths()
        {
            var t = NewTracer();
            t.BeginTurn("sess-len", "测试", "model-x");
            t.BeginStep(0);
            string bigArgs = new string('x', 5000);
            t.RecordToolStart("call-1", "glob", bigArgs);
            t.RecordToolEnd("call-1", new string('y', 9000), true, 100, null);
            t.EndTurn("success");

            var tool = (JObject)LatestStep()["toolCalls"][0];
            Assert.AreEqual(5000, (int)tool["argsLen"], "参数原始长度应保留");
            Assert.AreEqual(9000, (int)tool["resultLen"], "结果原始长度应保留");
            Assert.Less(((string)tool["arguments"]).Length, 5000, "content 仍应被截断以控制体积");
        }

        [Test]
        public async Task TraceAnalyze_FlagsLlmAsBottleneck()
        {
            // 构造一条 LLM 占 95% 的 trace
            var t = NewTracer();
            t.BeginTurn("sess-an", "跑一遍设计全流程", "qoder-local/group/never-stuck");
            t.BeginStep(0);
            t.BeginLlmCall();
            Thread.Sleep(100);
            t.EndLlmCall();
            t.RecordContext(3, 31, 34532, 3711, 16532);
            t.RecordToolStart("c1", "glob", "{}");
            t.RecordToolEnd("c1", "ok", true, 5, null);
            t.EndTurn("success");

            var handler = new TraceAnalyzeHandler(t);
            var result = await handler.ExecuteAsync("{\"action\":\"analyze\"}");

            Assert.IsTrue(result.Success, result.Error);
            StringAssert.Contains("LLM", result.Text);
            StringAssert.Contains("耗时归因", result.Text);
        }

        [Test]
        public async Task TraceAnalyze_DetectsApprovalTimeout()
        {
            var t = NewTracer();
            t.BeginTurn("sess-to", "跑设计全流程", "model-x");
            t.BeginStep(0);
            t.BeginLlmCall();
            t.EndLlmCall();
            t.RecordToolStart("c1", "design_workflow", "{}");
            t.RecordApproval("c1", 120500, "timeout");
            t.RecordToolEnd("c1", null, false, 0, "审批超时");
            t.EndTurn("cancelled");

            var handler = new TraceAnalyzeHandler(t);
            var result = await handler.ExecuteAsync("{\"action\":\"analyze\"}");

            Assert.IsTrue(result.Success, result.Error);
            StringAssert.Contains("审批", result.Text);
            StringAssert.Contains("取消", result.Text);
        }

        [Test]
        public async Task TraceAnalyze_ListAndRawWork()
        {
            var t = NewTracer();
            t.BeginTurn("sess-lr", "测试", "model-x");
            t.BeginStep(0);
            t.EndTurn("success");

            var handler = new TraceAnalyzeHandler(t);

            var list = await handler.ExecuteAsync("{\"action\":\"list\"}");
            Assert.IsTrue(list.Success, list.Error);
            StringAssert.Contains("trace-", list.Text);

            var raw = await handler.ExecuteAsync("{\"action\":\"raw\"}");
            Assert.IsTrue(raw.Success, raw.Error);
            Assert.DoesNotThrow(() => JObject.Parse(raw.Text), "raw 应返回合法 JSON");
        }

        [Test]
        public void TraceAnalyze_IsReadOnly()
        {
            var handler = new TraceAnalyzeHandler(NewTracer());
            Assert.IsTrue(handler.IsReadOnly, "trace_analyze 只读，不应触发审批");
            Assert.AreEqual("trace_analyze", handler.Name);
        }

        /// <summary>
        /// 回归：旧版 trace（无 llmMs 字段）必须靠推算得出 LLM 耗时。
        /// 修复前这里累加的是原始 0 值，导致 100% 等 LLM 的卡死被判成"未发现问题"。
        /// </summary>
        [Test]
        public async Task TraceAnalyze_LegacyTraceWithoutLlmMs_StillAttributesTime()
        {
            Directory.CreateDirectory(_dir);
            // 手工构造一条旧格式 trace：只有 durationMs，无 llmMs/ttftMs/context
            string legacy = @"{
  ""sessionId"": ""legacy01"",
  ""startTime"": ""2026-10-24 17:20:18.000"",
  ""endTime"": ""2026-10-24 17:22:27.000"",
  ""userInput"": ""跑一遍设计全流程"",
  ""model"": ""m"",
  ""totalSteps"": 1,
  ""outcome"": ""success"",
  ""steps"": [
    { ""step"": 1, ""durationMs"": 120000, ""toolCalls"": [] }
  ]
}";
            File.WriteAllText(Path.Combine(_dir, "trace-legacy.json"), legacy, System.Text.Encoding.UTF8);

            var handler = new TraceAnalyzeHandler(NewTracer());
            var r = await handler.ExecuteAsync("{\"action\":\"analyze\",\"file\":\"trace-legacy.json\"}");

            Assert.IsTrue(r.Success, r.Error);
            StringAssert.Contains("LLM 调用占 100%", r.Text,
                "旧 trace 的 LLM 耗时必须靠推算得出，否则会把卡死误判为正常");
        }

        // ── helpers ──

        private JObject LatestStep()
        {
            string file = Directory.GetFiles(_dir, "trace-*.json")
                .OrderByDescending(File.GetLastWriteTime).First();
            var root = JObject.Parse(File.ReadAllText(file));
            return (JObject)root["steps"][0];
        }

        /// <summary>步骤尚未落盘时，通过反射读取当前 step 的 TTFT</summary>
        private long LatestStepUnfinishedTtft(ConversationTracer t)
        {
            var f = typeof(ConversationTracer).GetField("_currentStep",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var step = (TraceStep)f.GetValue(t);
            return step.TtftMs;
        }
    }
}
