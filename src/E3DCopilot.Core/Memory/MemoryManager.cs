using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace E3DCopilot.Core.Memory
{
    /// <summary>
    /// 记忆管理器 — SQLite 持久化存储
    /// </summary>
    public class MemoryManager : IDisposable
    {
        private readonly string _dbPath;
        private SqliteConnection _connection;

        public MemoryManager(string dbPath = null)
        {
            _dbPath = dbPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "E3DCopilot", "memories.db");

            var dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            _connection = new SqliteConnection($"Data Source={_dbPath}");
            _connection.Open();

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS memories (
                        id TEXT PRIMARY KEY,
                        title TEXT NOT NULL,
                        content TEXT NOT NULL,
                        kind TEXT NOT NULL DEFAULT 'project_context',
                        tags TEXT NOT NULL DEFAULT '[]',
                        score REAL NOT NULL DEFAULT 0,
                        created_at TEXT NOT NULL,
                        updated_at TEXT NOT NULL
                    )";
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 获取所有记忆
        /// </summary>
        public List<MemoryEntry> List(string kindFilter = null)
        {
            var results = new List<MemoryEntry>();
            using (var cmd = _connection.CreateCommand())
            {
                if (!string.IsNullOrEmpty(kindFilter) && kindFilter != "all")
                {
                    cmd.CommandText = "SELECT * FROM memories WHERE kind = @kind ORDER BY created_at DESC";
                    cmd.Parameters.AddWithValue("@kind", kindFilter);
                }
                else
                {
                    cmd.CommandText = "SELECT * FROM memories ORDER BY created_at DESC";
                }

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(ReadEntry(reader));
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// 保存记忆（新增或更新）
        /// </summary>
        public MemoryEntry Save(MemoryEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Id))
                entry.Id = $"mem_{DateTime.UtcNow.Ticks}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";

            var now = DateTime.UtcNow.ToString("o");
            if (string.IsNullOrEmpty(entry.CreatedAt))
                entry.CreatedAt = now;
            entry.UpdatedAt = now;

            var tagsJson = JsonConvert.SerializeObject(entry.Tags ?? new string[0]);

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO memories (id, title, content, kind, tags, score, created_at, updated_at)
                    VALUES (@id, @title, @content, @kind, @tags, @score, @created_at, @updated_at)";
                cmd.Parameters.AddWithValue("@id", entry.Id);
                cmd.Parameters.AddWithValue("@title", entry.Title);
                cmd.Parameters.AddWithValue("@content", entry.Content);
                cmd.Parameters.AddWithValue("@kind", entry.Kind);
                cmd.Parameters.AddWithValue("@tags", tagsJson);
                cmd.Parameters.AddWithValue("@score", entry.Score);
                cmd.Parameters.AddWithValue("@created_at", entry.CreatedAt);
                cmd.Parameters.AddWithValue("@updated_at", entry.UpdatedAt);
                cmd.ExecuteNonQuery();
            }

            return entry;
        }

        /// <summary>
        /// 删除记忆
        /// </summary>
        public bool Delete(string id)
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM memories WHERE id = @id";
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        /// <summary>
        /// 获取记忆数量
        /// </summary>
        public int Count()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM memories";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private MemoryEntry ReadEntry(SqliteDataReader reader)
        {
            var tagsStr = reader.GetString(reader.GetOrdinal("tags"));
            var tags = JsonConvert.DeserializeObject<string[]>(tagsStr) ?? new string[0];

            return new MemoryEntry
            {
                Id = reader.GetString(reader.GetOrdinal("id")),
                Title = reader.GetString(reader.GetOrdinal("title")),
                Content = reader.GetString(reader.GetOrdinal("content")),
                Kind = reader.GetString(reader.GetOrdinal("kind")),
                Tags = tags,
                Score = reader.GetDouble(reader.GetOrdinal("score")),
                CreatedAt = reader.GetString(reader.GetOrdinal("created_at")),
                UpdatedAt = reader.GetString(reader.GetOrdinal("updated_at")),
            };
        }

        // ── 用户画像（C1②）：每次工具调用自动更新，跨会话持久化 ──
        private UserProfile _profile;

        public UserProfile Profile => _profile ?? (_profile = LoadProfile());

        private UserProfile LoadProfile()
        {
            try
            {
                var path = Path.Combine(Path.GetDirectoryName(_dbPath) ?? ".", "profile.json");
                if (File.Exists(path))
                {
                    var p = JsonConvert.DeserializeObject<UserProfile>(File.ReadAllText(path));
                    if (p != null) return p;
                }
            }
            catch { }
            return new UserProfile();
        }

        private void SaveProfile()
        {
            if (_profile == null) return;
            try
            {
                var path = Path.Combine(Path.GetDirectoryName(_dbPath) ?? ".", "profile.json");
                File.WriteAllText(path, JsonConvert.SerializeObject(_profile, Formatting.Indented));
            }
            catch { }
        }

        /// <summary>每次工具调用后调用，自动更新用户画像（C1②）</summary>
        public void UpdateProfileFromToolUse(string toolName, string argsJson, bool success)
        {
            if (string.IsNullOrEmpty(toolName)) return;
            var p = Profile;
            if (p.ToolUsage == null) p.ToolUsage = new Dictionary<string, int>();
            if (!p.ToolUsage.ContainsKey(toolName)) p.ToolUsage[toolName] = 0;
            p.ToolUsage[toolName]++;
            if (success)
            {
                if (p.SuccessTools == null) p.SuccessTools = new Dictionary<string, int>();
                if (!p.SuccessTools.ContainsKey(toolName)) p.SuccessTools[toolName] = 0;
                p.SuccessTools[toolName]++;
            }
            // 从参数提取 E3D 典型元件类型偏好（PIPE/EQUIP/VALVE/NOZZLE 等）
            if (!string.IsNullOrEmpty(argsJson))
            {
                foreach (var kw in new[] { "PIPE", "EQUIP", "VALVE", "NOZZLE", "STRUCTURE", "CABLE", "DUCT" })
                {
                    if (argsJson.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (p.PreferredElements == null) p.PreferredElements = new List<string>();
                        if (!p.PreferredElements.Contains(kw)) p.PreferredElements.Add(kw);
                    }
                }
            }
            p.LastActive = DateTime.UtcNow.ToString("o");
            SaveProfile();
        }

        /// <summary>构建注入 SystemPrompt 的上下文：用户画像 + 项目知识库（C1④）</summary>
        public string GetSystemPromptContext()
        {
            var sb = new System.Text.StringBuilder();
            var p = Profile;
            bool hasProfile = (p.ToolUsage != null && p.ToolUsage.Count > 0)
                || (p.PreferredElements != null && p.PreferredElements.Count > 0);
            if (hasProfile)
            {
                sb.AppendLine("<user_profile>");
                if (p.PreferredElements != null && p.PreferredElements.Count > 0)
                    sb.AppendLine("  Preferred elements: " + string.Join(", ", p.PreferredElements));
                if (p.ToolUsage != null && p.ToolUsage.Count > 0)
                {
                    var top = new List<KeyValuePair<string, int>>(p.ToolUsage);
                    top.Sort((a, b) => b.Value.CompareTo(a.Value));
                    var topTools = top.GetRange(0, Math.Min(5, top.Count));
                    sb.AppendLine("  Frequently used tools: " + string.Join(", ",
                        System.Array.ConvertAll(topTools.ToArray(), kv => kv.Key + "(" + kv.Value + ")")));
                }
                sb.AppendLine("</user_profile>");
            }

            // 项目知识库：固定目录 data/knowledge 下的 .md/.txt
            try
            {
                var knowledgeDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "E3DCopilot", "knowledge");
                if (Directory.Exists(knowledgeDir))
                {
                    var all = new List<string>(Directory.GetFiles(knowledgeDir, "*.md"));
                    all.AddRange(Directory.GetFiles(knowledgeDir, "*.txt"));
                    if (all.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("<project_knowledge>");
                        int budget = 4000;
                        foreach (var f in all)
                        {
                            var text = File.ReadAllText(f);
                            if (text.Length > 2000) text = text.Substring(0, 2000) + "...";
                            if (budget <= 0) break;
                            sb.AppendLine("### " + Path.GetFileName(f));
                            sb.AppendLine(text);
                            budget -= text.Length;
                        }
                        sb.AppendLine("</project_knowledge>");
                    }
                }
            }
            catch { }

            return sb.ToString().TrimEnd();
        }

        public void Dispose()
        {
            SaveProfile();
            _connection?.Close();
            _connection?.Dispose();
        }
    }

    /// <summary>用户画像（C1②）：随工具调用累积，跨会话持久化</summary>
    public class UserProfile
    {
        public Dictionary<string, int> ToolUsage;
        public Dictionary<string, int> SuccessTools;
        public List<string> PreferredElements;
        public string Language;
        public string LastActive;
    }
}
