using System;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// 几何计算（距离/角度/朝向/坐标转换）
    /// 只读操作
    /// </summary>
    public class CalculateHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public CalculateHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public string Name => "calculate";
        public string Description => "几何计算：两点间距离、角度计算、朝向分析、坐标转换";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""operation"": { ""type"": ""string"", ""enum"": [""distance"", ""angle"", ""midpoint"", ""direction""], ""description"": ""计算类型"" },
    ""pointA"": { ""type"": ""string"", ""description"": ""A 点 DBURI"", ""required"": true },
    ""pointB"": { ""type"": ""string"", ""description"": ""B 点 DBURI"", ""required"": true }
  },
  ""required"": [""operation"", ""pointA"", ""pointB""]
}";
        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var result = await _dispatcher.ExecuteAsync("calculate", args);
                return ToolResult.Ok(result, null);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"计算失败: {ex.Message}");
            }
        }
    }
}
