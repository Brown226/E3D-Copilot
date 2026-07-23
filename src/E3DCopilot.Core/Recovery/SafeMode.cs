using System;
using System.Collections.Generic;
using E3DCopilot.Core.Logging;

namespace E3DCopilot.Core.Recovery
{
    /// <summary>
    /// 安全模式 — 对齐 Reasonix desktop/safe_mode_test.go
    ///
    /// 触发条件：连续 2 次崩溃后进入安全模式
    /// 安全模式下：
    ///   - 禁用 MCP 插件
    ///   - 禁用技能包
    ///   - 仅保留核心工具（query/get_attributes/check/calculate/ask）
    ///   - UI 显示安全模式提示条
    /// </summary>
    public class SafeMode
    {
        private readonly CrashDetector _crashDetector;

        public SafeMode(CrashDetector crashDetector)
        {
            _crashDetector = crashDetector ?? throw new ArgumentNullException(nameof(crashDetector));
        }

        /// <summary>当前是否处于安全模式</summary>
        public bool IsActive { get; private set; }

        /// <summary>进入安全模式的原因</summary>
        public string Reason { get; private set; }

        /// <summary>安全模式下允许的核心工具白名单</summary>
        public static readonly HashSet<string> CoreTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "query",
            "get_attributes",
            "check",
            "calculate",
            "ask",
            "memory",
            "history",
            "todo_write"
        };

        /// <summary>
        /// 启动时评估是否应进入安全模式。
        /// 由 CopilotController 在初始化时调用。
        /// </summary>
        public bool Evaluate()
        {
            if (_crashDetector.ShouldEnterSafeMode())
            {
                IsActive = true;
                Reason = $"连续 {_crashDetector.ConsecutiveCrashes} 次异常退出，已进入安全模式。" +
                         "MCP 插件和技能包已禁用，仅保留核心工具。";
                CopilotLogger.Info("SafeMode: {0}", Reason);
                return true;
            }

            IsActive = false;
            Reason = null;
            return false;
        }

        /// <summary>
        /// 手动退出安全模式（用户确认后调用）
        /// </summary>
        public void Exit()
        {
            IsActive = false;
            Reason = null;
            CopilotLogger.Info("SafeMode: 用户手动退出安全模式");
        }

        /// <summary>
        /// 检查指定工具是否在安全模式下可用
        /// </summary>
        public bool IsToolAllowed(string toolName)
        {
            if (!IsActive) return true; // 非安全模式，所有工具可用
            return CoreTools.Contains(toolName);
        }

        /// <summary>
        /// 安全模式下是否允许加载 MCP 插件
        /// </summary>
        public bool AllowMcpPlugins => !IsActive;

        /// <summary>
        /// 安全模式下是否允许加载技能包
        /// </summary>
        public bool AllowSkills => !IsActive;

        /// <summary>
        /// 获取安全模式状态描述（供 UI 显示）
        /// </summary>
        public string GetStatusMessage()
        {
            if (!IsActive) return null;
            return $"🛡️ 安全模式：{Reason} 正常退出后自动恢复。";
        }
    }
}
