using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using E3DCopilot.Core.Logging;
using Newtonsoft.Json;

namespace E3DCopilot.Core.Tools.Mcp
{
    /// <summary>
    /// MCP 安装授权管理 — 对齐 Reasonix SPEC 3.3 "installation is the trust decision"
    ///
    /// 机制：
    ///   - 项目级 MCP 服务器首次连接需一次性确认
    ///   - 记录 exact identity（command + args hash）到 grants 文件
    ///   - identity 变化时重新确认
    ///   - 用户级安装（显式操作）直接授权
    /// </summary>
    public class McpInstallGrant
    {
        private readonly string _grantsPath;
        private Dictionary<string, GrantEntry> _grants;

        public McpInstallGrant(string baseDir = null)
        {
            baseDir = baseDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "E3DCopilot");

            _grantsPath = Path.Combine(baseDir, "mcp-grants.json");
            LoadGrants();
        }

        /// <summary>
        /// 检查指定 MCP 服务器是否已授权。
        /// 返回 true = 已授权可连接，false = 需要用户确认。
        /// </summary>
        public bool IsGranted(string serverName, string command, List<string> args = null)
        {
            string identity = ComputeIdentity(command, args);

            GrantEntry entry;
            if (_grants.TryGetValue(serverName, out entry))
            {
                // identity 匹配 = 已授权
                return entry.IdentityHash == identity;
            }
            return false;
        }

        /// <summary>
        /// 授权指定 MCP 服务器（用户确认后调用）
        /// </summary>
        public void Grant(string serverName, string command, List<string> args = null,
            string source = "user")
        {
            string identity = ComputeIdentity(command, args);

            _grants[serverName] = new GrantEntry
            {
                ServerName = serverName,
                IdentityHash = identity,
                Command = command,
                Args = args ?? new List<string>(),
                GrantedAt = DateTime.UtcNow.ToString("o"),
                Source = source
            };

            SaveGrants();
            CopilotLogger.Info("McpInstallGrant: 已授权 {0} (source={1})", serverName, source);
        }

        /// <summary>
        /// 撤销授权
        /// </summary>
        public void Revoke(string serverName)
        {
            if (_grants.Remove(serverName))
            {
                SaveGrants();
                CopilotLogger.Info("McpInstallGrant: 已撤销 {0}", serverName);
            }
        }

        /// <summary>
        /// 检查 identity 是否发生变化（需要重新确认）
        /// </summary>
        public bool IdentityChanged(string serverName, string command, List<string> args = null)
        {
            GrantEntry entry;
            if (!_grants.TryGetValue(serverName, out entry))
                return false; // 从未授权，不算"变化"

            string currentIdentity = ComputeIdentity(command, args);
            return entry.IdentityHash != currentIdentity;
        }

        /// <summary>
        /// 列出所有已授权的服务器
        /// </summary>
        public List<GrantEntry> ListGrants()
        {
            return _grants.Values.ToList();
        }

        // ═══════════════════════════════════════════════════════════
        //  内部实现
        // ═══════════════════════════════════════════════════════════

        private static string ComputeIdentity(string command, List<string> args)
        {
            string raw = command + "|" + (args != null ? string.Join(" ", args) : "");
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
            }
        }

        private void LoadGrants()
        {
            _grants = new Dictionary<string, GrantEntry>();
            try
            {
                if (File.Exists(_grantsPath))
                {
                    string json = File.ReadAllText(_grantsPath);
                    var list = JsonConvert.DeserializeObject<List<GrantEntry>>(json);
                    if (list != null)
                    {
                        foreach (var g in list)
                            _grants[g.ServerName] = g;
                    }
                }
            }
            catch (Exception ex)
            {
                CopilotLogger.Error(ex, "McpInstallGrant: 加载授权文件失败");
            }
        }

        private void SaveGrants()
        {
            try
            {
                var dir = Path.GetDirectoryName(_grantsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonConvert.SerializeObject(_grants.Values.ToList(), Formatting.Indented);
                File.WriteAllText(_grantsPath, json);
            }
            catch (Exception ex)
            {
                CopilotLogger.Error(ex, "McpInstallGrant: 保存授权文件失败");
            }
        }

        /// <summary>授权条目</summary>
        public class GrantEntry
        {
            [JsonProperty("serverName")]
            public string ServerName { get; set; }

            [JsonProperty("identityHash")]
            public string IdentityHash { get; set; }

            [JsonProperty("command")]
            public string Command { get; set; }

            [JsonProperty("args")]
            public List<string> Args { get; set; }

            [JsonProperty("grantedAt")]
            public string GrantedAt { get; set; }

            [JsonProperty("source")]
            public string Source { get; set; }
        }
    }
}
