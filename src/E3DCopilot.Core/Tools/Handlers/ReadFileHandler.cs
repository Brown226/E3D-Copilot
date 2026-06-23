using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Events;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// ReadFile — 读取文件内容
    /// 
    /// 元能力工具：
    /// AI 可读取本地文件，主要用于查阅 API 文档、配置文件、设计文档等。
    /// 
    /// 安全限制：
    /// - 默认只允许读取项目目录和 API 文档目录下的文件
    /// - 禁止读取 .exe, .dll, .chm 等二进制文件
    /// - 文件大小限制：默认 100KB
    /// </summary>
    public class ReadFileHandler : IToolHandler
    {
        private readonly IEventSink _sink;

        /// <summary>允许读取的根目录列表</summary>
        private static readonly string[] AllowedRoots =
        {
            // 项目根目录
            Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "..")),
            // E3D 官方 API 文档目录
            Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
                "E3D官方API文档")),
            // 当前程序目录
            AppDomain.CurrentDomain.BaseDirectory
        };

        /// <summary>最大文件大小（字节）</summary>
        private const int MaxFileSize = 100 * 1024; // 100KB

        /// <summary>禁止读取的扩展名</summary>
        private static readonly HashSet<string> BlockedExtensions = new HashSet<string>(
            new[] { ".exe", ".dll", ".chm", ".pdb", ".nupkg", ".zip", ".7z", ".rar" },
            StringComparer.OrdinalIgnoreCase);

        public ReadFileHandler(IEventSink sink = null)
        {
            _sink = sink;
        }

        public string Name => "read_file";
        public string Description => "Read the content of a file from the local filesystem. Use when: (1) you need to read API documentation, (2) you need to examine configuration files, (3) you need to read design documents or reference materials. Automatically handles Chinese text encoding. 读取本地文件内容（文本文件）。用于查阅 API 文档、配置文件、设计参考等。";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""path"": {
      ""type"": ""string"",
      ""description"": ""File path — can be absolute or relative (relative to project root). 文件路径，支持绝对路径和相对路径（相对项目根目录）""
    },
    ""offset"": {
      ""type"": ""integer"",
      ""description"": ""Line offset to start reading from (0-based). 开始读取的行号（从0开始）""
    },
    ""limit"": {
      ""type"": ""integer"",
      ""description"": ""Maximum lines to read. 最多读取的行数""
    },
    ""encoding"": {
      ""type"": ""string"",
      ""description"": ""File encoding (default: auto-detect). Supported: utf-8, gb2312, gbk, big5. 文件编码（默认自动检测）""
    }
  },
  ""required"": [""path""]
}";

        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(args);
                string path = json.Value<string>("path");

                if (string.IsNullOrWhiteSpace(path))
                    return ToolResult.Fail("File path is required");

                // 解析为绝对路径
                string fullPath = ResolvePath(path);
                if (fullPath == null)
                    return ToolResult.Fail($"Access denied: path '{path}' is not within allowed directories. 允许读取的目录范围：项目根目录及 E3D官方API文档 目录。");

                // 检查安全限制
                if (!File.Exists(fullPath))
                    return ToolResult.Fail($"File not found: {fullPath}");

                string ext = Path.GetExtension(fullPath);
                if (BlockedExtensions.Contains(ext))
                    return ToolResult.Fail($"Cannot read binary file: {ext} files are blocked. 不支持读取二进制文件。");

                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length > MaxFileSize)
                    return ToolResult.Fail($"File too large: {fileInfo.Length} bytes (max {MaxFileSize} bytes). 文件太大，请只读取需要的部分。");

                if (fileInfo.Length == 0)
                    return ToolResult.Ok("(empty file)", new { path = fullPath, size = 0 });

                // 自动检测编码读取
                string content = await ReadFileWithEncodingAsync(fullPath, json.Value<string>("encoding"), ct);

                // 截取行偏移和限制
                int offset = json.Value<int?>("offset") ?? 0;
                int limit = json.Value<int?>("limit") ?? 0;

                if (offset > 0 || limit > 0)
                {
                    var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    int start = Math.Min(offset, lines.Length);
                    int count = limit > 0 ? Math.Min(limit, lines.Length - start) : lines.Length - start;
                    content = string.Join("\n", lines, start, count);
                }

                _sink?.Emit(CopilotEvent.Notice($"Read file: {fullPath} ({content.Length} chars)"));

                return ToolResult.Ok(content, new
                {
                    path = fullPath,
                    size = content.Length,
                    lines = content.Split(new[] { '\n' }).Length
                });
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                return ToolResult.Fail($"Invalid JSON arguments: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"ReadFile failed: {ex.Message}");
            }
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
                // 相对路径：尝试在允许根目录下解析
                fullPath = null;
                foreach (var root in AllowedRoots)
                {
                    if (string.IsNullOrEmpty(root)) continue;
                    try
                    {
                        string candidate = Path.GetFullPath(Path.Combine(root, path));
                        if (File.Exists(candidate))
                        {
                            fullPath = candidate;
                            break;
                        }
                    }
                    catch { }
                }

                if (fullPath == null)
                    return null;
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

            return null; // 不在允许范围内
        }

        /// <summary>
        /// 读取文件（自动检测编码，支持中文）
        /// net48 没有 File.ReadAllTextAsync，使用同步读取 + Task.Run 包装
        /// </summary>
        private static async Task<string> ReadFileWithEncodingAsync(string path, string encodingHint, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                // 检测 BOM
                byte[] header = new byte[4];
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096))
                {
                    int read = fs.Read(header, 0, 4);
                    if (read < 2)
                        return File.ReadAllText(path, System.Text.Encoding.UTF8);
                }

                System.Text.Encoding encoding;

                // 根据 BOM 检测
                if (header[0] == 0xFF && header[1] == 0xFE)
                    encoding = System.Text.Encoding.Unicode; // UTF-16 LE
                else if (header[0] == 0xFE && header[1] == 0xFF)
                    encoding = System.Text.Encoding.BigEndianUnicode; // UTF-16 BE
                else if (header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF)
                    encoding = System.Text.Encoding.UTF8; // UTF-8 BOM
                else if (!string.IsNullOrEmpty(encodingHint))
                {
                    // 用户指定编码
                    string hint = encodingHint.ToLowerInvariant().Replace("-", "");
                    switch (hint)
                    {
                        case "gb2312":
                        case "gbk":
                            encoding = System.Text.Encoding.GetEncoding("GB2312");
                            break;
                        case "big5":
                            encoding = System.Text.Encoding.GetEncoding("BIG5");
                            break;
                        case "utf8":
                        case "utf-8":
                            encoding = System.Text.Encoding.UTF8;
                            break;
                        default:
                            encoding = System.Text.Encoding.UTF8;
                            break;
                    }
                }
                else
                {
                    // 默认 UTF-8
                    encoding = System.Text.Encoding.UTF8;
                }

                return File.ReadAllText(path, encoding);
            }, ct);
        }
    }
}
