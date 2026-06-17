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

        public VllmProvider(string baseUrl = "http://localhost:8000/v1",
            string model = "Qwen3.5-32B")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _model = model;
            _http = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(120000)
            };
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
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using (var response = await _http.PostAsync(
                $"{_baseUrl}/chat/completions", content, ct))
            {
                response.EnsureSuccessStatusCode();

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

                // 工具调用
                var toolCalls = delta["tool_calls"];
                if (toolCalls != null && toolCalls.Type == JTokenType.Array)
                {
                    foreach (var tc in toolCalls)
                    {
                        var function = tc["function"];
                        if (function == null) continue;

                        string name = function["name"]?.Value<string>() ?? "";
                        string args = function["arguments"]?.Value<string>() ?? "";

                        onChunk(Chunk.FromToolCall(new ToolCall
                        {
                            Name = name,
                            Arguments = args
                        }));
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
            if (usage != null)
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
