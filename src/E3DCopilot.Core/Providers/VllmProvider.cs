﻿﻿using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
            string apiKey = "")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _model = model;
            _apiKey = apiKey ?? "";
            
            // 强制启用 TLS 1.2（.NET 4.8 在某些系统上默认不包含，导致 HTTPS 连接失败）
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
            
            _http = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(120000)
            };
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
                        ["messages"] = new JArray(request.Messages.Select(SerializeMessage))
                    };

                    // DeepSeek/其他模型特殊优化（参考 Reasonix 设计）
                    var modelName = request.Model ?? _model;
                    
                    // DeepSeek 适配
                    if (IsDeepSeekModel(modelName))
                    {
                        // DeepSeek Reasoner 系列禁用 temperature，使用 effort 控制
                        if (IsDeepSeekReasoningModel(modelName))
                        {
                            bodyObj.Remove("temperature");
                            
                            // DeepSeek Reasoner 支持两种推理模式
                            // 1. thinking.type=enabled (推荐) 2. reasoning_effort=high
                            bodyObj["thinking"] = new JObject
                            {
                                ["type"] = "enabled"
                            };
                        }
                        else
                        {
                            // DeepSeek Chat 系列支持 standard reasoning_effort
                            bodyObj["reasoning_effort"] = "high";
                        }
                    }
                    // MiniMax 适配（可选）
                    else if (IsMiniMaxModel(modelName))
                    {
                        bodyObj["thinking"] = new JObject
                        {
                            ["type"] = "adaptive"
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
        
                    using (var response = await _http.SendAsync(requestMsg, ct))
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
                            while (!reader.EndOfStream && !ct.IsCancellationRequested)
                            {
                                string line = await reader.ReadLineAsync();
        
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
                // When there are tool_calls, content should be null (OpenAI spec)
                msg["content"] = null;
            }
            // User/System messages with images: use array format for vision models
            else if ((m.Role == MessageRole.User || m.Role == MessageRole.System) 
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
