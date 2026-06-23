using System;
using System.Threading;
using System.Threading.Tasks;

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
                return ToolResult.Ok(result, null);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"{Name} failed: {ex.Message}");
            }
        }
    }
}
