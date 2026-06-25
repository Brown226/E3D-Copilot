using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// Report — 生成 E3D 报表
    /// 
    /// 元能力工具：
    /// 查询元素列表并生成结构化报表（材料清单、属性汇总、统计表）。
    /// 支持输出格式：文本表格、CSV。
    /// </summary>
    public class ReportHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public ReportHandler(IToolDispatcher dispatcher) { _dispatcher = dispatcher; }

        public string Name => "report";
        public string Description =>
            "Generate reports from E3D data: material lists, attribute summaries, statistics. " +
            "生成 E3D 报表：材料清单、属性汇总、统计表。支持按类型查询并格式化输出。";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""type"": { ""type"": ""string"", ""description"": ""Element type to report (PIPE/EQUI/STRU/BRAN/etc.)"" },
    ""scope"": { ""type"": ""string"", ""description"": ""Scope filter (zone name or current element)"" },
    ""attributes"": {
      ""type"": ""array"", ""items"": { ""type"": ""string"" },
      ""description"": ""Attributes to include (default: NAME, TYPE, DESC, OWNER)"" },
    ""format"": { ""type"": ""string"", ""enum"": [""table"", ""csv""], ""description"": ""Output format (default: table)"" },
    ""name_pattern"": { ""type"": ""string"", ""description"": ""Name filter with wildcards"" },
    ""limit"": { ""type"": ""integer"", ""description"": ""Max rows (default: 50)"" }
  },
  ""required"": [""type""]
}";
        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var json = JObject.Parse(args);
                string type = json.Value<string>("type");
                string scope = json.Value<string>("scope") ?? "";
                string namePattern = json.Value<string>("name_pattern") ?? "";
                string format = json.Value<string>("format") ?? "table";
                int limit = json.Value<int?>("limit") ?? 50;
                limit = Math.Max(1, Math.Min(limit, 200));

                var attrsToken = json["attributes"];
                var attributes = new List<string> { "NAME", "TYPE", "DESC", "OWNER" };
                if (attrsToken is JArray arr)
                    attributes = arr.Select(t => t.ToString()).ToList();

                // 查询元素
                var queryParams = new Dictionary<string, object> { ["type"] = type };
                if (!string.IsNullOrEmpty(scope)) queryParams["scope"] = scope;
                if (!string.IsNullOrEmpty(namePattern)) queryParams["name"] = namePattern;
                queryParams["limit"] = limit;

                string queryResult = await _dispatcher.ExecuteAsync("query",
                    Newtonsoft.Json.JsonConvert.SerializeObject(queryParams));

                var queryJson = JObject.Parse(queryResult);
                var elements = queryJson["elements"] as JArray;
                if (elements == null || elements.Count == 0)
                    return ToolResult.Ok($"未找到类型为 {type} 的元素。");

                // 读取每个元素的属性
                var rows = new List<Dictionary<string, string>>();
                foreach (var elem in elements)
                {
                    ct.ThrowIfCancellationRequested();
                    string name = elem["name"]?.ToString() ?? elem.ToString();
                    var row = new Dictionary<string, string> { ["NAME"] = name };

                    foreach (var attr in attributes)
                    {
                        if (attr == "NAME") continue;
                        string val = _dispatcher.GetAttribute(name, attr);
                        row[attr] = val ?? "";
                    }
                    rows.Add(row);
                }

                // 格式化输出
                string output = format == "csv"
                    ? FormatCsv(rows, attributes)
                    : FormatTable(rows, attributes);

                string summary = $"共 {rows.Count} 条记录，属性: {string.Join(", ", attributes)}";
                return ToolResult.Ok($"{summary}\n\n{output}", new
                {
                    type, count = rows.Count, attributes, format
                });
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"Report failed: {ex.Message}");
            }
        }

        private string FormatTable(List<Dictionary<string, string>> rows, List<string> cols)
        {
            var sb = new StringBuilder();
            // 表头
            sb.AppendLine("| " + string.Join(" | ", cols) + " |");
            sb.AppendLine("| " + string.Join(" | ", cols.Select(c => new string('-', Math.Max(c.Length, 4)))) + " |");
            foreach (var row in rows)
            {
                sb.AppendLine("| " + string.Join(" | ", cols.Select(c =>
                {
                    string val = row.ContainsKey(c) ? row[c] : "";
                    return val.Length > 40 ? val.Substring(0, 37) + "..." : val;
                })) + " |");
            }
            return sb.ToString();
        }

        private string FormatCsv(List<Dictionary<string, string>> rows, List<string> cols)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", cols));
            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(",", cols.Select(c =>
                {
                    string val = row.ContainsKey(c) ? row[c] : "";
                    return val.Contains(",") || val.Contains("\"") || val.Contains("\n")
                        ? "\"" + val.Replace("\"", "\"\"") + "\"" : val;
                })));
            }
            return sb.ToString();
        }
    }
}
