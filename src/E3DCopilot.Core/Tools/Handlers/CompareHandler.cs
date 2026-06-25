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
    /// Compare — 对比两个元素的属性差异
    /// 
    /// 元能力工具：
    /// 读取两个元素的属性并逐项对比，输出差异列表。
    /// 用于变更审查、设计校核、版本对比。
    /// </summary>
    public class CompareHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public CompareHandler(IToolDispatcher dispatcher) { _dispatcher = dispatcher; }

        public string Name => "compare";
        public string Description =>
            "Compare attributes of two E3D elements and show differences. " +
            "对比两个 E3D 元素的属性差异。用于变更审查、设计校核。";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""element_a"": { ""type"": ""string"", ""description"": ""First element name or DBURI"" },
    ""element_b"": { ""type"": ""string"", ""description"": ""Second element name or DBURI"" },
    ""attributes"": {
      ""type"": ""array"", ""items"": { ""type"": ""string"" },
      ""description"": ""Specific attributes to compare (default: all common attributes)"" }
  },
  ""required"": [""element_a"", ""element_b""]
}";
        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var json = JObject.Parse(args);
                string elemA = json.Value<string>("element_a");
                string elemB = json.Value<string>("element_b");

                if (string.IsNullOrWhiteSpace(elemA) || string.IsNullOrWhiteSpace(elemB))
                    return ToolResult.Fail("Both element_a and element_b are required");

                // 获取两个元素的类型
                string typeA = _dispatcher.GetAttribute(elemA, "TYPE") ?? "";
                string typeB = _dispatcher.GetAttribute(elemB, "TYPE") ?? "";

                // 确定要对比的属性列表
                var attrsToken = json["attributes"];
                var attributes = new List<string>();
                if (attrsToken is JArray arr && arr.Count > 0)
                {
                    attributes = arr.Select(t => t.ToString()).ToList();
                }
                else
                {
                    // 自动检测：读取一批常用属性，跳过两者都为空的
                    string[] commonAttrs = {
                        "NAME", "TYPE", "DESC", "OWNER",
                        "WTHK", "BORE", "SCHD", "PROM", "SPEC",
                        "ORI", "SYMB", "PLAN", "ROOM",
                        "EAST", "NORTH", "UP"
                    };
                    attributes.AddRange(commonAttrs);
                }

                // 对比
                var diffs = new List<AttrDiff>();
                var matches = new List<AttrMatch>();

                foreach (var attr in attributes)
                {
                    ct.ThrowIfCancellationRequested();
                    string valA = _dispatcher.GetAttribute(elemA, attr) ?? "";
                    string valB = _dispatcher.GetAttribute(elemB, attr) ?? "";

                    if (valA == valB)
                    {
                        if (!string.IsNullOrEmpty(valA))
                            matches.Add(new AttrMatch { Attribute = attr, Value = valA });
                    }
                    else
                    {
                        diffs.Add(new AttrDiff
                        {
                            Attribute = attr,
                            ValueA = valA,
                            ValueB = valB
                        });
                    }
                }

                // 格式化输出
                var sb = new StringBuilder();
                sb.AppendLine($"对比: {elemA} vs {elemB}");
                if (typeA != typeB)
                    sb.AppendLine($"⚠ 类型不同: {typeA} vs {typeB}");
                sb.AppendLine();

                if (diffs.Count > 0)
                {
                    sb.AppendLine($"差异属性 ({diffs.Count} 项):");
                    sb.AppendLine("| 属性 | " + Truncate(elemA, 25) + " | " + Truncate(elemB, 25) + " |");
                    sb.AppendLine("| --- | --- | --- |");
                    foreach (var d in diffs)
                        sb.AppendLine($"| {d.Attribute} | {Truncate(d.ValueA, 25)} | {Truncate(d.ValueB, 25)} |");
                }
                else
                {
                    sb.AppendLine("✓ 两个元素属性完全相同");
                }

                if (matches.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"相同属性 ({matches.Count} 项): {string.Join(", ", matches.Select(m => m.Attribute))}");
                }

                return ToolResult.Ok(sb.ToString().TrimEnd(), new
                {
                    elementA = elemA, elementB = elemB,
                    diffCount = diffs.Count, matchCount = matches.Count,
                    diffs
                });
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"Compare failed: {ex.Message}");
            }
        }

        private string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length > max ? s.Substring(0, max - 3) + "..." : s);

        private class AttrDiff { public string Attribute; public string ValueA; public string ValueB; }
        private class AttrMatch { public string Attribute; public string Value; }
    }
}
