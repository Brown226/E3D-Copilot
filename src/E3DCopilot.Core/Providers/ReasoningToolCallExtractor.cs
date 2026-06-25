using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using E3DCopilot.Core.Providers;

namespace E3DCopilot.Core.Providers
{
    /// <summary>
    /// 从 reasoning_content 中提取工具调用意图
    /// 适配 MiMo v2.5 等推理模型：这些模型不返回结构化 delta.tool_calls，
    /// 而是在 reasoning 中以自然语言描述工具调用。
    /// 
    /// 支持的模式：
    ///   1. 函数调用风格: tool_name(arg1, arg2) 或 tool_name(key=value, ...)
    ///   2. "调用/使用 XXX 工具" + 后续参数
    /// </summary>
    public static class ReasoningToolCallExtractor
    {
        // 已知工具名（与 SystemPrompt 中的 Available Tools 对应）
        private static readonly HashSet<string> KnownTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "query", "get_attributes", "modify", "check", "calculate", "export",
            "execute_pml", "ask", "read_file", "write_file",
            "run_skill", "grep", "glob", "todo_write", "memory",
            "design", "piping", "geometry",
            "undo_redo", "report", "compare", "hierarchy", "batch"
        };

        // 匹配 tool_name(...) 模式
        // group 1 = 工具名, group 2 = 括号内参数
        private static readonly Regex ToolCallRegex = new Regex(
            @"\b([a-zA-Z_][a-zA-Z0-9_]*)\s*\(([^)]*(?:\([^)]*\)[^)]*)*)\)",
            RegexOptions.Compiled);

        // 匹配 JSON 格式参数: {"key": "value", ...}
        private static readonly Regex JsonArgsRegex = new Regex(
            @"\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}",
            RegexOptions.Compiled);

        // 匹配 key=value 逗号分隔参数
        private static readonly Regex KvPairRegex = new Regex(
            @"([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*(?:""([^""]*)""|'([^']*)'|([\w./\\-]+))",
            RegexOptions.Compiled);

        /// <summary>
        /// 从 reasoning 文本中提取工具调用
        /// </summary>
        /// <param name="reasoningText">完整的 reasoning 内容</param>
        /// <param name="knownToolNames">已注册的工具名列表（可选，为空时使用内置列表）</param>
        /// <returns>提取到的工具调用列表</returns>
        public static List<ToolCall> Extract(string reasoningText, IList<string> knownToolNames = null)
        {
            var result = new List<ToolCall>();
            if (string.IsNullOrEmpty(reasoningText)) return result;

            var tools = knownToolNames != null && knownToolNames.Count > 0
                ? new HashSet<string>(knownToolNames, StringComparer.OrdinalIgnoreCase)
                : KnownTools;

            // 所有匹配
            var matches = ToolCallRegex.Matches(reasoningText);
            foreach (Match m in matches)
            {
                string toolName = m.Groups[1].Value;
                string argsRaw = m.Groups[2].Value;

                // 只处理已知工具
                if (!tools.Contains(toolName)) continue;

                // 解析参数
                string argsJson = ParseArguments(argsRaw);
                if (argsJson == null) continue; // 无法解析，跳过

                result.Add(new ToolCall
                {
                    Id = $"reasoning_{Guid.NewGuid():N}",
                    Name = toolName,
                    Arguments = argsJson
                });
            }

            return result;
        }

        /// <summary>
        /// 解析工具调用参数为 JSON 字符串
        /// </summary>
        private static string ParseArguments(string argsRaw)
        {
            if (string.IsNullOrWhiteSpace(argsRaw)) return "{}";

            // 1. 尝试直接作为 JSON 解析
            string trimmed = argsRaw.Trim();
            if (trimmed.StartsWith("{"))
            {
                try
                {
                    var obj = Newtonsoft.Json.JsonConvert.DeserializeObject(trimmed);
                    if (obj != null)
                        return Newtonsoft.Json.JsonConvert.SerializeObject(obj);
                }
                catch { /* 不是有效 JSON，继续 */ }
            }

            // 2. 尝试 key=value 逗号分隔格式
            var kvMatches = KvPairRegex.Matches(argsRaw);
            if (kvMatches.Count > 0)
            {
                var dict = new Dictionary<string, string>();
                foreach (Match kv in kvMatches)
                {
                    string key = kv.Groups[1].Value;
                    string value = kv.Groups[2].Success ? kv.Groups[2].Value
                                 : kv.Groups[3].Success ? kv.Groups[3].Value
                                 : kv.Groups[4].Value;
                    dict[key] = value;
                }

                if (dict.Count > 0)
                {
                    try
                    {
                        return Newtonsoft.Json.JsonConvert.SerializeObject(dict);
                    }
                    catch { /* ignore */ }
                }
            }

            // 3. 尝试从文本中提取 JSON 块
            var jsonMatch = JsonArgsRegex.Match(argsRaw);
            if (jsonMatch.Success)
            {
                try
                {
                    var obj = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonMatch.Value);
                    if (obj != null)
                        return Newtonsoft.Json.JsonConvert.SerializeObject(obj);
                }
                catch { /* ignore */ }
            }

            // 4. 无法解析 — 用整个参数字符串作为单个 value
            // 只有当参数看起来像有意义的内容时才保留
            if (argsRaw.Length > 2 && argsRaw.Length < 500)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new { raw = argsRaw });
            }

            return null; // 无法解析
        }
    }
}
