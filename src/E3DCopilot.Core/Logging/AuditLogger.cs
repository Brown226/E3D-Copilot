using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace E3DCopilot.Core.Logging
{
    /// <summary>
    /// 操作审计日志 — 记录所有写操作的完整轨迹
    /// 
    /// 目的：
    /// 1. 问题追溯：出问题时能查"谁在什么时候改了什么"
    /// 2. 操作回滚：记录 before/after 快照，支持撤销
    /// 3. 合规审计：满足工程变更管理的审计要求
    /// 
    /// 日志格式：JSONL（每行一个 JSON 对象），便于追加和解析
    /// </summary>
    public class AuditLogger
    {
        private readonly string _logDirectory;
        private readonly object _writeLock = new object();
        private readonly bool _enabled;

        // 单文件最大大小（10MB），超出后轮转
        private const long MaxFileSize = 10 * 1024 * 1024;

        /// <summary>
        /// 创建审计日志记录器
        /// </summary>
        /// <param name="logDirectory">日志目录（默认 %LOCALAPPDATA%/.e3dcopilot/audit）</param>
        /// <param name="enabled">是否启用（从配置读取）</param>
        public AuditLogger(string logDirectory = null, bool enabled = true)
        {
            _enabled = enabled;
            _logDirectory = logDirectory ?? GetDefaultLogDirectory();

            if (_enabled)
            {
                try
                {
                    Directory.CreateDirectory(_logDirectory);
                }
                catch
                {
                    // 目录创建失败时禁用日志
                    _enabled = false;
                }
            }
        }

        private static string GetDefaultLogDirectory()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, ".e3dcopilot", "audit");
        }

        /// <summary>
        /// 记录写操作
        /// </summary>
        public void LogWriteOperation(WriteOperationEntry entry)
        {
            if (!_enabled || entry == null) return;

            try
            {
                entry.Timestamp = DateTime.Now;
                entry.SessionId = entry.SessionId ?? "unknown";

                var json = JsonConvert.SerializeObject(entry, Formatting.None);
                AppendToFile(json);
            }
            catch
            {
                // 审计日志失败不影响主流程
            }
        }

        /// <summary>
        /// 记录工具调用
        /// </summary>
        public void LogToolCall(
            string toolName,
            string args,
            bool success,
            string result = null,
            string sessionId = null,
            string elementTarget = null)
        {
            if (!_enabled) return;

            var entry = new WriteOperationEntry
            {
                OperationType = "tool_call",
                ToolName = toolName,
                Target = elementTarget,
                Arguments = TruncateForLog(args, 2000),
                Success = success,
                Result = TruncateForLog(result, 1000),
                SessionId = sessionId
            };

            LogWriteOperation(entry);
        }

        /// <summary>
        /// 记录属性修改（含 before/after）
        /// </summary>
        public void LogAttributeChange(
            string elementName,
            string attributeName,
            string oldValue,
            string newValue,
            string sessionId = null)
        {
            if (!_enabled) return;

            var entry = new WriteOperationEntry
            {
                OperationType = "attribute_change",
                Target = elementName,
                AttributeName = attributeName,
                OldValue = oldValue,
                NewValue = newValue,
                Success = true,
                SessionId = sessionId
            };

            LogWriteOperation(entry);
        }

        /// <summary>
        /// 记录 PML 脚本执行
        /// </summary>
        public void LogPmlExecution(
            string script,
            string result,
            bool success,
            string sessionId = null)
        {
            if (!_enabled) return;

            var entry = new WriteOperationEntry
            {
                OperationType = "pml_execution",
                PmlScript = TruncateForLog(script, 5000),
                Result = TruncateForLog(result, 1000),
                Success = success,
                SessionId = sessionId
            };

            LogWriteOperation(entry);
        }

        /// <summary>
        /// 查询指定元素的修改历史
        /// </summary>
        public List<WriteOperationEntry> GetElementHistory(string elementName, int maxEntries = 50)
        {
            var history = new List<WriteOperationEntry>();
            if (!_enabled || string.IsNullOrEmpty(elementName)) return history;

            try
            {
                var logFile = GetCurrentLogFile();
                if (!File.Exists(logFile)) return history;

                var lines = File.ReadAllLines(logFile);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var entry = JsonConvert.DeserializeObject<WriteOperationEntry>(line);
                        if (entry != null && 
                            !string.IsNullOrEmpty(entry.Target) &&
                            entry.Target.IndexOf(elementName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            history.Add(entry);
                            if (history.Count >= maxEntries) break;
                        }
                    }
                    catch { /* 跳过解析失败的行 */ }
                }
            }
            catch { /* 查询失败返回空列表 */ }

            history.Reverse(); // 最新的在前
            return history;
        }

        private void AppendToFile(string json)
        {
            lock (_writeLock)
            {
                var logFile = GetCurrentLogFile();

                // 检查文件大小，超出则轮转
                if (File.Exists(logFile))
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.Length > MaxFileSize)
                    {
                        RotateLogFile(logFile);
                    }
                }

                File.AppendAllText(logFile, json + Environment.NewLine, Encoding.UTF8);
            }
        }

        private string GetCurrentLogFile()
        {
            var dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            return Path.Combine(_logDirectory, $"audit-{dateStr}.jsonl");
        }

        private void RotateLogFile(string currentFile)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var rotatedFile = currentFile.Replace(".jsonl", $"-{timestamp}.old");
                File.Move(currentFile, rotatedFile);

                // 清理超过 30 天的旧日志
                CleanupOldLogs();
            }
            catch { /* 轮转失败继续写当前文件 */ }
        }

        private void CleanupOldLogs()
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-30);
                var files = Directory.GetFiles(_logDirectory, "audit-*.old");
                foreach (var file in files)
                {
                    if (File.GetCreationTime(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch { /* 清理失败忽略 */ }
        }

        private static string TruncateForLog(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;
            return value.Substring(0, maxLength) + "...[truncated]";
        }
    }

    /// <summary>
    /// 写操作审计条目
    /// </summary>
    public class WriteOperationEntry
    {
        /// <summary>操作时间戳</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>操作类型：tool_call / attribute_change / pml_execution</summary>
        public string OperationType { get; set; }

        /// <summary>会话 ID</summary>
        public string SessionId { get; set; }

        /// <summary>工具名称</summary>
        public string ToolName { get; set; }

        /// <summary>目标元素</summary>
        public string Target { get; set; }

        /// <summary>属性名（属性修改时）</summary>
        public string AttributeName { get; set; }

        /// <summary>修改前的值</summary>
        public string OldValue { get; set; }

        /// <summary>修改后的值</summary>
        public string NewValue { get; set; }

        /// <summary>工具参数</summary>
        public string Arguments { get; set; }

        /// <summary>PML 脚本</summary>
        public string PmlScript { get; set; }

        /// <summary>执行结果</summary>
        public string Result { get; set; }

        /// <summary>是否成功</summary>
        public bool Success { get; set; }

        /// <summary>错误信息（失败时）</summary>
        public string Error { get; set; }
    }
}
