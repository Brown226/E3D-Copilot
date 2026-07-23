using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Logging;

namespace E3DCopilot.Core.Agents
{
    /// <summary>
    /// Goal 模式控制器 — 对齐 Reasonix SPEC 3.7 Goal delivery
    ///
    /// /goal <objective> 启动自主执行：
    ///   - 每轮结束后自动注入 continuation turn
    ///   - 终止条件：模型报告完成 / 连续 3 次相同 blocked 状态 / 用户中断 / 安全轮次上限
    ///   - blocked 状态匹配：归一化大小写/空白/标点后比较
    /// </summary>
    public class GoalController
    {
        private readonly IEventSink _sink;

        // ── 配置 ──
        private const int MaxContinuationTurns = 50;   // 安全轮次上限
        private const int BlockedRepeatThreshold = 3;  // 连续相同 blocked 状态阈值

        // ── 状态 ──
        private string _objective;
        private bool _isActive;
        private int _turnCount;
        private readonly List<string> _blockedStates = new List<string>();
        private string _lastBlockedNormalized;
        private int _blockedRepeatCount;

        public GoalController(IEventSink sink = null)
        {
            _sink = sink;
        }

        /// <summary>Goal 是否活跃</summary>
        public bool IsActive => _isActive;

        /// <summary>当前目标描述</summary>
        public string Objective => _objective;

        /// <summary>已执行的 continuation 轮数</summary>
        public int TurnCount => _turnCount;

        // ═══════════════════════════════════════════════════════════
        //  启动 / 停止
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 启动 Goal 模式（对齐 Reasonix /goal <objective>）
        /// </summary>
        public void Start(string objective)
        {
            if (string.IsNullOrWhiteSpace(objective))
                throw new ArgumentException("objective cannot be empty");

            _objective = objective.Trim();
            _isActive = true;
            _turnCount = 0;
            _blockedStates.Clear();
            _lastBlockedNormalized = null;
            _blockedRepeatCount = 0;

            CopilotLogger.Info("GoalController: 启动目标 '{0}'", Truncate(_objective, 100));
            _sink?.Emit(CopilotEvent.Notice($"🎯 Goal 模式已启动: {_objective}"));
        }

        /// <summary>
        /// 停止 Goal 模式（用户中断或 /goal clear）
        /// </summary>
        public void Stop(string reason = null)
        {
            if (!_isActive) return;

            _isActive = false;
            string msg = reason != null
                ? $"Goal 模式已停止: {reason}"
                : "Goal 模式已停止";
            CopilotLogger.Info("GoalController: {0} (执行了 {1} 轮)", msg, _turnCount);
            _sink?.Emit(CopilotEvent.Notice($"🏁 {msg}（共 {_turnCount} 轮）"));
        }

        // ═══════════════════════════════════════════════════════════
        //  每轮结束后的决策
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 每轮 Agent 结束后调用，决定是否继续。
        /// 返回 continuation 消息（非 null = 继续执行），null = 停止。
        /// </summary>
        public string OnTurnComplete(string assistantResponse, bool hasToolCalls)
        {
            if (!_isActive) return null;

            _turnCount++;

            // 1. 安全轮次上限
            if (_turnCount >= MaxContinuationTurns)
            {
                Stop($"达到安全轮次上限 ({MaxContinuationTurns})");
                return null;
            }

            // 2. 检测模型是否报告完成
            if (IsGoalCompleted(assistantResponse))
            {
                Stop("目标已完成");
                return null;
            }

            // 3. 检测 blocked 状态
            string blockedState = DetectBlockedState(assistantResponse);
            if (blockedState != null)
            {
                string normalized = NormalizeForComparison(blockedState);
                if (normalized == _lastBlockedNormalized)
                {
                    _blockedRepeatCount++;
                    if (_blockedRepeatCount >= BlockedRepeatThreshold)
                    {
                        Stop($"连续 {BlockedRepeatThreshold} 次遇到相同阻塞: {Truncate(blockedState, 80)}");
                        return null;
                    }
                }
                else
                {
                    _lastBlockedNormalized = normalized;
                    _blockedRepeatCount = 1;
                }
                _blockedStates.Add(blockedState);
            }
            else
            {
                // 无 blocked，重置计数
                _lastBlockedNormalized = null;
                _blockedRepeatCount = 0;
            }

            // 4. 继续执行 — 注入 continuation turn
            return BuildContinuationPrompt(hasToolCalls);
        }

        // ═══════════════════════════════════════════════════════════
        //  内部逻辑
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 检测模型是否报告目标完成
        /// </summary>
        private bool IsGoalCompleted(string response)
        {
            if (string.IsNullOrEmpty(response)) return false;
            string lower = response.ToLowerInvariant();

            // 完成信号关键词
            var completionSignals = new[]
            {
                "goal completed", "goal achieved", "task completed",
                "目标已完成", "任务已完成", "已全部完成",
                "all done", "finished successfully",
                "[goal:complete]", "[goal:done]"
            };

            foreach (var signal in completionSignals)
            {
                if (lower.Contains(signal)) return true;
            }
            return false;
        }

        /// <summary>
        /// 检测 blocked 状态（模型报告无法继续）
        /// </summary>
        private string DetectBlockedState(string response)
        {
            if (string.IsNullOrEmpty(response)) return null;
            string lower = response.ToLowerInvariant();

            var blockedSignals = new[]
            {
                "blocked:", "i'm blocked", "cannot proceed",
                "需要用户提供", "无法继续", "被阻塞",
                "[goal:blocked]", "waiting for user"
            };

            foreach (var signal in blockedSignals)
            {
                int idx = lower.IndexOf(signal);
                if (idx >= 0)
                {
                    // 提取 blocked 原因（信号后面的文本，最多 200 字符）
                    int start = idx + signal.Length;
                    int len = Math.Min(200, response.Length - start);
                    return response.Substring(start, len).Trim();
                }
            }
            return null;
        }

        /// <summary>
        /// 归一化用于比较（对齐 Reasonix blocked-state matching:
        /// 归一化大小写/空白/标点）
        /// </summary>
        private static string NormalizeForComparison(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            // 小写 + 去除标点 + 压缩空白
            string lower = text.ToLowerInvariant();
            lower = Regex.Replace(lower, @"[^\w\s\u4e00-\u9fff]", "");
            lower = Regex.Replace(lower, @"\s+", " ");
            return lower.Trim();
        }

        /// <summary>
        /// 构建 continuation prompt（注入到下一轮用户消息）
        /// </summary>
        private string BuildContinuationPrompt(bool hasToolCalls)
        {
            string context = hasToolCalls
                ? "Continue working on the goal. The previous step completed a tool call."
                : "Continue working on the goal.";

            return $"[goal continuation | turn {_turnCount}/{MaxContinuationTurns}]\n" +
                   $"Objective: {_objective}\n\n" +
                   $"{context} " +
                   "If the goal is fully achieved, respond with '[goal:complete]' and a summary. " +
                   "If you are blocked and need user input, respond with '[goal:blocked] <reason>'. " +
                   "Otherwise, take the next concrete action toward the goal.";
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length > max ? s.Substring(0, max) + "..." : s;
        }
    }
}
