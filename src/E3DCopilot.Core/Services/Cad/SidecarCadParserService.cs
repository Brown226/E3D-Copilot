using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using E3DCopilot.Core.Models.Building;
using E3DCopilot.Core.Models.Geometry;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Services.Cad
{
    /// <summary>
    /// 通过独立进程 dwg-sidecar 解析 DWG。
    ///
    /// 为什么要独立进程：
    /// E3D 插件被锁死在 .NET Framework 4.8 + x86，进程内解析受位数和 Teigha 依赖约束；
    /// dwg-sidecar 是 x64 自包含可执行文件（基于 ACadSharp），用 stdio 收发一行 JSON，
    /// 与本进程运行时完全解耦。
    ///
    /// 通信协议（对齐 dwg-tools-plugin）：
    ///   请求：{"command":"graph","path":"...","maxSymbols":N,"maxSegments":N}
    ///   响应：{"ok":true,"segments":[{"kind","layer","handle","points":[{x,y}]}],...}
    ///
    /// 与 TeighaCadParserService 的关键差异：
    /// 不再按图层名关键词（土建/墙体/WALL）筛选，而是把图纸里的全部几何交给上层，
    /// 由调用方决定用哪些。关键词筛选会把"C1 柱墙剖线"这类专有图层漏掉。
    /// </summary>
    public class SidecarCadParserService : ICadParser
    {
        /// <summary>sidecar 可执行文件路径（可由配置覆盖）</summary>
        public string SidecarPath { get; set; }

        /// <summary>单次调用超时（毫秒）</summary>
        public int TimeoutMs { get; set; } = 60000;

        /// <summary>最多读取的线段数，防止大图撑爆内存</summary>
        public int MaxSegments { get; set; } = 20000;

        /// <summary>
        /// 图层过滤（可选）。为空表示不过滤，返回全部几何。
        /// 演示场景建议显式指定，避免轴线/标注被当成构件。
        /// </summary>
        public List<string> Layers { get; set; }

        public SidecarCadParserService(string sidecarPath = null)
        {
            SidecarPath = sidecarPath ?? FindSidecar();
        }

        /// <summary>
        /// 在常见位置查找 sidecar。找不到时返回 null，ParseFile 会给出明确错误。
        /// </summary>
        private static string FindSidecar()
        {
            var candidates = new List<string>
            {
                // 优先：与插件同目录的 tools/dwg-sidecar.exe（部署形态）
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "dwg-sidecar.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dwg-sidecar.exe"),
            };

            // E3D 安装目录下的 tools/。Loader（开发模式）通过 Assembly.Load(byte[])
            // 从 src/E3DCopilot.Addin/bin/Release/net48 加载 DLL，BaseDirectory 是那里，
            // 而不是 E3D 根目录；deploy.cmd 只把 sidecar 放到 <E3D_DIR>/tools/。
            // 因此从 des.exe 所在目录再探一次，避免开发模式下解析静默降级。
            string e3dDir = ResolveE3DInstallDir();
            if (!string.IsNullOrEmpty(e3dDir))
            {
                candidates.Add(Path.Combine(e3dDir, "tools", "dwg-sidecar.exe"));
            }

            // 环境变量显式指定（推荐，演示时不依赖猜测）
            string env = Environment.GetEnvironmentVariable("E3D_DWG_SIDECAR");
            if (!string.IsNullOrEmpty(env)) candidates.Insert(0, env);

            foreach (var c in candidates)
            {
                try { if (File.Exists(c)) return c; }
                catch { }
            }
            return null;
        }

        /// <summary>
        /// 定位 E3D 安装目录（des.exe 所在目录）。
        /// 插件可能被 Assembly.Load(byte[]) 从任意目录加载，BaseDirectory 不可靠，
        /// 因此优先取当前进程主模块（des.exe）的真实路径；再回退 E3D_DIR 环境变量。
        /// </summary>
        private static string ResolveE3DInstallDir()
        {
            try
            {
                using (var proc = System.Diagnostics.Process.GetCurrentProcess())
                {
                    string mainModule = proc.MainModule != null ? proc.MainModule.FileName : null;
                    if (!string.IsNullOrEmpty(mainModule))
                        return Path.GetDirectoryName(mainModule);
                }
            }
            catch { }

            string envDir = Environment.GetEnvironmentVariable("E3D_DIR");
            if (!string.IsNullOrEmpty(envDir) && Directory.Exists(envDir))
                return envDir;

            return null;
        }

        public CadParseResult ParseFile(string filePath)
        {
            var sw = Stopwatch.StartNew();
            var result = new CadParseResult();

            if (string.IsNullOrWhiteSpace(filePath))
            {
                result.Error = "图纸路径为空";
                return result;
            }
            if (!File.Exists(filePath))
            {
                result.Error = "图纸文件不存在: " + filePath;
                return result;
            }
            if (string.IsNullOrWhiteSpace(SidecarPath) || !File.Exists(SidecarPath))
            {
                result.Error = "未找到 dwg-sidecar，请设置环境变量 E3D_DWG_SIDECAR 指向 dwg-sidecar.exe";
                return result;
            }

            try
            {
                string responseJson = Invoke(filePath);
                var root = JObject.Parse(responseJson);

                if (root["ok"] != null && !root["ok"].Value<bool>())
                {
                    result.Error = "sidecar 解析失败: " + (root["error"]?.ToString() ?? "未知错误");
                    return result;
                }

                var segments = root["segments"] as JArray;
                if (segments == null)
                {
                    result.Error = "sidecar 返回中没有 segments 字段";
                    return result;
                }

                var layerSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var built = new List<LineSegment>();

                foreach (var seg in segments)
                {
                    string kind = seg["kind"]?.ToString();
                    string layer = seg["layer"]?.ToString() ?? "";
                    var pts = seg["points"] as JArray;
                    if (pts == null) continue;

                    // 只取有起止两点的线段。POLYLINE 可能有更多点，逐段拆开。
                    for (int i = 1; i < pts.Count; i++)
                    {
                        var p0 = ReadPoint(pts[i - 1]);
                        var p1 = ReadPoint(pts[i]);
                        if (p0 == null || p1 == null) continue;
                        if (p0.DistanceTo(p1) < 1e-6) continue;

                        if (Layers != null && Layers.Count > 0)
                        {
                            bool hit = false;
                            foreach (var l in Layers)
                            {
                                if (string.Equals(l, layer, StringComparison.OrdinalIgnoreCase)) { hit = true; break; }
                            }
                            if (!hit) continue;
                        }

                        built.Add(new LineSegment(p0, p1, layer));
                    }

                    if (!string.IsNullOrEmpty(layer)) layerSet.Add(layer);
                    result.Entities.Add(new CadEntityInfo { EntityType = kind, Layer = layer });
                }

                foreach (var l in layerSet)
                    result.Layers.Add(new CadLayerInfo { Name = l });

                result.WallSegments = built;
                result.TotalEntities = result.Entities.Count;
                result.WallCount = built.Count;
                result.Success = true;
                result.ParseDuration = sw.Elapsed;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = "sidecar 调用异常: " + ex.Message;
                result.ParseDuration = sw.Elapsed;
                return result;
            }
        }

        private static Point3D ReadPoint(JToken t)
        {
            if (t == null) return null;
            var x = t["x"];
            var y = t["y"];
            if (x == null || y == null) return null;
            double z = t["z"] != null ? t["z"].Value<double>() : 0;
            return new Point3D(x.Value<double>(), y.Value<double>(), z);
        }

        /// <summary>
        /// 启动 sidecar，写入一行 JSON 请求，读回一行 JSON 响应。
        /// </summary>
        private string Invoke(string filePath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = SidecarPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var request = new JObject
            {
                ["command"] = "graph",
                ["path"] = filePath,
                ["maxSymbols"] = 5000,
                ["maxSegments"] = MaxSegments
            };

            var requestBytes = new UTF8Encoding(false).GetBytes(
                request.ToString(Newtonsoft.Json.Formatting.None) + "\n");

            // ★ 关键：.NET Framework 的 Process.StandardInput 内部按 Console.InputEncoding
            //   解码/编码管道。当宿主进程的控制台代码页为 65001 时，该编码带 3 字节 BOM
            //   (EF BB BF)，会被写到子进程 stdin 的最前面。sidecar 用 JsonDocument.Parse
            //   读 stdin，遇到 BOM 直接抛 '0xEF' is an invalid start of a value ——
            //   表现为"图纸解析失败"，且只在部分机器（UTF-8 代码页）上复现。
            //   这里临时把 Console.InputEncoding 换成无 BOM 版本，用完还原。
            var originalInputEncoding = Console.InputEncoding;
            try { Console.InputEncoding = new UTF8Encoding(false); }
            catch { /* 无控制台的场景（如服务宿主）忽略 */ }

            try
            {
                using (var proc = new Process { StartInfo = psi })
                {
                proc.Start();

                // 直接写字节，不经 StreamWriter（StreamWriter 也会按编码写出 preamble）
                proc.StandardInput.BaseStream.Write(requestBytes, 0, requestBytes.Length);
                proc.StandardInput.BaseStream.Flush();
                proc.StandardInput.Close();

                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();

                if (!proc.WaitForExit(TimeoutMs))
                {
                    try { proc.Kill(); } catch { }
                    throw new TimeoutException("sidecar 超过 " + TimeoutMs + "ms 未返回");
                }

                if (string.IsNullOrWhiteSpace(stdout))
                {
                    throw new InvalidOperationException(
                        "sidecar 无输出。退出码=" + proc.ExitCode + " stderr=" + Truncate(stderr, 300));
                }

                // 剥离 UTF-8 BOM。sidecar 在控制台代码页为 65001 时会写出 BOM，
                // JObject.Parse 遇到 U+FEFF 会直接抛 JsonReaderException。
                // 代码页随机器而变，不剥离就会在某些机器上稳定失败。
                stdout = stdout.TrimStart('﻿', ' ', '\r', '\n', '\t');

                if (stdout.Length == 0 || stdout[0] != '{')
                {
                    throw new InvalidOperationException(
                        "sidecar 返回的不是 JSON（首字符=" +
                        (stdout.Length > 0 ? "'" + stdout[0] + "'" : "空") +
                        "）。内容=" + Truncate(stdout, 200));
                }

                    return stdout;
                }
            }
            finally
            {
                // 还原宿主编码，避免影响其它以 Console.InputEncoding 为基准的 IO
                try { Console.InputEncoding = originalInputEncoding; } catch { }
            }
        }

        private static string Truncate(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n) + "...";
        }
    }
}
