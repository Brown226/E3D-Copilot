using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Logging;

namespace E3DCopilot.Core.Tools.Mcp
{
    /// <summary>MCP server 连接失败记录</summary>
    public class McpFailure
    {
        public string ServerName { get; set; }
        public string Error { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>MCP server 状态信息（供前端 /mcp 查询）</summary>
    public class McpServerStatus
    {
        public string Name { get; set; }
        public string Transport { get; set; }
        public bool Connected { get; set; }
        public int ToolCount { get; set; }
        public bool HasTools { get; set; }
        public bool HasPrompts { get; set; }
        public bool HasResources { get; set; }
        public List<McpToolInfo> Tools { get; set; } = new List<McpToolInfo>();
    }

    /// <summary>
    /// MCP Host — 管理多个 MCP server 连接。
    /// 对齐 Reasonix internal/plugin Host 结构。
    /// 职责: 启动/关闭/状态查询 + 聚合所有 server 的工具。
    /// </summary>
    public class McpHost : IDisposable
    {
        private readonly object _lock = new object();
        private readonly List<McpClient> _clients = new List<McpClient>();
        private readonly List<McpRemoteTool> _tools = new List<McpRemoteTool>();
        private readonly List<McpFailure> _failures = new List<McpFailure>();
        private readonly Dictionary<string, CopilotConfig.McpServerConfig> _configs =
            new Dictionary<string, CopilotConfig.McpServerConfig>(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        /// <summary>启动安全管理器（可选，null=跳过授权检查）</summary>
        public McpLaunchSecurity Security { get; set; }

        /// <summary>授权请求回调（前端弹窗确认用，返回 true=允许）</summary>
        public Func<string, string, bool> ApprovalCallback { get; set; }

        /// <summary>每个 server 的启动超时（毫秒）</summary>
        public int PerServerTimeoutMs { get; set; } = 30000;

        /// <summary>并发启动数限制</summary>
        public int Concurrency { get; set; } = 4;

        /// <summary>所有已发现的 MCP 远程工具</summary>
        public IReadOnlyList<McpRemoteTool> GetAllTools()
        {
            lock (_lock) { return new List<McpRemoteTool>(_tools); }
        }

        /// <summary>所有已连接的 server 名称</summary>
        public IReadOnlyList<string> GetServerNames()
        {
            lock (_lock) { return _clients.Select(c => c.Name).ToList(); }
        }

        /// <summary>启动失败记录</summary>
        public IReadOnlyList<McpFailure> GetFailures()
        {
            lock (_lock) { return new List<McpFailure>(_failures); }
        }

        /// <summary>获取指定 server 的客户端</summary>
        public McpClient GetClient(string name)
        {
            lock (_lock) { return _clients.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); }
        }

        // ═══════════════════════════════════════════════════════════
        //  StartAll — 并发启动所有配置的 MCP server
        //  对齐 Reasonix Host.StartAll + StartPolicy
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 启动所有配置的 MCP server，发现工具并注册。
        /// </summary>
        public async Task StartAllAsync(List<CopilotConfig.McpServerConfig> configs, CancellationToken ct = default)
        {
            if (configs == null || configs.Count == 0) return;

            var tasks = new List<Task>();
            var semaphore = new SemaphoreSlim(Concurrency > 0 ? Concurrency : configs.Count);

            foreach (var config in configs)
            {
                if (string.IsNullOrWhiteSpace(config.Name)) continue;

                lock (_lock) { _configs[config.Name] = config; }

                await semaphore.WaitAsync(ct);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await StartServerAsync(config, ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            lock (_lock)
            {
                CopilotLogger.Info("McpHost 启动完成: {0} 个 server 连接, {1} 个工具可用, {2} 个失败",
                    _clients.Count, _tools.Count, _failures.Count);
            }
        }

        /// <summary>
        /// 启动单个 MCP server
        /// </summary>
        private async Task StartServerAsync(CopilotConfig.McpServerConfig config, CancellationToken ct)
        {
            try
            {
                // ── 安全检查（对齐 Reasonix mcplaunch 授权流程）──
                if (Security != null)
                {
                    string identityDigest = McpLaunchSecurity.ComputeIdentityDigest(config);
                    var (authorized, changed) = Security.IsAuthorized(
                        config.Name, "config", identityDigest);

                    if (!authorized)
                    {
                        // 请求用户授权
                        bool approved = ApprovalCallback?.Invoke(config.Name,
                            changed ? "配置已变更，需重新授权" : "首次启动，需确认授权") ?? true;

                        if (!approved)
                        {
                            lock (_lock)
                            {
                                _failures.Add(new McpFailure
                                {
                                    ServerName = config.Name,
                                    Error = "用户拒绝授权",
                                    Timestamp = DateTime.Now
                                });
                            }
                            return;
                        }

                        // 记录授权
                        Security.Authorize(config.Name, "config", identityDigest);
                    }
                }

                // 创建传输层
                IMcpTransport transport = CreateTransport(config);

                // 创建客户端（带三级超时）
                var client = new McpClient(
                    config.Name,
                    transport,
                    defaultCallTimeoutMs: 300000,
                    callTimeoutMs: config.CallTimeoutMs > 0 ? config.CallTimeoutMs : 0,
                    toolTimeouts: config.ToolTimeouts);

                // 初始化（带超时保护）
                using (var timeoutCts = new CancellationTokenSource(PerServerTimeoutMs))
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token))
                {
                    await client.InitializeAsync(linkedCts.Token).ConfigureAwait(false);
                }

                // 发现工具
                var toolInfos = await client.ListToolsAsync(ct).ConfigureAwait(false);
                var remoteTools = new List<McpRemoteTool>();
                var readOnlySet = new HashSet<string>(
                    config.ReadOnlyToolNames ?? new List<string>(),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var toolInfo in toolInfos)
                {
                    bool readOnlyOverride = readOnlySet.Contains(toolInfo.Name);
                    var remoteTool = new McpRemoteTool(
                        client, toolInfo, config.Name,
                        stripRawPrefix: config.StripRawPrefix,
                        readOnlyOverride: readOnlyOverride);
                    remoteTools.Add(remoteTool);
                }

                // 注册
                lock (_lock)
                {
                    _clients.Add(client);
                    _tools.AddRange(remoteTools);
                }

                CopilotLogger.Info("MCP [{0}] 启动成功: {1} 个工具", config.Name, remoteTools.Count);
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _failures.Add(new McpFailure
                    {
                        ServerName = config.Name,
                        Error = ex.Message,
                        Timestamp = DateTime.Now
                    });
                }
                CopilotLogger.Warn("MCP [{0}] 启动失败: {1}", config.Name, ex.Message);
            }
        }

        /// <summary>
        /// 根据配置创建传输层
        /// </summary>
        private static IMcpTransport CreateTransport(CopilotConfig.McpServerConfig config)
        {
            string type = (config.Type ?? "stdio").ToLowerInvariant();

            switch (type)
            {
                case "http":
                case "streamable-http":
                case "sse":
                    return new HttpTransport(
                        config.Url,
                        timeoutMs: config.CallTimeoutMs > 0 ? config.CallTimeoutMs : 30000,
                        headers: config.Headers);

                case "stdio":
                default:
                    return new StdioTransport(
                        config.Command,
                        (config.Args ?? new List<string>()).ToArray(),
                        timeoutMs: config.CallTimeoutMs > 0 ? config.CallTimeoutMs : 30000,
                        workingDirectory: config.Dir,
                        env: config.Env);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  状态查询（供前端 /mcp 使用）
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 获取所有 server 的状态信息
        /// </summary>
        public List<McpServerStatus> GetStatus()
        {
            lock (_lock)
            {
                var result = new List<McpServerStatus>();
                foreach (var client in _clients)
                {
                    result.Add(new McpServerStatus
                    {
                        Name = client.Name,
                        Transport = _configs.ContainsKey(client.Name)
                            ? (_configs[client.Name].Type ?? "stdio") : "stdio",
                        Connected = client.IsConnected,
                        ToolCount = client.ToolCount,
                        HasTools = client.HasTools,
                        HasPrompts = client.HasPrompts,
                        HasResources = client.HasResources,
                        Tools = _tools
                            .Where(t => t.ServerName == client.Name)
                            .Select(t => new McpToolInfo
                            {
                                Name = t.Name,
                                Description = t.Description,
                                ReadOnlyHint = t.IsReadOnly,
                                DestructiveHint = t.IsDestructive
                            }).ToList()
                    });
                }
                return result;
            }
        }

        /// <summary>
        /// 重启指定 server
        /// </summary>
        public async Task<bool> RestartServerAsync(string serverName, CancellationToken ct = default)
        {
            CopilotConfig.McpServerConfig config;
            lock (_lock)
            {
                if (!_configs.TryGetValue(serverName, out config)) return false;

                // 移除旧连接
                var oldClient = _clients.FirstOrDefault(c => c.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase));
                if (oldClient != null)
                {
                    _clients.Remove(oldClient);
                    _tools.RemoveAll(t => t.ServerName.Equals(serverName, StringComparison.OrdinalIgnoreCase));
                    try { oldClient.Dispose(); } catch { }
                }
                _failures.RemoveAll(f => f.ServerName.Equals(serverName, StringComparison.OrdinalIgnoreCase));
            }

            // 重新启动
            await StartServerAsync(config, ct).ConfigureAwait(false);
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_lock)
            {
                foreach (var client in _clients)
                {
                    try { client.Dispose(); } catch { }
                }
                _clients.Clear();
                _tools.Clear();
            }
        }
    }
}
