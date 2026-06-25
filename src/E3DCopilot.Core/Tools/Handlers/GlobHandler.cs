using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Events;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// Glob — 按文件名模式查找文件
    ///
    /// 元能力工具：
    /// AI 可按通配符模式查找文件，如 **/*.pml、*.cs 等。
    /// 参考 Reasonix builtin/glob.go，适配 .NET Framework 4.8。
    ///
    /// 安全限制：
    /// - 复用 AllowedRoots 安全模型
    /// - 结果数上限防止输出爆炸
    /// </summary>
    public class GlobHandler : IToolHandler
    {
        private readonly IEventSink _sink;

        /// <summary>允许搜索的根目录列表</summary>
        private static readonly string[] AllowedRoots = ResolveAllowedRoots();

        /// <summary>最大返回文件数</summary>
        private const int MaxResults = 200;

        /// <summary>最大递归深度（防止循环符号链接）</summary>
        private const int MaxDepth = 10;

        /// <summary>跳过的大型/生成目录</summary>
        private static readonly HashSet<string> SkipDirs = new HashSet<string>(
            new[]
            {
                "node_modules", ".git", "bin", "obj", "packages",
                ".vs", "Debug", "Release", "x86", "x64",
                "dist", "build", ".next", ".cache", "__pycache__"
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

        public GlobHandler(IEventSink sink = null)
        {
            _sink = sink;
        }

        public string Name => "glob";

        public string Description =>
            "Find files by name pattern (glob). Use when: (1) locating PML macro files (*.pmlmac), " +
            "(2) finding all source files of a type (*.cs, *.ts), (3) discovering project structure. " +
            "按通配符模式查找文件。支持 ** 递归匹配，如 **/*.pml、**/config*.json。";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""pattern"": {
      ""type"": ""string"",
      ""description"": ""File glob pattern — supports *, ?, **. 文件通配符模式。例: '*.pml', '**/*.cs', 'config*.json'""
    },
    ""path"": {
      ""type"": ""string"",
      ""description"": ""Root directory to search in (default: project root). 搜索根目录，默认项目根""
    },
    ""max_results"": {
      ""type"": ""integer"",
      ""description"": ""Maximum files to return (default: 50, max: 200). 最大返回文件数""
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
                    int maxResults = json.Value<int?>("max_results") ?? 50;
                    maxResults = Math.Max(1, Math.Min(maxResults, MaxResults));

                    // 解析搜索根目录
                    string[] searchRoots;
                    if (!string.IsNullOrWhiteSpace(searchPath))
                    {
                        string fullPath = ResolvePath(searchPath);
                        if (fullPath == null)
                            return ToolResult.Fail($"Access denied or not found: {searchPath}");
                        if (!Directory.Exists(fullPath))
                            return ToolResult.Fail($"Not a directory: {fullPath}");
                        searchRoots = new[] { fullPath };
                    }
                    else
                    {
                        searchRoots = AllowedRoots;
                    }

                    // 判断是否有 ** 递归模式
                    bool recursive = pattern.Contains("**");

                    // 分离目录部分和文件名部分
                    // 例: **/*.cs → dirPattern = "**", filePattern = "*.cs"
                    // 例: src/**/*.pml → dirPattern = "src/**", filePattern = "*.pml"
                    string filePattern;
                    string dirPattern;
                    int lastSep = Math.Max(pattern.LastIndexOf('/'), pattern.LastIndexOf('\\'));
                    if (lastSep >= 0)
                    {
                        dirPattern = pattern.Substring(0, lastSep);
                        filePattern = pattern.Substring(lastSep + 1);
                    }
                    else
                    {
                        dirPattern = recursive ? "**" : "";
                        filePattern = pattern;
                    }

                    // 执行搜索
                    var results = new List<GlobResult>();
                    foreach (var root in searchRoots)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (!Directory.Exists(root)) continue;

                        if (recursive)
                        {
                            // 递归搜索：遍历所有子目录
                            SearchRecursive(root, filePattern, dirPattern, maxResults, results, 0, ct);
                        }
                        else
                        {
                            // 单层搜索
                            try
                            {
                                var files = Directory.GetFiles(root, filePattern, SearchOption.TopDirectoryOnly);
                                foreach (var f in files)
                                {
                                    if (results.Count >= maxResults) break;
                                    results.Add(MakeResult(f, root));
                                }
                            }
                            catch { }
                        }

                        if (results.Count >= maxResults) break;
                    }

                    if (results.Count == 0)
                    {
                        return ToolResult.Ok(
                            $"No files found matching '{pattern}'. 尝试换其他模式或放宽搜索范围。",
                            new { pattern, count = 0 });
                    }

                    // 格式化输出
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"Found {results.Count} files matching '{pattern}':");
                    sb.AppendLine();
                    foreach (var r in results)
                    {
                        ct.ThrowIfCancellationRequested();
                        string sizeStr = r.SizeBytes < 1024 ? $"{r.SizeBytes}B"
                            : r.SizeBytes < 1024 * 1024 ? $"{r.SizeBytes / 1024}KB"
                            : $"{r.SizeBytes / (1024 * 1024)}MB";
                        sb.AppendLine($"  {r.RelativePath}  ({sizeStr})");
                    }

                    string summary = $"Glob: '{pattern}' → {results.Count} files";
                    _sink?.Emit(CopilotEvent.Notice(summary));

                    return ToolResult.Ok(sb.ToString().TrimEnd(), new
                    {
                        pattern,
                        count = results.Count,
                        files = results.Select(r => new { r.RelativePath, r.SizeBytes }).ToList()
                    });
                }
                catch (Newtonsoft.Json.JsonException ex)
                {
                    return ToolResult.Fail($"Invalid JSON arguments: {ex.Message}");
                }
                catch (OperationCanceledException)
                {
                    return ToolResult.Fail("Search cancelled");
                }
                catch (Exception ex)
                {
                    return ToolResult.Fail($"Glob failed: {ex.Message}");
                }
            }, ct);
        }

        private void SearchRecursive(string dir, string filePattern, string dirPattern,
            int maxResults, List<GlobResult> results, int depth, CancellationToken ct)
        {
            if (depth > MaxDepth || results.Count >= maxResults) return;

            try
            {
                // 在当前目录搜索文件
                var files = Directory.GetFiles(dir, filePattern, SearchOption.TopDirectoryOnly);
                foreach (var f in files)
                {
                    ct.ThrowIfCancellationRequested();
                    if (results.Count >= maxResults) return;
                    results.Add(MakeResult(f, AllowedRoots));
                }

                // 递归子目录
                foreach (var subDir in Directory.GetDirectories(dir))
                {
                    ct.ThrowIfCancellationRequested();
                    if (results.Count >= maxResults) return;

                    string dirName = Path.GetFileName(subDir);
                    if (SkipDirs.Contains(dirName)) continue;
                    if (dirName.StartsWith(".")) continue; // 跳过隐藏目录

                    SearchRecursive(subDir, filePattern, dirPattern, maxResults, results, depth + 1, ct);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
        }

        private GlobResult MakeResult(string filePath, string rootDir)
        {
            string relativePath;
            try
            {
                string normalizedRoot = Path.GetFullPath(rootDir).TrimEnd('\\', '/');
                string normalizedFile = Path.GetFullPath(filePath);
                if (normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                    relativePath = normalizedFile.Substring(normalizedRoot.Length).TrimStart('\\', '/');
                else
                    relativePath = filePath;
            }
            catch
            {
                relativePath = filePath;
            }

            long size = 0;
            try { size = new FileInfo(filePath).Length; } catch { }

            return new GlobResult
            {
                FullPath = filePath,
                RelativePath = relativePath,
                SizeBytes = size
            };
        }

        private GlobResult MakeResult(string filePath, string[] roots)
        {
            // 尝试找到最佳根目录来计算相对路径
            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root)) continue;
                try
                {
                    string normalizedRoot = Path.GetFullPath(root).TrimEnd('\\', '/');
                    string normalizedFile = Path.GetFullPath(filePath);
                    if (normalizedFile.StartsWith(normalizedRoot + "\\", StringComparison.OrdinalIgnoreCase))
                    {
                        return new GlobResult
                        {
                            FullPath = filePath,
                            RelativePath = normalizedFile.Substring(normalizedRoot.Length + 1),
                            SizeBytes = new FileInfo(filePath).Length
                        };
                    }
                }
                catch { }
            }

            return new GlobResult
            {
                FullPath = filePath,
                RelativePath = Path.GetFileName(filePath),
                SizeBytes = 0
            };
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
                        if (Directory.Exists(candidate))
                        {
                            fullPath = candidate;
                            break;
                        }
                    }
                    catch { }
                }

                if (fullPath == null) return null;
            }

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

        private class GlobResult
        {
            public string FullPath { get; set; }
            public string RelativePath { get; set; }
            public long SizeBytes { get; set; }
        }
    }
}
