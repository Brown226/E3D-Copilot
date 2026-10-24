using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using E3DCopilot.Core.Providers;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    /// <summary>
    /// LLM 流式输出时机。
    ///
    /// 这里锁的是一个真实踩过的坑：
    /// HttpClient.SendAsync 若不指定 HttpCompletionOption.ResponseHeadersRead，
    /// 默认会等到「整个响应体收完」才返回。SSE 的每个增量都会被缓冲在底层，
    /// 等全部结束才一次性交给回调。表现是：用户点发送后界面长时间无任何反应，
    /// 然后整段文字瞬间蹦出来——这正是"不丝滑"的直接来源。
    ///
    /// 实测同一端点同一提问：
    ///   默认(ResponseContentRead)：首块 7782ms，674 块在 14ms 内集中到达
    ///   ResponseHeadersRead     ：首块 1400ms，跨度 5918ms，逐块到达
    ///
    /// 这个测试用一个本地慢速 SSE 服务复现该场景，不依赖外部模型。
    /// </summary>
    [TestFixture]
    public class VllmStreamingTests
    {
        private HttpListener _listener;
        private string _baseUrl;
        private Thread _serverThread;
        private volatile bool _stop;

        [SetUp]
        public void SetUp()
        {
            // 找一个空闲端口
            int port = 0;
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            _baseUrl = $"http://127.0.0.1:{port}/v1";
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            _stop = false;

            _serverThread = new Thread(ServeLoop) { IsBackground = true };
            _serverThread.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _stop = true;
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
        }

        /// <summary>
        /// 模拟真实 LLM：先延迟首字，然后按固定间隔逐块推送 SSE。
        /// 共 20 块，每块间隔 60ms —— 总计约 1.2s 的流式跨度。
        /// </summary>
        private void ServeLoop()
        {
            while (!_stop)
            {
                HttpListenerContext ctx;
                try { ctx = _listener.GetContext(); }
                catch { return; }

                try
                {
                    var res = ctx.Response;

                    // 路径含 "fail" 时返回 500，用于验证错误回传（provider 会自动追加 /chat/completions）
                    if (ctx.Request.Url.AbsolutePath.Contains("fail"))
                    {
                        var errBytes = Encoding.UTF8.GetBytes("{\"error\":\"boom\"}");
                        res.StatusCode = 500;
                        res.ContentType = "application/json";
                        res.ContentLength64 = errBytes.Length;
                        res.OutputStream.Write(errBytes, 0, errBytes.Length);
                        res.OutputStream.Close();
                        res.Close();
                        continue;
                    }

                    res.StatusCode = 200;
                    res.ContentType = "text/event-stream";
                    res.SendChunked = true;
                    res.Headers["Cache-Control"] = "no-cache";

                    var output = res.OutputStream;
                    var enc = new UTF8Encoding(false);

                    void Send(string payload)
                    {
                        var bytes = enc.GetBytes("data: " + payload + "\n\n");
                        output.Write(bytes, 0, bytes.Length);
                        output.Flush();   // 关键：每块立即下发，模拟真实 SSE
                    }

                    // 首字延迟 300ms（模拟模型 prefill）
                    Thread.Sleep(300);

                    for (int i = 0; i < 20; i++)
                    {
                        var json = "{\"choices\":[{\"delta\":{\"content\":\"字" + i + "\"}}]}";
                        Send(json);
                        Thread.Sleep(60);
                    }
                    Send("[DONE]");

                    output.Close();
                    res.Close();
                }
                catch
                {
                    try { ctx.Response.Abort(); } catch { }
                }
            }
        }

        [Test]
        public void 流式输出_首字应及时到达且增量分散到达()
        {
            var provider = new VllmProvider(_baseUrl, "test-model", "");

            var request = new CopilotRequest
            {
                Model = "test-model",
                Temperature = 0.1,
                MaxTokens = 256,
                Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = MessageRole.User, Content = "hi" }
                }
            };

            var arrivals = new List<long>();
            var sw = Stopwatch.StartNew();
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                var task = provider.StreamAsync(request, chunk =>
                {
                    if (!string.IsNullOrEmpty(chunk.Content))
                        arrivals.Add(sw.ElapsedMilliseconds);
                }, cts.Token);

                Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(20)), "流式调用未在 20s 内完成");
            }
            sw.Stop();

            Assert.IsNotEmpty(arrivals, "应至少收到一个内容增量");
            Assert.AreEqual(20, arrivals.Count, "应收到全部 20 个增量");

            long first = arrivals[0];
            long last = arrivals[arrivals.Count - 1];
            long span = last - first;

            // 服务端首字延迟 300ms，加上连接开销，首块应在 1.5s 内出现
            Assert.Less(first, 1500,
                $"首个增量到达过慢（{first}ms）。若接近总耗时，说明响应体被整体缓冲后才开始读——" +
                "检查 SendAsync 是否漏了 HttpCompletionOption.ResponseHeadersRead。");

            // 服务端 20 块 × 60ms ≈ 1200ms 的真实跨度。
            // 若被缓冲，所有块会在最后几毫秒内集中到达（span 接近 0）。
            Assert.Greater(span, 700,
                $"增量集中到达（跨度仅 {span}ms），说明流被整体缓冲后一次性交付。" +
                "这正是『界面长时间无反应、然后文字瞬间全出』的表现。");
        }

        [Test]
        public void 增量内容_应完整且顺序正确()
        {
            var provider = new VllmProvider(_baseUrl, "test-model", "");
            var request = new CopilotRequest
            {
                Model = "test-model",
                Temperature = 0.1,
                MaxTokens = 256,
                Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = MessageRole.User, Content = "hi" }
                }
            };

            var text = new StringBuilder();
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                var task = provider.StreamAsync(request, chunk =>
                {
                    if (!string.IsNullOrEmpty(chunk.Content)) text.Append(chunk.Content);
                }, cts.Token);
                Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(20)));
            }

            var expected = new StringBuilder();
            for (int i = 0; i < 20; i++) expected.Append("字" + i);

            Assert.AreEqual(expected.ToString(), text.ToString(),
                "流式内容必须按服务端顺序完整拼接，不能丢块或乱序");
        }

        [Test]
        public void 服务端报错_应作为错误信息回传而不是抛异常崩溃()
        {
            // baseUrl 含 fail 时模拟服务端返回 500
            var provider = new VllmProvider(_baseUrl + "/fail", "test-model", "");
            var request = new CopilotRequest
            {
                Model = "test-model",
                Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = MessageRole.User, Content = "hi" }
                }
            };

            var text = new StringBuilder();
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                var task = provider.StreamAsync(request, chunk =>
                {
                    if (chunk.Content != null) text.Append(chunk.Content);
                }, cts.Token);

                Assert.DoesNotThrow(() => task.Wait(TimeSpan.FromSeconds(20)),
                    "服务端错误不应以未处理异常的形式抛给上层");
            }

            Assert.That(text.ToString(), Does.Contain("API error"),
                "应把 HTTP 错误码回传给界面，便于当场判断问题");
        }
    }
}
