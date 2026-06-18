using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Providers
{
    /// <summary>
    /// vLLM + Qwen 适配器（OpenAI 兼容接口）
    /// net48 兼容：使用回调模式替代 IAsyncEnumerable
    /// </summary>
    public class VllmProvider : ICopilotProvider
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _model;
        private readonly string _apiKey;
        
        // 流式 tool_calls 累积器（按 index 存储）
        private readonly System.Collections.Generic.Dictionary<int, ToolCallAccumulator> _toolCallAccumulators
            = new System.Collections.Generic.Dictionary<int, ToolCallAccumulator>();

        public VllmProvider(string baseUrl = "http://localhost:8000/v1",
            string model = "Qwen3.5-32B",
            string apiKey = "")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _model = model;
            _apiKey = apiKey ?? "";
            _http = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(120000)
            };
        }
        
        /// <summary>
        /// ToolCall 累积器（用于流式分片累积）
        /// </summary>
        private class ToolCallAccumulator
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public StringBuilder Arguments { get; } = new StringBuilder();
            public bool NameSent { get; set; }
        }

        /// <summary>
        /// 流式调用 vLLM，通过 onChunk 回调逐块返回
        /// 使用 StreamReader 逐行读取 SSE 流
        /// </summary>
        public async Task StreamAsync(
            CopilotRequest request,
            Action<Chunk> onChunk,
            CancellationToken ct)
        {
            // 重置 tool_calls 累积器（每次新请求）
            _toolCallAccumulators.Clear();
            
            var body = new
            {
                model = request.Model ?? _model,
                messages = request.Messages.ConvertAll(m => new
                {
                    role = RoleToString(m.Role),
                    content = m.Content
                }),
                stream = true,
                temperature = request.Temperature,
                max_tokens = request.MaxTokens
            };

            string jsonBody = JsonConvert.SerializeObject(body);
            // 不使用默认 UTF8 编码（会带 BOM），手工构造避免 API 拒绝
            var jsonBytes = Encoding.UTF8.GetBytes(jsonBody);
            var content = new ByteArrayContent(jsonBytes);
            content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            if (!string.IsNullOrEmpty(_apiKey))
            {
                _http.DefaultRequestHeaders.Remove("Authorization");
                _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            }

            using (var response = await _http.PostAsync(
                $"{_baseUrl}/chat/completions", content, ct))
            {
                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    onChunk(Chunk.FromText($"\n[API 错误 {response.StatusCode}]: {errorBody}"));
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

                        // SSE 结束标志
                        if (data == "[DONE]") break;

                        try
                        {
                            var json = JObject.Parse(data);
                            ProcessChunk(json, onChunk);
                        }
                        catch (JsonReaderException)
                        {
                            // 跳过解析失败的chunk（vLLM 偶尔输出非标准格式）
                            continue;
                        }
                    }
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

                // 检查 finish_reason，用于判断 tool_calls 是否完成
                var finishReason = choice["finish_reason"]?.Value<string>();

                // 工具调用（流式分片累积）
                var toolCalls = delta["tool_calls"];
                if (toolCalls != null && toolCalls.Type == JTokenType.Array)
                {
                    foreach (var tc in toolCalls)
                    {
                        if (tc.Type != JTokenType.Object) continue;
                        
                        var indexToken = tc["index"];
                        if (indexToken == null || indexToken.Type != JTokenType.Integer) continue;
                        
                        int index = indexToken.Value<int>();
                        
                        // 获取或创建累积器
                        if (!_toolCallAccumulators.TryGetValue(index, out var acc))
                        {
                            acc = new ToolCallAccumulator();
                            _toolCallAccumulators[index] = acc;
                        }
                        
                        var function = tc["function"];
                        if (function != null && function.Type == JTokenType.Object)
                        {
                            // 累积 name（通常只在第一个 chunk 出现）
                            var name = function["name"];
                            if (name != null && name.Type == JTokenType.String)
                            {
                                acc.Name = name.Value<string>();
                            }
                            
                            // 累积 id（通常只在第一个 chunk 出现）
                            var id = tc["id"];
                            if (id != null && id.Type == JTokenType.String)
                            {
                                acc.Id = id.Value<string>();
                            }
                            
                            // 累积 arguments（分片追加）
                            var argsToken = function["arguments"];
                            if (argsToken != null && argsToken.Type == JTokenType.String)
                            {
                                var argsFragment = argsToken.Value<string>();
                                if (!string.IsNullOrEmpty(argsFragment))
                                {
                                    acc.Arguments.Append(argsFragment);
                                }
                            }
                            
                            // 当 name 首次出现时，发送 ToolCallStart
                            if (!string.IsNullOrEmpty(acc.Name) && !acc.NameSent)
                            {
                                acc.NameSent = true;
                                onChunk(Chunk.FromToolCall(new ToolCall
                                {
                                    Id = acc.Id ?? $"call_{index}",
                                    Name = acc.Name,
                                    Arguments = "" // 参数还在累积中
                                }));
                            }
                        }
                        
                        // 当 finish_reason 为 "tool_calls" 时，发送完整的 ToolCall（包含累积的 arguments）
                        if (finishReason == "tool_calls" && acc.NameSent)
                        {
                            onChunk(Chunk.FromToolCall(new ToolCall
                            {
                                Id = acc.Id ?? $"call_{index}",
                                Name = acc.Name,
                                Arguments = acc.Arguments.ToString()
                            }));
                            
                            // 清理累积器
                            _toolCallAccumulators.Remove(index);
                        }
                    }
                    continue;
                }

                // 推理过程（部分模型支持）
                string reasoning = delta["reasoning_content"]?.Value<string>();
                if (!string.IsNullOrEmpty(reasoning))
                {
                    onChunk(Chunk.FromReasoning(reasoning));
                    continue;
                }

                // 文本内容
                string text = delta["content"]?.Value<string>();
                if (!string.IsNullOrEmpty(text))
                {
                    onChunk(Chunk.FromText(text));
                }
            }

            // Token 用量（仅最后一个 chunk 包含）
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
    }
}
