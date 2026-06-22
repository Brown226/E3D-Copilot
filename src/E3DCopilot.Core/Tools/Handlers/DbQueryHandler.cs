using System;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// Query E3D elements (by type/name/scope)
    /// Corresponds to cline-chinese-main's ReadFile/SearchFiles ToolHandler
    /// </summary>
    public class DbQueryHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public DbQueryHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public string Name => "query";
        public string Description => "Query E3D elements by type (PIPE/EQUI/STRU), name pattern (wildcards), scope (SITE/ZONE/OWN)";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""type"": { ""type"": ""string"", ""description"": ""Element type like PIPE/EQUI/STRU/BRAN"" },
    ""name"": { ""type"": ""string"", ""description"": ""Name pattern, supports * wildcard"" },
    ""scope"": { ""type"": ""string"", ""description"": ""Query scope DBURI, default current MDB"" },
    ""limit"": { ""type"": ""integer"", ""description"": ""Max results, default 50"" }
  },
  ""required"": [""type""]
}";
        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                // Execute through ToolRegistry
                var result = await _dispatcher.ExecuteAsync("query", args);
                return ToolResult.Ok(result, null);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"Query failed: {ex.Message}");
            }
        }
    }
}
