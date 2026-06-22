using System;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// Design 建模工具 — Equipment/Component 创建、删除、定位
    /// Write operation (需要审批)
    /// </summary>
    public class DesignHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public DesignHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public string Name => "design";
        public string Description => "Design modeling: create/delete equipment and components, set position and orientation. Use for creating pumps, vessels, columns, nozzles, and other equipment items.";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""action"": { ""type"": ""string"", ""enum"": [""create_equipment"", ""create_component"", ""delete_element"", ""set_position""], ""description"": ""Design action to perform"" },
    ""parent"": { ""type"": ""string"", ""description"": ""Parent element DBURI or name"" },
    ""name"": { ""type"": ""string"", ""description"": ""New element name"" },
    ""type"": { ""type"": ""string"", ""description"": ""Element type (EQUIPMENT, COMPONENT, NOZZLE, etc.)"" },
    ""element"": { ""type"": ""string"", ""description"": ""Target element for delete/set_position"" },
    ""x"": { ""type"": ""number"", ""description"": ""[set_position] X coordinate (mm)"" },
    ""y"": { ""type"": ""number"", ""description"": ""[set_position] Y coordinate (mm)"" },
    ""z"": { ""type"": ""number"", ""description"": ""[set_position] Z coordinate (mm)"" },
    ""attributes"": { ""type"": ""object"", ""description"": ""Additional attributes as key-value pairs"" }
  },
  ""required"": [""action""]
}";
        public bool IsReadOnly => false;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var result = await _dispatcher.ExecuteAsync("design", args);
                return ToolResult.Ok(result, null);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"Design operation failed: {ex.Message}");
            }
        }
    }
}
