using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Security
{
    /// <summary>
    /// 命令权限控制器（参考 cline-chinese-main 的 CommandPermissionController）
    /// 支持：
    /// - Glob 模式白名单/黑名单
    /// - 工具级权限控制
    /// - 批量操作检测（>5 个元素需确认）
    /// - 链式命令分段验证
    /// - Shell 重定向检测
    /// </summary>
    public class CommandPermissionController
    {
        private readonly List<PermissionRule> _rules;
        private readonly HashSet<string> _dangerousPatterns;
        private readonly HashSet<string> _writeTools;

        /// <summary>
        /// 权限模式
        /// </summary>
        public enum AccessMode
        {
            Allow,      // 允许
            Block,      // 阻止
            Ask         // 需确认
        }

        /// <summary>
        /// 权限规则
        /// </summary>
        public class PermissionRule
        {
            public string Pattern { get; set; }         // Glob 模式
            public AccessMode Mode { get; set; }         // 权限
            public string Description { get; set; }      // 规则说明
            public bool IsRegex { get; set; }             // 是否为正则
        }

        public CommandPermissionController()
        {
            _rules = new List<PermissionRule>();
            _dangerousPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ">", ">>", "<", "|", "&&", ";", "rm", "del", "format", "drop"
            };
            _writeTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "modify", "execute_pml", "export", "set_attribute", "delete", "create"
            };
        }

        /// <summary>
        /// 添加权限规则
        /// </summary>
        public void AddRule(string pattern, AccessMode mode, string description = null)
        {
            _rules.Add(new PermissionRule
            {
                Pattern = pattern,
                Mode = mode,
                Description = description,
                IsRegex = pattern.StartsWith("^") || pattern.EndsWith("$")
            });
        }

        /// <summary>
        /// 检查工具是否允许执行
        /// </summary>
        public AccessMode CheckTool(string toolName, string args)
        {
            // 1. 精确规则匹配
            foreach (var rule in _rules)
            {
                if (rule.IsRegex)
                {
                    if (Regex.IsMatch(toolName, rule.Pattern, RegexOptions.IgnoreCase))
                        return rule.Mode;
                }
                else if (MatchGlob(toolName, rule.Pattern))
                {
                    return rule.Mode;
                }
            }

            // 2. 写工具默认 Ask
            if (_writeTools.Contains(toolName))
                return AccessMode.Ask;

            // 3. 只读工具默认 Allow
            return AccessMode.Allow;
        }

        /// <summary>
        /// 检查参数中是否包含危险操作
        /// </summary>
        public bool HasDangerousPattern(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
                return false;

            foreach (var pattern in _dangerousPatterns)
            {
                if (args.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 检测是否为批量操作（JSON array 参数）
        /// </summary>
        public bool IsBatchOperation(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
                return false;

            try
            {
                var json = JObject.Parse(args);
                // 检查是否有 array 参数
                foreach (var prop in json.Properties())
                {
                    if (prop.Value is JArray array && array.Count > 5)
                        return true;

                    if (prop.Value is JObject obj)
                    {
                        // 嵌套检查
                        foreach (var nested in obj.Properties())
                        {
                            if (nested.Value is JArray nestedArray && nestedArray.Count > 5)
                                return true;
                        }
                    }
                }
            }
            catch
            {
                // JSON 解析失败，不视为批量
            }
            return false;
        }

        /// <summary>
        /// 简单的 Glob 模式匹配（支持 * 和 ?）
        /// </summary>
        private static bool MatchGlob(string input, string pattern)
        {
            if (pattern == "*")
                return true;

            // 将 Glob 转为正则
            var regex = "^" + Regex.Escape(pattern)
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".") + "$";

            return Regex.IsMatch(input, regex, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// 创建默认权限配置
        /// </summary>
        public static CommandPermissionController CreateDefault()
        {
            var ctrl = new CommandPermissionController();

            // 只读工具 —— 自动批准（Allow）
            ctrl.AddRule("query",          AccessMode.Allow, "查询数据库");
            ctrl.AddRule("get_attributes", AccessMode.Allow, "获取属性");
            ctrl.AddRule("check",          AccessMode.Allow, "检查/校验");
            ctrl.AddRule("calculate",      AccessMode.Allow, "几何计算（纯数学）");

            // 导出 —— 涉及文件写入，需确认
            ctrl.AddRule("export",    AccessMode.Ask, "导出需确认");

            // 写工具 —— 明确需审批
            ctrl.AddRule("modify",       AccessMode.Ask, "属性修改需审批");
            ctrl.AddRule("execute_pml",  AccessMode.Ask, "PML 执行需审批");

            return ctrl;
        }
    }
}
