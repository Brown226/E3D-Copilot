using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Mcp
{
    public class McpResource
    {
        public string Uri;
        public string Name;
        public string Description;
        public string MimeType;
    }

    public class McpPromptArg
    {
        public string Name;
        public string Description;
        public bool Required;
    }

    public class McpPrompt
    {
        public string Name;
        public string Description;
        public List<McpPromptArg> Arguments = new List<McpPromptArg>();
    }

    /// <summary>传输层抽象（stdio / http 均实现），由 McpClient 通过依赖注入使用，便于测试</summary>
    public interface IMcpTransport : IDisposable
    {
        Task<JObject> SendAsync(JObject request, CancellationToken ct);
    }

    /// <summary>只读 MCP 客户端接口：仅 resources / prompts（不暴露 tools/call 写操作）</summary>
    public interface IMcpClient : IDisposable
    {
        string Name { get; }
        Task InitializeAsync(CancellationToken ct);
        Task<McpResource[]> ListResourcesAsync(CancellationToken ct);
        Task<string> ReadResourceAsync(string uri, CancellationToken ct);
        Task<McpPrompt[]> ListPromptsAsync(CancellationToken ct);
        Task<string> GetPromptAsync(string name, Dictionary<string, string> arguments, CancellationToken ct);
    }

    /// <summary>只读 MCP 客户端：协议 2024-11-05，仅 resources/prompts，按 Reasonix plugin 设计简化</summary>
    public class McpClient : IMcpClient
    {
        private readonly IMcpTransport _transport;
        private int _nextId = 1;
        private bool _initialized;

        public string Name { get; }

        public McpClient(string name, IMcpTransport transport)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        private async Task<JObject> RpcAsync(string method, JObject para, CancellationToken ct)
        {
            var req = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = _nextId++,
                ["method"] = method
            };
            if (para != null) req["params"] = para;

            var resp = await _transport.SendAsync(req, ct).ConfigureAwait(false);
            if (resp["error"] != null)
                throw new InvalidOperationException("MCP error: " + resp["error"].ToString());
            return resp;
        }

        public async Task InitializeAsync(CancellationToken ct)
        {
            if (_initialized) return;
            await RpcAsync("initialize", new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JObject(),
                ["clientInfo"] = new JObject { ["name"] = "e3d-copilot", ["version"] = "2.0" }
            }, ct).ConfigureAwait(false);
            _initialized = true;
        }

        public async Task<McpResource[]> ListResourcesAsync(CancellationToken ct)
        {
            var r = await RpcAsync("resources/list", new JObject(), ct).ConfigureAwait(false);
            var arr = r["result"]?["resources"] as JArray;
            if (arr == null) return new McpResource[0];
            var list = new List<McpResource>();
            foreach (var item in arr)
                list.Add(new McpResource
                {
                    Uri = item["uri"]?.Value<string>(),
                    Name = item["name"]?.Value<string>(),
                    Description = item["description"]?.Value<string>(),
                    MimeType = item["mimeType"]?.Value<string>()
                });
            return list.ToArray();
        }

        public async Task<string> ReadResourceAsync(string uri, CancellationToken ct)
        {
            var r = await RpcAsync("resources/read", new JObject { ["uri"] = uri }, ct).ConfigureAwait(false);
            var contents = r["result"]?["contents"] as JArray;
            if (contents == null) return "";
            var sb = new System.Text.StringBuilder();
            foreach (var c in contents)
                sb.Append(c["text"]?.Value<string>());
            return sb.ToString();
        }

        public async Task<McpPrompt[]> ListPromptsAsync(CancellationToken ct)
        {
            var r = await RpcAsync("prompts/list", new JObject(), ct).ConfigureAwait(false);
            var arr = r["result"]?["prompts"] as JArray;
            if (arr == null) return new McpPrompt[0];
            var list = new List<McpPrompt>();
            foreach (var item in arr)
            {
                var p = new McpPrompt
                {
                    Name = item["name"]?.Value<string>(),
                    Description = item["description"]?.Value<string>()
                };
                var args = item["arguments"] as JArray;
                if (args != null)
                    foreach (var a in args)
                        p.Arguments.Add(new McpPromptArg
                        {
                            Name = a["name"]?.Value<string>(),
                            Description = a["description"]?.Value<string>(),
                            Required = a["required"]?.Value<bool>() ?? false
                        });
                list.Add(p);
            }
            return list.ToArray();
        }

        public async Task<string> GetPromptAsync(string name, Dictionary<string, string> arguments, CancellationToken ct)
        {
            var para = new JObject { ["name"] = name };
            if (arguments != null && arguments.Count > 0)
            {
                var arr = new JArray();
                foreach (var kv in arguments)
                    arr.Add(new JObject { ["name"] = kv.Key, ["value"] = kv.Value });
                para["arguments"] = arr;
            }
            var r = await RpcAsync("prompts/get", para, ct).ConfigureAwait(false);
            var msgs = r["result"]?["messages"] as JArray;
            if (msgs == null) return "";
            var sb = new System.Text.StringBuilder();
            foreach (var m in msgs)
            {
                var content = m["content"];
                if (content != null && content.Type == JTokenType.Object)
                    sb.Append(content["text"]?.Value<string>());
            }
            return sb.ToString();
        }

        public void Dispose() => _transport.Dispose();
    }
}
