using System;
using System.Threading;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Logging;

namespace E3DCopilot.Core.Recovery
{
    /// <summary>
    /// 挂起看门狗 — 对齐 Reasonix desktop/hang_watchdog.go
    ///
    /// 机制：
    ///   - 后台 Timer 每 30s 检查 AgentLoop 最后活动时间
    ///   - 超过阈值（默认 120s 无 LLM 响应）触发超时通知
    ///   - 可选：超时后自动取消当前操作
    /// </summary>
    public class HangWatchdog : IDisposable
    {
        private readonly IEventSink _sink;
        private readonly int _checkIntervalMs;
        private readonly int _timeoutMs;
        private Timer _timer;
        private long _lastActivityTicks;
        private bool _isRunning;
        private bool _hasWarned;
        private CancellationTokenSource _activeCts;

        /// <summary>默认检查间隔：30 秒</summary>
        private const int DefaultCheckIntervalMs = 30000;

        /// <summary>默认超时阈值：120 秒</summary>
        private const int DefaultTimeoutMs = 120000;

        public HangWatchdog(IEventSink sink, int checkIntervalMs = DefaultCheckIntervalMs, int timeoutMs = DefaultTimeoutMs)
        {
            _sink = sink;
            _checkIntervalMs = checkIntervalMs;
            _timeoutMs = timeoutMs;
            _lastActivityTicks = DateTime.UtcNow.Ticks;
        }

        /// <summary>看门狗是否正在运行</summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 启动看门狗
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _hasWarned = false;
            _lastActivityTicks = DateTime.UtcNow.Ticks;
            _timer = new Timer(CheckHang, null, _checkIntervalMs, _checkIntervalMs);
            CopilotLogger.Info("HangWatchdog: 已启动 (间隔={0}ms, 超时={1}ms)", _checkIntervalMs, _timeoutMs);
        }

        /// <summary>
        /// 停止看门狗
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _timer?.Dispose();
            _timer = null;
            _activeCts = null;
        }

        /// <summary>
        /// 记录活动（每次 LLM 响应/工具执行时调用）
        /// </summary>
        public void RecordActivity()
        {
            _lastActivityTicks = DateTime.UtcNow.Ticks;
            _hasWarned = false; // 有新活动，重置警告状态
        }

        /// <summary>
        /// 关联一个 CancellationTokenSource，超时时自动取消
        /// </summary>
        public void WatchOperation(CancellationTokenSource cts)
        {
            _activeCts = cts;
            RecordActivity();
        }

        /// <summary>
        /// 操作完成，清除关联
        /// </summary>
        public void OperationComplete()
        {
            _activeCts = null;
            RecordActivity();
        }

        private void CheckHang(object state)
        {
            if (!_isRunning) return;

            var elapsed = new TimeSpan(DateTime.UtcNow.Ticks - _lastActivityTicks);
            if (elapsed.TotalMilliseconds < _timeoutMs) return;

            // 超时！
            if (!_hasWarned)
            {
                _hasWarned = true;
                int elapsedSec = (int)elapsed.TotalSeconds;
                CopilotLogger.Info("HangWatchdog: 检测到可能的挂起 ({0}s 无活动)", elapsedSec);
                _sink?.Emit(CopilotEvent.Notice(
                    $"⚠️ 检测到响应超时（{elapsedSec}s 无活动）。如果 LLM 服务无响应，请检查网络连接或取消当前操作。"));

                // 如果关联了 CTS，触发取消
                var cts = _activeCts;
                if (cts != null && !cts.IsCancellationRequested)
                {
                    try
                    {
                        cts.Cancel();
                        _sink?.Emit(CopilotEvent.Notice("已自动取消超时操作"));
                    }
                    catch { }
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
