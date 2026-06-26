using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// 通用透传 Handler — 将 LLM 工具调用直接转发到 IToolDispatcher
    /// 替代 7 个只有 Name/Description/Schema/ReadOnly 不同的零逻辑 Handler
    /// </summary>
    public class DispatcherBackedHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public DispatcherBackedHandler(IToolDispatcher dispatcher,
            string name, string description, string parameterSchema, bool isReadOnly)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? "";
            ParameterSchema = parameterSchema ?? "{}";
            IsReadOnly = isReadOnly;
        }

        public string Name { get; }
        public string Description { get; }
        public string ParameterSchema { get; }
        public bool IsReadOnly { get; }

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var result = await _dispatcher.ExecuteAsync(Name, args);

                // 检测 dispatcher 返回的错误信息
                if (!string.IsNullOrEmpty(result) && result.StartsWith("{"))
                {
                    try
                    {
                        var j = JObject.Parse(result);
                        var successToken = j["success"];
                        if (successToken != null && successToken.Value<bool>() == false)
                        {
                            var msg = j["error"]?.ToString() ?? j["message"]?.ToString() ?? "Unknown error";
                            return ToolResult.Fail($"{Name} failed: {msg}");
                        }
                    }
                    catch { /* 不是 JSON，按纯文本处理 */ }
                }

                // 最小安全方案：Text 不变（LLM 侧零影响），Data 放结构化 meta 供前端渲染
                var meta = new JObject
                {
                    ["tool"] = Name,
                    ["summary"] = $"{Name} 执行完成",
                };
                return ToolResult.Ok(result, meta);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"{Name} failed: {ex.Message}");
            }
        }
    }
}
