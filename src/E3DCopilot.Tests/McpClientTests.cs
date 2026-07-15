using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools.Mcp;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class McpClientTests
    {
        class FakeTransport : IMcpTransport
        {
            public List<JObject> Sent = new List<JObject>();
            public Dictionary<string, JObject> Responses = new Dictionary<string, JObject>();
            public void Dispose() { }
            public Task<JObject> SendAsync(JObject request, CancellationToken ct)
            {
                Sent.Add(request);
                string method = request["method"].Value<string>();
                var resp = Responses.TryGetValue(method, out var r)
                    ? (JObject)r.DeepClone()
                    : new JObject { ["result"] = new JObject() };
                resp["jsonrpc"] = "2.0";
                resp["id"] = request["id"];
                return Task.FromResult(resp);
            }
        }

        [Test]
        public async Task Initialize_SendsInitializeRequest()
        {
            var t = new FakeTransport();
            var client = new McpClient("s1", t);
            await client.InitializeAsync(CancellationToken.None);
            Assert.IsTrue(t.Sent.Exists(m => m["method"].Value<string>() == "initialize"));
        }

        [Test]
        public async Task ListResources_ParsesArray()
        {
            var t = new FakeTransport();
            t.Responses["resources/list"] = new JObject
            {
                ["result"] = new JObject
                {
                    ["resources"] = new JArray(new JObject { ["uri"] = "file:///a", ["name"] = "A" })
                }
            };
            var client = new McpClient("s1", t);
            var res = await client.ListResourcesAsync(CancellationToken.None);
            Assert.AreEqual(1, res.Length);
            Assert.AreEqual("file:///a", res[0].Uri);
        }

        [Test]
        public async Task ReadResource_ConcatenatesText()
        {
            var t = new FakeTransport();
            t.Responses["resources/read"] = new JObject
            {
                ["result"] = new JObject
                {
                    ["contents"] = new JArray(
                        new JObject { ["text"] = "hello " },
                        new JObject { ["text"] = "world" })
                }
            };
            var client = new McpClient("s1", t);
            var txt = await client.ReadResourceAsync("x", CancellationToken.None);
            Assert.AreEqual("hello world", txt);
        }

        [Test]
        public async Task GetPrompt_ParsesMessages()
        {
            var t = new FakeTransport();
            t.Responses["prompts/get"] = new JObject
            {
                ["result"] = new JObject
                {
                    ["messages"] = new JArray(
                        new JObject { ["content"] = new JObject { ["type"] = "text", ["text"] = "tip: use PML" } })
                }
            };
            var client = new McpClient("s1", t);
            var txt = await client.GetPromptAsync("p", new Dictionary<string, string>(), CancellationToken.None);
            Assert.IsTrue(txt.Contains("PML"));
        }

        [Test]
        public void Handler_IsReadOnly()
        {
            var h = new McpToolHandler(new McpRegistry());
            Assert.IsTrue(h.IsReadOnly);
            Assert.AreEqual("mcp_knowledge", h.Name);
        }

        [Test]
        public async Task Handler_UnknownServer_Fails()
        {
            var h = new McpToolHandler(new McpRegistry());
            var r = await h.ExecuteAsync("{\"server\":\"nope\",\"action\":\"list\"}", CancellationToken.None);
            Assert.IsFalse(r.Success);
        }
    }
}
