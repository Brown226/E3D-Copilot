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
      ""enum"": [""search"", ""remember"", ""forget"", ""list"", ""read""],
      ""description"": ""Operation: search(BM25全文检索), remember(记住新知识), forget(遗忘/归档), list(列出全部), read(读取单条完整内容)""
    },
    ""query"": {
      ""type"": ""string"",
      ""description"": ""[search] Search keywords — BM25 ranked retrieval over title, content, and tags. 搜索关键词""
    },
    ""scope"": {
      ""type"": ""string"",
      ""enum"": [""project"", ""global""],
      ""description"": ""[search] Search scope: project(current project only) or global(all memories). 搜索范围""
    },
    ""id"": {
      ""type"": ""string"",
      ""description"": ""[forget/read] Memory ID. [remember] Existing ID to update (omit for new). 记忆ID""
    },
    ""title"": {
      ""type"": ""string"",
      ""description"": ""[remember] Descriptive title for the memory. 记忆标题""
    },
    ""content"": {
      ""type"": ""string"",
      ""description"": ""[remember] The actual content/knowledge to remember. 记忆内容""
    },
    ""kind"": {
      ""type"": ""string"",
      ""enum"": [""project_context"", ""user_preference"", ""technical_decision"", ""coding_pattern"", ""troubleshooting""],
      ""description"": ""[remember] Memory category. [list] Filter by kind. 记忆分类""
    },
    ""tags"": {
      ""type"": ""array"",
      ""items"": { ""type"": ""string"" },
      ""description"": ""[remember] Tags for categorization and search. 标签数组""
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
                    case "remember":
                    case "save": // 兼容旧接口
                        return Remember(json);
                    case "forget":
                    case "delete": // 兼容旧接口
                        return Forget(json);
                    case "list":
                        return List(json);
                    case "read":
                        return Read(json);
                    default:
                        return ToolResult.Fail($"Unknown action: {action}. Supported: search, remember, forget, list, read");
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
        /// BM25 全文搜索记忆（对齐 Reasonix memory tool search）
        /// </summary>
        private ToolResult Search(JObject json)
        {
            string query = json.Value<string>("query");
            if (string.IsNullOrWhiteSpace(query))
                return ToolResult.Fail("query is required for search");

            // 优先使用 BM25 索引
            var bm25Results = _memoryManager.Search(query, 10);

            if (bm25Results.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Found {bm25Results.Count} memories matching '{query}' (BM25 ranked):");
                sb.AppendLine();

                var allMemories = _memoryManager.List();
                var memMap = allMemories.ToDictionary(m => m.Id, m => m);

                foreach (var hit in bm25Results)
                {
                    MemoryEntry m;
                    if (!memMap.TryGetValue(hit.DocId, out m)) continue;

                    sb.AppendLine($"── {m.Title} [{m.Id}] (score: {hit.NormalizedScore:F2}) ──");
                    sb.AppendLine($"  Kind: {m.Kind}");
                    if (m.Tags != null && m.Tags.Length > 0)
                        sb.AppendLine($"  Tags: {string.Join(", ", m.Tags)}");

                    string excerpt = m.Content;
                    if (excerpt.Length > 300)
                        excerpt = excerpt.Substring(0, 297) + "...";
                    sb.AppendLine($"  Content: {excerpt}");
                    sb.AppendLine();
                }

                _sink?.Emit(CopilotEvent.Notice($"Memory search: '{query}' → {bm25Results.Count} results (BM25)"));
                return ToolResult.Ok(sb.ToString().TrimEnd(), new { query, count = bm25Results.Count });
            }

            // BM25 无结果时回退到关键词匹配
            var allMem = _memoryManager.List();
            if (allMem.Count == 0)
                return ToolResult.Ok("No memories stored yet. Use action='remember' to create one.", null);

            string queryLower = query.ToLowerInvariant();
            var keywords = queryLower.Split(new[] { ' ', ',', '，', ';' }, StringSplitOptions.RemoveEmptyEntries);

            var matched = new List<(MemoryEntry Entry, int Score)>();
            foreach (var m in allMem)
            {
                int score = 0;
                string titleLower = (m.Title ?? "").ToLowerInvariant();
                string contentLower = (m.Content ?? "").ToLowerInvariant();

                foreach (var kw in keywords)
                {
                    if (titleLower.Contains(kw)) score += 3;
                    if (contentLower.Contains(kw)) score += 2;
                    if (m.Tags != null && m.Tags.Any(t => t.ToLowerInvariant().Contains(kw)))
                        score += 1;
                }

                if (score > 0)
                    matched.Add((m, score));
            }

            var results = matched
                .OrderByDescending(x => x.Score)
                .Take(10)
                .Select(x => x.Entry)
                .ToList();

            if (results.Count == 0)
            {
                return ToolResult.Ok(
                    $"No memories matching '{query}'. Try different keywords or use action='remember' to save new knowledge.",
                    new { query, count = 0 });
            }

            var fallbackSb = new StringBuilder();
            fallbackSb.AppendLine($"Found {results.Count} memories matching '{query}':");
            fallbackSb.AppendLine();
            foreach (var m in results)
            {
                fallbackSb.AppendLine($"── {m.Title} [{m.Id}] ──");
                string excerpt = m.Content;
                if (excerpt.Length > 300)
                    excerpt = excerpt.Substring(0, 297) + "...";
                fallbackSb.AppendLine($"  Content: {excerpt}");
                fallbackSb.AppendLine();
            }

            return ToolResult.Ok(fallbackSb.ToString().TrimEnd(), new { query, count = results.Count });
        }

        /// <summary>
        /// 记住新知识（对齐 Reasonix remember tool）
        /// 注意：此操作需要用户审批（ApprovalMode.Ask）
        /// </summary>
        private ToolResult Remember(JObject json)
        {
            string title = json.Value<string>("title");
            string content = json.Value<string>("content");

            if (string.IsNullOrWhiteSpace(title))
                return ToolResult.Fail("title is required for remember");
            if (string.IsNullOrWhiteSpace(content))
                return ToolResult.Fail("content is required for remember");

            string id = json.Value<string>("id");
            string kind = json.Value<string>("kind") ?? "project_context";
            var tagsToken = json["tags"] as JArray;
            string[] tags = tagsToken != null
                ? tagsToken.Select(t => t.ToString()).ToArray()
                : new string[0];

            MemoryEntry saved;
            if (!string.IsNullOrEmpty(id))
            {
                // 更新已有记忆
                var entry = new MemoryEntry
                {
                    Id = id,
                    Title = title,
                    Content = content,
                    Kind = kind,
                    Tags = tags
                };
                saved = _memoryManager.Save(entry);
            }
            else
            {
                // 新建（自动去重）
                saved = _memoryManager.Remember(title, content, kind, tags);
            }

            if (saved == null)
                return ToolResult.Fail("Failed to save memory");

            string action = string.IsNullOrEmpty(id) ? "Remembered" : "Updated";
            string msg = $"{action}: {saved.Title} [{saved.Id}]";

            _sink?.Emit(CopilotEvent.Notice(msg));

            return ToolResult.Ok(msg, new
            {
                operation = "remember",
                saved.Id,
                saved.Title,
                saved.Kind,
                saved.Tags
            });
        }

        /// <summary>
        /// 遗忘知识（对齐 Reasonix forget tool）— 归档而非真删
        /// 注意：此操作需要用户审批（ApprovalMode.Ask）
        /// </summary>
        private ToolResult Forget(JObject json)
        {
            string id = json.Value<string>("id");
            if (string.IsNullOrWhiteSpace(id))
                return ToolResult.Fail("id is required for forget");

            bool forgotten = _memoryManager.Forget(id);
            if (!forgotten)
                return ToolResult.Fail($"Memory '{id}' not found");

            string msg = $"Forgotten (archived): {id}";
            _sink?.Emit(CopilotEvent.Notice(msg));

            return ToolResult.Ok(msg, new { operation = "forget", id, archived = true });
        }

        /// <summary>
        /// 读取单条记忆的完整内容
        /// </summary>
        private ToolResult Read(JObject json)
        {
            string id = json.Value<string>("id");
            if (string.IsNullOrWhiteSpace(id))
                return ToolResult.Fail("id is required for read");

            var all = _memoryManager.List();
            var entry = all.FirstOrDefault(m => m.Id == id);
            if (entry == null)
                return ToolResult.Fail($"Memory '{id}' not found");

            var sb = new StringBuilder();
            sb.AppendLine($"# {entry.Title}");
            sb.AppendLine($"ID: {entry.Id}");
            sb.AppendLine($"Kind: {entry.Kind}");
            if (entry.Tags != null && entry.Tags.Length > 0)
                sb.AppendLine($"Tags: {string.Join(", ", entry.Tags)}");
            sb.AppendLine($"Created: {entry.CreatedAt}");
            sb.AppendLine($"Updated: {entry.UpdatedAt}");
            sb.AppendLine();
            sb.AppendLine(entry.Content);

            return ToolResult.Ok(sb.ToString().TrimEnd(), new { entry.Id, entry.Title, entry.Kind });
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
