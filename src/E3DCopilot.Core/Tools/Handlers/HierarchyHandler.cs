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
    /// Hierarchy — 元素层级浏览
    /// 
    /// 元能力工具：
    /// 查看元素的父子关系、所属区域、层级结构。
    /// 支持向上（父级）和向下（子级）浏览。
    /// </summary>
    public class HierarchyHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public HierarchyHandler(IToolDispatcher dispatcher) { _dispatcher = dispatcher; }

        public string Name => "hierarchy";
        public string Description =>
            "Browse element hierarchy: parent, children, zone membership. " +
            "浏览元素层级结构：父元素、子元素、所属区域。";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""element"": { ""type"": ""string"", ""description"": ""Element to inspect (default: current selected element)"" },
    ""direction"": { ""type"": ""string"", ""enum"": [""up"", ""down"", ""both"", ""info""], ""description"": ""Browse direction: up=parents, down=children, both=full tree, info=summary (default: info)"" },
    ""depth"": { ""type"": ""integer"", ""description"": ""Max depth for tree traversal (default: 2)"" }
  }
}";
        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var json = JObject.Parse(args);
                string element = json.Value<string>("element") ?? _dispatcher.GetCurrentElementName();
                string direction = json.Value<string>("direction") ?? "info";
                int depth = json.Value<int?>("depth") ?? 2;
                depth = Math.Max(1, Math.Min(depth, 5));

                if (string.IsNullOrWhiteSpace(element))
                    return ToolResult.Fail("No element specified and no current element selected.");

                switch (direction.ToLower())
                {
                    case "info":
                        return GetElementInfo(element);
                    case "up":
                        return GetParents(element, depth);
                    case "down":
                        return GetChildren(element, depth);
                    case "both":
                        return GetFullTree(element, depth);
                    default:
                        return ToolResult.Fail($"Unknown direction: {direction}");
                }
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"Hierarchy failed: {ex.Message}");
            }
        }

        private ToolResult GetElementInfo(string element)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"元素: {element}");

            string type = _dispatcher.GetAttribute(element, "TYPE") ?? "";
            string desc = _dispatcher.GetAttribute(element, "DESC") ?? "";
            string owner = _dispatcher.GetAttribute(element, "OWNER") ?? "";
            string name = _dispatcher.GetAttribute(element, "NAME") ?? "";

            sb.AppendLine($"  类型: {type}");
            if (!string.IsNullOrEmpty(name) && name != element) sb.AppendLine($"  名称: {name}");
            if (!string.IsNullOrEmpty(desc)) sb.AppendLine($"  描述: {desc}");
            if (!string.IsNullOrEmpty(owner)) sb.AppendLine($"  所属: {owner}");

            // 尝试获取父元素
            string parent = _dispatcher.GetAttribute(element, "PARENT") ?? "";
            if (!string.IsNullOrEmpty(parent))
                sb.AppendLine($"  父级: {parent}");

            // 查询子元素
            var children = QueryChildren(element);
            if (children.Count > 0)
            {
                sb.AppendLine($"  子元素 ({children.Count} 个):");
                foreach (var child in children.Take(20))
                    sb.AppendLine($"    - {child}");
                if (children.Count > 20)
                    sb.AppendLine($"    ... 还有 {children.Count - 20} 个");
            }

            return ToolResult.Ok(sb.ToString().TrimEnd(), new
            {
                element, type, desc, owner,
                childCount = children.Count
            });
        }

        private ToolResult GetParents(string element, int depth)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"向上浏览: {element}");
            sb.AppendLine();

            string current = element;
            for (int i = 0; i < depth; i++)
            {
                string parent = _dispatcher.GetAttribute(current, "PARENT") ?? "";
                if (string.IsNullOrEmpty(parent)) break;

                string indent = new string(' ', i * 2);
                string type = _dispatcher.GetAttribute(parent, "TYPE") ?? "";
                sb.AppendLine($"{indent}↑ {parent} ({type})");
                current = parent;
            }

            if (current == element)
                sb.AppendLine("（未找到父级信息）");

            return ToolResult.Ok(sb.ToString().TrimEnd());
        }

        private ToolResult GetChildren(string element, int depth)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"向下浏览: {element}");
            sb.AppendLine();

            int total = TraverseChildren(element, 0, depth, sb);

            if (total == 0)
                sb.AppendLine("（无子元素）");

            return ToolResult.Ok(sb.ToString().TrimEnd(), new { element, totalChildren = total });
        }

        private int TraverseChildren(string element, int currentDepth, int maxDepth, StringBuilder sb)
        {
            if (currentDepth >= maxDepth) return 0;

            var children = QueryChildren(element);
            int total = children.Count;
            string indent = new string(' ', currentDepth * 2);

            foreach (var child in children)
            {
                string type = _dispatcher.GetAttribute(child, "TYPE") ?? "";
                string prefix = currentDepth == 0 ? "├─ " : "│  ";
                sb.AppendLine($"{indent}{prefix}{child} ({type})");

                if (currentDepth < maxDepth - 1)
                    total += TraverseChildren(child, currentDepth + 1, maxDepth, sb);
            }

            return total;
        }

        private ToolResult GetFullTree(string element, int depth)
        {
            var sb = new StringBuilder();
            string type = _dispatcher.GetAttribute(element, "TYPE") ?? "";
            sb.AppendLine($"{element} ({type})");

            int total = TraverseChildren(element, 0, depth, sb);
            return ToolResult.Ok(sb.ToString().TrimEnd(), new { element, totalDescendants = total });
        }

        private List<string> QueryChildren(string parent)
        {
            try
            {
                string result = _dispatcher.ExecuteAsync("query", Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    type = "*",
                    scope = parent,
                    limit = 100
                })).Result;

                var json = JObject.Parse(result);
                var elements = json["elements"] as JArray;
                if (elements == null) return new List<string>();

                return elements.Select(e =>
                {
                    var nameToken = e["name"];
                    return nameToken != null ? nameToken.ToString() : e.ToString();
                }).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
