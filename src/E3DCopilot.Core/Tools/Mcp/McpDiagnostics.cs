using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Logging;

namespace E3DCopilot.Core.Tools.Mcp
{
    /// <summary>诊断结果条目</summary>
    public class McpDiagEntry
    {
        public string Check { get; set; }
        public bool Passed { get; set; }
        public string Detail { get; set; }
        public long DurationMs { get; set; }
    }

    /// <summary>诊断报告</summary>
    public class McpDiagReport
    {
        public string ServerName { get; set; }
        public DateTime Timestamp { get; set; }
        public bool Healthy { get; set; }
        public List<McpDiagEntry> Checks { get; set; } = new List<McpDiagEntry>();
        public string Summary { get; set; }
    }

    /// <summary>
    /// MCP 诊断器 — 对齐 Reasonix internal/mcpdiag。
    /// 检测传输层连通性、握手状态、工具发现，快速定位连接问题。
    /// </summary>
    public static class McpDiagnostics
    {
        /// <summary>
        /// 诊断指定 MCP server 的连接状态
        /// </summary>
        public static async Task<McpDiagReport> DiagnoseAsync(
            CopilotConfig.McpServerConfig config, CancellationToken ct = default)
        {
            var report = new McpDiagReport
            {
                ServerName = config.Name,
                Timestamp = DateTime.Now
            };

            // 1. 配置检查
            var configCheck = new McpDiagEntry { Check = "配置有效性" };
            string type = (config.Type ?? "stdio").ToLowerInvariant();
            if (type == "stdio" && string.IsNullOrWhiteSpace(config.Command))
            {
                configCheck.Passed = false;
                configCheck.Detail = "stdio 模式缺少 Command 配置";
            }
            else if ((type == "http" || type == "sse") && string.IsNullOrWhiteSpace(config.Url))
            {
                configCheck.Passed = false;
                configCheck.Detail = $"{type} 模式缺少 Url 配置";
            }
            else
            {
                configCheck.Passed = true;
                configCheck.Detail = $"传输方式: {type}";
            }
            report.Checks.Add(configCheck);
            if (!configCheck.Passed) { report.Summary = "配置无效"; return report; }

            // 2. 传输层连接
            var transportCheck = new McpDiagEntry { Check = "传输层连接" };
            var sw = Stopwatch.StartNew();
            IMcpTransport transport = null;
            try
            {
                transport = CreateTransport(config);
                sw.Stop();
                transportCheck.Passed = true;
                transportCheck.DurationMs = sw.ElapsedMilliseconds;
                transportCheck.Detail = type == "stdio"
                    ? $"进程已启动: {config.Command}"
                    : $"HTTP 端点可达: {config.Url}";
            }
            catch (Exception ex)
            {
                sw.Stop();
                transportCheck.Passed = false;
                transportCheck.DurationMs = sw.ElapsedMilliseconds;
                transportCheck.Detail = $"连接失败: {ex.Message}";
            }
            report.Checks.Add(transportCheck);
            if (!transportCheck.Passed || transport == null)
            {
                report.Summary = "传输层连接失败";
                return report;
            }

            // 3. MCP 握手（initialize）
            var handshakeCheck = new McpDiagEntry { Check = "MCP 握手" };
            McpClient client = null;
            try
            {
                client = new McpClient(config.Name, transport);
                sw.Restart();
                await client.InitializeAsync(ct).ConfigureAwait(false);
                sw.Stop();
                handshakeCheck.Passed = true;
                handshakeCheck.DurationMs = sw.ElapsedMilliseconds;
                handshakeCheck.Detail = $"协议 2024-11-05, tools={client.HasTools}, prompts={client.HasPrompts}, resources={client.HasResources}";
            }
            catch (Exception ex)
            {
                sw.Stop();
                handshakeCheck.Passed = false;
                handshakeCheck.DurationMs = sw.ElapsedMilliseconds;
                handshakeCheck.Detail = $"握手失败: {ex.Message}";
            }
            report.Checks.Add(handshakeCheck);
            if (!handshakeCheck.Passed)
            {
                report.Summary = "MCP 握手失败（server 可能不兼容或超时）";
                try { transport.Dispose(); } catch { }
                return report;
            }

            // 4. 工具发现（tools/list）
            if (client.HasTools)
            {
                var toolsCheck = new McpDiagEntry { Check = "工具发现" };
                try
                {
                    sw.Restart();
                    var tools = await client.ListToolsAsync(ct).ConfigureAwait(false);
                    sw.Stop();
                    toolsCheck.Passed = true;
                    toolsCheck.DurationMs = sw.ElapsedMilliseconds;
                    toolsCheck.Detail = $"发现 {tools.Length} 个工具";
                    if (tools.Length > 0)
                    {
                        var names = new List<string>();
                        for (int i = 0; i < Math.Min(5, tools.Length); i++)
                            names.Add(tools[i].Name);
                        toolsCheck.Detail += $": {string.Join(", ", names)}";
                        if (tools.Length > 5) toolsCheck.Detail += $"... (+{tools.Length - 5})";
                    }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    toolsCheck.Passed = false;
                    toolsCheck.DurationMs = sw.ElapsedMilliseconds;
                    toolsCheck.Detail = $"工具发现失败: {ex.Message}";
                }
                report.Checks.Add(toolsCheck);
            }

            // 清理
            try { client?.Dispose(); } catch { }

            // 汇总
            bool allPassed = true;
            foreach (var check in report.Checks)
                if (!check.Passed) { allPassed = false; break; }

            report.Healthy = allPassed;
            report.Summary = allPassed ? "连接正常" : "存在异常，请检查失败项";
            return report;
        }

