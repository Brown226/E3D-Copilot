using System;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// 检查验证（存在性/属性/间距/命名）
    /// 只读操作
    /// </summary>
    public class CheckHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public CheckHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public string Name => "check";
        public string Description => "检查验证：元素存在性检查、属性值验证、间距检查、命名规范验证";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""type"": { ""type"": ""string"", ""enum"": [""exists"", ""attribute"", ""clearance"", ""naming""] },
    ""dburi"": { ""type"": ""string"", ""description"": ""目标元素 DBURI"", ""required"": true },
    ""value"": { ""type"": ""string"", ""description"": ""检查值"", ""required"": false }
  },
  ""required"": [""type"", ""dburi""]
}";
        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var result = await _dispatcher.ExecuteAsync("check", args);
                return ToolResult.Ok(result, null);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"检查失败: {ex.Message}");
            }
        }
    }
}
