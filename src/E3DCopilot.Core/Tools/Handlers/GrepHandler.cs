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
    /// Grep — 在文件内容中搜索文本/正则模式
    ///
    /// 元能力工具：
    /// AI 可在项目文件中搜索 PML 宏引用、配置模式、API 用法等。
    /// 参考 Reasonix builtin/grep.go 的设计，适配 .NET Framework 4.8。
    ///
    /// 安全限制：
    /// - 复用 ReadFileHandler 的 AllowedRoots 安全模型
    /// - 禁止搜索二进制文件
    /// - 结果数上限防止输出爆炸
    /// </summary>
    public class GrepHandler : IToolHandler
    {
        private readonly IEventSink _sink;

        /// <summary>允许搜索的根目录列表</summary>
        private static readonly string[] AllowedRoots = ResolveAllowedRoots();

        /// <summary>最大文件大小（字节）— 跳过超大文件</summary>
        private const int MaxFileSize = 512 * 1024; // 512KB

        /// <summary>最大匹配数</summary>
        private const int MaxMatches = 200;

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

            // 开发环境回退：项目根目录
            var devRoot = Path.GetFullPath(Path.Combine(appDir, "..", "..", "..", "..", "..", ".."));
            if (Directory.Exists(devRoot)) roots.Add(devRoot);

            // 开发环境回退：E3D 官方 API 文档目录
            var devDocs = Path.GetFullPath(Path.Combine(appDir, "..", "..", "..", "..", "..", "E3D官方API文档"));
            if (Directory.Exists(devDocs)) roots.Add(devDocs);

            // PML 语法与项目合集目录
            var pmlDir = Path.GetFullPath(Path.Combine(appDir, "..", "..", "..", "..", "..", "PML语法与项目合集"));
            if (Directory.Exists(pmlDir)) roots.Add(pmlDir);

            return roots.ToArray();
        }

        public GrepHandler(IEventSink sink = null)
        {
            _sink = sink;
        }

        public string Name => "grep";

        public string Description =>
            "Search file contents using text or regex pattern. Use when: (1) finding PML macro references, " +
            "(2) locating API usage patterns, (3) searching configuration files, (4) finding specific text across project files. " +
            "在项目文件中搜索文本或正则表达式。适合查找 PML 宏引用、API 用法、配置模式等。";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""pattern"": {
      ""type"": ""string"",
      ""description"": ""Search pattern — plain text or regex. 搜索模式，支持纯文本和正则表达式。例: 'coll all PIPE', 'DbElement\.Get\w+'""
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

                        sb.AppendLine($"  L{m.LineNumber}: {m.LineText.TrimEnd()}");

                        // 上下文行
                        foreach (var ctx in m.ContextBefore)
                            sb.Insert(sb.Length - m.LineText.TrimEnd().Length - $"  L{m.LineNumber}: ".Length - Environment.NewLine.Length,
                                $"  L{ctx.Key}: {ctx.Value.TrimEnd()}\n");

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
    }
}
