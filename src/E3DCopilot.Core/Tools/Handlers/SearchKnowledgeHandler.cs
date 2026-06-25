using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// SearchKnowledge — 搜索本地知识库（结构化知识库 + API 文档）
    /// 
    /// 元能力工具 — 第 0 层：
    /// AI 在生成 PML/C# 代码前调用此工具，查找 API 签名/PML 语法/黄金范式/领域术语。
    /// 
    /// 搜索策略（两级）：
    /// 1. 优先搜索 knowledge/ 结构化知识库（快速精准，已验证的内容）
    /// 2. 回退到 E3D官方API文档/docs/ 的 HTML 全文搜索（全面覆盖）
    /// </summary>
    public class SearchKnowledgeHandler : IToolHandler
    {
        private readonly IEventSink _sink;

        // 知识库根目录：优先用程序同级 knowledge/，回退到开发目录
        private static readonly string KnowledgeRoot = ResolveKnowledgeRoot();

        // 文档根目录：优先用程序同级 docs/，回退到开发目录
        private static readonly string DocsRoot = ResolveDocsRoot();

        private static string ResolveKnowledgeRoot()
        {
            // 策略：从 BaseDirectory 向上搜索 knowledge/ 目录
            // - 生产模式：knowledge/ 被复制到 exe 同级目录，第一层就命中
            // - 开发模式：knowledge/ 在源码根目录，向上 5~6 层命中
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++)
            {
                var candidate = Path.Combine(dir, "knowledge");
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            // 未找到：返回 exe 同级（部署时应确保 knowledge/ 已复制过去）
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "knowledge");
        }

        private static string ResolveDocsRoot()
        {
            // 策略同上：从 BaseDirectory 向上搜索 docs/ 目录
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++)
            {
                var candidate = Path.Combine(dir, "docs");
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs");
        }

        // 预加载的搜索索引
        private static Dictionary<string, List<string>> _keywordIndex = null;
        private static Dictionary<string, FileMeta> _fileIndex = null;
        private static readonly object _indexLock = new object();

        // 模块目录映射
        private static readonly Dictionary<string, string> ModuleDirs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ApplicationFramework", "ApplicationFramework" },
            { "Presentation", "ApplicationFramework" },
            { "Database", "Database" },
            { "Design", "Design" },
            { "Piping", "Piping" },
            { "Geometry", "Geometry" },
            { "Graphics", "Graphics" },
            { "Shared", "Shared" },
            { "Utilities", "Utilities" },
            { "Standalone", "Standalone" }
        };

        // 知识源目录映射
        private static readonly Dictionary<string, string> SourceDirs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "api", "api" },
            { "pml", "pml" },
            { "pattern", "patterns" },
            { "domain", "domain" }
        };

        public SearchKnowledgeHandler(IEventSink sink = null)
        {
            _sink = sink;
        }

        public string Name => "search_knowledge";
        public string Description => "搜索 E3D 知识库（API 签名/PML 语法/黄金范式/领域术语）。在生成 PML 或 C# 代码前调用，避免 API 幻觉。支持分源检索：api(C# API签名)、pml(PML语法)、pattern(已验证黄金范式)、domain(领域术语映射)。Search local E3D knowledge base for API signatures, PML syntax, verified patterns, and domain terminology. Use before generating code to eliminate API hallucination.";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""query"": {
      ""type"": ""string"",
      ""description"": ""搜索关键词 — 你想找什么？支持中英文、属性名、类名。例如: 'GetAsString', 'coll all', '壁厚', 'DbElement'""
    },
    ""source"": {
      ""type"": ""string"",
      ""enum"": [""api"", ""pml"", ""pattern"", ""domain"", ""all""],
      ""description"": ""知识源：api(C# API签名) / pml(PML语法) / pattern(黄金范式) / domain(领域术语) / all(全部)""
    },
    ""language"": {
      ""type"": ""string"",
      ""enum"": [""PML"", ""C#"", ""both""],
      ""description"": ""代码语言筛选（默认 both）""
    },
    ""limit"": {
      ""type"": ""integer"",
      ""description"": ""返回结果数（默认 5，最大 20）""
    },
    ""include_examples"": {
      ""type"": ""boolean"",
      ""description"": ""是否附带代码示例（默认 true）""
    }
  },
  ""required"": [""query""]
}";

        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            await Task.CompletedTask;

            try
            {
                var json = JObject.Parse(args);
                string query = json.Value<string>("query");
                string source = json.Value<string>("source") ?? "all";
                string language = json.Value<string>("language") ?? "both";
                int limit = json.Value<int?>("limit") ?? 5;
                bool includeExamples = json.Value<bool?>("include_examples") ?? true;

                if (string.IsNullOrWhiteSpace(query))
                    return ToolResult.Fail("搜索关键词不能为空");

                limit = Math.Max(1, Math.Min(limit, 20));

                // 第一阶段：搜索结构化知识库
                var knowledgeResults = SearchKnowledgeBase(query, source, limit, includeExamples, ct);

                // 第二阶段（可选）：如果知识库结果不足，回退到 HTML 全文搜索
                if (knowledgeResults.Count < limit && Directory.Exists(DocsRoot))
                {
                    int htmlNeeded = limit - knowledgeResults.Count;
                    var htmlResults = await SearchHtmlDocsAsync(query, source, htmlNeeded, ct);
                    knowledgeResults.AddRange(htmlResults);
                }

                if (knowledgeResults.Count == 0)
                {
                    string srcHint = source != "all" ? $" in source '{source}'" : "";
                    return ToolResult.Ok(
                        $"在知识库中未找到 '{query}'{srcHint} 的相关结果。试试换其他关键词或 source=all。", null);
                }

                // 格式化为输出
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"找到 {knowledgeResults.Count} 条关于 '{query}' 的结果：");
                sb.AppendLine();

                for (int i = 0; i < knowledgeResults.Count; i++)
                {
                    var r = knowledgeResults[i];
                    sb.AppendLine($"--- 结果 {i + 1} ---");
                    sb.AppendLine($"来源: {r.SourceLabel}");
                    sb.AppendLine($"文件: {r.FileName}");
                    if (!string.IsNullOrEmpty(r.Signature))
                        sb.AppendLine($"签名: {r.Signature}");
                    sb.AppendLine();
                    sb.AppendLine(r.Excerpt);
                    if (includeExamples && !string.IsNullOrEmpty(r.Example))
                    {
                        sb.AppendLine();
                        sb.AppendLine("代码示例:");
                        sb.AppendLine(r.Example);
                    }
                    sb.AppendLine();
                }

                _sink?.Emit(CopilotEvent.Notice($"Knowledge search: '{query}' ({source}) → {knowledgeResults.Count} results"));

                return ToolResult.Ok(sb.ToString().TrimEnd(), new
                {
                    query,
                    source,
                    count = knowledgeResults.Count,
                    results = knowledgeResults.Select(r => new
                    {
                        source = r.SourceLabel,
                        file = r.FileName,
                        signature = r.Signature,
                        excerpt = r.Excerpt,
                        example = r.Example
                    }).ToList()
                });
            }
            catch (JsonException ex)
            {
                return ToolResult.Fail($"参数 JSON 解析错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"知识搜索失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 搜索结构化知识库（knowledge/ 目录 + search_index.json）
        /// </summary>
        private List<SearchResult> SearchKnowledgeBase(string query, string source, int limit, bool includeExamples, CancellationToken ct)
        {
            var results = new List<SearchResult>();

            if (!Directory.Exists(KnowledgeRoot))
                return results;

            EnsureIndexLoaded();

            // 确定搜索的知识源目录
            var targetSources = new List<string>();
            if (source == "all")
                targetSources.AddRange(SourceDirs.Keys);
            else if (SourceDirs.ContainsKey(source))
                targetSources.Add(source);
            else
                targetSources.Add("all");

            // 核心：用 search_index.json 做关键词匹配
            var matchedFiles = new HashSet<string>();
            string queryLower = query.ToLowerInvariant();

            // 1. 精确关键词匹配
            if (_keywordIndex != null)
            {
                foreach (var kvp in _keywordIndex)
                {
                    ct.ThrowIfCancellationRequested();

                    // 检查关键词是否匹配搜索词
                    if (kvp.Key.IndexOf(queryLower, StringComparison.OrdinalIgnoreCase) >= 0
                        || queryLower.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        foreach (var file in kvp.Value)
                        {
                            // 检查知识源过滤
                            if (MatchesSourceFilter(file, targetSources))
                                matchedFiles.Add(file);
                        }
                    }
                }
            }

            // 2. 文件名匹配（作为补充）
            if (matchedFiles.Count < 20 && _fileIndex != null)
            {
                foreach (var kvp in _fileIndex)
                {
                    ct.ThrowIfCancellationRequested();

                    string fileName = Path.GetFileNameWithoutExtension(kvp.Key);
                    if (fileName.IndexOf(queryLower, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (MatchesSourceFilter(kvp.Key, targetSources))
                            matchedFiles.Add(kvp.Key);
                    }

                    // 标签匹配
                    if (kvp.Value.tags != null)
                    {
                        foreach (var tag in kvp.Value.tags)
                        {
                            if (tag.IndexOf(queryLower, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                if (MatchesSourceFilter(kvp.Key, targetSources))
                                    matchedFiles.Add(kvp.Key);
                                break;
                            }
                        }
                    }
                }
            }

            // 3. 读取匹配文件内容，提取摘要
            int count = 0;
            foreach (var file in matchedFiles)
            {
                if (count >= 20) break;
                ct.ThrowIfCancellationRequested();

                string fullPath = Path.Combine(KnowledgeRoot, file);
                if (!File.Exists(fullPath)) continue;

                try
                {
                    string content = File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
                    FileMeta meta = _fileIndex != null && _fileIndex.ContainsKey(file)
                        ? _fileIndex[file] : null;

                    // 提取签名（查找 | 方法 |行或 ```csharp 后的第一行签名）
                    string signature = ExtractSignature(content);
                    // 提取代码示例
                    string example = includeExamples ? ExtractCodeExample(content) : null;
                    // 提取摘要
                    string excerpt = ExtractExcerpt(content, query);

                    results.Add(new SearchResult
                    {
                        FileName = Path.GetFileName(file),
                        FilePath = fullPath,
                        SourceLabel = GetSourceLabel(file),
                        Signature = signature,
                        Excerpt = excerpt,
                        Example = example,
                        Verified = meta?.verified ?? false
                    });
                    count++;
                }
                catch { /* 跳过无法读取的文件 */ }
            }

            return results.Take(limit).ToList();
        }

        /// <summary>
        /// 回退到 HTML 全文搜索
        /// </summary>
        private async Task<List<SearchResult>> SearchHtmlDocsAsync(
            string query, string source, int maxResults, CancellationToken ct)
        {
            var results = new List<SearchResult>();

            if (!Directory.Exists(DocsRoot))
                return results;

            string searchDir = DocsRoot;
            if (source != "all" && ModuleDirs.ContainsKey(source))
                searchDir = Path.Combine(DocsRoot, ModuleDirs[source]);

            if (!Directory.Exists(searchDir))
                return results;

            var htmlFiles = Directory.GetFiles(searchDir, "*.html", SearchOption.AllDirectories);

            foreach (var file in htmlFiles)
            {
                if (results.Count >= maxResults) break;
                ct.ThrowIfCancellationRequested();

                try
                {
                    var result = await SearchHtmlFileAsync(file, query, ct);
                    if (result != null)
                        results.Add(result);
                }
                catch { }
            }

            return results;
        }

        private async Task<SearchResult> SearchHtmlFileAsync(
            string filePath, string query, CancellationToken ct)
        {
            string content = File.ReadAllText(filePath, System.Text.Encoding.UTF8);

            // 跳过二进制
            if (content.Contains("\0")) return null;

            // 搜索关键词
            int matchIndex = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (matchIndex < 0)
            {
                var keywords = query.Split(new[] { ' ', '\t', '，', '。', '；', '：' },
                    StringSplitOptions.RemoveEmptyEntries);
                foreach (var kw in keywords)
                {
                    if (content.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchIndex = content.IndexOf(kw, StringComparison.OrdinalIgnoreCase);
                        break;
                    }
                }
                if (matchIndex < 0) return null;
            }

            // 提取上下文
            int excerptStart = Math.Max(0, matchIndex - 200);
            int excerptEnd = Math.Min(content.Length, matchIndex + query.Length + 200);
            string excerpt = content.Substring(excerptStart, excerptEnd - excerptStart);
            excerpt = Regex.Replace(excerpt, @"<[^>]+>", " ");
            excerpt = Regex.Replace(excerpt, @"\s+", " ").Trim();
            if (excerpt.Length > 500)
                excerpt = excerpt.Substring(0, 497) + "...";

            return new SearchResult
            {
                FileName = Path.GetFileName(filePath),
                FilePath = filePath,
                SourceLabel = "HTML 文档",
                Excerpt = excerpt,
                Verified = false
            };
        }

        #region 索引加载

        private void EnsureIndexLoaded()
        {
            if (_keywordIndex != null && _fileIndex != null)
                return;

            lock (_indexLock)
            {
                if (_keywordIndex != null && _fileIndex != null)
                    return;

                string indexPath = Path.Combine(KnowledgeRoot, "search_index.json");
                if (!File.Exists(indexPath))
                {
                    _keywordIndex = new Dictionary<string, List<string>>();
                    _fileIndex = new Dictionary<string, FileMeta>();
                    return;
                }

                try
                {
                    string json = File.ReadAllText(indexPath, System.Text.Encoding.UTF8);
                    var index = JObject.Parse(json);

                    _keywordIndex = new Dictionary<string, List<string>>();
                    var kwObj = index["keywords"] as JObject;
                    if (kwObj != null)
                    {
                        foreach (var prop in kwObj.Properties())
                        {
                            var files = prop.Value.ToObject<List<string>>();
                            _keywordIndex[prop.Name.ToLowerInvariant()] = files;
                        }
                    }

                    _fileIndex = new Dictionary<string, FileMeta>();
                    var fileObj = index["files"] as JObject;
                    if (fileObj != null)
                    {
                        foreach (var prop in fileObj.Properties())
                        {
                            var meta = prop.Value.ToObject<FileMeta>();
                            _fileIndex[prop.Name] = meta;
                        }
                    }
                }
                catch
                {
                    _keywordIndex = new Dictionary<string, List<string>>();
                    _fileIndex = new Dictionary<string, FileMeta>();
                }
            }
        }

        #endregion

        #region 辅助方法

        private bool MatchesSourceFilter(string filePath, List<string> sources)
        {
            foreach (var src in sources)
            {
                if (SourceDirs.TryGetValue(src, out var dir))
                {
                    if (filePath.StartsWith(dir + "/", StringComparison.OrdinalIgnoreCase)
                        || filePath.StartsWith(dir + "\\", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            // 如果 sources 包含 "all" 并且文件在任何已知源目录下
            if (sources.Contains("all"))
            {
                foreach (var dir in SourceDirs.Values)
                {
                    if (filePath.StartsWith(dir + "/", StringComparison.OrdinalIgnoreCase)
                        || filePath.StartsWith(dir + "\\", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        private string GetSourceLabel(string filePath)
        {
            foreach (var kvp in SourceDirs)
            {
                if (filePath.StartsWith(kvp.Value + "/", StringComparison.OrdinalIgnoreCase)
                    || filePath.StartsWith(kvp.Value + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    switch (kvp.Key)
                    {
                        case "api": return "C# API 文档";
                        case "pml": return "PML 语法";
                        case "pattern": return "黄金范式";
                        case "domain": return "领域术语";
                    }
                }
            }
            return "知识库";
        }

        private string ExtractSignature(string content)
        {
            // 查找 Markdown 表格中的方法签名行
            var tableMatch = Regex.Match(content, @"\|.*?\b(\w+)\b.*?\|.*?`([^`]+)`.*?\|", RegexOptions.Multiline);
            if (tableMatch.Success)
            {
                string method = tableMatch.Groups[1].Value;
                string sig = tableMatch.Groups[2].Value;
                return $"{method}: {sig}";
            }
            return null;
        }

        private string ExtractCodeExample(string content)
        {
            // 提取第一个 csharp 代码块
            var csharpMatch = Regex.Match(content, @"```csharp\s*\n(.*?)```", RegexOptions.Singleline);
            if (csharpMatch.Success)
            {
                string code = csharpMatch.Groups[1].Value.Trim();
                if (code.Length > 300)
                    code = code.Substring(0, 297) + "...";
                return code;
            }

            // 提取第一个 pml 代码块
            var pmlMatch = Regex.Match(content, @"```pml\s*\n(.*?)```", RegexOptions.Singleline);
            if (pmlMatch.Success)
            {
                string code = pmlMatch.Groups[1].Value.Trim();
                if (code.Length > 300)
                    code = code.Substring(0, 297) + "...";
                return code;
            }

            return null;
        }

        private string ExtractExcerpt(string content, string query)
        {
            // 找文件开头的摘要描述
            var lines = content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // 找 **一句话** 或 **用途** 行
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Contains("**用途**") || trimmed.Contains("**一句话**"))
                {
                    int idx = trimmed.IndexOf(':');
                    if (idx > 0 && idx < trimmed.Length - 1)
                        return trimmed.Substring(idx + 1).Trim();
                }
            }

            // 找第一段有意义的内容
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 20 && !trimmed.StartsWith("#") && !trimmed.StartsWith("```")
                    && !trimmed.StartsWith("---"))
                {
                    string excerpt = trimmed;
                    if (excerpt.Length > 200)
                        excerpt = excerpt.Substring(0, 197) + "...";
                    return excerpt;
                }
            }

            // 找包含搜索词的内容
            int queryIdx = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (queryIdx > 0)
            {
                int start = Math.Max(0, queryIdx - 80);
                int end = Math.Min(content.Length, queryIdx + query.Length + 120);
                string excerpt = content.Substring(start, end - start);
                excerpt = excerpt.Replace("\n", " ").Replace("\r", "");
                if (excerpt.Length > 200)
                    excerpt = excerpt.Substring(0, 197) + "...";
                return excerpt;
            }

            return "(文档摘要)";
        }

        #endregion

        private class FileMeta
        {
            public string title { get; set; }
            public List<string> tags { get; set; }
            public string summary { get; set; }
            public bool verified { get; set; }
        }

        private class SearchResult
        {
            public string FileName { get; set; }
            public string FilePath { get; set; }
            public string SourceLabel { get; set; }
            public string Signature { get; set; }
            public string Excerpt { get; set; }
            public string Example { get; set; }
            public bool Verified { get; set; }
        }
    }
}
