using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using E3DCopilot.Core.Logging;
using E3DCopilot.Core.Providers;
using E3DCopilot.Core.Tools;
using Newtonsoft.Json;

namespace E3DCopilot.Core
{
    /// <summary>
    /// 操作前快照管理器 — 基于 turn 的 E3D 修改操作安全网
    ///
    /// 设计参考 Reasonix internal/checkpoint/checkpoint.go:
    ///   - 按 turn 分组：每个用户对话轮次一个 checkpoint 文件
    ///   - modify 操作：记录旧值，可自动回滚
    ///   - execute_pml 操作：记录脚本全文，供手动审查/回滚
    ///   - 文件快照：写文件前备份原内容，支持回滚
    ///   - 持久化到磁盘：JSON 文件 + 快照文件，崩溃后仍可恢复
    /// </summary>
    public class CheckpointManager
    {
        private readonly string _checkpointsDir;
        private readonly string _snapshotsDir;
        private Checkpoint _current;
        private readonly object _lock = new object();
        private readonly List<Checkpoint> _completed = new List<Checkpoint>();

        public CheckpointManager(string dir = null)
        {
            _checkpointsDir = dir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "E3DCopilot", "checkpoints");
            _snapshotsDir = Path.Combine(_checkpointsDir, "snapshots");
            if (!Directory.Exists(_checkpointsDir))
                Directory.CreateDirectory(_checkpointsDir);
            if (!Directory.Exists(_snapshotsDir))
                Directory.CreateDirectory(_snapshotsDir);
        }

        /// <summary>
        /// 开始一个新的用户 turn checkpoint（对齐 Reasonix Store.Begin）
        /// </summary>
        public void BeginTurn(int turn, string prompt)
        {
            lock (_lock)
            {
                if (_current != null)
                {
                    _current.CompletedAt = DateTime.UtcNow;
                    PersistCheckpoint(_current);
                    _completed.Add(_current);
                }
                _current = new Checkpoint
                {
                    Id = $"ckpt_{DateTime.UtcNow:yyyyMMddHHmmss}_{turn}",
                    Turn = turn,
                    Prompt = Truncate(prompt, 200),
                    StartedAt = DateTime.UtcNow,
                    Operations = new List<CheckpointOperation>()
                };
            }
        }

        /// <summary>
        /// 写文件前快照 — 备份原文件内容（对齐 Reasonix onPreEdit）
        /// </summary>
        public string SnapshotFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                string content = File.ReadAllText(filePath);
                string snapId = $"snap_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
                string snapPath = Path.Combine(_snapshotsDir, snapId);
                File.WriteAllText(snapPath, content);

                // 记录快照引用到当前操作
                lock (_lock)
                {
                    if (_current != null)
                    {
                        _current.Operations.Add(new CheckpointOperation
                        {
                            ToolName = "__snapshot__",
                            Args = snapId,
                            Timestamp = DateTime.UtcNow,
                            SnapshotPath = snapPath,
                            SnapshotOriginalPath = filePath
                        });
                    }
                }

