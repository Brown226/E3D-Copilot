using System;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// Import/Export (Excel/CSV/PML script)
    /// Write operation
    /// </summary>
    public class ExportHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public ExportHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public string Name => "export";
        public string Description => "Import/Export: export element list to Excel/CSV, generate PML script, batch export attributes";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""action"": { ""type"": ""string"", ""enum"": [""export"", ""import"", ""generate_pml""], ""description"": ""Operation type"" },
    ""format"": { ""type"": ""string"", ""enum"": [""csv"", ""excel"", ""pml""], ""description"": ""Export format"" },
    ""query"": { ""type"": ""string"", ""description"": ""Element query condition for export"" },
    ""filePath"": { ""type"": ""string"", ""description"": ""Export file path"" }
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
                return ToolResult.Fail($"Export failed: {ex.Message}");
            }
        }
    }
}
