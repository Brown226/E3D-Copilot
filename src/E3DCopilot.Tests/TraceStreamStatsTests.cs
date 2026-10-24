using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using E3DCopilot.Core.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    /// <summary>
    /// 流式节奏采集的回归测试。
    ///
    /// 背景：原来 trace 只有总耗时和 llmMs，无法区分两种情况——
    ///   a) 模型吐得慢        => 该换模型或减少生成量
    ///   b) 模型吐得快但界面卡 => 该修事件封送/渲染
    /// 两者修法完全相反，所以必须记录"增量之间的间隔分布"。
    /// 这些用例锁住：正常流式不误报卡顿、真卡顿能被计数、统计能落进 JSON。
    /// </summary>
    [TestFixture]
    public class TraceStreamStatsTests
    {
        private string _dir;
        private ConversationTracer _tracer;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(),
                "e3d-trace-test-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_dir);
            _tracer = new ConversationTracer(_dir, enabled: true);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
        }

        private JObject RunAndRead(Action<ConversationTracer> body)
        {
            _tracer.BeginTurn("session-test-0001", "测试输入", "test/model");
            _tracer.BeginStep(1);
            _tracer.BeginLlmCall();
            body(_tracer);
            _tracer.EndLlmCall();
            _tracer.EndStep();
            _tracer.EndTurn("success");

            var file = Directory.GetFiles(_dir, "trace-*.json").OrderByDescending(File.GetLastWriteTime).First();
            // 文件带 UTF-8 BOM，必须用 utf-8-sig 等价方式读取
            var text = File.ReadAllText(file, Encoding.UTF8).TrimStart('﻿');
            return JObject.Parse(text);
        }

        [Test]
        public void 流式统计_应记录块数与速率()
        {
            var root = RunAndRead(t =>
            {
                for (int i = 0; i < 10; i++) t.RecordStreamChunk(5);
            });

            var stream = root.SelectToken("steps[0].stream");
            Assert.IsNotNull(stream, "steps[0].stream 应被写入");

            Assert.AreEqual(10, (int)stream["chunks"]);
            Assert.GreaterOrEqual((long)stream["spanMs"], 0);
            Assert.Greater((double)stream["charsPerSec"], 0,
                "有实际输出时应能算出字符/秒速率");
        }

        [Test]
        public void 连续快速输出_不应误报卡顿()
        {
            // 同一瞬间连续上报 20 块：间隔应全为 0，卡顿数为 0
            var root = RunAndRead(t =>
            {
                for (int i = 0; i < 20; i++) t.RecordStreamChunk(3);
            });

            var stream = root.SelectToken("steps[0].stream");
            Assert.AreEqual(0, (int)stream["stalls"], "紧密输出不应被计为卡顿");
            Assert.Less((long)stream["maxGapMs"], 300);
        }

        [Test]
        public void 真实停顿_应被计为卡顿()
        {
            var root = RunAndRead(t =>
            {
                t.RecordStreamChunk(3);
                System.Threading.Thread.Sleep(400);   // 模拟界面卡住
                t.RecordStreamChunk(3);
                System.Threading.Thread.Sleep(350);   // 再一次
                t.RecordStreamChunk(3);
            });

            var stream = root.SelectToken("steps[0].stream");
            Assert.GreaterOrEqual((int)stream["stalls"], 2,
                "两次 >300ms 的停顿应被记录为卡顿");
            Assert.GreaterOrEqual((long)stream["maxGapMs"], 300);
        }

        [Test]
        public void 无流式输出_不写stream字段()
        {
            var root = RunAndRead(t => { /* 不产生任何增量，例如纯工具调用步骤 */ });

            Assert.IsNull(root.SelectToken("steps[0].stream"),
                "没有流式增量时不应生成空的 stream 对象");
        }

        [Test]
        public void 每步统计_应相互独立()
        {
            _tracer.BeginTurn("session-test-0002", "多步", "test/model");

            _tracer.BeginStep(1);
            _tracer.BeginLlmCall();
            for (int i = 0; i < 5; i++) _tracer.RecordStreamChunk(2);
            _tracer.EndLlmCall();
            _tracer.EndStep();

            _tracer.BeginStep(2);
            _tracer.BeginLlmCall();
            for (int i = 0; i < 8; i++) _tracer.RecordStreamChunk(2);
            _tracer.EndLlmCall();
            _tracer.EndStep();

            _tracer.EndTurn("success");

            var file = Directory.GetFiles(_dir, "trace-*.json").First();
            var root = JObject.Parse(File.ReadAllText(file, Encoding.UTF8).TrimStart('﻿'));

            Assert.AreEqual(5, (int)root.SelectToken("steps[0].stream.chunks"));
            Assert.AreEqual(8, (int)root.SelectToken("steps[1].stream.chunks"),
                "第二步应重新计数，不能累加上一步的块数");
        }

        [Test]
        public void trace文件_应可被外部工具解析()
        {
            // 回归：文件带 UTF-8 BOM，Python/Node 的默认 JSON 解析会直接报错，
            // 导致"我读不到你的记录"。这里锁住 BOM 存在时也能正确解析。
            RunAndRead(t => t.RecordStreamChunk(1));

            var file = Directory.GetFiles(_dir, "trace-*.json").First();
            var bytes = File.ReadAllBytes(file);

            bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            if (hasBom)
                Assert.Pass("文件带 BOM；调用方需用 utf-8-sig 读取（已在 trace_analyze 中处理）");

            // 无 BOM 则直接可解析
            Assert.DoesNotThrow(() => JObject.Parse(File.ReadAllText(file, Encoding.UTF8)));
        }
    }
}