                return snapId;
            }
            catch { return null; }
        }

        /// <summary>
        /// 回滚指定 checkpoint 中所有快照文件（对齐 Reasonix rewind）
        /// </summary>
        public int RollbackCheckpoint(string checkpointId)
        {
            int restored = 0;
            try
            {
                var cp = LoadCheckpoint(checkpointId);
                if (cp?.Operations == null) return 0;

                foreach (var op in cp.Operations)
                {
                    if (op.ToolName == "__snapshot__" &&
                        !string.IsNullOrEmpty(op.SnapshotPath) &&
                        !string.IsNullOrEmpty(op.SnapshotOriginalPath))
                    {
                        try
                        {
                            if (File.Exists(op.SnapshotPath))
                            {
                                File.Copy(op.SnapshotPath, op.SnapshotOriginalPath, overwrite: true);
                                restored++;
                            }
                        }
                        catch { /* 跳过无法恢复的文件 */ }
                    }
                }
            }
            catch { }
            return restored;
        }

        /// <summary>
        /// 回滚最近一个 checkpoint
        /// </summary>
        public int RollbackLatest()
        {
            var latest = GetLatestCheckpoint();
            if (latest == null) return 0;
            return RollbackCheckpoint(latest.Id);
        }

        /// <summary>
        /// 记录一次写操作（在工具执行后调用，对齐 Reasonix Snapshot）
        /// </summary>
        public void RecordOperation(string toolName, string args,
            UndoEntry undoInfo = null, string pmlScript = null)
        {
            lock (_lock)
            {
                if (_current == null) return;
                // 过滤 __snapshot__ 操作（已在 SnapshotFile 中记录）
                if (toolName == "__snapshot__") return;
                _current.Operations.Add(new CheckpointOperation
                {
                    ToolName = toolName,
                    Args = Truncate(args, 2000),
                    Timestamp = DateTime.UtcNow,
                    UndoInfo = undoInfo,
                    PmlScript = Truncate(pmlScript, 10000)
                });
            }
        }

        /// <summary>
        /// 完成当前 turn 的 checkpoint（持久化到磁盘）
        /// </summary>
        public void CompleteTurn()
        {
            lock (_lock)
            {
                if (_current == null) return;
                if (_current.Operations.Count > 0)
                {
                    _current.CompletedAt = DateTime.UtcNow;
                    PersistCheckpoint(_current);
                    _completed.Add(_current);
                }
                _current = null;
            }
        }

        /// <summary>
        /// 列出所有 checkpoint 摘要（对齐 Reasonix Store.List）
        /// </summary>
        public List<CheckpointMeta> ListCheckpoints()
        {
            var result = new List<CheckpointMeta>();
            try
            {
                foreach (var path in Directory.GetFiles(_checkpointsDir, "ckpt_*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(path);
                        var cp = JsonConvert.DeserializeObject<Checkpoint>(json);
                        if (cp == null) continue;
                        result.Add(new CheckpointMeta
                        {
                            Id = cp.Id,
                            Turn = cp.Turn,
                            Prompt = cp.Prompt,
                            StartedAt = cp.StartedAt,
                            OperationCount = cp.Operations?.Count ?? 0,
                            FilePath = path
                        });
                    }
                    catch { /* 跳过损坏文件 */ }
                }
            }
            catch { /* 列举失败返回空列表 */ }
            return result.OrderByDescending(c => c.StartedAt).ToList();
        }

        /// <summary>
        /// 加载指定 checkpoint 的完整内容（含操作详情）
        /// </summary>
        public Checkpoint LoadCheckpoint(string id)
        {
            try
            {
                var path = Directory.GetFiles(_checkpointsDir, $"{id}.json").FirstOrDefault();
                if (path == null) return null;
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<Checkpoint>(json);
            }
            catch { return null; }
        }

        /// <summary>
        /// 获取最近一个可回滚的 checkpoint（对齐 Reasonix rewind 逻辑）
        /// </summary>
        public Checkpoint GetLatestCheckpoint()
        {
            var list = ListCheckpoints();
            if (list.Count == 0) return null;
            return LoadCheckpoint(list[0].Id);
        }

        /// <summary>
        /// 删除指定 checkpoint
        /// </summary>
        public void Delete(string id)
        {
            try
            {
                var path = Path.Combine(_checkpointsDir, $"{id}.json");
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                CopilotLogger.Error(ex, "CheckpointManager.Delete failed: {0}", id);
            }
        }

        /// <summary>
        /// 清理超过 maxCount 个的旧 checkpoint
        /// </summary>
        public void Cleanup(int maxCount = 20)
        {
            try
            {
                var files = Directory.GetFiles(_checkpointsDir, "ckpt_*.json")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .Skip(maxCount)
                    .ToList();
                foreach (var f in files)
                {
                    try { f.Delete(); } catch { }
                }
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════
        //  内部方法
        // ═══════════════════════════════════════════════════════════

        private void PersistCheckpoint(Checkpoint cp)
        {
            try
            {
                string path = Path.Combine(_checkpointsDir, $"{cp.Id}.json");
                string json = JsonConvert.SerializeObject(cp, Formatting.Indented);
                // 原子写入：tmp + Replace
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(path))
                    File.Replace(tmp, path, null);
                else
                    File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                CopilotLogger.Error(ex, "CheckpointManager.Persist failed: {0}", cp.Id);
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length > max ? s.Substring(0, max) + "..." : s;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  数据模型
    // ═══════════════════════════════════════════════════════════

    public class Checkpoint
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("turn")]
        public int Turn { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("startedAt")]
        public DateTime StartedAt { get; set; }

        [JsonProperty("completedAt")]
        public DateTime CompletedAt { get; set; }

        [JsonProperty("operations")]
        public List<CheckpointOperation> Operations { get; set; }
    }

    public class CheckpointOperation
    {
        [JsonProperty("toolName")]
        public string ToolName { get; set; }

        [JsonProperty("args")]
        public string Args { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// modify 操作的撤销信息（element + attribute + oldValue）
        /// 非空时可通过 UndoRedoManager 自动回滚
        /// </summary>
        [JsonProperty("undoInfo", NullValueHandling = NullValueHandling.Ignore)]
        public UndoEntry UndoInfo { get; set; }

        /// <summary>
        /// execute_pml 操作的脚本全文（供手动审查/回滚）
        /// </summary>
        [JsonProperty("pmlScript", NullValueHandling = NullValueHandling.Ignore)]
        public string PmlScript { get; set; }

        /// <summary>
        /// 文件快照路径（WriteFile/EditFile 操作前备份）
        /// </summary>
        [JsonProperty("snapshotPath", NullValueHandling = NullValueHandling.Ignore)]
        public string SnapshotPath { get; set; }

        /// <summary>
        /// 快照对应的原始文件路径
        /// </summary>
        [JsonProperty("snapshotOriginalPath", NullValueHandling = NullValueHandling.Ignore)]
        public string SnapshotOriginalPath { get; set; }
    }

    public class CheckpointMeta
    {
        public string Id { get; set; }
        public int Turn { get; set; }
        public string Prompt { get; set; }
        public DateTime StartedAt { get; set; }
        public int OperationCount { get; set; }
        public string FilePath { get; set; }
    }
}
