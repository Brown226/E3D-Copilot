using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Logging;
using Newtonsoft.Json;

namespace E3DCopilot.Core.Tools.Mcp
{
    // ═══════════════════════════════════════════════════════════
    //  数据模型（对齐 Reasonix internal/mcplaunch）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// MCP server 启动身份（不含 secrets）— 对齐 Reasonix ProjectLaunchIdentity。
    /// 用于生成身份指纹，判断 server 配置是否被篡改。
    /// </summary>
    public class McpLaunchIdentity
    {
        [JsonProperty("server")]
        public string Server { get; set; }

        [JsonProperty("transport")]
        public string Transport { get; set; }

        [JsonProperty("command_path")]
        public string CommandPath { get; set; }

        [JsonProperty("args")]
        public List<string> Args { get; set; }

        [JsonProperty("dir")]
        public string Dir { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("env_keys")]
        public List<string> EnvKeys { get; set; }

        [JsonProperty("header_keys")]
        public List<string> HeaderKeys { get; set; }
    }

    /// <summary>
    /// 持久化启动授权记录 — 对齐 Reasonix LaunchGrant。
    /// 用户首次批准某 MCP server 后写入，后续启动自动通过。
    /// </summary>
    public class McpLaunchGrant
    {
        [JsonProperty("scope")]
        public string Scope { get; set; } = "workspace";

        [JsonProperty("workspace_fingerprint")]
        public string WorkspaceFingerprint { get; set; }

        [JsonProperty("server")]
        public string Server { get; set; }

        [JsonProperty("config_source")]
        public string ConfigSource { get; set; }

