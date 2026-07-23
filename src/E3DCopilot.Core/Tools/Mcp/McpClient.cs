using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Mcp
{
    // ═══════════════════════════════════════════════════════════
    //  数据模型（对齐 Reasonix mcpTool / McpResource / McpPrompt）
    // ═══════════════════════════════════════════════════════════

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

    /// <summary>MCP 工具元数据（对齐 Reasonix mcpTool）</summary>
    public class McpToolInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string InputSchema { get; set; }
        public bool ReadOnlyHint { get; set; }
        public bool DestructiveHint { get; set; }
    }

    /// <summary>MCP tools/call 结果（对齐 Reasonix parseToolResult）</summary>
    public class McpToolCallResult
    {
        public string Text { get; set; }
        public List<string> Images { get; set; }
        public bool IsError { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    //  传输层接口
    // ═══════════════════════════════════════════════════════════

    /// <summary>传输层抽象（stdio / http 均实现）</summary>
    public interface IMcpTransport : IDisposable
    {
        Task<JObject> SendAsync(JObject request, CancellationToken ct);
    }

    /// <summary>只读 MCP 客户端接口（兼容现有 mcp_knowledge 工具）</summary>
    public interface IMcpClient : IDisposable
    {
        string Name { get; }
        Task InitializeAsync(CancellationToken ct);
        Task<McpResource[]> ListResourcesAsync(CancellationToken ct);
        Task<string> ReadResourceAsync(string uri, CancellationToken ct);
        Task<McpPrompt[]> ListPromptsAsync(CancellationToken ct);
        Task<string> GetPromptAsync(string name, Dictionary<string, string> arguments, CancellationToken ct);
    }

    // ═══════════════════════════════════════════════════════════
    //  McpClient — 完整 MCP 客户端（对齐 Reasonix Client）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 完整 MCP 客户端：支持 tools/list + tools/call + resources + prompts。
    /// 对齐 Reasonix internal/plugin Client 结构。
    /// 协议版本: 2024-11-05
    /// </summary>
    public class McpClient : IMcpClient
    {
        private readonly IMcpTransport _transport;
        private int _nextId = 1;
        private bool _initialized;
        private readonly object _lock = new object();

        // ── Server capabilities（initialize 时记录）──
        public bool HasTools { get; private set; }
        public bool HasPrompts { get; private set; }
        public bool HasResources { get; private set; }

        // ── 工具缓存（对齐 Reasonix toolsListed + toolAdapters）──
        private bool _toolsListed;
        private McpToolInfo[] _cachedTools;

        // ── 超时配置（对齐 Reasonix 三级超时）──
        private readonly int _defaultCallTimeoutMs;
        private readonly int _callTimeoutMs;
        private readonly Dictionary<string, int> _toolTimeouts;

        public string Name { get; }

        /// <summary>连接状态</summary>
        public bool IsConnected { get; private set; }

        /// <summary>已发现的工具数量</summary>
        public int ToolCount => _cachedTools?.Length ?? 0;

        public McpClient(string name, IMcpTransport transport,
            int defaultCallTimeoutMs = 300000,
            int callTimeoutMs = 0,
            Dictionary<string, int> toolTimeouts = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _defaultCallTimeoutMs = defaultCallTimeoutMs;
            _callTimeoutMs = callTimeoutMs;
            _toolTimeouts = toolTimeouts;
        }

        // ═══════════════════════════════════════════════════════════
        //  JSON-RPC 基础方法
        // ═══════════════════════════════════════════════════════════

        private async Task<JObject> RpcAsync(string method, JObject para, CancellationToken ct, int? timeoutOverrideMs = null)
        {
            int id;
            lock (_lock) { id = _nextId++; }

            var req = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method
            };
            if (para != null) req["params"] = para;

            // 三级超时: per-tool > per-server > default
            int timeoutMs = timeoutOverrideMs ?? GetTimeoutMs(method, para);
            using (var timeoutCts = new CancellationTokenSource(timeoutMs))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token))
            {
                JObject resp;
                try
                {
                    resp = await _transport.SendAsync(req, linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    throw new TimeoutException($"MCP {method} 超时 ({timeoutMs}ms)，server: {Name}");
                }

                if (resp["error"] != null)
                    throw new InvalidOperationException($"MCP error [{Name}]: {resp["error"]}");

                IsConnected = true;
                return resp;
            }
        }

        private int GetTimeoutMs(string method, JObject para)
        {
            // per-tool 超时
            if (method == "tools/call" && para != null && _toolTimeouts != null)
            {
                string toolName = para["name"]?.Value<string>();
                if (!string.IsNullOrEmpty(toolName) && _toolTimeouts.TryGetValue(toolName, out int tt))
                    return tt;
            }
            // per-server 超时
            if (_callTimeoutMs > 0) return _callTimeoutMs;
            // 全局默认
            return _defaultCallTimeoutMs;
        }

        // ═══════════════════════════════════════════════════════════
        //  Initialize — 握手 + 能力检测（对齐 Reasonix initializeSession）
        // ═══════════════════════════════════════════════════════════

        public async Task InitializeAsync(CancellationToken ct)
        {
            if (_initialized) return;

            var resp = await RpcAsync("initialize", new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JObject(),
                ["clientInfo"] = new JObject { ["name"] = "e3d-copilot", ["version"] = "2.0" }
            }, ct).ConfigureAwait(false);

            // 记录 server capabilities
            var caps = resp["result"]?["capabilities"] as JObject;
            if (caps != null)
            {
                HasTools = caps["tools"] != null;
                HasPrompts = caps["prompts"] != null;
                HasResources = caps["resources"] != null;
            }

            // 发送 initialized 通知
            try
            {
                var notify = new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["method"] = "notifications/initialized",
                    ["params"] = new JObject()
                };
                await _transport.SendAsync(notify, ct).ConfigureAwait(false);
            }
            catch { /* 通知失败不影响主流程 */ }

            _initialized = true;
            CopilotLogger.Info("MCP [{0}] 初始化完成: tools={1}, prompts={2}, resources={3}",
                Name, HasTools, HasPrompts, HasResources);
        }

        // ═══════════════════════════════════════════════════════════
        //  tools/list — 工具发现（对齐 Reasonix listTools）
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 获取 MCP server 提供的工具列表（带缓存）
        /// </summary>
        public async Task<McpToolInfo[]> ListToolsAsync(CancellationToken ct)
        {
            if (_toolsListed && _cachedTools != null)
                return _cachedTools;

            if (!HasTools)
                return new McpToolInfo[0];

            var resp = await RpcAsync("tools/list", new JObject(), ct).ConfigureAwait(false);
            var toolsArr = resp["result"]?["tools"] as JArray;
            if (toolsArr == null)
            {
                _cachedTools = new McpToolInfo[0];
                _toolsListed = true;
                return _cachedTools;
            }

            var list = new List<McpToolInfo>();
            foreach (var item in toolsArr)
            {
                var annotations = item["annotations"] as JObject;
                list.Add(new McpToolInfo
                {
                    Name = item["name"]?.Value<string>() ?? "",
                    Description = item["description"]?.Value<string>() ?? "",
                    InputSchema = item["inputSchema"]?.ToString(Formatting.None) ?? "{\"type\":\"object\"}",
                    ReadOnlyHint = annotations?["readOnlyHint"]?.Value<bool>() ?? false,
                    DestructiveHint = annotations?["destructiveHint"]?.Value<bool>() ?? false
                });
            }

            _cachedTools = list.ToArray();
            _toolsListed = true;
            CopilotLogger.Info("MCP [{0}] 发现 {1} 个工具", Name, _cachedTools.Length);
            return _cachedTools;
        }

        // ═══════════════════════════════════════════════════════════
        //  tools/call — 工具执行（对齐 Reasonix remoteTool.Execute）
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 调用 MCP 工具（对齐 Reasonix tools/call + parseToolResult）
        /// </summary>
        public async Task<McpToolCallResult> CallToolAsync(string rawToolName, string argumentsJson, CancellationToken ct)
        {
            JObject args = null;
            if (!string.IsNullOrEmpty(argumentsJson))
            {
                try { args = JObject.Parse(argumentsJson); }
                catch { args = new JObject(); }
            }

            var para = new JObject
            {
                ["name"] = rawToolName,
                ["arguments"] = args ?? new JObject()
            };

            var resp = await RpcAsync("tools/call", para, ct).ConfigureAwait(false);
            return ParseToolResult(resp["result"]);
        }

        /// <summary>
        /// 解析 tools/call 结果（对齐 Reasonix parseToolResult）
        /// </summary>
        private static McpToolCallResult ParseToolResult(JToken result)
        {
            var output = new McpToolCallResult { Images = new List<string>() };

            if (result == null)
            {
                output.Text = "(no result)";
                return output;
            }

            output.IsError = result["isError"]?.Value<bool>() ?? false;
            var content = result["content"] as JArray;
            if (content == null)
            {
                output.Text = result.ToString(Formatting.None);
                return output;
            }

            var sb = new System.Text.StringBuilder();
            int imageCount = 0;
            foreach (var c in content)
            {
                string type = c["type"]?.Value<string>() ?? "text";
                if (type == "text")
                {
                    sb.Append(c["text"]?.Value<string>() ?? "");
                }
                else if (type == "image")
                {
                    if (imageCount < 5)
                    {
                        string mime = c["mimeType"]?.Value<string>() ?? "image/png";
                        string data = c["data"]?.Value<string>() ?? "";
                        if (!string.IsNullOrEmpty(data))
                        {
                            output.Images.Add($"data:{mime};base64,{data}");
                            sb.Append($"[image: {mime}]");
                            imageCount++;
                        }
                    }
                    else
                    {
                        sb.Append("[image omitted: limit reached]");
                    }
                }
            }

            output.Text = sb.ToString();
            return output;
        }

        // ═══════════════════════════════════════════════════════════
        //  resources / prompts（保持向后兼容）
        // ═══════════════════════════════════════════════════════════

        public async Task<McpResource[]> ListResourcesAsync(CancellationToken ct)
        {
            if (!HasResources && _initialized) return new McpResource[0];

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
            if (!HasPrompts && _initialized) return new McpPrompt[0];

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
