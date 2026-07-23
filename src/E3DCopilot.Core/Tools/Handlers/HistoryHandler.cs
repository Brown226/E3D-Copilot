using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// History — 历史会话检索工具（对齐 Reasonix builtin/history.go）
    ///
    /// 功能：
    ///   - search: BM25 搜索历史会话 JSONL 文件
    ///   - around: 读取命中点上下文窗口（前后 N 条消息）
    ///   - list: 列出最近会话
    ///
    /// scope:
    ///   - project: 搜索当前项目的会话目录
    ///   - global: 搜索全局会话目录 + 压缩归档
    /// </summary>
    public class HistoryHandler : IToolHandler
    {
        private readonly IEventSink _sink;
        private readonly SessionStore _sessionStore;
        private BM25Index _historyIndex;
        private bool _indexBuilt;
        private readonly object _indexLock = new object();

        // 命中点上下文窗口大小
        private const int AroundWindow = 5;

        public HistoryHandler(SessionStore sessionStore, IEventSink sink = null)
        {
            _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
            _sink = sink;
        }

        public string Name => "history";

        public string Description =>
            "Search past conversation transcripts using BM25 retrieval. " +
            "Use when you need to recall what was discussed or done in a previous session. " +
            "搜索历史对话记录。适合回忆之前会话中讨论的内容或执行的操作。";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""operation"": {
      ""type"": ""string"",
      ""enum"": [""search"", ""around"", ""list""],
      ""description"": ""search(BM25搜索历史会话), around(读取命中点上下文), list(列出最近会话)""
    },
    ""query"": {
      ""type"": ""string"",
      ""description"": ""[search] Search keywords. 搜索关键词""
    },
    ""scope"": {
      ""type"": ""string"",
      ""enum"": [""project"", ""global""],
      ""description"": ""Search scope: project(current sessions dir) or global(all sessions + archives). 搜索范围""
    },
    ""doc_id"": {
      ""type"": ""string"",
      ""description"": ""[around] The document/session hit ID from a previous search result. 搜索命中ID""
    },
    ""line"": {
      ""type"": ""integer"",
      ""description"": ""[around] The line number in the session file to read around. 行号""
    },
    ""limit"": {
      ""type"": ""integer"",
      ""description"": ""[list] Max number of sessions to list (default 10). 最大列出数量""
    }
  },
  ""required"": [""operation""]
}";

        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            await Task.CompletedTask;

            try
            {
                var json = JObject.Parse(args);
                string operation = json.Value<string>("operation")?.ToLowerInvariant();

                switch (operation)
                {
                    case "search":
                        return Search(json);
                    case "around":
                        return Around(json);
                    case "list":
                        return ListSessions(json);
                    default:
                        return ToolResult.Fail($"Unknown operation: {operation}. Supported: search, around, list");
                }
            }
            catch (JsonException ex)
            {
                return ToolResult.Fail($"Invalid JSON arguments: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"History operation failed: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  search — BM25 搜索历史会话
        // ═══════════════════════════════════════════════════════════

        private ToolResult Search(JObject json)
        {
            string query = json.Value<string>("query");
            if (string.IsNullOrWhiteSpace(query))
                return ToolResult.Fail("query is required for search");

            EnsureIndex();

            if (_historyIndex == null || _historyIndex.Count == 0)
                return ToolResult.Ok("No session history available yet.", new { count = 0 });

            var hits = _historyIndex.Search(query, 10);
            if (hits.Count == 0)
            {
                return ToolResult.Ok(
                    $"No history matching '{query}'. Try rarer terms or widen scope to 'global'.",
                    new { query, count = 0 });
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {hits.Count} history hits for '{query}':");
            sb.AppendLine();

            foreach (var hit in hits)
            {
                // hit.DocId 格式: "sessionPath:lineNumber"
                sb.AppendLine($"  [{hit.DocId}] (score: {hit.NormalizedScore:F2})");
                if (!string.IsNullOrEmpty(hit.Source))
                    sb.AppendLine($"    Preview: {hit.Source}");
                sb.AppendLine();
            }

            sb.AppendLine("Use operation='around' with doc_id and line to read context around a hit.");

            _sink?.Emit(CopilotEvent.Notice($"History search: '{query}' → {hits.Count} hits"));
            return ToolResult.Ok(sb.ToString().TrimEnd(), new { query, count = hits.Count });
        }

        // ═══════════════════════════════════════════════════════════
        //  around — 读取命中点上下文窗口
        // ═══════════════════════════════════════════════════════════

        private ToolResult Around(JObject json)
        {
            string docId = json.Value<string>("doc_id");
            if (string.IsNullOrWhiteSpace(docId))
                return ToolResult.Fail("doc_id is required for around operation");

            int line = json.Value<int?>("line") ?? 0;

            // docId 格式: "sessionPath:lineNumber" 或直接是文件路径
            string sessionPath = docId;
            if (docId.Contains(":") && !Path.IsPathRooted(docId))
            {
                var parts = docId.Split(':');
                sessionPath = parts[0];
                if (parts.Length > 1)
                    int.TryParse(parts[1], out line);
            }

            if (!File.Exists(sessionPath))
                return ToolResult.Fail($"Session file not found: {sessionPath}");

            try
            {
                var lines = File.ReadAllLines(sessionPath, Encoding.UTF8);
                int start = Math.Max(0, line - AroundWindow);
                int end = Math.Min(lines.Length - 1, line + AroundWindow);

                var sb = new StringBuilder();
                sb.AppendLine($"Context around line {line} in {Path.GetFileName(sessionPath)}:");
                sb.AppendLine();

                for (int i = start; i <= end; i++)
                {
                    string marker = i == line ? ">>>" : "   ";
                    // 尝试解析消息角色
                    string role = "?";
                    string content = lines[i];
                    try
                    {
                        var dto = JsonConvert.DeserializeObject<JObject>(lines[i]);
                        role = dto?.Value<string>("role") ?? "?";
                        content = dto?.Value<string>("content") ?? lines[i];
                        if (content.Length > 200)
                            content = content.Substring(0, 197) + "...";
                    }
                    catch { }

                    sb.AppendLine($"{marker} L{i} [{role}]: {content}");
                }

                return ToolResult.Ok(sb.ToString().TrimEnd(), new { doc_id = docId, line, window = AroundWindow });
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"Failed to read session: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  list — 列出最近会话
        // ═══════════════════════════════════════════════════════════

        private ToolResult ListSessions(JObject json)
        {
            int limit = json.Value<int?>("limit") ?? 10;
            var sessions = _sessionStore.ListSessions();

            if (sessions.Count == 0)
                return ToolResult.Ok("No saved sessions found.", new { count = 0 });

            var sb = new StringBuilder();
            sb.AppendLine($"Recent sessions ({Math.Min(limit, sessions.Count)} of {sessions.Count}):");
            sb.AppendLine();

            foreach (var s in sessions.Take(limit))
            {
                string preview = string.IsNullOrEmpty(s.Preview) ? "(no preview)" : s.Preview;
                sb.AppendLine($"  {s.LastModified:yyyy-MM-dd HH:mm} | {s.TurnCount} turns | {preview}");
                sb.AppendLine($"    Path: {s.SessionPath}");
            }

            return ToolResult.Ok(sb.ToString().TrimEnd(), new { count = sessions.Count });
        }

        // ═══════════════════════════════════════════════════════════
        //  索引构建
        // ═══════════════════════════════════════════════════════════

        private void EnsureIndex()
        {
            lock (_indexLock)
            {
                if (_indexBuilt) return;
                _indexBuilt = true;

                _historyIndex = new BM25Index();
                try
                {
                    var sessionsDir = _sessionStore.SessionsDir;
                    if (!Directory.Exists(sessionsDir)) return;

                    foreach (var path in Directory.GetFiles(sessionsDir, "*.jsonl"))
                    {
                        IndexSessionFile(path);
                    }
                }
                catch { /* 索引构建失败不影响基本功能 */ }
            }
        }

        private void IndexSessionFile(string path)
        {
            try
            {
                var lines = File.ReadAllLines(path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;

                    string content = null;
                    string role = null;
                    try
                    {
                        var dto = JsonConvert.DeserializeObject<JObject>(lines[i]);
                        role = dto?.Value<string>("role");
                        content = dto?.Value<string>("content");
                    }
                    catch { continue; }

                    // 只索引 user 和 assistant 消息（tool 结果太长且可重新获取）
                    if (content == null || (role != "user" && role != "assistant"))
                        continue;

                    // 跳过过短内容
                    if (content.Length < 10) continue;

                    string docId = $"{path}:{i}";
                    string preview = content.Length > 100 ? content.Substring(0, 97) + "..." : content;
                    _historyIndex.Add(docId, content, preview);
                }
            }
            catch { /* 单文件索引失败跳过 */ }
        }
    }
}