        [JsonProperty("identity_fingerprint")]
        public string IdentityDigest { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// 安全状态文件结构 — 对齐 Reasonix State（mcp-security.json）
    /// </summary>
    public class McpSecurityState
    {
        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        [JsonProperty("launch_grants")]
        public List<McpLaunchGrant> LaunchGrants { get; set; } = new List<McpLaunchGrant>();
    }

    // ═══════════════════════════════════════════════════════════
    //  McpLaunchSecurity — 安全存储管理器
    //  对齐 Reasonix internal/mcplaunch Manager
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// MCP 启动安全管理器 — 对齐 Reasonix mcplaunch.Manager。
    /// 管理 mcp-security.json 中的启动授权记录。
    /// 首次启动项目级 MCP 时需要用户确认，授权后记住。
    /// </summary>
    public class McpLaunchSecurity
    {
        private readonly string _statePath;
        private readonly string _workspaceFingerprint;
        private readonly object _lock = new object();

        private const int StoreVersion = 1;

        /// <summary>
        /// 创建安全管理器
        /// </summary>
        /// <param name="statePath">mcp-security.json 文件路径</param>
        /// <param name="workspace">工作区路径（用于生成指纹）</param>
        public McpLaunchSecurity(string statePath, string workspace = null)
        {
            _statePath = statePath;
            _workspaceFingerprint = ComputeWorkspaceFingerprint(workspace);
        }

        /// <summary>工作区指纹</summary>
        public string WorkspaceFingerprint => _workspaceFingerprint;

        // ═══════════════════════════════════════════════════════════
        //  授权检查
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 检查指定 server 是否已授权启动。
        /// 返回: (authorized, identityChanged)
        /// - authorized=true: 已授权，可直接启动
        /// - identityChanged=true: 配置已变更（身份指纹不匹配），需重新授权
        /// </summary>
        public (bool authorized, bool identityChanged) IsAuthorized(
            string server, string configSource, string identityDigest)
        {
            lock (_lock)
            {
                var state = LoadState();
                foreach (var grant in state.LaunchGrants)
                {
                    if (grant.Server != server
                        || grant.ConfigSource != configSource
                        || grant.WorkspaceFingerprint != _workspaceFingerprint)
                        continue;

                    if (grant.IdentityDigest == identityDigest)
                        return (true, false);

                    // 找到同 server 的授权但指纹不同 → 配置被修改
                    return (false, true);
                }
                return (false, false);
            }
        }

        /// <summary>
        /// 记录授权（用户批准后调用）
        /// </summary>
        public void Authorize(string server, string configSource, string identityDigest)
        {
            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(identityDigest))
                return;

            var grant = new McpLaunchGrant
            {
                Scope = "workspace",
                WorkspaceFingerprint = _workspaceFingerprint,
                Server = server.Trim(),
                ConfigSource = (configSource ?? "config").Trim(),
                IdentityDigest = identityDigest.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            lock (_lock)
            {
                var state = LoadState();

                // 移除同 server 的旧授权，写入新授权
                state.LaunchGrants.RemoveAll(g =>
                    g.Server == grant.Server
                    && g.WorkspaceFingerprint == _workspaceFingerprint);
                state.LaunchGrants.Add(grant);

                SaveState(state);
            }

            CopilotLogger.Info("MCP 授权已记录: server={0}, source={1}", server, configSource);
        }

        /// <summary>
        /// 撤销指定 server 的授权
        /// </summary>
        public void Revoke(string server)
        {
            lock (_lock)
            {
                var state = LoadState();
                state.LaunchGrants.RemoveAll(g =>
                    g.Server == server.Trim()
                    && g.WorkspaceFingerprint == _workspaceFingerprint);
                SaveState(state);
            }
        }

        /// <summary>
        /// 获取所有已授权的 server 列表
        /// </summary>
        public List<McpLaunchGrant> GetGrants()
        {
            lock (_lock)
            {
                var state = LoadState();
                return state.LaunchGrants
                    .Where(g => g.WorkspaceFingerprint == _workspaceFingerprint)
                    .ToList();
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  身份指纹计算（对齐 Reasonix ProjectLaunchIdentityDigest）
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 从配置计算 server 身份指纹。
        /// 不含 secrets（Env values / Header values），仅用 key 名。
        /// </summary>
        public static string ComputeIdentityDigest(CopilotConfig.McpServerConfig config)
        {
            var identity = new McpLaunchIdentity
            {
                Server = (config.Name ?? "").Trim(),
                Transport = (config.Type ?? "stdio").Trim().ToLowerInvariant(),
                CommandPath = (config.Command ?? "").Trim(),
                Args = config.Args ?? new List<string>(),
                Dir = (config.Dir ?? "").Trim(),
                Url = (config.Url ?? "").Trim(),
                EnvKeys = config.Env != null
                    ? config.Env.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList()
                    : new List<string>(),
                HeaderKeys = config.Headers != null
                    ? config.Headers.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList()
                    : new List<string>()
            };

            string json = JsonConvert.SerializeObject(identity, Formatting.None);
            return ComputeSha256(json);
        }

        // ═══════════════════════════════════════════════════════════
        //  持久化
        // ═══════════════════════════════════════════════════════════

        private McpSecurityState LoadState()
        {
            if (string.IsNullOrWhiteSpace(_statePath) || !File.Exists(_statePath))
                return new McpSecurityState { Version = StoreVersion };

            try
            {
                string json = File.ReadAllText(_statePath, Encoding.UTF8);
                var state = JsonConvert.DeserializeObject<McpSecurityState>(json);
                if (state == null || state.Version != StoreVersion)
                    return new McpSecurityState { Version = StoreVersion };
                if (state.LaunchGrants == null)
                    state.LaunchGrants = new List<McpLaunchGrant>();
                return state;
            }
            catch
            {
                return new McpSecurityState { Version = StoreVersion };
            }
        }

        private void SaveState(McpSecurityState state)
        {
            if (string.IsNullOrWhiteSpace(_statePath)) return;

            try
            {
                var dir = Path.GetDirectoryName(_statePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonConvert.SerializeObject(state, Formatting.Indented);
                File.WriteAllText(_statePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                CopilotLogger.Warn("MCP 安全状态保存失败: {0}", ex.Message);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  工具方法
        // ═══════════════════════════════════════════════════════════

        private static string ComputeWorkspaceFingerprint(string workspace)
        {
            if (string.IsNullOrWhiteSpace(workspace)) return "";
            string normalized = workspace.Trim().Replace('\\', '/').ToLowerInvariant();
            return ComputeSha256(normalized);
        }

        private static string ComputeSha256(string input)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(64);
                foreach (byte b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
