using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Memory;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// Memory — 记忆存取工具（暴露 MemoryManager 给 LLM）
    ///
    /// 元能力工具：
    /// AI 可在对话中主动保存关键发现（如项目配置规范、用户偏好、技术决策），
    /// 或检索历史记忆来辅助当前任务。
    ///
    /// 操作模式：
    /// - search: 按关键词搜索记忆（标题 + 内容 + 标签模糊匹配）
    /// - save:   保存新记忆或更新现有记忆
    /// - delete: 删除指定记忆
    /// - list:   列出所有记忆（可按 kind 过滤）
    ///
    /// 对齐 Reasonix builtin/memory.go 的 remember/forget 设计。
    /// </summary>
    public class MemoryHandler : IToolHandler
    {
        private readonly IEventSink _sink;
        private readonly MemoryManager _memoryManager;

        public MemoryHandler(MemoryManager memoryManager, IEventSink sink = null)
        {
            _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
            _sink = sink;
        }

        public string Name => "memory";

        public string Description =>
            "Save, search, and retrieve cross-session memories. Use when: " +
            "(1) user says \"remember this\" or \"don't forget\", " +
            "(2) you discover important project facts worth persisting, " +
            "(3) you need to recall previously saved knowledge. " +
            "保存/搜索/检索跨会话记忆。适合记录项目规范、用户偏好、技术决策等。";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""action"": {
      ""type"": ""string"",
      ""enum"": [""search"", ""save"", ""delete"", ""list""],
      ""description"": ""Operation: search(按关键词搜索), save(保存/更新), delete(删除), list(列出全部)""
    },
    ""query"": {
      ""type"": ""string"",
      ""description"": ""[search] Search keywords — matches title, content, and tags. 搜索关键词""
    },
    ""id"": {
      ""type"": ""string"",
      ""description"": ""[delete] Memory ID to delete. [save] Existing ID to update (omit for new). 记忆ID""
    },
    ""title"": {
      ""type"": ""string"",
      ""description"": ""[save] Descriptive title for the memory. 记忆标题""
    },
    ""content"": {
      ""type"": ""string"",
      ""description"": ""[save] The actual content/knowledge to remember. 记忆内容""
    },
    ""kind"": {
      ""type"": ""string"",
      ""enum"": [""project_context"", ""user_preference"", ""technical_decision"", ""coding_pattern"", ""troubleshooting""],
      ""description"": ""[save] Memory category. [list] Filter by kind. 记忆分类""
    },
    ""tags"": {
      ""type"": ""array"",
      ""items"": { ""type"": ""string"" },
      ""description"": ""[save] Tags for categorization and search. 标签数组""
    }
  },
  ""required"": [""action""]
}";

        public bool IsReadOnly => false; // save/delete 是写操作

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            await Task.CompletedTask;

            try
            {
                var json = JObject.Parse(args);
                string action = json.Value<string>("action")?.ToLowerInvariant();

                switch (action)
                {
                    case "search":
                        return Search(json);
                    case "save":
                        return Save(json);
                    case "delete":
                        return Delete(json);
                    case "list":
                        return List(json);
                    default:
                        return ToolResult.Fail($"Unknown action: {action}. Supported: search, save, delete, list");
                }
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                return ToolResult.Fail($"Invalid JSON arguments: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"Memory operation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 搜索记忆 — 关键词匹配标题、内容、标签
        /// </summary>
        private ToolResult Search(JObject json)
        {
            string query = json.Value<string>("query");
            if (string.IsNullOrWhiteSpace(query))
                return ToolResult.Fail("query is required for search");

            var allMemories = _memoryManager.List();
            if (allMemories.Count == 0)
                return ToolResult.Ok("No memories stored yet. Use action='save' to create one.", null);

            // 模糊匹配：标题、内容、标签
            string queryLower = query.ToLowerInvariant();
            var keywords = queryLower.Split(new[] { ' ', ',', '，', ';' }, StringSplitOptions.RemoveEmptyEntries);

            var matched = new List<(MemoryEntry Entry, int Score)>();
            foreach (var m in allMemories)
            {
                int score = 0;
                string titleLower = (m.Title ?? "").ToLowerInvariant();
                string contentLower = (m.Content ?? "").ToLowerInvariant();

                foreach (var kw in keywords)
                {
                    if (titleLower.Contains(kw)) score += 3;        // 标题匹配权重高
                    if (contentLower.Contains(kw)) score += 2;       // 内容匹配
                    if (m.Tags != null && m.Tags.Any(t => t.ToLowerInvariant().Contains(kw)))
                        score += 1;                                   // 标签匹配
                }

                if (score > 0)
                    matched.Add((m, score));
            }

            // 按分数排序，取前 10
            var results = matched
                .OrderByDescending(x => x.Score)
                .Take(10)
                .Select(x => x.Entry)
                .ToList();

            if (results.Count == 0)
            {
                return ToolResult.Ok(
                    $"No memories matching '{query}'. Try different keywords or save new memories.",
                    new { query, count = 0 });
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {results.Count} memories matching '{query}':");
            sb.AppendLine();

            foreach (var m in results)
            {
                sb.AppendLine($"── {m.Title} [{m.Id}] ──");
                sb.AppendLine($"  Kind: {m.Kind}");
                if (m.Tags != null && m.Tags.Length > 0)
                    sb.AppendLine($"  Tags: {string.Join(", ", m.Tags)}");

                string excerpt = m.Content;
                if (excerpt.Length > 300)
                    excerpt = excerpt.Substring(0, 297) + "...";
                sb.AppendLine($"  Content: {excerpt}");
                sb.AppendLine();
            }

            _sink?.Emit(CopilotEvent.Notice($"Memory search: '{query}' → {results.Count} results"));

            return ToolResult.Ok(sb.ToString().TrimEnd(), new
            {
                query,
                count = results.Count,
                results = results.Select(m => new
                {
                    m.Id, m.Title, m.Kind, m.Tags,
                    content = m.Content.Length > 300 ? m.Content.Substring(0, 297) + "..." : m.Content
                }).ToList()
            });
        }

        /// <summary>
        /// 保存记忆 — 新增或更新（有 id 则更新，无 id 则新建）
        /// </summary>
        private ToolResult Save(JObject json)
        {
            string title = json.Value<string>("title");
            string content = json.Value<string>("content");

            if (string.IsNullOrWhiteSpace(title))
                return ToolResult.Fail("title is required for save");
            if (string.IsNullOrWhiteSpace(content))
                return ToolResult.Fail("content is required for save");

            string id = json.Value<string>("id");
            string kind = json.Value<string>("kind") ?? "project_context";
            var tagsToken = json["tags"] as JArray;
            string[] tags = tagsToken != null
                ? tagsToken.Select(t => t.ToString()).ToArray()
                : new string[0];

            var entry = new MemoryEntry
            {
                Id = id, // null 时 MemoryManager 会自动生成
                Title = title,
                Content = content,
                Kind = kind,
                Tags = tags
            };

            var saved = _memoryManager.Save(entry);

            string action = string.IsNullOrEmpty(id) ? "Created" : "Updated";
            string msg = $"{action} memory: {saved.Title} [{saved.Id}]";

            _sink?.Emit(CopilotEvent.Notice(msg));

            return ToolResult.Ok(msg, new
            {
                operation = "save",
                saved.Id,
                saved.Title,
                saved.Kind,
                saved.Tags
            });
        }

        /// <summary>
        /// 删除记忆
        /// </summary>
        private ToolResult Delete(JObject json)
        {
            string id = json.Value<string>("id");
            if (string.IsNullOrWhiteSpace(id))
                return ToolResult.Fail("id is required for delete");

            bool deleted = _memoryManager.Delete(id);
            if (!deleted)
                return ToolResult.Fail($"Memory '{id}' not found");

            string msg = $"Deleted memory: {id}";
            _sink?.Emit(CopilotEvent.Notice(msg));

            return ToolResult.Ok(msg, new { operation = "delete", id });
        }

        /// <summary>
        /// 列出所有记忆
        /// </summary>
        private ToolResult List(JObject json)
        {
            string kindFilter = json.Value<string>("kind");
            var memories = _memoryManager.List(kindFilter);

            if (memories.Count == 0)
            {
                string filterMsg = string.IsNullOrEmpty(kindFilter) || kindFilter == "all"
                    ? "No memories stored yet."
                    : $"No memories of kind '{kindFilter}'.";
                return ToolResult.Ok(filterMsg, new { count = 0 });
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Memories ({memories.Count} total):");
            sb.AppendLine();

            foreach (var m in memories)
            {
                string tagsStr = m.Tags != null && m.Tags.Length > 0
                    ? $" [{string.Join(", ", m.Tags)}]"
                    : "";
                sb.AppendLine($"  • {m.Title} ({m.Kind}){tagsStr} — {m.Id}");

                // 显示内容摘要
                string excerpt = m.Content;
                if (excerpt.Length > 100)
                    excerpt = excerpt.Substring(0, 97) + "...";
                sb.AppendLine($"    {excerpt}");
            }

            return ToolResult.Ok(sb.ToString().TrimEnd(), new
            {
                count = memories.Count,
                kind = kindFilter ?? "all"
            });
        }
    }
}
