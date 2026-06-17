using System;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// 执行 PML 脚本（万能兜底执行器）
    /// 对应 cline-chinese-main 的 ExecuteCommand ToolHandler
    /// </summary>
    public class PmlCommandHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public PmlCommandHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public string Name => "execute_pml";
        public string Description => "执行 PML 脚本（万能兜底工具）。复杂查询、批量操作、特殊业务逻辑均可通过 PML 执行";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""script"": { ""type"": ""string"", ""description"": ""PML 脚本内容"", ""required"": true }
  },
  ""required"": [""script""]
}";
        public bool IsReadOnly => false;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var result = await _dispatcher.ExecuteAsync("execute_pml", args);
                return ToolResult.Ok(result, null);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"PML 执行失败: {ex.Message}");
            }
        }
    }
}
