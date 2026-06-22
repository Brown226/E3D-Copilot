using System;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// Geometry 几何工具 — 查询元素位置/方向，计算元素间距离
    /// 使用 E3D D3Point/D3Vector 进行几何运算
    /// Read-only operation
    /// </summary>
    public class GeometryHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public GeometryHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public string Name => "geometry";
        public string Description => "Geometry queries: get element position and orientation in 3D space, calculate distance between elements, compute bounding box information. Uses Aveva.Core.Geometry (D3Point, D3Vector).";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""action"": { ""type"": ""string"", ""enum"": [""get_position"", ""get_orientation"", ""distance_between"", ""bounding_box""], ""description"": ""Geometry action"" },
    ""element"": { ""type"": ""string"", ""description"": ""Target element for position/orientation query"" },
    ""element1"": { ""type"": ""string"", ""description"": ""[distance_between] First element"" },
    ""element2"": { ""type"": ""string"", ""description"": ""[distance_between] Second element"" }
  },
  ""required"": [""action""]
}";
        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var result = await _dispatcher.ExecuteAsync("geometry", args);
                return ToolResult.Ok(result, null);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"Geometry operation failed: {ex.Message}");
            }
        }
    }
}
