using System;
using System.Linq;
using System.Text.Json;
using E3DCopilot.Core.Messaging;

namespace E3DCopilot.WebHost
{
    /// <summary>
    /// Bridge — MCP 管理
    /// 提供 MCP server 状态查询和重启功能
    /// </summary>
    public partial class Bridge
    {
        /// <summary>
        /// 获取所有 MCP server 状态
        /// </summary>
        private void HandleMcpStatus(string requestId)
        {
            try
            {
                var mcpHost = _controller.McpHost;
                if (mcpHost == null)
                {
                    SendToFrontend("mcp:status", new
                    {
                        servers = new object[0],
                        failures = new object[0],
                        message = "MCP 未配置"
                    }, requestId);
                    return;
                }

                var servers = mcpHost.GetStatus().Select(s => new
                {
                    name = s.Name,
                    transport = s.Transport,
                    connected = s.Connected,
                    toolCount = s.ToolCount,
                    hasTools = s.HasTools,
                    hasPrompts = s.HasPrompts,
                    hasResources = s.HasResources,
                    tools = s.Tools.Select(t => new
                    {
                        name = t.Name,
                        description = t.Description,
                        readOnly = t.ReadOnlyHint,
                        destructive = t.DestructiveHint
                    }).ToList()
                }).ToList();

                var failures = mcpHost.GetFailures().Select(f => new
                {
                    server = f.ServerName,
                    error = f.Error,
                    time = f.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                }).ToList();

                SendToFrontend("mcp:status", new
                {
                    servers,
                    failures,
                    totalTools = servers.Sum(s => s.toolCount)
                }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"获取 MCP 状态失败: {ex.Message}" }, requestId);
            }
        }

        /// <summary>
        /// 重启指定 MCP server
        /// </summary>
        private async void HandleMcpRestart(JsonElement? payload, string requestId)
        {
            try
            {
                string serverName = null;
                if (payload.HasValue && payload.Value.TryGetProperty("server", out var serverProp))
                    serverName = serverProp.GetString();

                if (string.IsNullOrEmpty(serverName))
                {
                    SendToFrontend(MessageTypes.Error, new { message = "缺少 server 参数" }, requestId);
                    return;
                }

                var mcpHost = _controller.McpHost;
                if (mcpHost == null)
                {
                    SendToFrontend(MessageTypes.Error, new { message = "MCP 未配置" }, requestId);
                    return;
                }

                bool success = await mcpHost.RestartServerAsync(serverName);

                // 重启后重新注册工具
                if (success)
                {
                    foreach (var tool in mcpHost.GetAllTools())
                    {
                        if (_controller.Executor.GetHandler(tool.Name) == null)
                            _controller.Executor.Register(tool);
                    }
                }

                SendToFrontend("mcp:restart", new
                {
                    server = serverName,
                    success,
                    message = success ? $"MCP server '{serverName}' 已重启" : $"未找到 MCP server '{serverName}'"
                }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"重启 MCP 失败: {ex.Message}" }, requestId);
            }
        }

        /// <summary>
        /// 诊断指定 MCP server
        /// </summary>
        private void HandleMcpDiagnose(JsonElement? payload, string requestId)
        {
            try
            {
                string serverName = null;
                if (payload.HasValue && payload.Value.TryGetProperty("server", out var serverProp))
                    serverName = serverProp.GetString();

                var mcpHost = _controller.McpHost;
                if (mcpHost == null || string.IsNullOrEmpty(serverName))
                {
                    SendToFrontend("mcp:diagnose", new
                    {
                        healthy = false,
                        summary = "MCP 未配置或缺少 server 参数"
                    }, requestId);
                    return;
                }

                var report = E3DCopilot.Core.Tools.Mcp.McpDiagnostics.QuickDiagnose(mcpHost, serverName);

                SendToFrontend("mcp:diagnose", new
                {
                    server = report.ServerName,
                    healthy = report.Healthy,
                    summary = report.Summary,
                    timestamp = report.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    checks = report.Checks.Select(c => new
                    {
                        check = c.Check,
                        passed = c.Passed,
                        detail = c.Detail,
                        durationMs = c.DurationMs
                    }).ToList()
                }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"MCP 诊断失败: {ex.Message}" }, requestId);
            }
        }
    }
}
