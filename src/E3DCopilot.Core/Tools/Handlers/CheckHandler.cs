using System;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// Check validation (existence/attributes/clearance/naming)
    /// Read-only operation
    /// </summary>
    public class CheckHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public CheckHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public string Name => "check";
        public string Description => "Check and validate: element existence check, attribute value validation, clearance check, naming convention validation";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""type"": { ""type"": ""string"", ""enum"": [""exists"", ""attribute"", ""naming"", ""clearance""], ""description"": ""检查类型: exists(存在性), attribute(属性值), naming(命名规范), clearance(净距/碰撞，未实现)"" },
    ""element"": { ""type"": ""string"", ""description"": ""目标元素名称或 DBURI"" },
    ""dburi"": { ""type"": ""string"", ""description"": ""目标元素 DBURI（element 的别名）"" },
    ""attribute"": { ""type"": ""string"", ""description"": ""[attribute] 要检查的属性名"" },
    ""attr"": { ""type"": ""string"", ""description"": ""[attribute] attribute 的别名"" },
    ""expected"": { ""type"": ""string"", ""description"": ""[attribute] 期望值，为空时仅检查属性是否存在"" },
    ""value"": { ""type"": ""string"", ""description"": ""[attribute] expected 的别名"" },
    ""pattern"": { ""type"": ""string"", ""description"": ""[naming] 正则表达式，如 '^PIPE-\\d{{3}}$'"" },
    ""elementName"": { ""type"": ""string"", ""description"": ""[naming] 要检查的元素名（不填则用 element）"" }
  },
  ""required"": [""type"", ""element""]
}";
        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var result = await _dispatcher.ExecuteAsync("check", args);
                return ToolResult.Ok(result, null);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"Check failed: {ex.Message}");
            }
        }
    }
}
