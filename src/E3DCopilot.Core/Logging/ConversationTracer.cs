using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace E3DCopilot.Core.Logging
{
    /// <summary>
    /// 对话执行轨迹记录器 — 自动采集 AgentLoop 每步执行数据，
    /// 对话结束后输出结构化 JSON trace 文件，供 AI 诊断分析。
    /// 
    /// 使用方式：
    ///   1. CopilotController 构造时创建实例
    ///   2. AgentLoop 每步调用 BeginStep / RecordXxx / EndStep
    ///   3. 对话结束调用 EndTurn，自动写入 trace 文件
    /// 
    /// 输出路径: %LOCALAPPDATA%/E3DCopilot/traces/trace-{timestamp}-{sessionId}.json
    /// </summary>
    public class ConversationTracer
    {
        private readonly string _traceDir;
        private readonly bool _enabled;
        private readonly int _retentionDays;
        private readonly object _lock = new object();

        // ── 当前 turn 状态 ──
        private ConversationTrace _current;
        private TraceStep _currentStep;
        private Stopwatch _stepWatch;
        private readonly Dictionary<string, TraceToolCall> _pendingTools = new Dictionary<string, TraceToolCall>();

        // ── 截断保护 ──
        private const int MaxReasoningChars = 8000;
        private const int MaxTextChars = 4000;
        private const int MaxToolResultChars = 2000;
        private const int MaxArgumentsChars = 2000;

        /// <summary>
        /// 创建轨迹记录器
        /// </summary>
        /// <param name="traceDir">trace 文件目录（默认 %LOCALAPPDATA%/E3DCopilot/traces）</param>
        /// <param name="enabled">是否启用</param>
        /// <param name="retentionDays">trace 文件保留天数</param>
        public ConversationTracer(string traceDir = null, bool enabled = true, int retentionDays = 7)
        {
            _enabled = enabled;
            _retentionDays = retentionDays;
            _traceDir = traceDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "E3DCopilot", "traces");

            if (_enabled)
            {
                try
                {
                    Directory.CreateDirectory(_traceDir);
                }
                catch
                {
                    // 目录创建失败时禁用
                }
            }
        }

        /// <summary>是否启用</summary>
        public bool IsEnabled => _enabled;

        /// <summary>Trace 文件目录</summary>
        public string TraceDir => _traceDir;

        // ═══════════════════════════════════════════════════════════
        //  Turn 生命周期
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 开始一轮对话（AgentLoop.RunAsync 入口调用）
        /// </summary>
        public void BeginTurn(string sessionId, string userInput, string model)
        {
            if (!_enabled) return;

            lock (_lock)
            {
                _current = new ConversationTrace
                {
                    SessionId = sessionId,
                    StartTime = DateTime.Now,
                    UserInput = Truncate(userInput, 500),
                    Model = model
                };
                _pendingTools.Clear();
            }
        }

        /// <summary>
        /// 结束一轮对话，写入 trace 文件
        /// </summary>
        /// <param name="outcome">结果: success / error / cancelled / max_steps</param>
        public void EndTurn(string outcome)
        {
            if (!_enabled || _current == null) return;

            lock (_lock)
            {
                try
                {
                    // 如果有未关闭的 step，先关闭
                    if (_currentStep != null)
                    {
                        FinalizeStep();
                    }

                    _current.EndTime = DateTime.Now;
                    _current.Outcome = outcome;
                    _current.TotalSteps = _current.Steps.Count;

                    // 汇总 Token
                    foreach (var step in _current.Steps)
                    {
                        if (step.Tokens != null)
                        {
                            _current.TotalTokens.Prompt += step.Tokens.Prompt;
                            _current.TotalTokens.Completion += step.Tokens.Completion;
                        }
                    }

                    // 写入文件
                    WriteTraceFile(_current);
                }
                catch (Exception ex)
                {
                    CopilotLogger.Warn("ConversationTracer.EndTurn 写入失败: {0}", ex.Message);
                }
                finally
                {
                    _current = null;
                    _currentStep = null;
                    _pendingTools.Clear();
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Step 生命周期
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 开始新步骤（每轮 LLM 调用前）
        /// </summary>
        public void BeginStep(int stepNumber)
        {
            if (!_enabled || _current == null) return;

            lock (_lock)
            {
                // 关闭上一个 step
                if (_currentStep != null)
                {
                    FinalizeStep();
                }

                _currentStep = new TraceStep
                {
                    Step = stepNumber + 1, // 对外显示从 1 开始
                    Timestamp = DateTime.Now
                };
                _stepWatch = Stopwatch.StartNew();
                _pendingTools.Clear();
            }
        }

        /// <summary>
        /// 结束当前步骤
        /// </summary>
        public void EndStep()
        {
            if (!_enabled || _currentStep == null) return;

            lock (_lock)
            {
                FinalizeStep();
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  数据采集方法
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 记录 LLM 思考链（reasoning_content）
        /// </summary>
        public void RecordReasoning(string reasoning)
        {
            if (!_enabled || _currentStep == null || string.IsNullOrEmpty(reasoning)) return;

            lock (_lock)
            {
                if (_currentStep.Reasoning == null)
                    _currentStep.Reasoning = reasoning;
                else
                    _currentStep.Reasoning += reasoning;

                // 截断保护
                if (_currentStep.Reasoning.Length > MaxReasoningChars)
                    _currentStep.Reasoning = _currentStep.Reasoning.Substring(0, MaxReasoningChars) + "...[truncated]";
            }
        }

        /// <summary>
        /// 记录 LLM 输出文本
        /// </summary>
        public void RecordText(string text)
        {
            if (!_enabled || _currentStep == null || string.IsNullOrEmpty(text)) return;

            lock (_lock)
            {
                if (_currentStep.Text == null)
                    _currentStep.Text = text;
                else
                    _currentStep.Text += text;

                if (_currentStep.Text.Length > MaxTextChars)
                    _currentStep.Text = _currentStep.Text.Substring(0, MaxTextChars) + "...[truncated]";
            }
        }

        /// <summary>
        /// 记录工具调用开始
        /// </summary>
        public void RecordToolStart(string id, string name, string arguments)
        {
            if (!_enabled || _currentStep == null) return;

            lock (_lock)
            {
                var toolCall = new TraceToolCall
                {
                    Id = id,
                    Name = name,
                    Arguments = Truncate(arguments, MaxArgumentsChars)
                };
                _pendingTools[id] = toolCall;
                _currentStep.ToolCalls.Add(toolCall);
            }
        }

        /// <summary>
        /// 记录工具调用结束
        /// </summary>
        public void RecordToolEnd(string id, string result, bool success, long durationMs, string error)
        {
            if (!_enabled || _currentStep == null) return;

            lock (_lock)
            {
                TraceToolCall toolCall;
                if (_pendingTools.TryGetValue(id, out toolCall))
                {
                    toolCall.Result = Truncate(result, MaxToolResultChars);
                    toolCall.Success = success;
                    toolCall.DurationMs = durationMs;
                    toolCall.Error = error;
                    _pendingTools.Remove(id);
                }
            }
        }

        /// <summary>
        /// 记录 Token 用量
        /// </summary>
        public void RecordTokens(int promptTokens, int completionTokens)
        {
            if (!_enabled || _currentStep == null) return;

            lock (_lock)
            {
                if (_currentStep.Tokens == null)
                    _currentStep.Tokens = new TraceTokenUsage();

                _currentStep.Tokens.Prompt = promptTokens;
                _currentStep.Tokens.Completion = completionTokens;
            }
        }

        /// <summary>
        /// 记录步骤内事件（Notice 级别）
        /// </summary>
        public void RecordEvent(string eventName)
        {
            if (!_enabled || string.IsNullOrEmpty(eventName)) return;

            lock (_lock)
            {
                if (_currentStep != null)
                {
                    if (_currentStep.Events == null)
                        _currentStep.Events = new List<string>();
                    _currentStep.Events.Add(eventName);
                }
                else if (_current != null)
                {
                    // 没有活跃 step 时归入系统事件
                    _current.SystemEvents.Add(eventName);
                }
            }
        }

        /// <summary>
        /// 记录系统级事件（压缩/恢复/循环守卫等）
        /// </summary>
        public void RecordSystemEvent(string eventName)
        {
            if (!_enabled || _current == null || string.IsNullOrEmpty(eventName)) return;

            lock (_lock)
            {
                _current.SystemEvents.Add(eventName);
            }
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        public void RecordError(string error)
        {
            if (!_enabled || _current == null || string.IsNullOrEmpty(error)) return;

            lock (_lock)
            {
                _current.Errors.Add(error);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  查询方法
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 获取最近的 trace 文件路径
        /// </summary>
        public string GetLatestTracePath()
        {
            if (!_enabled || !Directory.Exists(_traceDir)) return null;

            try
            {
                var files = Directory.GetFiles(_traceDir, "trace-*.json");
                if (files.Length == 0) return null;

                return files
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .First();
            }
            catch { return null; }
        }

        /// <summary>
        /// 列出最近 N 条 trace 文件
        /// </summary>
        public List<TraceFileInfo> ListTraces(int count = 10)
        {
            var result = new List<TraceFileInfo>();
            if (!_enabled || !Directory.Exists(_traceDir)) return result;

            try
            {
                var files = Directory.GetFiles(_traceDir, "trace-*.json");
                foreach (var file in files.OrderByDescending(f => File.GetLastWriteTime(f)).Take(count))
                {
                    var info = new FileInfo(file);
                    result.Add(new TraceFileInfo
                    {
                        Path = file,
                        FileName = System.IO.Path.GetFileName(file),
                        SizeBytes = info.Length,
                        LastModified = info.LastWriteTime
                    });
                }
            }
            catch { /* 列举失败返回空列表 */ }

            return result;
        }

        /// <summary>
        /// 读取指定 trace 文件内容
        /// </summary>
        public string ReadTrace(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            try
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch { return null; }
        }

        // ═══════════════════════════════════════════════════════════
        //  内部方法
        // ═══════════════════════════════════════════════════════════

        private void FinalizeStep()
        {
            if (_currentStep == null) return;

            if (_stepWatch != null)
            {
                _stepWatch.Stop();
                _currentStep.DurationMs = _stepWatch.ElapsedMilliseconds;
            }

            _current?.Steps.Add(_currentStep);
            _currentStep = null;
            _stepWatch = null;
        }

        private void WriteTraceFile(ConversationTrace trace)
        {
            string timestamp = trace.StartTime.ToString("yyyyMMdd-HHmmss");
            string sessionShort = (trace.SessionId ?? "unknown").Substring(0, Math.Min(6, trace.SessionId?.Length ?? 6));
            string fileName = $"trace-{timestamp}-{sessionShort}.json";
            string filePath = Path.Combine(_traceDir, fileName);

            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                DateFormatString = "yyyy-MM-dd HH:mm:ss.fff"
            };

            string json = JsonConvert.SerializeObject(trace, settings);
            File.WriteAllText(filePath, json, Encoding.UTF8);

            CopilotLogger.Info("Trace 已写入: {0} ({1} steps, outcome={2})",
                filePath, trace.TotalSteps, trace.Outcome);

            // 异步清理过期文件
            CleanupOldTraces();
        }

        private void CleanupOldTraces()
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-_retentionDays);
                var files = Directory.GetFiles(_traceDir, "trace-*.json");
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

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;
            return value.Substring(0, maxLength) + "...[truncated]";
        }
    }

    /// <summary>
    /// Trace 文件摘要信息
    /// </summary>
    public class TraceFileInfo
    {
        public string Path { get; set; }
        public string FileName { get; set; }
        public long SizeBytes { get; set; }
        public DateTime LastModified { get; set; }
    }
}
