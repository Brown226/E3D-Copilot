using System.Collections.Generic;
using System.Text;
using E3DCopilot.Core.Skills;

namespace E3DCopilot.Core
{
    public static class SystemPrompt
    {
        /// <summary>
        /// 静态基础指令 — 会话创建时生成一次，永不变更
        /// 对应 Reasonix 的 cache-stable prefix：vLLM 前缀缓存整段命中
        /// </summary>
        private static string _cachedBasePrompt;
        private static readonly object _cacheLock = new object();

        /// <summary>
        /// 获取基础 System Prompt（带缓存）
        /// </summary>
        public static string GetBasePrompt()
        {
            if (_cachedBasePrompt != null) return _cachedBasePrompt;
            lock (_cacheLock)
            {
                if (_cachedBasePrompt != null) return _cachedBasePrompt;
                _cachedBasePrompt = BuildBasePrompt();
                return _cachedBasePrompt;
            }
        }

        /// <summary>
        /// 构建可缓存的静态部分（角色 + 原则 + 领域概要）
        /// 不含动态上下文和工具 schema（工具 schema 由 LLM 协议自动提供）
        /// </summary>
        private static string BuildBasePrompt()
        {
            return
"You are E小智, AI assistant for AVEVA E3D plant design. " +
"Understand engineers in Chinese, call tools to query/modify E3D data, respond concisely.\n\n" +

"## Principles\n" +
"1. Query before modify — single-attribute reads use get_attributes, NEVER execute_pml\n" +
"2. Execute before reporting — call tools first, then summarise; never write conclusions before results\n" +
"3. Minimal operations — only what's needed; batch previews required for bulk changes\n" +
"4. Report outcomes — success/failure/quantity for every operation\n" +
"5. Multi-step tasks — use todo_write to lay out steps, keep one in_progress at a time, flip completed items as you finish\n" +
"6. User decisions — when scope/approach/risk is ambiguous with no safe default, call ask() with 2-4 choices; don't guess for the user\n\n" +

"## Domain Notes\n" +
"- E3D hierarchy: Project → Zone → SubZone → Element. Always scope queries to the current zone.\n" +
"- New PML code? Run `run_skill(\"aveva-pml-language\")` for syntax reference, then `read_file` the details.\n" +
"- PML knowledge search: `grep(knowledge=true, pattern=\"...\")`.\n\n" +

"## Response\n" +
"Use tables for data, show quantities for batch ops, include reasons for errors. " +
"Tool schemas are available via the API — read them for parameter details.";
        }

        /// <summary>
        /// 构建完整的 System Prompt = 静态基础 + 动态上下文 + Skill 索引
        /// 对应 Reasonix boot.go 的 sysPrompt 装配：基础 → 记忆 → Skills → 动态上下文
        /// 静态基础部分享受 vLLM 前缀缓存，动态后缀在每回合末端断裂
        /// </summary>
        public static string Build(string currentElement = null, string currentZone = null, 
            List<string> selectedElements = null, SkillManager skillManager = null)
        {
            var sb = new StringBuilder();
            
            // Part A: 静态基础（前缀缓存命中区）
            sb.AppendLine(GetBasePrompt());
            
            // Part B: Skill 索引（一次性注入，与基础一起缓存）
            if (skillManager != null)
            {
                var skills = skillManager.ListSkills();
                if (skills.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("## Available Skills");
                    sb.AppendLine("Use run_skill(name=\"<skill-name>\") to load a skill's full playbook for domain guidance.");
                    foreach (var sk in skills)
                    {
                        if (!sk.Enabled) continue;
                        string tag = sk.RunAs == "subagent" ? " [🧬 subagent]" : "";
                        sb.AppendLine($"- {sk.Name}{tag}: {sk.Description}");
                    }
                }
            }
            
            // Part C: 动态上下文（每轮变化，缓存断裂点）
            sb.AppendLine();
            sb.AppendLine("## Current Context");
            sb.AppendLine($"- Current selected element: {currentElement ?? "none (click an element first)"}");
            
            if (selectedElements != null && selectedElements.Count > 0)
            {
                sb.AppendLine($"- Selected elements ({selectedElements.Count} total): [{string.Join(", ", selectedElements)}]");
                sb.AppendLine("- 用户可能想对以上所有选中元素进行操作。如果用户说\"这些\"、\"所有选中的\"，指的是上面列出的所有元素。");
            }
            
            sb.AppendLine($"- Current zone: {currentZone ?? "unknown"}");
            sb.AppendLine();
            sb.AppendLine("## Element Selection");
            sb.AppendLine("- If the user says \"this element\", \"current element\", \"选中元素\", \"这个\" — it refers to the Current selected element above.");
            sb.AppendLine("- If the user says \"these elements\", \"all selected\", \"这些\", \"所有选中的\" — it refers to ALL Selected elements above.");
            sb.AppendLine("- You can query/modify the current element by using its name directly in tool calls.");
            sb.AppendLine("- 要获取当前元素属性，调用 get_attributes(element=元素名)");
            sb.AppendLine("- 严禁用 execute_pml 读取元素属性，必须用 get_attributes 或 query");
            
            return sb.ToString();
        }
    }
}
