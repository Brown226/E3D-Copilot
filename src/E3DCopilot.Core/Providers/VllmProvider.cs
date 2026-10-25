﻿using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Providers
{
    /// <summary>
    /// vLLM + Qwen adapter (OpenAI-compatible interface)
    /// net48 compatible: uses callback mode instead of IAsyncEnumerable
    /// </summary>
    public class VllmProvider : ICopilotProvider
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _model;
        private readonly string _apiKey;
        private readonly CopilotConfig.ProviderConfig _configProvider;

        /// <summary>
        /// 重试回调（由 CopilotController 注入，通知前端重试状态）
        /// </summary>
        public Action<string, int> OnRetry { get; set; }

        /// <summary>
        /// Provider 名称
        /// </summary>
        public string Name { get; set; } = "vllm";
        
        // Streaming tool_calls accumulator (stored by index)
        private readonly System.Collections.Generic.Dictionary<int, ToolCallAccumulator> _toolCallAccumulators
            = new System.Collections.Generic.Dictionary<int, ToolCallAccumulator>();

        public VllmProvider(string baseUrl = "http://localhost:8000/v1",
            string model = "Qwen3.5-32B",
            string apiKey = "",
            CopilotConfig.ProviderConfig configProvider = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _model = model;
            _apiKey = apiKey ?? "";
            // 该字段此前从未被赋值（编译器一直报 CS0649），导致 config.json 里的
            // Effort / ReasoningProtocol 永远不生效，请求固定用硬编码的 "high" / "deepseek"。
            _configProvider = configProvider;

            // 强制启用 TLS 1.2（.NET 4.8 在某些系统上默认不包含，导致 HTTPS 连接失败）
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;

            // 超时尊重配置（推理模型思考可达数分钟，120s 硬编码会把长思考掐断）
            int timeoutMs = _configProvider?.TimeoutMs > 0 ? _configProvider.TimeoutMs : 120000;
            _http = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(timeoutMs)
            };
        }

        /// <summary>
        /// 从 /v1/models 自动发现当前模型的真实上下文窗口。
        /// 配置里 ContextWindow <= 0 时以服务端返回为准（context_length / max_input_tokens），
        /// 回写内存中的 ProviderConfig —— BuildRequest 预算窗口与 ContextCompactor 阈值均依赖该值。
        /// </summary>
        public async Task DiscoverContextWindowAsync(CopilotConfig.ProviderConfig config, string model = null)
        {
            try
            {
                string url = $"{_baseUrl}/models";
                using (var resp = await _http.GetAsync(url, new CancellationTokenSource(8000).Token))
                {
                    if (!resp.IsSuccessStatusCode) return;
                    string body = await resp.Content.ReadAsStringAsync();
                    var root = JObject.Parse(body);
                    string target = model ?? _model;
                    foreach (var m in root["models"] as JArray ?? root["data"] as JArray ?? new JArray())
                    {
                        if (!string.Equals(m["id"]?.Value<string>(), target, StringComparison.OrdinalIgnoreCase))
                            continue;
                        int? window = m["context_length"]?.Type == JTokenType.Integer ? m["context_length"].Value<int>()
                            : m["context_window"]?.Type == JTokenType.Integer ? m["context_window"].Value<int>()
                            : m["max_input_tokens"]?.Type == JTokenType.Integer ? m["max_input_tokens"].Value<int>()
                            : (int?)null;
                        if (window > 0)
                        {
                            config.ContextWindow = window.Value;
                            CopilotLogger.Info("ContextWindow 自动发现: {0} → {1}", target, window.Value);
                        }
                        return;
                    }
                }
            }
            catch
            {
                // 发现失败保持配置现状，静默降级
            }
        }
        
        /// <summary>
        /// ToolCall accumulator (for streaming fragment accumulation)
        /// </summary>
        private class ToolCallAccumulator
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public StringBuilder Arguments { get; } = new StringBuilder();
            public bool NameSent { get; set; }
        }

        /// <summary>
        /// 健康检查 — 调用 /models 端点检查 Provider 是否可用
        /// </summary>
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

        /// <summary>
        /// Stream call vLLM, returns chunks via onChunk callback
        /// Uses StreamReader to read SSE line by line
        /// </summary>
        public async Task StreamAsync(
            CopilotRequest request,
            Action<Chunk> onChunk,
            CancellationToken ct)
        {
            // Reset tool_calls accumulator (new request each time)
            _toolCallAccumulators.Clear();

            int maxRetries = 2;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Build request body (using JObject for conditional fields)
                    var bodyObj = new JObject
                    {
                        ["model"] = request.Model ?? _model,
                        ["stream"] = true,
                        ["temperature"] = request.Temperature,
                        ["max_tokens"] = request.MaxTokens,
                        // OpenAI 兼容流式默认不回传 usage，必须显式请求。
                        // ContextCompactor 依赖真实 prompt_tokens 触发压缩，缺了就永远走字符估算。
                        ["stream_options"] = new JObject { ["include_usage"] = true },
                        ["messages"] = new JArray(request.Messages.Select(SerializeMessage))
                    };

                    // DeepSeek/其他模型特殊优化（参考 Reasonix 设计）
                    var modelName = request.Model ?? _model;
                    string effort = request.Effort ?? _configProvider?.Effort ?? "high";
                    string protocol = request.ReasoningProtocol ?? _configProvider?.ReasoningProtocol ?? "deepseek";

                    // 根据推理协议配置推理字段
                    if (protocol == "deepseek" || protocol == "openai")
                    {
                        // DeepSeek Reasoner / 推理模型 — 禁用 temperature，用 thinking
                        if (IsDeepSeekReasoningModel(modelName))
                        {
                            bodyObj.Remove("temperature");
                            bodyObj["thinking"] = new JObject
                            {
                                ["type"] = "enabled"
                            };
                            // 可选：传递 effort（部分模型支持 reasoning_effort 配合 thinking）
                            if (!string.IsNullOrEmpty(effort) && effort != "high")
                                bodyObj["reasoning_effort"] = effort;
                        }
                        else if (IsDeepSeekModel(modelName))
                        {
                            // DeepSeek Chat 系列 — 用 reasoning_effort
                            bodyObj["reasoning_effort"] = effort;
                        }
                        else if (protocol == "deepseek")
                        {
                            // 非 DeepSeek 模型但配置了 deepseek 协议：尝试设 effort
                            bodyObj["reasoning_effort"] = effort;
                        }
                        // openai 协议：不设额外字段，让 API 自行决定
                    }

                    // MiniMax 适配
                    if (IsMiniMaxModel(modelName))
                    {
                        bodyObj["thinking"] = new JObject
                        {
                            ["type"] = effort == "adaptive" ? "adaptive" : "enabled"
                        };
                    }
        
                    // Inject tool definitions (OpenAI-compatible format)
                    if (request.Tools != null && request.Tools.Count > 0)
                    {
                        bodyObj["tools"] = new JArray(request.Tools.Select(t => new JObject
                        {
                            ["type"] = "function",
                            ["function"] = new JObject
                            {
                                ["name"] = t.Name,
                                ["description"] = t.Description ?? "",
                                ["parameters"] = !string.IsNullOrEmpty(t.ParametersJson)
                                    ? SafeParseParameters(t.ParametersJson)
                                    : JObject.FromObject(new { type = "object", properties = new JObject() })
                            }
                        }));
                    }
        
                    string jsonBody = bodyObj.ToString(Formatting.None);
        
                    // Debug: output request body size
                    System.Diagnostics.Debug.WriteLine($"[VllmProvider] Request body size: {jsonBody.Length} chars, tools: {request.Tools?.Count}");
                    // Avoid default UTF8 encoding (would include BOM), construct manually to avoid API rejection
                    var jsonBytes = Encoding.UTF8.GetBytes(jsonBody);
                    var content = new ByteArrayContent(jsonBytes);
                    content.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                    var requestMsg = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions")
                    {
                        Content = content
                    };
                    if (!string.IsNullOrEmpty(_apiKey))
                    {
                        requestMsg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                    }
        
                    // HttpCompletionOption.ResponseHeadersRead 是流式输出的关键。
                    // 默认值 ResponseContentRead 会让 SendAsync 一直挂到「整个响应体收完」才返回，
                    // 于是 SSE 的每一个增量都被缓冲在底层，等全部结束才一次性交给 ProcessChunk。
                    // 表现就是：模型其实在逐字吐，但界面先长时间没有任何反应，然后整段文字瞬间出现。
                    // 实测同一端点同一提问：
                    //   默认(ResponseContentRead)：首块 7782ms、712 块在 14ms 内集中到达
                    //   ResponseHeadersRead     ：首块约 1.5s、增量按真实节奏逐块到达
                    using (var response = await _http.SendAsync(requestMsg,
                        HttpCompletionOption.ResponseHeadersRead, ct))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            string errorBody = await response.Content.ReadAsStringAsync();
                            onChunk(Chunk.FromText($"\n[API error {response.StatusCode}]: {errorBody}"));
                            return;
                        }
        
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var reader = new StreamReader(stream))
                        {
                            while (!ct.IsCancellationRequested)
                            {
                                // 不用 reader.EndOfStream 做循环条件：它在到达 EOF 前会做同步阻塞读，
                                // 在流式场景下会把异步退化成同步等待。ReadLineAsync 返回 null 即为流结束。
                                string line = await reader.ReadLineAsync();
                                if (line == null) break;

                                if (string.IsNullOrEmpty(line)) continue;
                                if (!line.StartsWith("data: ")) continue;
        
                                string data = line.Substring(6).Trim();
        
                                // SSE end marker
                                if (data == "[DONE]") break;
        
                                try
                                {
                                    var json = JObject.Parse(data);
                                    ProcessChunk(json, onChunk);
                                }
                                catch (JsonReaderException)
                                {
                                    // Skip chunks that fail to parse (vLLM occasionally outputs non-standard format)
                                    continue;
                                }
                            }
                        }
                    }
            
                    return; // 成功则退出
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    int delayMs = (int)Math.Pow(2, attempt) * 1000;
                    onChunk(Chunk.FromText($"\n[LLM 连接失败 (第{attempt + 1}次), {delayMs}ms 后重试: {ex.GetType().Name}]"));
                    OnRetry?.Invoke(ex.GetType().Name, attempt + 1);
                    await Task.Delay(delayMs, ct);
                }
            }
        }

        private void ProcessChunk(JObject json, Action<Chunk> onChunk)
        {
            var choices = json["choices"];
            if (choices == null || choices.Type != JTokenType.Array) return;

            foreach (var choice in choices)
            {
                var delta = choice["delta"];
                if (delta == null) continue;

                // Check finish_reason, used to determine if tool_calls are complete
                var finishReason = choice["finish_reason"]?.Value<string>();

                // Tool calls (streaming fragment accumulation)
                var toolCalls = delta["tool_calls"];
                if (toolCalls != null && toolCalls.Type == JTokenType.Array)
                {
                    foreach (var tc in toolCalls)
                    {
                        if (tc.Type != JTokenType.Object) continue;
                        
                        var indexToken = tc["index"];
                        if (indexToken == null || indexToken.Type != JTokenType.Integer) continue;
                        
                        int index = indexToken.Value<int>();
                        
                        // Get or create accumulator
                        if (!_toolCallAccumulators.TryGetValue(index, out var acc))
                        {
                            acc = new ToolCallAccumulator();
                            _toolCallAccumulators[index] = acc;
                        }
                        
                        var function = tc["function"];
                        if (function != null && function.Type == JTokenType.Object)
                        {
                            // Accumulate name (usually appears in first chunk only)
                            var name = function["name"];
                            if (name != null && name.Type == JTokenType.String)
                            {
                                acc.Name = name.Value<string>();
                            }
                            
                            // Accumulate id (usually appears in first chunk only)
                            var id = tc["id"];
                            if (id != null && id.Type == JTokenType.String)
                            {
                                acc.Id = id.Value<string>();
                            }
                            
                            // Accumulate arguments (append fragments)
                            var argsToken = function["arguments"];
                            if (argsToken != null && argsToken.Type == JTokenType.String)
                            {
                                var argsFragment = argsToken.Value<string>();
                                if (!string.IsNullOrEmpty(argsFragment))
                                {
                                    acc.Arguments.Append(argsFragment);
                                }
                            }
                            
                            if (!string.IsNullOrEmpty(acc.Name) && !acc.NameSent)
                            {
                                acc.NameSent = true;
                            }
                        }
                    }
                    // Continue to next choice, but DON'T skip finish_reason check
                }

                // Consume accumulated tool calls when finish_reason signals completion
                // 注意：部分模型（Qwen3等）在有 tool_calls 时仍返回 finish_reason="stop"
                // 所以用 !string.IsNullOrEmpty(finishReason) 而非仅 "tool_calls"
                if (!string.IsNullOrEmpty(finishReason) && _toolCallAccumulators.Count > 0)
                {
                    foreach (var kvp in _toolCallAccumulators)
                    {
                        var acc = kvp.Value;
                        if (!string.IsNullOrEmpty(acc.Name) && acc.NameSent)
                        {
                            string accumulatedArgs = acc.Arguments.ToString();
                            onChunk(Chunk.FromToolCall(new ToolCall
                            {
                                Id = acc.Id ?? $"call_{kvp.Key}",
                                Name = acc.Name,
                                Arguments = accumulatedArgs
                            }));
                        }
                    }
                    _toolCallAccumulators.Clear();
                }

                // Reasoning process (some models support)
                string reasoning = delta["reasoning_content"]?.Value<string>();
                if (!string.IsNullOrEmpty(reasoning))
                {
                    onChunk(Chunk.FromReasoning(reasoning));
                    continue;
                }

                // Text content
                string text = delta["content"]?.Value<string>();
                if (!string.IsNullOrEmpty(text))
                {
                    onChunk(Chunk.FromText(text));
                }
            }

            // Token usage (only last chunk contains it)
            var usage = json["usage"];
            if (usage != null && usage.Type == JTokenType.Object)
            {
                int completion = usage["completion_tokens"]?.Value<int>() ?? 0;
                int prompt = usage["prompt_tokens"]?.Value<int>() ?? 0;
                if (completion > 0 || prompt > 0)
                {
                    onChunk(Chunk.FromUsage(completion, prompt));
                }
            }
        }

        /// <summary>
        /// Serialize ChatMessage to OpenAI message format
        /// Correctly handles assistant tool_calls and tool result responses
        /// Supports multimodal content (text + images) for vision models
        /// </summary>
        private static JObject SerializeMessage(ChatMessage m)
        {
            var msg = new JObject
            {
                ["role"] = RoleToString(m.Role),
            };

            // Assistant message: inject tool_calls (required for multi-turn)
            if (m.Role == MessageRole.Assistant
                && m.ToolCalls != null
                && m.ToolCalls.Count > 0)
            {
                msg["tool_calls"] = new JArray(m.ToolCalls.Select(tc => new JObject
                {
                    ["id"] = tc.Id,
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = tc.Name,
                        ["arguments"] = tc.Arguments
                    }
                }));
                // OpenAI 规范允许 content 与 tool_calls 并存；保留 assistant 的中间陈述文本
                // （下一轮模型才能看到自己「边说边做」的过程，丢弃会破坏多轮连贯性）
                msg["content"] = string.IsNullOrEmpty(m.Content) ? null : m.Content;
            }

            // 历史 reasoning_content 不回传：DeepSeek/Qwen3 官方均在输入侧忽略它，
            // 回传只会白白重发整段思考（多轮场景 token 翻倍）。最新一条的思考
            // 已通过 Reasoning 通道实时呈现给用户，模型无需再看到自己的旧思考。

            // User/System messages with images: use array format for vision models
            if ((m.Role == MessageRole.User || m.Role == MessageRole.System)
                     && m.Images != null && m.Images.Length > 0)
            {
                var contentArray = new JArray();
                
                // Add text content first
                if (!string.IsNullOrEmpty(m.Content))
                {
                    contentArray.Add(new JObject
                    {
                        ["type"] = "text",
                        ["text"] = m.Content
                    });
                }
                
                // Add each image as image_url
                foreach (var imageBase64 in m.Images)
                {
                    if (!string.IsNullOrEmpty(imageBase64))
                    {
                        contentArray.Add(new JObject
                        {
                            ["type"] = "image_url",
                            ["image_url"] = new JObject
                            {
                                ["url"] = imageBase64
                            }
                        });
                    }
                }
                
                msg["content"] = contentArray;
            }
            // Regular text-only message
            else
            {
                msg["content"] = m.Content;
            }

            // Tool result message: always include tool_call_id (DeepSeek requires it for all tool messages)
            if (m.Role == MessageRole.Tool)
            {
                msg["tool_call_id"] = m.ToolCallId ?? "";
            }

            return msg;
        }

        /// <summary>
        /// Safely parse JSON Schema parameters, return empty object on failure
        /// </summary>
        private static JObject SafeParseParameters(string json)
        {
            if (string.IsNullOrEmpty(json)) 
                return JObject.FromObject(new { type = "object", properties = new JObject() });
            try
            {
                return JObject.Parse(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VllmProvider] SafeParseParameters failed for json={json?.Substring(0, Math.Min(200, json?.Length ?? 0))}: {ex.Message}");
                return JObject.FromObject(new { type = "object", properties = new JObject() });
            }
        }

        private static string RoleToString(MessageRole role)
        {
            switch (role)
            {
                case MessageRole.System: return "system";
                case MessageRole.User: return "user";
                case MessageRole.Assistant: return "assistant";
                case MessageRole.Tool: return "tool";
                default: return "user";
            }
        }

        // ── DeepSeek 模型检测 ──
    private static bool IsDeepSeekModel(string modelName)
    {
        if (string.IsNullOrEmpty(modelName)) return false;
        return modelName.IndexOf("deepseek", StringComparison.OrdinalIgnoreCase) >= 0
            || modelName.IndexOf("DeepSeek", StringComparison.Ordinal) >= 0;
    }

    private static bool IsDeepSeekReasoningModel(string modelName)
    {
        if (string.IsNullOrEmpty(modelName)) return false;
        return modelName.IndexOf("reasoner", StringComparison.OrdinalIgnoreCase) >= 0
            || modelName.IndexOf("DeepSeek-R1", StringComparison.Ordinal) >= 0
            || modelName.IndexOf("reasoning", StringComparison.OrdinalIgnoreCase) >= 0
            || modelName.IndexOf("deepseek-v4", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsMiniMaxModel(string modelName)
    {
        if (string.IsNullOrEmpty(modelName)) return false;
        return modelName.IndexOf("minimax", StringComparison.OrdinalIgnoreCase) >= 0
            || modelName.IndexOf("minimax-m3", StringComparison.OrdinalIgnoreCase) >= 0;
    }
    }
}
