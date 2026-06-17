using System;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// 查询 E3D 元素（按类型/名称/范围）
    /// 对应 cline-chinese-main 的 ReadFile/SearchFiles ToolHandler
    /// </summary>
    public class DbQueryHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public DbQueryHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public string Name => "query";
        public string Description => "查询 E3D 元素：按类型（PIPE/EQUI/STRU）、名称模式（通配符）、范围（SITE/ZONE/OWN）过滤";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""type"": { ""type"": ""string"", ""description"": ""元素类型，如 PIPE/EQUI/STRU/BRAN"", ""required"": true },
    ""name"": { ""type"": ""string"", ""description"": ""名称模式，支持 * 通配符"", ""required"": false },
    ""scope"": { ""type"": ""string"", ""description"": ""查询范围（DBURI），默认当前 MDB"", ""required"": false },
    ""limit"": { ""type"": ""integer"", ""description"": ""最大返回条数，默认 50"", ""required"": false }
  },
  ""required"": [""type""]
}";
        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                // 通过 ToolRegistry 执行实际查询
                var result = await _dispatcher.ExecuteAsync("query", args);
                return ToolResult.Ok(result, null);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"查询失败: {ex.Message}");
            }
        }
    }
}
