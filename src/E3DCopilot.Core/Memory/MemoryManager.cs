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

        public void Dispose()
        {
            _connection?.Close();
            _connection?.Dispose();
        }
    }
}
