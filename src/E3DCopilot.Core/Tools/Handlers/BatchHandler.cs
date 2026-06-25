using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Events;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// Batch — 批量操作引擎
    /// 
    /// 元能力工具：
    /// 查询符合条件的元素并批量修改属性。
    /// 流程：query → 过滤 → 逐个 modify，支持 dry-run 预览。
    /// </summary>
    public class BatchHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public BatchHandler(IToolDispatcher dispatcher) { _dispatcher = dispatcher; }

        public string Name => "batch";
        public string Description =>
            "Batch modify: query elements by type/name/scope, then apply attribute changes to all matches. " +
            "批量操作：先查询符合条件的元素，再统一修改属性。支持 dry-run 预览。";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""query_type"": { ""type"": ""string"", ""description"": ""Element type to query (PIPE/EQUI/STRU/etc.)"" },
    ""query_name"": { ""type"": ""string"", ""description"": ""Name pattern with wildcards"" },
    ""query_scope"": { ""type"": ""string"", ""description"": ""Scope filter"" },
    ""attributes"": {
      ""type"": ""object"",
      ""description"": ""Key-value pairs to set on all matched elements, e.g. {\""WTHK\"": \""SCH40\""}"" },
    ""preview"": { ""type"": ""boolean"", ""description"": ""Preview only, don't actually modify (default: false)"" },
    ""limit"": { ""type"": ""integer"", ""description"": ""Max elements to modify (default: 50)"" }
  },
  ""required"": [""query_type"", ""attributes""]
}";
        public bool IsReadOnly => false;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var json = JObject.Parse(args);
                string queryType = json.Value<string>("query_type");
                string queryName = json.Value<string>("query_name");
                string queryScope = json.Value<string>("query_scope");
                bool preview = json.Value<bool?>("preview") ?? false;
                int limit = json.Value<int?>("limit") ?? 50;
                limit = Math.Max(1, Math.Min(limit, 200));

                var attrsToken = json["attributes"] as JObject;
                if (attrsToken == null || attrsToken.Properties().Count() == 0)
                    return ToolResult.Fail("attributes is required (key-value pairs to modify)");

                var attributes = new Dictionary<string, string>();
                foreach (var prop in attrsToken.Properties())
                    attributes[prop.Name] = prop.Value.ToString();

                // 1. 查询匹配的元素
                var queryParams = new Dictionary<string, object> { ["type"] = queryType, ["limit"] = limit };
                if (!string.IsNullOrEmpty(queryName)) queryParams["name"] = queryName;
                if (!string.IsNullOrEmpty(queryScope)) queryParams["scope"] = queryScope;

                string queryResult = await _dispatcher.ExecuteAsync("query",
                    Newtonsoft.Json.JsonConvert.SerializeObject(queryParams));

                var queryJson = JObject.Parse(queryResult);
                var elements = queryJson["elements"] as JArray;
                if (elements == null || elements.Count == 0)
                    return ToolResult.Ok($"未找到符合条件的元素 (type={queryType})。");

                var names = elements.Select(e =>
                {
                    var nameToken = e["name"];
                    return nameToken != null ? nameToken.ToString() : e.ToString();
                }).ToList();

                // 2. Dry-run 模式：只预览，不修改
                if (preview)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"预览: 将修改 {names.Count} 个元素");
                    sb.AppendLine($"修改内容: {string.Join(", ", attributes.Select(a => $"{a.Key}={a.Value}"))}");
                    sb.AppendLine();
                    sb.AppendLine("将被修改的元素:");
                    foreach (var name in names.Take(30))
                    {
                        // 显示当前值
                        var currentVals = attributes.Keys.Select(k => $"{k}={_dispatcher.GetAttribute(name, k) ?? "?"}");
                        sb.AppendLine($"  {name} → {string.Join(", ", currentVals)}");
                    }
                    if (names.Count > 30)
                        sb.AppendLine($"  ... 还有 {names.Count - 30} 个");

                    return ToolResult.Ok(sb.ToString().TrimEnd(), new
                    {
                        preview = true, count = names.Count, attributes
                    });
                }

                // 3. 执行批量修改
                int success = 0, failed = 0;
                var errors = new List<string>();
                var sbResult = new StringBuilder();

                foreach (var name in names)
                {
                    ct.ThrowIfCancellationRequested();

                    bool allOk = true;
                    foreach (var attr in attributes)
                    {
                        string modifyArgs = Newtonsoft.Json.JsonConvert.SerializeObject(new
                        {
                            dburi = name,
                            attribute = attr.Key,
                            value = attr.Value
                        });

                        try
                        {
                            await _dispatcher.ExecuteAsync("modify", modifyArgs);
                        }
                        catch (Exception ex)
                        {
                            allOk = false;
                            errors.Add($"{name}.{attr.Key}: {ex.Message}");
                        }
                    }

                    if (allOk)
                    {
                        success++;
                        sbResult.AppendLine($"✓ {name}");
                    }
                    else
                    {
                        failed++;
                        sbResult.AppendLine($"✗ {name}");
                    }
                }

                string summary = $"批量修改完成: {success} 成功, {failed} 失败, 共 {names.Count} 个元素";
                if (errors.Count > 0)
                {
                    sbResult.AppendLine();
                    sbResult.AppendLine("错误:");
                    foreach (var err in errors.Take(10))
                        sbResult.AppendLine($"  {err}");
                }

                _sink?.Emit(CopilotEvent.Notice($"Batch: {success}/{names.Count} modified"));
                return ToolResult.Ok($"{summary}\n\n{sbResult.ToString().TrimEnd()}", new
                {
                    success, failed, total = names.Count, attributes
                });
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"Batch failed: {ex.Message}");
            }
        }

        private readonly Events.IEventSink _sink;
        public BatchHandler(IToolDispatcher dispatcher, Events.IEventSink sink = null)
        {
            _dispatcher = dispatcher;
            _sink = sink;
        }
    }
}
