using System;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// Modify E3D element attributes (single or batch)
    /// Write operation, triggers approval flow
    /// </summary>
    public class ModifyHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public ModifyHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public string Name => "modify";
        public string Description => "Modify E3D element attribute values (single or batch). Query first to confirm target element";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""dburi"": { ""type"": ""string"", ""description"": ""Target element DBURI"" },
    ""attributes"": { ""type"": ""object"", ""description"": ""Attribute key-value pairs to modify"" },
    ""preview"": { ""type"": ""boolean"", ""description"": ""Preview only, do not execute"" }
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
                return ToolResult.Fail($"Modify failed: {ex.Message}");
            }
        }
    }
}
