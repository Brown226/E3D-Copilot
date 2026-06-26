using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using E3DCopilot.Core.Logging;
using E3DCopilot.Core.Providers;
using Newtonsoft.Json;

namespace E3DCopilot.Core
{
    /// <summary>
    /// 会话持久化存储 — JSONL 格式，原子写入，崩溃恢复
    ///
    /// 设计参考 Reasonix internal/agent/save.go:
    ///   - JSONL 格式：每行一条消息，部分写入只损坏最后一行
    ///   - 原子写入：先写 .tmp 再 File.Replace，崩溃不破坏已有文件
    ///   - 崩溃标记：.active 哨兵文件，turn 开始时创建、结束时删除
    ///   - HasContent 检查：空会话（仅 system prompt）不持久化
    /// </summary>
    public class SessionStore
    {
        private readonly string _sessionsDir;

        public SessionStore(string sessionsDir = null)
        {
            _sessionsDir = sessionsDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "E3DCopilot", "sessions");

            if (!Directory.Exists(_sessionsDir))
                Directory.CreateDirectory(_sessionsDir);
        }

        public string SessionsDir => _sessionsDir;

        // ═══════════════════════════════════════════════════════════
        //  Save — 原子写入 JSONL（对齐 Reasonix Session.Save）
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 将会话消息持久化为 JSONL 文件。
        /// 使用 tmp + File.Replace 原子写入，崩溃不会破坏已有文件。
        /// </summary>
        public void Save(CopilotSession session, string path = null)
        {
            if (session == null || !HasContent(session)) return;

            path = path ?? session.SessionPath ?? NewSessionPath();
            session.SessionPath = path;

            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // 写入 .tmp 文件
                string tmpPath = path + ".tmp";
                using (var writer = new StreamWriter(tmpPath, false, System.Text.Encoding.UTF8))
                {
                    // 快照消息列表（线程安全复制，对齐 Reasonix Snapshot()）
                    List<ChatMessage> snapshot;
                    lock (session.Messages)
                    {
                        snapshot = new List<ChatMessage>(session.Messages);
                    }

                    foreach (var msg in snapshot)
                    {
                        var dto = ToDto(msg);
                        string line = JsonConvert.SerializeObject(dto, Formatting.None);
                        writer.WriteLine(line);
                    }
                }

                // 原子替换：File.Replace 在 NTFS 上是原子操作（.NET 2.0+）
                if (File.Exists(path))
                    File.Replace(tmpPath, path, null);
                else
                    File.Move(tmpPath, path);
            }
            catch (Exception ex)
            {
                CopilotLogger.Error(ex, "SessionStore.Save failed: {0}", path);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Load — 从 JSONL 恢复会话（对齐 Reasonix LoadSession）
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 从 JSONL 文件加载会话。文件不存在返回 null。
        /// </summary>
        public CopilotSession Load(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                var session = new CopilotSession { SessionPath = path };

                using (var reader = new StreamReader(path, System.Text.Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var dto = JsonConvert.DeserializeObject<SessionMessageDto>(line);
                        if (dto == null) continue;

                        var msg = FromDto(dto);
                        session.Messages.Add(msg);
                    }
                }

                return session;
            }
            catch (Exception ex)
            {
                CopilotLogger.Error(ex, "SessionStore.Load failed: {0}", path);
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  崩溃标记 — .active 哨兵文件
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 标记会话为"活跃中"（turn 开始时调用）。
        /// 写入 .active 哨兵文件，正常结束时由 MarkClean 删除。
        /// </summary>
        public void MarkActive(string sessionPath)
        {
            if (string.IsNullOrEmpty(sessionPath)) return;
            try
            {
                string markerPath = sessionPath + ".active";
                File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            }
            catch { /* 哨兵文件写入失败不影响主流程 */ }
        }

        /// <summary>
        /// 标记会话为"已正常结束"（turn 完成时调用）。
        /// 删除 .active 哨兵文件。
        /// </summary>
        public void MarkClean(string sessionPath)
        {
            if (string.IsNullOrEmpty(sessionPath)) return;
            try
            {
                string markerPath = sessionPath + ".active";
                if (File.Exists(markerPath))
                    File.Delete(markerPath);
            }
            catch { /* 删除失败不影响主流程 */ }
        }

        /// <summary>
        /// 检查是否有崩溃时未正常结束的会话。
        /// 返回 .active 标记仍然存在的会话路径列表。
        /// </summary>
        public List<CrashedSessionInfo> GetCrashedSessions()
        {
            var result = new List<CrashedSessionInfo>();
            try
            {
                foreach (var markerPath in Directory.GetFiles(_sessionsDir, "*.jsonl.active"))
                {
                    string sessionPath = markerPath.Substring(0, markerPath.Length - ".active".Length);
                    if (!File.Exists(sessionPath)) continue;

                    var info = new CrashedSessionInfo
                    {
                        SessionPath = sessionPath,
                        Preview = GetPreview(sessionPath),
                        TurnCount = CountUserTurns(sessionPath),
                        CrashTime = File.GetLastWriteTimeUtc(markerPath)
                    };
                    result.Add(info);
                }
            }
            catch { /* 列举失败返回空列表 */ }
            return result;
        }

        // ═══════════════════════════════════════════════════════════
        //  ListSessions — 列出所有已保存会话（对齐 Reasonix ListSessions）
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 列出所有已保存会话，按最后修改时间降序排列。
        /// 排除崩溃未恢复的会话（由 GetCrashedSessions 单独处理）。
        /// </summary>
        public List<SavedSessionInfo> ListSessions()
        {
            var result = new List<SavedSessionInfo>();
            try
            {
                foreach (var path in Directory.GetFiles(_sessionsDir, "*.jsonl"))
                {
                    // 跳过有 .active 标记的（崩溃会话，单独处理）
                    if (File.Exists(path + ".active")) continue;

                    var info = new FileInfo(path);
                    result.Add(new SavedSessionInfo
                    {
                        SessionPath = path,
                        Preview = GetPreview(path),
                        TurnCount = CountUserTurns(path),
                        LastModified = info.LastWriteTime,
                        SizeBytes = info.Length
                    });
                }
            }
            catch { /* 列举失败返回空列表 */ }

            return result.OrderByDescending(s => s.LastModified).ToList();
        }

        /// <summary>
        /// 删除指定会话文件及其哨兵文件。
        /// </summary>
        public void Delete(string sessionPath)
        {
            try
            {
                if (File.Exists(sessionPath))
                    File.Delete(sessionPath);
                string marker = sessionPath + ".active";
                if (File.Exists(marker))
                    File.Delete(marker);
            }
            catch (Exception ex)
            {
                CopilotLogger.Error(ex, "SessionStore.Delete failed: {0}", sessionPath);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  辅助方法
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 生成新的会话文件路径（时间戳命名，对齐 Reasonix NewSessionPath）
        /// </summary>
        public string NewSessionPath()
        {
            string ts = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            return Path.Combine(_sessionsDir, $"{ts}-{Guid.NewGuid().ToString("N").Substring(0, 6)}.jsonl");
        }

        /// <summary>
        /// 检查会话是否有可持久化的内容（对齐 Reasonix HasContent）
        /// </summary>
        private static bool HasContent(CopilotSession session)
        {
            foreach (var msg in session.Messages)
            {
                if (msg.Role != MessageRole.System)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 提取会话预览（第一条用户消息，截断到 80 字符）
        /// </summary>
        private static string GetPreview(string path)
        {
            try
            {
                using (var reader = new StreamReader(path, System.Text.Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var dto = JsonConvert.DeserializeObject<SessionMessageDto>(line);
                        if (dto != null && dto.Role == "user")
                        {
                            string preview = dto.Content ?? "";
                            if (preview.Length > 80)
                                preview = preview.Substring(0, 77) + "...";
                            return preview;
                        }
                    }
                }
            }
            catch { /* 读取失败返回空字符串 */ }
            return "";
        }

        /// <summary>
        /// 统计用户消息轮次数（对齐 Reasonix previewSession 的 turns 计数）
        /// </summary>
        private static int CountUserTurns(string path)
        {
            try
            {
                int count = 0;
                using (var reader = new StreamReader(path, System.Text.Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var dto = JsonConvert.DeserializeObject<SessionMessageDto>(line);
                        if (dto != null && dto.Role == "user")
                            count++;
                    }
                }
                return count;
            }
            catch { return 0; }
        }

        // ═══════════════════════════════════════════════════════════
        //  DTO 序列化 — 不存储 Images（base64 太大，恢复时不需要）
        // ═══════════════════════════════════════════════════════════

        private static SessionMessageDto ToDto(ChatMessage msg)
        {
            return new SessionMessageDto
            {
                Role = RoleToString(msg.Role),
                Content = msg.Content,
                ToolCalls = msg.ToolCalls,
                ToolCallId = msg.ToolCallId
                // 故意不存 Images：base64 图片太大，恢复时不需要
            };
        }

        private static ChatMessage FromDto(SessionMessageDto dto)
        {
            return new ChatMessage
            {
                Role = StringToRole(dto.Role),
                Content = dto.Content,
                ToolCalls = dto.ToolCalls,
                ToolCallId = dto.ToolCallId
            };
        }

        private static string RoleToString(MessageRole role)
        {
            switch (role)
            {
                case MessageRole.System: return "system";
                case MessageRole.User: return "user";
                case MessageRole.Assistant: return "assistant";
                case MessageRole.Tool: return "tool";
                default: return "user";
            }
        }

        private static MessageRole StringToRole(string role)
        {
            switch (role)
            {
                case "system": return MessageRole.System;
                case "user": return MessageRole.User;
                case "assistant": return MessageRole.Assistant;
                case "tool": return MessageRole.Tool;
                default: return MessageRole.User;
            }
        }
    }

    /// <summary>
    /// JSONL 序列化 DTO — 扁平结构，不包含 Images
    /// </summary>
    internal class SessionMessageDto
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("toolCalls", NullValueHandling = NullValueHandling.Ignore)]
        public List<ToolCall> ToolCalls { get; set; }

        [JsonProperty("toolCallId", NullValueHandling = NullValueHandling.Ignore)]
        public string ToolCallId { get; set; }
    }

    /// <summary>
    /// 已保存会话的摘要信息（供历史面板使用）
    /// </summary>
    public class SavedSessionInfo
    {
        public string SessionPath { get; set; }
        public string Preview { get; set; }
        public int TurnCount { get; set; }
        public DateTime LastModified { get; set; }
        public long SizeBytes { get; set; }
    }

    /// <summary>
    /// 崩溃会话信息（供恢复提示使用）
    /// </summary>
    public class CrashedSessionInfo
    {
        public string SessionPath { get; set; }
        public string Preview { get; set; }
        public int TurnCount { get; set; }
        public DateTime CrashTime { get; set; }
    }
}
