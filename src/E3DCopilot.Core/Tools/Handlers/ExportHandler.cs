using System;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// 导入导出（Excel/CSV/PML 脚本）
    /// 写操作
    /// </summary>
    public class ExportHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public ExportHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public string Name => "export";
        public string Description => "导入导出：导出元素列表到 Excel/CSV、生成 PML 脚本、批量导出属性";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""action"": { ""type"": ""string"", ""enum"": [""export"", ""import"", ""generate_pml""], ""description"": ""操作类型"" },
    ""format"": { ""type"": ""string"", ""enum"": [""csv"", ""excel"", ""pml""], ""description"": ""导出格式"" },
    ""query"": { ""type"": ""string"", ""description"": ""要导出的元素查询条件"" },
    ""filePath"": { ""type"": ""string"", ""description"": ""导出文件路径"" }
  },
  ""required"": [""action"", ""format""]
}";
        public bool IsReadOnly => false;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var result = await _dispatcher.ExecuteAsync("export", args);
                return ToolResult.Ok(result, null);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"导出失败: {ex.Message}");
            }
        }
    }
}
