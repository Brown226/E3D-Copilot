using System.Collections.Generic;

namespace E3DCopilot.Core.Agents
{
    /// <summary>
    /// 子代理上下文 — 每个子代理拥有独立名称、系统提示词、会话状态
    /// 对齐 Reasonix SubagentSpec / SubagentRun
    /// </summary>
    public class SubagentContext
    {
        /// <summary>子代理唯一标识，如 "inspector-pipe"</summary>
        public string Name { get; set; }

        /// <summary>专长系统提示词</summary>
        public string SystemPrompt { get; set; }

        /// <summary>是否仅只读（MVP 阶段仅支持只读）</summary>
        public bool IsReadOnly { get; set; } = true;

        /// <summary>子代理模式：Planner（只读规划）/ Executor（执行）。C2 双模型协作</summary>
        public SubagentMode Mode { get; set; } = SubagentMode.Executor;

        /// <summary>指定使用的 Provider（provider/model 引用），实现 handoff 专长切换。C2</summary>
        public string PreferredProvider { get; set; }

        /// <summary>独立会话上下文</summary>
        public CopilotSession Session { get; set; } = new CopilotSession();
    }

    /// <summary>
    /// 子代理执行结果
    /// </summary>
    public class AgentResult
    {
        /// <summary>输出文本（最终助手回复）</summary>
        public string Output { get; set; }

        /// <summary>是否成功</summary>
        public bool Success { get; set; } = true;
    }

    /// <summary>子代理模式（C2 双模型 / handoff）</summary>
    public enum SubagentMode
    {
        /// <summary>规划者：只读产出计划</summary>
        Planner,
        /// <summary>执行者：执行任务</summary>
        Executor
    }
}
