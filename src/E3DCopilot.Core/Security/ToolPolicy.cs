using System.Collections.Generic;

namespace E3DCopilot.Core.Security
{
    /// <summary>
    /// 工具审批模式（借鉴 Cline 的 Tool Policy 设计）
    /// </summary>
    public enum ApprovalMode
    {
        Auto,       // 自动执行，无需确认
        Ask,        // 每次需用户确认
        PlanOnly    // 仅在 Plan Mode 下可用（只读工具）
    }

    /// <summary>
    /// 工具预设组合（借鉴 Cline 的 Tool Presets）
    /// </summary>
    public class ToolPreset
    {
        public string Name { get; }
        public Dictionary<string, ApprovalMode> Policies { get; }

        public ToolPreset(string name, Dictionary<string, ApprovalMode> policies)
        {
            Name = name;
            Policies = policies;
        }

        /// <summary>观察模式：只读自动，写操作禁用</summary>
        public static ToolPreset Observe => new ToolPreset("observe", new Dictionary<string, ApprovalMode>
        {
            ["query"] = ApprovalMode.Auto,
            ["get_attributes"] = ApprovalMode.Auto,
            ["check"] = ApprovalMode.Auto,
            ["calculate"] = ApprovalMode.Auto,
            ["modify"] = ApprovalMode.PlanOnly,
            ["export"] = ApprovalMode.PlanOnly,
            ["execute_pml"] = ApprovalMode.PlanOnly
        });

        /// <summary>确认模式：只读自动，写需确认</summary>
        public static ToolPreset Confirm => new ToolPreset("confirm", new Dictionary<string, ApprovalMode>
        {
            ["query"] = ApprovalMode.Auto,
            ["get_attributes"] = ApprovalMode.Auto,
            ["check"] = ApprovalMode.Auto,
            ["calculate"] = ApprovalMode.Auto,
            ["modify"] = ApprovalMode.Ask,
            ["export"] = ApprovalMode.Ask,
            ["execute_pml"] = ApprovalMode.Ask
        });

        /// <summary>自动模式：全部自动（低风险操作）</summary>
        public static ToolPreset Auto => new ToolPreset("auto", new Dictionary<string, ApprovalMode>
        {
            ["query"] = ApprovalMode.Auto,
            ["get_attributes"] = ApprovalMode.Auto,
            ["check"] = ApprovalMode.Auto,
            ["calculate"] = ApprovalMode.Auto,
            ["modify"] = ApprovalMode.Auto,
            ["export"] = ApprovalMode.Auto,
            ["execute_pml"] = ApprovalMode.Auto
        });
    }

    /// <summary>
    /// 工具权限策略，管理每个工具的执行模式
    /// </summary>
    public class ToolPolicy
    {
        private readonly Dictionary<string, PolicyEntry> _policies
            = new Dictionary<string, PolicyEntry>();

        public class PolicyEntry
        {
            public ApprovalMode Mode { get; set; }
            public bool Enabled { get; set; } = true;
        }

        /// <summary>设置工具策略</summary>
        public void Set(string toolName, ApprovalMode mode, bool enabled = true)
        {
            _policies[toolName] = new PolicyEntry { Mode = mode, Enabled = enabled };
        }

        /// <summary>获取工具审批模式，默认 Ask</summary>
        public ApprovalMode GetMode(string toolName)
        {
            if (_policies.TryGetValue(toolName, out var p) && p.Enabled)
                return p.Mode;
            return ApprovalMode.Ask;
        }

        /// <summary>应用预设</summary>
        public void ApplyPreset(ToolPreset preset)
        {
            foreach (var kv in preset.Policies)
            {
                Set(kv.Key, kv.Value);
            }
        }

        /// <summary>检查工具是否允许执行</summary>
        public bool IsAllowed(string toolName, bool isPlanMode)
        {
            var mode = GetMode(toolName);
            if (mode == ApprovalMode.Auto) return true;
            if (mode == ApprovalMode.PlanOnly) return isPlanMode;
            // Ask 模式：需要外部审批，此处返回 true 但需 PermissionGate 进一步检查
            return true;
        }
    }
}