        /// <summary>
        /// 诊断 McpHost 中所有已配置 server 的状态
        /// </summary>
        public static McpDiagReport QuickDiagnose(McpHost host, string serverName)
        {
            var report = new McpDiagReport
            {
                ServerName = serverName,
                Timestamp = DateTime.Now
            };

            var client = host.GetClient(serverName);
            if (client == null)
            {
                report.Healthy = false;
                report.Summary = $"未找到 server '{serverName}'（可能未启动或启动失败）";

                // 检查失败记录
                foreach (var f in host.GetFailures())
                {
                    if (f.ServerName.Equals(serverName, StringComparison.OrdinalIgnoreCase))
                    {
                        report.Checks.Add(new McpDiagEntry
                        {
                            Check = "启动失败记录",
                            Passed = false,
                            Detail = $"{f.Error} ({f.Timestamp:HH:mm:ss})"
                        });
                    }
                }
                return report;
            }

            report.Healthy = client.IsConnected;
            report.Summary = client.IsConnected
                ? $"连接正常, {client.ToolCount} 个工具"
                : "连接已断开";
            report.Checks.Add(new McpDiagEntry
            {
                Check = "连接状态",
                Passed = client.IsConnected,
                Detail = $"tools={client.HasTools}, prompts={client.HasPrompts}, resources={client.HasResources}"
            });

            return report;
        }

        private static IMcpTransport CreateTransport(CopilotConfig.McpServerConfig config)
        {
            string type = (config.Type ?? "stdio").ToLowerInvariant();
            switch (type)
            {
                case "http":
                case "streamable-http":
                case "sse":
                    return new HttpTransport(config.Url,
                        timeoutMs: config.CallTimeoutMs > 0 ? config.CallTimeoutMs : 15000,
                        headers: config.Headers);
                default:
                    return new StdioTransport(config.Command,
                        (config.Args ?? new List<string>()).ToArray(),
                        timeoutMs: config.CallTimeoutMs > 0 ? config.CallTimeoutMs : 15000,
                        workingDirectory: config.Dir,
                        env: config.Env);
            }
        }
    }
}
