using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Security
{
    /// <summary>
    /// 工具审批模式（借鉴 Cline 的 Tool Policy 设计）
    /// </summary>
    public enum ApprovalMode
    {
        Auto,       // 自动执行，无需确认
        Ask,        // 每次需用户确认
        Deny,       // 硬拒绝，任何模式下都不执行（对齐 Reasonix deny）
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
    /// 工具权限策略 — 对齐 Reasonix internal/permission/ 的 Policy.Decide 逻辑
    ///
    /// 规则语法：
    ///   - "modify" → 匹配该工具的所有调用
    ///   - "modify(/SITE/**)" → 仅匹配 element_name 参数以 /SITE/ 开头的调用
    ///   - "execute_pml(NEW *)" → 仅匹配 PML 脚本以 NEW 开头的调用
    ///
    /// 优先级：deny > ask > allow > fallback
    /// </summary>
    public class ToolPolicy
    {
        private readonly Dictionary<string, PolicyEntry> _policies
            = new Dictionary<string, PolicyEntry>();

        // 规则列表（对齐 Reasonix Policy.Allow/Ask/Deny []Rule）
        private readonly List<PolicyRule> _denyRules = new List<PolicyRule>();
        private readonly List<PolicyRule> _askRules = new List<PolicyRule>();
        private readonly List<PolicyRule> _allowRules = new List<PolicyRule>();

        /// <summary>无规则匹配时的默认模式（对齐 Reasonix Mode 字段）</summary>
        public ApprovalMode FallbackMode { get; set; } = ApprovalMode.Ask;

        public class PolicyEntry
        {
            public ApprovalMode Mode { get; set; }
            public bool Enabled { get; set; } = true;
        }

        /// <summary>
        /// 规则定义（对齐 Reasonix permission.Rule）
        /// 格式："Tool" 或 "Tool(specifier)"
        /// </summary>
        public class PolicyRule
        {
            public string ToolName { get; set; }
            public string Specifier { get; set; } // null = 匹配所有调用
            public bool IsPrefix { get; set; }    // specifier 以 * 结尾 = 前缀匹配

            /// <summary>解析规则字符串，如 "modify(/SITE/**)" 或 "execute_pml(NEW *)"</summary>
            public static PolicyRule Parse(string rule)
            {
                if (string.IsNullOrWhiteSpace(rule)) return null;
                rule = rule.Trim();

                int parenStart = rule.IndexOf('(');
                if (parenStart < 0)
                {
                    return new PolicyRule { ToolName = rule.ToLowerInvariant(), Specifier = null };
                }

                string tool = rule.Substring(0, parenStart).Trim().ToLowerInvariant();
                int parenEnd = rule.LastIndexOf(')');
                if (parenEnd <= parenStart)
                {
                    return new PolicyRule { ToolName = tool, Specifier = null };
                }

                string spec = rule.Substring(parenStart + 1, parenEnd - parenStart - 1).Trim();
                bool isPrefix = spec.EndsWith("*") || spec.EndsWith("**");

                return new PolicyRule
                {
                    ToolName = tool,
                    Specifier = spec.TrimEnd('*').TrimEnd('/'),
                    IsPrefix = isPrefix
                };
            }

            /// <summary>检查规则是否匹配给定的工具调用</summary>
            public bool Matches(string toolName, string subject)
            {
                if (!string.Equals(ToolName, toolName, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (Specifier == null) return true; // 无 specifier = 匹配所有
                if (string.IsNullOrEmpty(subject)) return false;

                if (IsPrefix)
                {
                    return subject.StartsWith(Specifier, StringComparison.OrdinalIgnoreCase);
                }

                // 通配符匹配（支持 ** 和 *）
                return WildcardMatch(subject, Specifier);
            }
        }

        public ToolPolicy()
        {
            // 元能力工具默认 Auto — 它们不操作 E3D 数据，不需要审批
            var metaTools = new[]
            {
                "todo_write", "complete_step", "memory", "run_skill",
                "read_file", "write_file", "grep", "glob",
                "ask", "history",
            };
            foreach (var tool in metaTools)
                Set(tool, ApprovalMode.Auto);
        }

        /// <summary>设置工具策略</summary>
        public void Set(string toolName, ApprovalMode mode, bool enabled = true)
        {
            _policies[toolName] = new PolicyEntry { Mode = mode, Enabled = enabled };
        }

        // ═══════════════════════════════════════════════════════════
        //  规则管理（对齐 Reasonix [permissions] 配置段）
        // ═══════════════════════════════════════════════════════════

        /// <summary>添加 deny 规则（硬拒绝，任何模式下都不执行）</summary>
        public void AddDenyRule(string rule)
        {
            var parsed = PolicyRule.Parse(rule);
            if (parsed != null) _denyRules.Add(parsed);
        }

        /// <summary>添加 ask 规则（强制询问，即使其他规则允许）</summary>
        public void AddAskRule(string rule)
        {
            var parsed = PolicyRule.Parse(rule);
            if (parsed != null) _askRules.Add(parsed);
        }

        /// <summary>添加 allow 规则（永不询问）</summary>
        public void AddAllowRule(string rule)
        {
            var parsed = PolicyRule.Parse(rule);
            if (parsed != null) _allowRules.Add(parsed);
        }

        /// <summary>从配置加载规则列表</summary>
        public void LoadRules(List<string> deny = null, List<string> ask = null, List<string> allow = null)
        {
            if (deny != null) foreach (var r in deny) AddDenyRule(r);
            if (ask != null) foreach (var r in ask) AddAskRule(r);
            if (allow != null) foreach (var r in allow) AddAllowRule(r);
        }

        // ═══════════════════════════════════════════════════════════
        //  Decide — 核心决策逻辑（对齐 Reasonix Policy.Decide）
        //  优先级：deny > ask > allow > per-tool policy > fallback
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 决策给定工具调用的审批模式。
        /// subject 从 tool call JSON args 中提取（element_name/path/command）。
        /// </summary>
        public ApprovalMode Decide(string toolName, string argsJson = null)
        {
            string subject = ExtractSubject(argsJson);

            // 1. deny 规则最高优先
            foreach (var rule in _denyRules)
            {
                if (rule.Matches(toolName, subject))
                    return ApprovalMode.Deny;
            }

            // 2. ask 规则
            foreach (var rule in _askRules)
            {
                if (rule.Matches(toolName, subject))
                    return ApprovalMode.Ask;
            }

            // 3. allow 规则
            foreach (var rule in _allowRules)
            {
                if (rule.Matches(toolName, subject))
                    return ApprovalMode.Auto;
            }

            // 4. 回退到 per-tool policy
            return GetMode(toolName);
        }

        /// <summary>获取工具审批模式（无规则匹配时），默认回退到 FallbackMode</summary>
        public ApprovalMode GetMode(string toolName)
        {
            if (_policies.TryGetValue(toolName, out var p) && p.Enabled)
                return p.Mode;
            return FallbackMode;
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
        public bool IsAllowed(string toolName, bool isPlanMode, string argsJson = null)
        {
            var mode = Decide(toolName, argsJson);
            if (mode == ApprovalMode.Deny) return false;
            if (mode == ApprovalMode.Auto) return true;
            if (mode == ApprovalMode.PlanOnly) return isPlanMode;
            // Ask 模式：需要外部审批，此处返回 true 但需 PermissionGate 进一步检查
            return true;
        }

        // ═══════════════════════════════════════════════════════════
        //  辅助方法
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 从 tool call JSON args 中提取 subject（对齐 Reasonix 的已知 key 提取）
        /// 已知 key: element_name, path, file_path, command, pml_script
        /// </summary>
        private static string ExtractSubject(string argsJson)
        {
            if (string.IsNullOrEmpty(argsJson)) return null;
            try
            {
                var obj = JObject.Parse(argsJson);
                // 按优先级尝试提取
                foreach (var key in new[] { "element_name", "path", "file_path", "command", "pml_script", "script" })
                {
                    var val = obj.Value<string>(key);
                    if (!string.IsNullOrEmpty(val))
                        return val;
                }
            }
            catch { }
            return null;
        }

        /// <summary>简单通配符匹配（支持 * 和 **）</summary>
        private static bool WildcardMatch(string input, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return true;
            // 将通配符转为正则
            string regex = "^" + Regex.Escape(pattern)
                .Replace("\\*\\*", ".*")
                .Replace("\\*", "[^/]*") + "$";
            try
            {
                return Regex.IsMatch(input, regex, RegexOptions.IgnoreCase);
            }
            catch { return false; }
        }
    }
}
