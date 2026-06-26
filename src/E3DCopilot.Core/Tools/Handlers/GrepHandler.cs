using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Events;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// Grep — 搜索文件内容 + 知识库索引
    ///
    /// 元能力工具：
    /// 1. knowledge=true：优先用 search_index.json 关键词索引搜索 knowledge/ 目录（快速精准）
    /// 2. knowledge=false（默认）：逐文件扫描，支持正则表达式
    /// </summary>
    public class GrepHandler : IToolHandler
    {
        private readonly IEventSink _sink;

        /// <summary>允许搜索的根目录列表</summary>
        private static readonly string[] AllowedRoots = ResolveAllowedRoots();

        /// <summary>知识库根目录</summary>
        private static readonly string KnowledgeRoot = ResolveKnowledgeRoot();

        /// <summary>最大文件大小（字节）— 跳过超大文件</summary>
        private const int MaxFileSize = 512 * 1024; // 512KB

        /// <summary>最大匹配数</summary>
        private const int MaxMatches = 200;

        // ── 知识库索引（静态，延迟加载） ──
        private static Dictionary<string, List<string>> _keywordIndex;
        private static Dictionary<string, KnowledgeFileMeta> _fileIndex;
        private static readonly object _indexLock = new object();

        /// <summary>禁止搜索的扩展名</summary>
        private static readonly HashSet<string> BlockedExtensions = new HashSet<string>(
            new[]
            {
                ".exe", ".dll", ".chm", ".pdb", ".nupkg", ".zip", ".7z", ".rar",
                ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg",
                ".woff", ".woff2", ".ttf", ".eot", ".mp3", ".mp4", ".avi", ".pdf"
            },
            StringComparer.OrdinalIgnoreCase);

        /// <summary>默认搜索的文本文件扩展名</summary>
        private static readonly HashSet<string> TextExtensions = new HashSet<string>(
            new[]
            {
                ".cs", ".ts", ".tsx", ".js", ".jsx", ".json", ".xml", ".html", ".htm",
                ".css", ".scss", ".less", ".md", ".txt", ".log", ".cfg", ".ini", ".yaml", ".yml",
                ".pml", ".pmlmac", ".pmlfrm", ".bat", ".cmd", ".sh", ".ps1",
                ".py", ".go", ".rs", ".java", ".kt", ".swift", ".c", ".cpp", ".h", ".hpp"
            },
            StringComparer.OrdinalIgnoreCase);

        private static string[] ResolveAllowedRoots()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var roots = new List<string> { appDir };

            var knowledgeDir = Path.Combine(appDir, "knowledge");
            var docsDir = Path.Combine(appDir, "docs");
            if (Directory.Exists(knowledgeDir)) roots.Add(knowledgeDir);
            if (Directory.Exists(docsDir)) roots.Add(docsDir);

            // 向上搜索：将沿途所有目录加入可搜索范围
            var dir = appDir;
            for (int i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++)
            {
                if (!roots.Contains(dir)) roots.Add(dir);
                dir = Path.GetDirectoryName(dir);
            }

            return roots.ToArray();
        }

        private static string ResolveKnowledgeRoot()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++)
            {
                var candidate = Path.Combine(dir, "knowledge");
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "knowledge");
        }

        public GrepHandler(IEventSink sink = null)
        {
            _sink = sink;
        }

        public string Name => "grep";

        public string Description =>
            "Search file contents using text or regex pattern. " +
            "When knowledge=true, uses pre-built keyword index for fast, relevant results from E3D knowledge base. " +
            "在文件中搜索文本/正则。knowledge=true 时优先查知识库索引（API签名/PML语法/黄金范式）。";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""pattern"": {
      ""type"": ""string"",
      ""description"": ""Search pattern — plain text or regex. 搜索模式，支持纯文本和正则表达式。例: 'coll all PIPE', 'DbElement\.Get\w+'""
    },
    ""knowledge"": {
      ""type"": ""boolean"",
      ""description"": ""Search E3D knowledge base using keyword index (default: false). 设为 true 搜索 E3D 知识库（API签名/PML语法/黄金范式）""
    },
    ""source"": {
      ""type"": ""string"",
      ""enum"": [""api"", ""pml"", ""pattern"", ""domain"", ""all""],
      ""description"": ""Knowledge source filter (only when knowledge=true). 知识源筛选：api/pml/pattern/domain/all""
    },
    ""path"": {
      ""type"": ""string"",
      ""description"": ""Directory or file to search in (default: project root). 搜索的目录或文件路径，默认项目根目录""
    },
    ""file_pattern"": {
      ""type"": ""string"",
      ""description"": ""File name glob filter, e.g. '*.pml', '*.cs'. 文件名过滤，如 '*.pml'、'*.cs'""
    },
    ""ignore_case"": {
      ""type"": ""boolean"",
      ""description"": ""Case insensitive search (default: false). 是否忽略大小写""
    },
    ""max_results"": {
      ""type"": ""integer"",
      ""description"": ""Maximum matches to return (default: 50, max: 200). 最大返回匹配数""
    },
    ""context_lines"": {
      ""type"": ""integer"",
      ""description"": ""Lines of context around each match (default: 1). 匹配行前后的上下文行数""
    }
  },
  ""required"": [""pattern""]
}";

        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var json = JObject.Parse(args);
                    string pattern = json.Value<string>("pattern");
                    if (string.IsNullOrWhiteSpace(pattern))
                        return ToolResult.Fail("pattern is required");

                    // ── 知识库索引模式 ──
                    bool knowledgeMode = json.Value<bool?>("knowledge") ?? false;
                    if (knowledgeMode)
                    {
                        string source = json.Value<string>("source") ?? "all";
                        int kLimit = json.Value<int?>("max_results") ?? 10;
                        return SearchKnowledgeIndex(pattern, source, kLimit, ct);
                    }

                    string searchPath = json.Value<string>("path");
                    string filePattern = json.Value<string>("file_pattern");
                    bool ignoreCase = json.Value<bool?>("ignore_case") ?? false;
                    int maxResults = json.Value<int?>("max_results") ?? 50;
                    int contextLines = json.Value<int?>("context_lines") ?? 1;
                    maxResults = Math.Max(1, Math.Min(maxResults, MaxMatches));
                    contextLines = Math.Max(0, Math.Min(contextLines, 3));

                    // 构建正则
                    RegexOptions regexOpts = RegexOptions.Compiled;
                    if (ignoreCase) regexOpts |= RegexOptions.IgnoreCase;

                    Regex regex;
                    try
                    {
                        regex = new Regex(pattern, regexOpts, TimeSpan.FromSeconds(5));
                    }
                    catch (ArgumentException ex)
                    {
                        return ToolResult.Fail($"Invalid regex pattern: {ex.Message}");
                    }

                    // 解析搜索根目录
                    string[] searchRoots;
                    if (!string.IsNullOrWhiteSpace(searchPath))
                    {
                        string fullPath = ResolvePath(searchPath);
                        if (fullPath == null)
                            return ToolResult.Fail($"Access denied or not found: {searchPath}");

                        if (File.Exists(fullPath))
                        {
                            // 单文件搜索
                            searchRoots = new[] { fullPath };
                        }
                        else
                        {
                            searchRoots = new[] { fullPath };
                        }
                    }
                    else
                    {
                        searchRoots = AllowedRoots;
                    }

                    // 执行搜索
                    var matches = new List<GrepMatch>();
                    foreach (var root in searchRoots)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (File.Exists(root))
                        {
                            SearchFile(root, regex, contextLines, maxResults, matches, ct);
                        }
                        else if (Directory.Exists(root))
                        {
                            SearchDirectory(root, regex, filePattern, contextLines, maxResults, matches, ct);
                        }

                        if (matches.Count >= maxResults) break;
                    }

                    if (matches.Count == 0)
                    {
                        return ToolResult.Ok(
                            $"No matches found for pattern '{pattern}'. 尝试换其他搜索词或放宽搜索范围。",
                            new { pattern, matchCount = 0 });
                    }

                    // 格式化输出
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"Found {matches.Count} matches for '{pattern}':");
                    sb.AppendLine();

                    string currentFile = null;
                    foreach (var m in matches)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (m.FilePath != currentFile)
                        {
                            if (currentFile != null) sb.AppendLine();
                            currentFile = m.FilePath;
                            sb.AppendLine($"── {m.FilePath} ──");
                        }

                        // 上下文行（before 在匹配行之前，after 在之后）
                        foreach (var ctx in m.ContextBefore)
                            sb.AppendLine($"  L{ctx.Key}: {ctx.Value.TrimEnd()}");

                        sb.AppendLine($"  L{m.LineNumber}: {m.LineText.TrimEnd()}");

                        foreach (var ctx in m.ContextAfter)
                            sb.AppendLine($"  L{ctx.Key}: {ctx.Value.TrimEnd()}");
                    }

                    string summary = $"Grep: '{pattern}' → {matches.Count} matches";
                    _sink?.Emit(CopilotEvent.Notice(summary));

                    return ToolResult.Ok(sb.ToString().TrimEnd(), new
                    {
                        pattern,
                        matchCount = matches.Count,
                        files = matches.Select(m => m.FilePath).Distinct().Count()
                    });
                }
                catch (Newtonsoft.Json.JsonException ex)
                {
                    return ToolResult.Fail($"Invalid JSON arguments: {ex.Message}");
                }
                catch (RegexMatchTimeoutException)
                {
                    return ToolResult.Fail("Regex timed out (pattern too complex or pathological). Try a simpler pattern.");
                }
                catch (OperationCanceledException)
                {
                    return ToolResult.Fail("Search cancelled");
                }
                catch (Exception ex)
                {
                    return ToolResult.Fail($"Grep failed: {ex.Message}");
                }
            }, ct);
        }

        private void SearchDirectory(string dir, Regex regex, string filePattern,
            int contextLines, int maxResults, List<GrepMatch> matches, CancellationToken ct)
        {
            try
            {
                // 获取文件列表
                string[] files;
                if (!string.IsNullOrWhiteSpace(filePattern))
                    files = Directory.GetFiles(dir, filePattern, SearchOption.AllDirectories);
                else
                    files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();
                    if (matches.Count >= maxResults) return;

                    string ext = Path.GetExtension(file);
                    if (BlockedExtensions.Contains(ext)) continue;

                    // 如果没指定 file_pattern，只搜索已知文本扩展名
                    if (string.IsNullOrWhiteSpace(filePattern) && !TextExtensions.Contains(ext)) continue;

                    SearchFile(file, regex, contextLines, maxResults, matches, ct);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
        }

        private void SearchFile(string filePath, Regex regex, int contextLines,
            int maxResults, List<GrepMatch> matches, CancellationToken ct)
        {
            try
            {
                var fi = new FileInfo(filePath);
                if (fi.Length == 0 || fi.Length > MaxFileSize) return;

                string ext = Path.GetExtension(filePath);
                if (BlockedExtensions.Contains(ext)) return;

                string[] lines;
                try
                {
                    lines = File.ReadAllLines(filePath, System.Text.Encoding.UTF8);
                }
                catch
                {
                    // 编码问题，尝试默认
                    try { lines = File.ReadAllLines(filePath); }
                    catch { return; }
                }

                // 检测是否包含 null 字节（二进制文件）
                if (lines.Length > 0 && lines[0].Contains("\0")) return;

                for (int i = 0; i < lines.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (matches.Count >= maxResults) return;

                    if (regex.IsMatch(lines[i]))
                    {
                        var match = new GrepMatch
                        {
                            FilePath = filePath,
                            LineNumber = i + 1,
                            LineText = lines[i]
                        };

                        // 收集上下文
                        for (int j = Math.Max(0, i - contextLines); j < i; j++)
                            match.ContextBefore.Add(j + 1, lines[j]);
                        for (int j = i + 1; j <= Math.Min(lines.Length - 1, i + contextLines); j++)
                            match.ContextAfter.Add(j + 1, lines[j]);

                        matches.Add(match);
                    }
                }
            }
            catch { /* 跳过无法读取的文件 */ }
        }

        /// <summary>
        /// 解析路径并验证是否在允许范围内
        /// </summary>
        private static string ResolvePath(string path)
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                fullPath = null;
                foreach (var root in AllowedRoots)
                {
                    if (string.IsNullOrEmpty(root)) continue;
                    try
                    {
                        string candidate = Path.GetFullPath(Path.Combine(root, path));
                        if (File.Exists(candidate) || Directory.Exists(candidate))
                        {
                            fullPath = candidate;
                            break;
                        }
                    }
                    catch { }
                }

                if (fullPath == null) return null;
            }

            // 安全检查：必须在允许根目录下
            foreach (var root in AllowedRoots)
            {
                if (string.IsNullOrEmpty(root)) continue;
                try
                {
                    string normalizedRoot = Path.GetFullPath(root).TrimEnd('\\', '/') + "\\";
                    string normalizedPath = Path.GetFullPath(fullPath).TrimEnd('\\', '/') + "\\";
                    if (normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                        return fullPath;
                }
                catch { }
            }

            return null;
        }

        private class GrepMatch
        {
            public string FilePath { get; set; }
            public int LineNumber { get; set; }
            public string LineText { get; set; }
            public Dictionary<int, string> ContextBefore { get; set; } = new Dictionary<int, string>();
            public Dictionary<int, string> ContextAfter { get; set; } = new Dictionary<int, string>();
        }

        // ═══════════════════════════════════════════════════════════
        //  知识库索引搜索（原 SearchKnowledgeHandler 的核心逻辑）
        // ═══════════════════════════════════════════════════════════

        private static readonly Dictionary<string, string> SourceDirs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "api", "api" }, { "pml", "pml" }, { "pattern", "patterns" }, { "domain", "domain" }
        };

        private ToolResult SearchKnowledgeIndex(string query, string source, int limit, CancellationToken ct)
        {
            if (!Directory.Exists(KnowledgeRoot))
                return ToolResult.Fail($"Knowledge directory not found: {KnowledgeRoot}");

            EnsureKnowledgeIndexLoaded();

            var targetSources = new List<string>();
            if (source == "all")
                targetSources.AddRange(SourceDirs.Keys);
            else if (SourceDirs.ContainsKey(source))
                targetSources.Add(source);

            string queryLower = query.ToLowerInvariant();
            var matchedFiles = new HashSet<string>();

            // 1. 关键词索引匹配
            if (_keywordIndex != null)
            {
                foreach (var kvp in _keywordIndex)
                {
                    ct.ThrowIfCancellationRequested();
                    if (kvp.Key.IndexOf(queryLower, StringComparison.OrdinalIgnoreCase) >= 0
                        || queryLower.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        foreach (var file in kvp.Value)
                            if (MatchesSourceFilter(file, targetSources))
                                matchedFiles.Add(file);
                    }
                }
            }

            // 2. 文件名 + 标签匹配（补充）
            if (matchedFiles.Count < 20 && _fileIndex != null)
            {
                foreach (var kvp in _fileIndex)
                {
                    ct.ThrowIfCancellationRequested();
                    string fileName = Path.GetFileNameWithoutExtension(kvp.Key);
                    if (fileName.IndexOf(queryLower, StringComparison.OrdinalIgnoreCase) >= 0)
                        if (MatchesSourceFilter(kvp.Key, targetSources))
                            matchedFiles.Add(kvp.Key);

                    if (kvp.Value.tags != null)
                        foreach (var tag in kvp.Value.tags)
                            if (tag.IndexOf(queryLower, StringComparison.OrdinalIgnoreCase) >= 0)
                            { if (MatchesSourceFilter(kvp.Key, targetSources)) matchedFiles.Add(kvp.Key); break; }
                }
            }

            // 3. 读取匹配文件，提取摘要
            var sb = new System.Text.StringBuilder();
            int count = 0;
            foreach (var file in matchedFiles)
            {
                if (count >= limit) break;
                ct.ThrowIfCancellationRequested();

                string fullPath = Path.Combine(KnowledgeRoot, file);
                if (!File.Exists(fullPath)) continue;

                try
                {
                    string content = File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
                    string signature = ExtractKnowledgeSignature(content);
                    string example = ExtractKnowledgeExample(content);
                    string excerpt = ExtractKnowledgeExcerpt(content, query);

                    sb.AppendLine($"--- 结果 {count + 1} ---");
                    sb.AppendLine($"来源: {GetSourceLabel(file)}");
                    sb.AppendLine($"文件: {Path.GetFileName(file)}");
                    if (!string.IsNullOrEmpty(signature)) sb.AppendLine($"签名: {signature}");
                    sb.AppendLine();
                    sb.AppendLine(excerpt);
                    if (!string.IsNullOrEmpty(example))
                    {
                        sb.AppendLine();
                        sb.AppendLine("代码示例:");
                        sb.AppendLine(example);
                    }
                    sb.AppendLine();
                    count++;
                }
                catch { }
            }

            // 4. 索引匹配不足时，回退到 knowledge/ 下 .md 文件直接内容搜索
            if (count < limit)
            {
                var contentResults = SearchKnowledgeContent(query, source, limit - count, matchedFiles, ct);
                foreach (var r in contentResults)
                {
                    sb.AppendLine($"--- 结果 {count + 1} ---");
                    sb.AppendLine($"来源: {r.SourceLabel}");
                    sb.AppendLine($"文件: {r.FileName}");
                    sb.AppendLine();
                    sb.AppendLine(r.Excerpt);
                    if (!string.IsNullOrEmpty(r.Example))
                    {
                        sb.AppendLine();
                        sb.AppendLine("代码示例:");
                        sb.AppendLine(r.Example);
                    }
                    sb.AppendLine();
                    count++;
                }
            }

            if (count == 0)
                return ToolResult.Ok($"在知识库中未找到 '{query}' 的相关结果。试试换其他关键词或 source=all。", null);

            _sink?.Emit(CopilotEvent.Notice($"Knowledge search: '{query}' → {count} results"));
            return ToolResult.Ok(sb.ToString().TrimEnd(), new { query, source, count });
        }

        /// <summary>
        /// 回退搜索：直接扫描 knowledge/ 下的 .md 文件内容
        /// 当 search_index.json 关键词索引未命中足够结果时使用
        /// </summary>
        private List<KnowledgeContentResult> SearchKnowledgeContent(
            string query, string source, int maxResults, HashSet<string> alreadyMatched, CancellationToken ct)
        {
            var results = new List<KnowledgeContentResult>();
            if (!Directory.Exists(KnowledgeRoot)) return results;

            var searchDirs = new List<string>();
            if (source == "all")
            {
                foreach (var dir in SourceDirs.Values)
                    searchDirs.Add(Path.Combine(KnowledgeRoot, dir));
            }
            else if (SourceDirs.ContainsKey(source))
            {
                searchDirs.Add(Path.Combine(KnowledgeRoot, SourceDirs[source]));
            }

            foreach (var dir in searchDirs)
            {
                if (results.Count >= maxResults) break;
                if (!Directory.Exists(dir)) continue;

                try
                {
                    foreach (var file in Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories))
                    {
                        if (results.Count >= maxResults) break;
                        ct.ThrowIfCancellationRequested();

                        // 跳过已通过索引匹配的文件
                        string relPath = GetRelativePath(KnowledgeRoot, file);
                        if (alreadyMatched.Contains(relPath)) continue;

                        try
                        {
                            string content = File.ReadAllText(file, System.Text.Encoding.UTF8);
                            if (content.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                                continue;

                            results.Add(new KnowledgeContentResult
                            {
                                FileName = Path.GetFileName(file),
                                SourceLabel = GetSourceLabel(relPath),
                                Excerpt = ExtractKnowledgeExcerpt(content, query),
                                Example = ExtractKnowledgeExample(content)
                            });
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return results;
        }

        private static string GetRelativePath(string root, string fullPath)
        {
            string normalizedRoot = root.TrimEnd('\\', '/');
            string normalizedFile = fullPath.Replace('/', '\\');
            if (normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return normalizedFile.Substring(normalizedRoot.Length).TrimStart('\\');
            return Path.GetFileName(fullPath);
        }

        private void EnsureKnowledgeIndexLoaded()
        {
            if (_keywordIndex != null && _fileIndex != null) return;
            lock (_indexLock)
            {
                if (_keywordIndex != null && _fileIndex != null) return;

                string indexPath = Path.Combine(KnowledgeRoot, "search_index.json");
                if (!File.Exists(indexPath))
                {
                    _keywordIndex = new Dictionary<string, List<string>>();
                    _fileIndex = new Dictionary<string, KnowledgeFileMeta>();
                    return;
                }
                try
                {
                    string json = File.ReadAllText(indexPath, System.Text.Encoding.UTF8);
                    var index = JObject.Parse(json);

                    _keywordIndex = new Dictionary<string, List<string>>();
                    var kwObj = index["keywords"] as JObject;
                    if (kwObj != null)
                        foreach (var prop in kwObj.Properties())
                            _keywordIndex[prop.Name.ToLowerInvariant()] = prop.Value.ToObject<List<string>>();

                    _fileIndex = new Dictionary<string, KnowledgeFileMeta>();
                    var fileObj = index["files"] as JObject;
                    if (fileObj != null)
                        foreach (var prop in fileObj.Properties())
                            _fileIndex[prop.Name] = prop.Value.ToObject<KnowledgeFileMeta>();
                }
                catch
                {
                    _keywordIndex = new Dictionary<string, List<string>>();
                    _fileIndex = new Dictionary<string, KnowledgeFileMeta>();
                }
            }
        }

        private bool MatchesSourceFilter(string file, List<string> targets)
        {
            if (targets.Count == 0 || targets.Contains("all")) return true;
            string fileLower = file.ToLowerInvariant();
            foreach (var t in targets)
                if (fileLower.Contains(t)) return true;
            return false;
        }

        private string GetSourceLabel(string file)
        {
            string f = file.ToLowerInvariant();
            if (f.Contains("api")) return "C# API";
            if (f.Contains("pml")) return "PML 语法";
            if (f.Contains("pattern")) return "黄金范式";
            if (f.Contains("domain")) return "领域术语";
            return "知识库";
        }

        private string ExtractKnowledgeSignature(string content)
        {
            var lines = content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string t = line.Trim();
                if (t.Contains("| 方法 |") || t.Contains("| Method |"))
                {
                    int idx = lines.ToList().IndexOf(line);
                    if (idx + 1 < lines.Length)
                    {
                        string next = lines[idx + 1].Trim();
                        if (next.StartsWith("|"))
                            return next.Replace("|", " ").Trim();
                    }
                }
                if (t.StartsWith("```csharp") || t.StartsWith("```pml"))
                {
                    int idx = lines.ToList().IndexOf(line);
                    if (idx + 1 < lines.Length)
                        return lines[idx + 1].Trim();
                }
            }
            return null;
        }

        private string ExtractKnowledgeExample(string content)
        {
            var lines = content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool inCode = false;
            var code = new System.Text.StringBuilder();
            foreach (var line in lines)
            {
                string t = line.Trim();
                if (t.StartsWith("```csharp") || t.StartsWith("```pml"))
                {
                    inCode = true;
                    continue;
                }
                if (inCode && t == "```")
                {
                    if (code.Length > 0) break;
                    inCode = false;
                    continue;
                }
                if (inCode)
                {
                    code.AppendLine(line);
                    if (code.Length > 500) break;
                }
            }
            return code.Length > 0 ? code.ToString().TrimEnd() : null;
        }

        private string ExtractKnowledgeExcerpt(string content, string query)
        {
            var lines = content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string t = line.Trim();
                if (t.Contains("**用途**") || t.Contains("**一句话**"))
                {
                    int idx = t.IndexOf(':');
                    if (idx > 0 && idx < t.Length - 1) return t.Substring(idx + 1).Trim();
                }
            }
            foreach (var line in lines)
            {
                string t = line.Trim();
                if (t.Length > 20 && !t.StartsWith("#") && !t.StartsWith("```") && !t.StartsWith("---"))
                {
                    string excerpt = t.Length > 200 ? t.Substring(0, 197) + "..." : t;
                    return excerpt;
                }
            }
            int queryIdx = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (queryIdx > 0)
            {
                int start = Math.Max(0, queryIdx - 80);
                int end = Math.Min(content.Length, queryIdx + query.Length + 120);
                string excerpt = content.Substring(start, end - start).Replace("\n", " ").Replace("\r", "");
                return excerpt.Length > 200 ? excerpt.Substring(0, 197) + "..." : excerpt;
            }
            return "(文档摘要)";
        }

        private class KnowledgeFileMeta
        {
            public string title { get; set; }
            public List<string> tags { get; set; }
            public string summary { get; set; }
            public bool verified { get; set; }
        }

        private class KnowledgeContentResult
        {
            public string FileName { get; set; }
            public string SourceLabel { get; set; }
            public string Excerpt { get; set; }
            public string Example { get; set; }
        }
    }
}
