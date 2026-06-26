using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;
using E3DCopilot.Core.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CNPE.ISO.E3D;
using CNPE.ISO.E3D.Core;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// ISO等轴测图出图工具 - 封装CNPE.IC.ISO的核心功能
    /// 通过 ISODRAFT 模块生成管道 ISO 图，与结构出图完全分离。
    /// 核心流程：PipeReader 读取管道数据 → IsoItem 生成 TRAN/DXF → CadProxy 在 AutoCAD 中格式化输出 DWG
    /// </summary>
    public class IsoDrawingHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public IsoDrawingHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public string Name => "generate_iso_drawing";
        public string Description => "从E3D提取管道数据并通过ISODRAFT模块生成ISO等轴测图。支持单管道生成、批量生成、状态查询。生成的DWG文件包含材料表、标注和消隐。";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""action"": {
      ""type"": ""string"",
      ""enum"": [""generate"", ""batch_generate"", ""query_status""],
      ""description"": ""操作类型：generate-生成单个管道ISO图，batch_generate-批量生成，query_status-查询生成状态""
    },
    ""pipe_name"": {
      ""type"": ""string"",
      ""description"": ""管道名称，如 /PIPE-1001 或 PIPE-1001""
    },
    ""pipe_names"": {
      ""type"": ""array"",
      ""items"": { ""type"": ""string"" },
      ""description"": ""批量处理时的管道名称列表""
    },
    ""project_id"": {
      ""type"": ""string"",
      ""enum"": [""1907"", ""1916"", ""2016"", ""2026""],
      ""description"": ""项目编号，不同项目有不同的材料规格和编码规则""
    },
    ""output_dir"": {
      ""type"": ""string"",
      ""description"": ""输出目录路径，默认从配置读取""
    },
    ""cad_exe_path"": {
      ""type"": ""string"",
      ""description"": ""AutoCAD可执行文件路径，默认从配置读取或自动检测""
    },
    ""include_material_list"": {
      ""type"": ""boolean"",
      ""description"": ""是否包含材料清单，默认为 true（由ISO模板控制）""
    },
    ""template_type"": {
      ""type"": ""string"",
      ""enum"": [""standard"", ""detailed"", ""simplified""],
      ""description"": ""模板类型（预留参数，当前由ISODRAFT模板文件控制）""
    },
    ""open_in_cad"": {
      ""type"": ""boolean"",
      ""description"": ""生成完成后是否自动用AutoCAD打开DWG文件，默认为 false""
    }
  },
  ""required"": [""action""]
}";
        public bool IsReadOnly => false;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                string action = json["action"]?.ToString();

                if (string.IsNullOrEmpty(action))
                    return ToolResult.Fail("缺少 action 参数");

                switch (action.ToLower())
                {
                    case "generate":
                        return await GenerateSingleIso(json, ct);
                    case "batch_generate":
                        return await BatchGenerateIso(json, ct);
                    case "query_status":
                        return await QueryGenerationStatus(json, ct);
                    default:
                        return ToolResult.Fail($"不支持的操作: {action}");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"ISO出图工具执行失败: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  generate — 单管道 ISO 生成
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 生成单个管道的ISO图
        /// </summary>
        private async Task<ToolResult> GenerateSingleIso(JObject json, CancellationToken ct)
        {
            string pipeName = json["pipe_name"]?.ToString();
            if (string.IsNullOrEmpty(pipeName))
                return ToolResult.Fail("生成单个ISO图时需要指定 pipe_name 参数");

            ct.ThrowIfCancellationRequested();

            // 从配置中读取默认值
            var config = CopilotConfig.Load();

            string projectId = json["project_id"]?.ToString() ?? config.Iso.DefaultProjectId;
            string outputDir = json["output_dir"]?.ToString() ?? config.Iso.DefaultOutputDir;
            string cadExePath = json["cad_exe_path"]?.ToString() ?? config.Iso.AutoCadPath;
            bool openInCad = json["open_in_cad"]?.Value<bool>() ?? false;

            // 如果配置中没有AutoCAD路径，尝试自动检测
            if (string.IsNullOrEmpty(cadExePath))
            {
                cadExePath = DetectAutoCadPathFromRegistry();
                if (!string.IsNullOrEmpty(cadExePath))
                {
                    config.Iso.AutoCadPath = cadExePath;
                    config.Save();
                }
            }

            if (string.IsNullOrEmpty(cadExePath))
                return ToolResult.Fail("未找到AutoCAD路径，请在配置中设置 cad_exe_path 或确保AutoCAD已安装");

            if (string.IsNullOrEmpty(outputDir))
                return ToolResult.Fail("未设置输出目录，请在配置中设置 output_dir 或在参数中指定");

            // 确保输出目录存在
            try
            {
                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"无法创建输出目录: {ex.Message}");
            }

            // 验证管道是否存在
            if (!ValidatePipeExists(pipeName))
                return ToolResult.Fail($"管道 {pipeName} 不存在或无法访问");

            // 调用 CNPE.ISO.E3D.Draw 生成 ISO 图
            var sw = Stopwatch.StartNew();
            var result = await GenerateIsoUsingDraw(
                new List<string> { pipeName },
                new List<string>(),
                outputDir,
                cadExePath);

            sw.Stop();

            if (result.Success)
            {
                // 可选：自动打开 AutoCAD
                if (openInCad)
                {
                    OpenInAutoCad(cadExePath, outputDir);
                }

                var meta = new JObject
                {
                    ["tool"] = "generate_iso_drawing",
                    ["action"] = "generate",
                    ["pipe_name"] = pipeName,
                    ["project_id"] = projectId,
                    ["output_dir"] = outputDir,
                    ["generation_time"] = sw.Elapsed.TotalSeconds,
                    ["message"] = result.Message
                };

                return ToolResult.Ok(
                    $"ISO图纸生成成功\n" +
                    $"管道: {pipeName}\n" +
                    $"项目: {projectId}\n" +
                    $"输出目录: {outputDir}\n" +
                    $"生成时间: {sw.Elapsed.TotalSeconds:F1}秒\n" +
                    $"详细信息: {result.Message}",
                    meta);
            }
            else
            {
                return ToolResult.Fail($"ISO图纸生成失败: {result.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  batch_generate — 批量生成
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 批量生成ISO图
        /// </summary>
        private async Task<ToolResult> BatchGenerateIso(JObject json, CancellationToken ct)
        {
            var pipeNames = json["pipe_names"] as JArray;
            if (pipeNames == null || pipeNames.Count == 0)
                return ToolResult.Fail("批量生成时需要指定 pipe_names 参数");

            ct.ThrowIfCancellationRequested();

            var config = CopilotConfig.Load();

            string projectId = json["project_id"]?.ToString() ?? config.Iso.DefaultProjectId;
            string outputDir = json["output_dir"]?.ToString() ?? config.Iso.DefaultOutputDir;
            string cadExePath = json["cad_exe_path"]?.ToString() ?? config.Iso.AutoCadPath;
            bool openInCad = json["open_in_cad"]?.Value<bool>() ?? false;

            if (string.IsNullOrEmpty(cadExePath))
            {
                cadExePath = DetectAutoCadPathFromRegistry();
                if (!string.IsNullOrEmpty(cadExePath))
                {
                    config.Iso.AutoCadPath = cadExePath;
                    config.Save();
                }
            }

            if (string.IsNullOrEmpty(cadExePath))
                return ToolResult.Fail("未找到AutoCAD路径，请在配置中设置 cad_exe_path 或确保AutoCAD已安装");

            if (string.IsNullOrEmpty(outputDir))
                return ToolResult.Fail("未设置输出目录，请在配置中设置 output_dir 或在参数中指定");

            // 确保输出目录存在
            try
            {
                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"无法创建输出目录: {ex.Message}");
            }

            // 收集所有管道名称
            var allPipes = pipeNames.Select(p => p.ToString()).ToList();

            // 预验证管道是否存在
            var validPipes = new List<string>();
            var invalidPipes = new List<string>();
            foreach (var pipeName in allPipes)
            {
                if (ValidatePipeExists(pipeName))
                    validPipes.Add(pipeName);
                else
                    invalidPipes.Add(pipeName);
            }

            if (validPipes.Count == 0)
                return ToolResult.Fail($"所有管道均不存在或无法访问: {string.Join(", ", invalidPipes)}");

            // 调用 Draw.Instance.Detail() 一次性处理所有有效管道
            // Draw 内部会创建一个 CadProxy（启动一次 AutoCAD），然后逐个处理管道
            var sw = Stopwatch.StartNew();
            var result = await GenerateIsoUsingDraw(
                validPipes,
                new List<string>(),
                outputDir,
                cadExePath);

            sw.Stop();

            // 扫描输出目录获取生成的文件
            var generatedFiles = ScanOutputFiles(outputDir);

            // 可选：自动打开 AutoCAD
            if (openInCad && generatedFiles.Count > 0)
            {
                OpenInAutoCad(cadExePath, outputDir);
            }

            int successCount = generatedFiles.Count;
            int failCount = validPipes.Count - successCount;
            if (failCount < 0) failCount = 0; // 防御性：可能一个管道生成多个文件

            var summary = $"批量ISO出图完成\n" +
                         $"总计: {allPipes.Count} 个管道\n" +
                         $"有效管道: {validPipes.Count} 个\n" +
                         $"无效管道: {invalidPipes.Count} 个\n" +
                         $"生成DWG文件: {successCount} 个\n" +
                         $"输出目录: {outputDir}\n" +
                         $"总耗时: {sw.Elapsed.TotalSeconds:F1}秒\n" +
                         $"Draw返回消息: {result.Message}";

            if (invalidPipes.Count > 0)
            {
                summary += $"\n不存在的管道: {string.Join(", ", invalidPipes)}";
            }

            if (generatedFiles.Count > 0)
            {
                summary += "\n生成的文件:\n";
                foreach (var f in generatedFiles.Take(10))
                    summary += $"  - {f.Name} ({f.LastWriteTime:yyyy-MM-dd HH:mm})\n";
                if (generatedFiles.Count > 10)
                    summary += $"  ... 还有 {generatedFiles.Count - 10} 个文件\n";
            }

            var meta = new JObject
            {
                ["tool"] = "generate_iso_drawing",
                ["action"] = "batch_generate",
                ["total"] = allPipes.Count,
                ["valid"] = validPipes.Count,
                ["invalid"] = invalidPipes.Count,
                ["generated_files"] = generatedFiles.Count,
                ["output_dir"] = outputDir,
                ["generation_time"] = sw.Elapsed.TotalSeconds,
                ["draw_message"] = result.Message,
                ["invalid_pipes"] = new JArray(invalidPipes),
                ["recent_files"] = JArray.FromObject(generatedFiles.Take(20).Select(f => new
                {
                    file_name = f.Name,
                    full_path = f.FullName,
                    size = f.Length,
                    last_modified = f.LastWriteTime
                }))
            };

            return ToolResult.Ok(summary, meta);
        }

        // ═══════════════════════════════════════════════════════════
        //  query_status — 查询生成状态
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 查询生成状态
        /// </summary>
        private async Task<ToolResult> QueryGenerationStatus(JObject json, CancellationToken ct)
        {
            string outputDir = json["output_dir"]?.ToString() ?? CopilotConfig.Load().Iso.DefaultOutputDir;

            if (string.IsNullOrEmpty(outputDir))
            {
                return ToolResult.Fail("未设置输出目录，请在配置中设置 output_dir 或在参数中指定");
            }

            if (!Directory.Exists(outputDir))
            {
                return ToolResult.Ok($"输出目录不存在: {outputDir}", new JObject
                {
                    ["tool"] = "generate_iso_drawing",
                    ["action"] = "query_status",
                    ["output_dir"] = outputDir,
                    ["exists"] = false
                });
            }

            var files = Directory.GetFiles(outputDir, "*.dwg", SearchOption.AllDirectories)
                               .Concat(Directory.GetFiles(outputDir, "*.dxf", SearchOption.AllDirectories))
                               .ToArray();

            var recentFiles = files.Select(f => new FileInfo(f))
                                  .OrderByDescending(fi => fi.LastWriteTime)
                                  .Take(10)
                                  .Select(fi => new
                                  {
                                      FileName = fi.Name,
                                      FullPath = fi.FullName,
                                      Size = fi.Length,
                                      LastModified = fi.LastWriteTime
                                  })
                                  .ToArray();

            var summary = $"ISO出图目录状态\n" +
                         $"目录: {outputDir}\n" +
                         $"图纸文件总数: {files.Length}\n" +
                         $"最近生成的文件:\n" +
                         string.Join("\n", recentFiles.Take(5).Select(f => $"  - {f.FileName} ({f.LastModified:yyyy-MM-dd HH:mm})"));

            var meta = new JObject
            {
                ["tool"] = "generate_iso_drawing",
                ["action"] = "query_status",
                ["output_dir"] = outputDir,
                ["exists"] = true,
                ["total_files"] = files.Length,
                ["recent_files"] = JArray.FromObject(recentFiles)
            };

            return ToolResult.Ok(summary, meta);
        }

        // ═══════════════════════════════════════════════════════════
        //  核心生成逻辑 — 调用 CNPE.ISO.E3D.Draw
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 调用 CNPE.ISO.E3D.Draw.Instance.Detail() 生成 ISO 图纸
        /// 
        /// Draw.Detail() 完整流程：
        /// 1. 创建 CadProxy（启动 AutoCAD，通过 WCF 命名管道连接）
        /// 2. 对每个管道：
        ///    a. IsoItem.SetItemModel() — 读取 E3D 数据（PipeReader, BranReader, ConnReader 等）
        ///    b. IsoItem.OutputTran() — 通过 ISODRAFT PML 命令生成 TRAN 文件
        ///    c. IsoItem.ResetTran() — 修改 TRAN 文件（SKEY 编号、ATTA 点处理等）
        ///    d. IsoItem.ConvertTranToDxfs() — 将 TRAN 转换为 DXF
        ///    e. CadProxy.FormatDxf() — 在 AutoCAD 中格式化 DXF 并保存为 DWG
        /// </summary>
        private async Task<IsoGenerationResult> GenerateIsoUsingDraw(
            List<string> pipes, List<string> brans,
            string outputDir, string cadExePath)
        {
            try
            {
                string outputMes = null;

                // Draw.Detail() 是同步阻塞调用，在后台线程执行
                // 不传递 CancellationToken，因为 Draw 内部无法响应取消
                // （启动 AutoCAD + ISODRAFT PML 执行是不可中断的）
                await Task.Run(() =>
                {
                    Draw.Instance.Detail(pipes, brans, outputDir, cadExePath, out outputMes);
                });

                bool success = outputMes != null && outputMes.StartsWith("出图成功");

                return new IsoGenerationResult
                {
                    Success = success,
                    Message = outputMes ?? "Draw.Detail 未返回消息"
                };
            }
            catch (Exception ex)
            {
                return new IsoGenerationResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  辅助方法
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 验证管道是否存在
        /// 通过 PipeReader 构造函数验证元素是否为 PIPE 或 BRAN 类型
        /// </summary>
        private bool ValidatePipeExists(string pipeName)
        {
            try
            {
                // PipeReader 构造函数会调用 DbElement.GetElement() 并验证元素类型
                // 如果元素不存在或不是 PIPE/BRAN，会抛出异常
                var pipeReader = new PipeReader(pipeName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 扫描输出目录中的 DWG 文件
        /// </summary>
        private List<FileInfo> ScanOutputFiles(string outputDir)
        {
            var files = new List<FileInfo>();
            try
            {
                if (Directory.Exists(outputDir))
                {
                    files = Directory.GetFiles(outputDir, "*.dwg", SearchOption.TopDirectoryOnly)
                                     .Select(f => new FileInfo(f))
                                     .OrderByDescending(f => f.LastWriteTime)
                                     .ToList();
                }
            }
            catch
            {
                // 忽略扫描错误
            }
            return files;
        }

        /// <summary>
        /// 用 AutoCAD 打开输出目录中最近的 DWG 文件
        /// </summary>
        private void OpenInAutoCad(string cadExePath, string outputDir)
        {
            try
            {
                var files = ScanOutputFiles(outputDir);
                if (files.Count > 0)
                {
                    // 打开最近生成的 DWG 文件
                    var latestFile = files.First();
                    Process.Start(cadExePath, $"\"{latestFile.FullName}\"");
                }
            }
            catch
            {
                // 忽略打开错误
            }
        }

        /// <summary>
        /// 从注册表检测AutoCAD安装路径
        /// </summary>
        private string DetectAutoCadPathFromRegistry()
        {
            try
            {
                // 尝试从注册表读取AutoCAD安装路径
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Autodesk\AutoCAD"))
                {
                    if (key != null)
                    {
                        foreach (var versionName in key.GetSubKeyNames())
                        {
                            using (var versionKey = key.OpenSubKey(versionName))
                            {
                                if (versionKey != null)
                                {
                                    foreach (var releaseName in versionKey.GetSubKeyNames())
                                    {
                                        using (var releaseKey = versionKey.OpenSubKey(releaseName))
                                        {
                                            if (releaseKey != null)
                                            {
                                                var installPath = releaseKey.GetValue("AcadLocation") as string;
                                                if (!string.IsNullOrEmpty(installPath))
                                                {
                                                    var exePath = Path.Combine(installPath, "acad.exe");
                                                    if (File.Exists(exePath))
                                                    {
                                                        return exePath;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // 尝试常见路径
                var commonPaths = new[]
                {
                    @"C:\Program Files\Autodesk\AutoCAD 2022\acad.exe",
                    @"C:\Program Files\Autodesk\AutoCAD 2023\acad.exe",
                    @"C:\Program Files\Autodesk\AutoCAD 2024\acad.exe",
                    @"C:\Program Files\Autodesk\AutoCAD 2025\acad.exe",
                    @"D:\AutoCAD 2022\acad.exe",
                    @"D:\AutoCAD 2023\acad.exe",
                    @"D:\AutoCAD 2024\acad.exe",
                    @"D:\AutoCAD 2025\acad.exe"
                };

                return commonPaths.FirstOrDefault(p => File.Exists(p));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// ISO生成结果
        /// </summary>
        private class IsoGenerationResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
        }
    }
}
