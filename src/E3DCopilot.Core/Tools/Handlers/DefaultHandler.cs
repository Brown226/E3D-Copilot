using System.Threading;
using System.Threading.Tasks;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// 空/默认 Handler — 工具未实现时返回占位结果
    /// </summary>
    public class DefaultHandler : IToolHandler
    {
        private readonly string _name;
        private readonly string _description;

        public DefaultHandler(string name, string description)
        {
            _name = name;
            _description = description;
        }

        public string Name => _name;
        public string Description => _description;
        public string ParameterSchema => "{}";
        public bool IsReadOnly => true;

        public Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            return Task.FromResult(ToolResult.Fail($"工具 {_name} 尚未实现"));
        }
    }
}
