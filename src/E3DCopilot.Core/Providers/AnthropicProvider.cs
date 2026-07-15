using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Providers
{
    /// <summary>
    /// Anthropic 原生 Messages API 适配器（SSE 流式，net48 兼容回调模式）。
    /// 端点约定：ProviderConfig.BaseUrl 形如 https://api.anthropic.com/v1，
    /// 实际请求打往 {BaseUrl}/messages。
    /// </summary>
    public class AnthropicProvider : ICopilotProvider
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _model;
        private readonly string _apiKey;

        public string Name { get; set; } = "anthropic";

        /// <summary>重试回调（由 CopilotController 注入）</summary>
        public Action<string, int> OnRetry { get; set; }

        public AnthropicProvider(string baseUrl = "https://api.anthropic.com/v1",
            string model = "claude-sonnet-4-0",
            string apiKey = "")
        {
            _baseUrl = (baseUrl ?? "https://api.anthropic.com/v1").TrimEnd('/');
            _model = model;
            _apiKey = apiKey ?? "";

            // 强制启用 TLS 1.2（.NET 4.8 在某些系统上默认不包含）
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;

            _http = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(120000)
            };
        }

        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                using (var resp = await _http.GetAsync($"{_baseUrl}/models", new CancellationTokenSource(5000).Token))
                {
                    return resp.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task StreamAsync(CopilotRequest request, Action<Chunk> onChunk, CancellationToken ct)
        {
            int maxRetries = 2;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // 提取 system 消息（Anthropic 将其作为顶层字段）
                    string systemText = null;
                    var conversation = new List<ChatMessage>();
                    foreach (var m in request.Messages)
                    {
                        if (m.Role == MessageRole.System)
                            systemText = m.Content;
                        else
                            conversation.Add(m);
                    }

                    var body = new JObject
                    {
                        ["model"] = request.Model ?? _model,
                        ["max_tokens"] = request.MaxTokens,
                        ["stream"] = true,
                        ["messages"] = new JArray(conversation.Select(SerializeMessage).Where(m => m != null))
                    };

                    if (!string.IsNullOrEmpty(systemText))
                        body["system"] = systemText;

                    if (request.Tools != null && request.Tools.Count > 0)
                    {
                        body["tools"] = new JArray(request.Tools.Select(t => new JObject
                        {
                            ["name"] = t.Name,
                            ["description"] = t.Description ?? "",
                            ["input_schema"] = !string.IsNullOrEmpty(t.ParametersJson)
                                ? SafeParse(t.ParametersJson)
                                : JObject.FromObject(new { type = "object", properties = new JObject() })
                        }));
                    }

                    var jsonBody = body.ToString(Formatting.None);
                    var content = new ByteArrayContent(Encoding.UTF8.GetBytes(jsonBody));
                    content.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                    var msg = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/messages")
                    {
                        Content = content
                    };
                    msg.Headers.Add("x-api-key", _apiKey);
                    msg.Headers.Add("anthropic-version", "2023-06-01");
                    msg.Headers.Add("anthropic-beta", "tools-2024-04-04");

                    using (var response = await _http.SendAsync(msg, ct))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            var err = await response.Content.ReadAsStringAsync();
                            onChunk(Chunk.FromText($"\n[API error {response.StatusCode}]: {err}"));
                            return;
                        }

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var reader = new StreamReader(stream))
                        {
                            string pendingToolName = null;
                            string pendingToolId = null;
                            var pendingArgs = new StringBuilder();

                            while (!reader.EndOfStream && !ct.IsCancellationRequested)
                            {
                                string line = await reader.ReadLineAsync();
                                if (string.IsNullOrEmpty(line) || !line.StartsWith("data:")) continue;

                                string data = line.Substring(5).Trim();
                                if (data == "[DONE]") break;

                                JObject evt;
                                try { evt = JObject.Parse(data); }
                                catch (JsonReaderException) { continue; }

                                string type = evt["type"]?.Value<string>();
                                if (type == "content_block_delta")
                                {
                                    var delta = evt["delta"];
                                    if (delta == null) continue;
                                    string dType = delta["type"]?.Value<string>();
                                    if (dType == "text_delta")
                                    {
                                        onChunk(Chunk.FromText(delta["text"]?.Value<string>() ?? ""));
                                    }
                                    else if (dType == "input_json_delta")
                                    {
                                        pendingArgs.Append(delta["partial_json"]?.Value<string>() ?? "");
                                    }
                                }
                                else if (type == "content_block_start")
                                {
                                    var block = evt["content_block"];
                                    if (block != null && block["type"]?.Value<string>() == "tool_use")
                                    {
                                        pendingToolName = block["name"]?.Value<string>();
                                        pendingToolId = block["id"]?.Value<string>();
                                        pendingArgs = new StringBuilder();
                                    }
                                }
                                else if (type == "content_block_stop")
                                {
                                    if (!string.IsNullOrEmpty(pendingToolName))
                                    {
                                        onChunk(Chunk.FromToolCall(new ToolCall
                                        {
                                            Id = pendingToolId ?? Guid.NewGuid().ToString("N"),
                                            Name = pendingToolName,
                                            Arguments = pendingArgs.ToString()
                                        }));
                                        pendingToolName = null;
                                        pendingToolId = null;
                                        pendingArgs = new StringBuilder();
                                    }
                                }
                                else if (type == "message_delta")
                                {
                                    var usage = evt["usage"];
                                    if (usage != null && usage.Type == JTokenType.Object)
                                    {
                                        int outTok = usage["output_tokens"]?.Value<int>() ?? 0;
                                        int inTok = usage["input_tokens"]?.Value<int>() ?? 0;
                                        if (outTok > 0 || inTok > 0)
                                            onChunk(Chunk.FromUsage(outTok, inTok));
                                    }
                                }
                            }
                        }
                    }

                    return;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    int delayMs = (int)Math.Pow(2, attempt) * 1000;
                    onChunk(Chunk.FromText($"\n[Anthropic 连接失败 (第{attempt + 1}次), {delayMs}ms 后重试: {ex.GetType().Name}]"));
                    OnRetry?.Invoke(ex.GetType().Name, attempt + 1);
                    await Task.Delay(delayMs, ct);
                }
            }
        }

        /// <summary>
        /// 将内部 ChatMessage 序列化为 Anthropic message 格式。System 消息返回 null（由调用方抽为顶层字段）。
        /// </summary>
        private static JObject SerializeMessage(ChatMessage m)
        {
            if (m.Role == MessageRole.System) return null;

            var obj = new JObject
            {
                ["role"] = m.Role == MessageRole.Assistant ? "assistant" : "user"
            };

            if (m.Role == MessageRole.Tool)
            {
                obj["content"] = new JArray(new JObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = m.ToolCallId ?? "",
                    ["content"] = m.Content ?? ""
                });
            }
            else if (m.Role == MessageRole.Assistant && m.ToolCalls != null && m.ToolCalls.Count > 0)
            {
                var content = new JArray();
                if (!string.IsNullOrEmpty(m.Content))
                    content.Add(new JObject { ["type"] = "text", ["text"] = m.Content });
                foreach (var tc in m.ToolCalls)
                {
                    content.Add(new JObject
                    {
                        ["type"] = "tool_use",
                        ["id"] = tc.Id,
                        ["name"] = tc.Name,
                        ["input"] = !string.IsNullOrEmpty(tc.Arguments) ? SafeParse(tc.Arguments) : new JObject()
                    });
                }
                obj["content"] = content;
            }
            else
            {
                obj["content"] = m.Content ?? "";
            }

            return obj;
        }

        private static JObject SafeParse(string json)
        {
            try
            {
                return JObject.Parse(json);
            }
            catch (Exception)
            {
                return JObject.FromObject(new { type = "object", properties = new JObject() });
            }
        }
    }
}
