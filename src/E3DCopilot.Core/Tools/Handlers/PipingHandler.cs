using System;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// Piping 管道工具 — Pipe/Branch/Fitment 操作
    /// Write operation (需要审批)
    /// </summary>
    public class PipingHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public PipingHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public string Name => "piping";
        public string Description => "Piping operations: create pipe, create branch, add fitment, set pipe specification. For all pipe-related modeling tasks.";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""action"": { ""type"": ""string"", ""enum"": [""create_pipe"", ""create_branch"", ""add_fitment"", ""set_spec""], ""description"": ""Piping action to perform"" },
    ""parent"": { ""type"": ""string"", ""description"": ""Parent element DBURI or name"" },
    ""name"": { ""type"": ""string"", ""description"": ""New pipe/branch name"" },
    ""pipe"": { ""type"": ""string"", ""description"": ""[create_branch/add_fitment] Target pipe name"" },
    ""spec"": { ""type"": ""string"", ""description"": ""[set_spec] Pipe specification (e.g. SCH40, SCH80)"" },
    ""fitmentType"": { ""type"": ""string"", ""description"": ""[add_fitment] Fitment type (FLANGE, VALVE, ELBOW, TEE, REDUCER)"" },
    ""attributes"": { ""type"": ""object"", ""description"": ""Additional attributes"" }
  },
  ""required"": [""action""]
}";
        public bool IsReadOnly => false;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var result = await _dispatcher.ExecuteAsync("piping", args);
                return ToolResult.Ok(result, null);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"Piping operation failed: {ex.Message}");
            }
        }
    }
}
