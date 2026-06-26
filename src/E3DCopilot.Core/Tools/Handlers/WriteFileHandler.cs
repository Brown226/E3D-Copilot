using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Events;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// WriteFile — 写入文件内容（创建或覆盖）
    /// 
    /// 对齐 Reasonix builtin/writefile.go：
    ///   - 写入前创建父目录
    ///   - 路径限制在允许根目录内（安全约束）
    ///   - 返回写入字节数
    /// 
    /// 安全限制：
    ///   - 默认只允许写入项目目录
    ///   - 禁止覆盖 .exe, .dll 等二进制文件
    /// </summary>
    public class WriteFileHandler : IToolHandler
    {
        private readonly IEventSink _sink;

        private static readonly string[] AllowedRoots = ResolveAllowedRoots();

        private static string[] ResolveAllowedRoots()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var roots = new System.Collections.Generic.List<string> { appDir };

            // 开发环境回退：项目根目录
            var devRoot = Path.GetFullPath(Path.Combine(appDir, "..", "..", "..", "..", "..", ".."));
            if (Directory.Exists(devRoot)) roots.Add(devRoot);

            // 用户临时目录（导出文件用）
            roots.Add(Path.GetTempPath());

            return roots.ToArray();
        }

        private static readonly System.Collections.Generic.HashSet<string> BlockedExtensions = 
            new System.Collections.Generic.HashSet<string>(
                new[] { ".exe", ".dll", ".pdb", ".nupkg", ".sys", ".drv" },
                StringComparer.OrdinalIgnoreCase);

        public WriteFileHandler(IEventSink sink = null)
        {
            _sink = sink;
        }

        public string Name => "write_file";
        public string Description => "Write content to a file at the given path (overwriting existing content). " +
            "Creates parent directories as needed. Use to: (1) save generated PML macros to a file, " +
            "(2) export reports or data, (3) save configuration. " +
            "写入文件内容，自动创建父目录。用于保存生成的PML宏、导出报表、保存配置。";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""path"": {
      ""type"": ""string"",
      ""description"": ""File path — absolute or relative to project root. 文件路径""
    },
    ""content"": {
      ""type"": ""string"",
      ""description"": ""Full content to write. 要写入的完整内容""
    }
  },
  ""required"": [""path"", ""content""]
}";

        public bool IsReadOnly => false;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(args);
                string path = json.Value<string>("path");
                string content = json.Value<string>("content");

                if (string.IsNullOrWhiteSpace(path))
                    return ToolResult.Fail("File path is required");
                if (content == null)
                    return ToolResult.Fail("Content is required (use empty string for empty file)");

                // 解析为绝对路径
                string fullPath = ResolvePath(path);
                if (fullPath == null)
                    return ToolResult.Fail($"Access denied: path '{path}' is not within allowed directories.");

                // 安全：禁止覆盖二进制文件
                string ext = Path.GetExtension(fullPath);
                if (BlockedExtensions.Contains(ext))
                    return ToolResult.Fail($"Cannot write to binary file type: {ext}");

                // 写入前检查：相同内容跳过（自动检测编码，避免误判）
                if (File.Exists(fullPath))
                {
                    string existing = await Task.Run(() =>
                    {
                        // 检测 BOM 判断编码
                        byte[] header = new byte[3];
                        using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096))
                        {
                            fs.Read(header, 0, Math.Min(3, (int)fs.Length));
                        }
                        var enc = (header[0] == 0xFF && header[1] == 0xFE) ? System.Text.Encoding.Unicode
                                 : (header[0] == 0xFE && header[1] == 0xFF) ? System.Text.Encoding.BigEndianUnicode
                                 : System.Text.Encoding.UTF8;
                        return File.ReadAllText(fullPath, enc);
                    }, ct);
                    if (existing == content)
                        return ToolResult.Ok($"{fullPath} already contains the exact content; no changes made",
                            new { path = fullPath, bytes = content.Length, changed = false });
                }

                // 创建父目录
                string dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // 写入文件（UTF-8 无 BOM）
                await Task.Run(() => File.WriteAllText(fullPath, content, new System.Text.UTF8Encoding(false)), ct);

                _sink?.Emit(CopilotEvent.Notice($"Wrote file: {fullPath} ({content.Length} bytes)"));

                return ToolResult.Ok($"wrote {content.Length} bytes to {fullPath}",
                    new { path = fullPath, bytes = content.Length, changed = true });
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                return ToolResult.Fail($"Invalid JSON arguments: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                return ToolResult.Fail($"Permission denied: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"WriteFile failed: {ex.Message}");
            }
        }

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
                        fullPath = candidate;
                        break;
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

            // 如果在临时目录下也允许
            try
            {
                string tmpRoot = Path.GetTempPath().TrimEnd('\\', '/') + "\\";
                string normPath = Path.GetFullPath(fullPath).TrimEnd('\\', '/') + "\\";
                if (normPath.StartsWith(tmpRoot, StringComparison.OrdinalIgnoreCase))
                    return fullPath;
            }
            catch { }

            return null;
        }
    }
}
