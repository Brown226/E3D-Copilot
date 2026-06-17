using System;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// 修改 E3D 元素属性（单个或批量）
    /// 写操作，需触发审批流
    /// </summary>
    public class ModifyHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public ModifyHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public string Name => "modify";
        public string Description => "修改 E3D 元素属性值（单个或批量）。修改前需先查询确认目标元素";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""dburi"": { ""type"": ""string"", ""description"": ""目标元素的 DBURI"", ""required"": true },
    ""attributes"": { ""type"": ""object"", ""description"": ""要修改的属性键值对，如 {""WTHK"": ""6.0""}"", ""required"": true },
    ""preview"": { ""type"": ""boolean"", ""description"": ""是否仅预览不执行"", ""required"": false }
  },
  ""required"": [""dburi"", ""attributes""]
}";
        public bool IsReadOnly => false;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var result = await _dispatcher.ExecuteAsync("modify", args);
                return ToolResult.Ok(result, null);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"修改失败: {ex.Message}");
            }
        }
    }
}
