using System;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Logging;

namespace E3DCopilot.Core.Agents
{
    /// <summary>
    /// Todo 进度守卫 — 对齐 Reasonix SPEC 5 "adaptive progress lease"
    ///
    /// 机制：
    ///   - 跟踪 todo_write 的 in_progress 项
    ///   - 每次工具调用完成后续约进度租约
    ///   - 连续 8 轮无进度 → 注入重新评估 nudge
    ///   - 连续 16 轮无进度 → 暂停并保留工作
    ///   - 精确重复不算进度（对齐 Reasonix "exact repeats do not renew"）
    /// </summary>
    public class ProgressGuard
    {
        private readonly IEventSink _sink;

        // ── 配置（对齐 Reasonix SPEC 5） ──
        private const int NudgeThreshold = 8;    // 无进度轮数 → nudge
        private const int PauseThreshold = 16;   // 无进度轮数 → 暂停
        private bool _nudgeSent;

        // ── 状态 ──
        private int _noProgressRounds;
        private string _lastActionSig;  // 上一次动作签名（用于检测精确重复）
        private bool _hasActiveTodo;
        private bool _isPaused;

        public ProgressGuard(IEventSink sink = null)
        {
            _sink = sink;
        }

        /// <summary>是否已暂停（达到 PauseThreshold）</summary>
        public bool IsPaused => _isPaused;

        /// <summary>当前无进度轮数</summary>
        public int NoProgressRounds => _noProgressRounds;

        /// <summary>是否有活跃的 todo 项</summary>
        public bool HasActiveTodo => _hasActiveTodo;

        // ═══════════════════════════════════════════════════════════
        //  进度跟踪
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 设置是否有活跃 todo（由 TodoWriteHandler 调用）
        /// </summary>
        public void SetActiveTodo(bool hasInProgress)
        {
            _hasActiveTodo = hasInProgress;
            if (!hasInProgress)
            {
                // 无活跃 todo 时不追踪进度
                _noProgressRounds = 0;
                _nudgeSent = false;
            }
        }

        /// <summary>
        /// 每次工具调用完成后调用，评估是否有进度。
        /// 返回 null = 正常继续，非 null = 需要注入的 nudge 消息。
        /// </summary>
        public string RecordToolCompletion(string toolName, string resultSignature)
        {
            if (!_hasActiveTodo) return null;
            if (_isPaused) return null;

            // 检测精确重复（对齐 Reasonix "exact repeats do not renew"）
            string sig = $"{toolName}:{resultSignature}";
            bool isNewProgress = sig != _lastActionSig;
            _lastActionSig = sig;

            if (isNewProgress)
            {
                // 有新进度，续约租约
                _noProgressRounds = 0;
                _nudgeSent = false;
                return null;
            }

            // 精确重复，不续约
            _noProgressRounds++;

            // 检查阈值
            if (_noProgressRounds >= PauseThreshold)
            {
                _isPaused = true;
                CopilotLogger.Info("ProgressGuard: 连续 {0} 轮无进度，暂停执行", _noProgressRounds);
                _sink?.Emit(CopilotEvent.Notice(
                    $"⏸️ 进度守卫：连续 {_noProgressRounds} 轮无新进度，已暂停。请检查任务是否需要调整方向。"));
                return BuildPauseMessage();
            }

            if (_noProgressRounds >= NudgeThreshold && !_nudgeSent)
            {
                _nudgeSent = true;
                CopilotLogger.Info("ProgressGuard: 连续 {0} 轮无进度，注入 nudge", _noProgressRounds);
                return BuildNudgeMessage();
            }

            return null;
        }

        /// <summary>
        /// 重置状态（新 turn 开始时调用）
        /// </summary>
        public void ResetTurn()
        {
            // 不重置 _noProgressRounds（跨 step 累积）
            // 只重置 per-turn 状态
        }

        /// <summary>
        /// 完全重置（新会话或用户干预后）
        /// </summary>
        public void FullReset()
        {
            _noProgressRounds = 0;
            _lastActionSig = null;
            _nudgeSent = false;
            _isPaused = false;
        }

        /// <summary>
        /// 用户干预后恢复执行
        /// </summary>
        public void Resume()
        {
            _isPaused = false;
            _noProgressRounds = 0;
            _nudgeSent = false;
        }

        // ═══════════════════════════════════════════════════════════
        //  内部消息构建
        // ═══════════════════════════════════════════════════════════

        private string BuildNudgeMessage()
        {
            return "[progress guard] You have repeated the same action multiple times without making " +
                   "measurable progress on the active todo item. Reassess your approach: " +
                   "try a different tool, break the task into smaller steps, or if the current " +
                   "approach is fundamentally blocked, explain the situation to the user and " +
                   "propose an alternative.";
        }

        private string BuildPauseMessage()
        {
            return "[progress guard: PAUSED] Execution has been paused because no new progress was " +
                   "detected after " + PauseThreshold + " consecutive tool calls. " +
                   "The work so far has been preserved. To continue, the user should provide " +
                   "new guidance or confirm a different approach. " +
                   "Summarize what was attempted and what appears to be blocking progress.";
        }
    }
}
