using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace E3DCopilot.Core.Providers
{
    /// <summary>
    /// XML 格式工具调用解析器（兜底方案）
    /// 当 LLM 不支持标准 OpenAI tool_calls 格式时，从文本中提取
    /// <tool_invocation name="..." arguments={...} /> 并转为 ToolCall
    /// 
    /// 支持的格式：
    ///   自闭合：<tool_invocation name="query" arguments={"type":"pipe"} />
    ///   标签对：<tool_invocation name="query" arguments={"type":"pipe"}></tool_invocation>
    ///   多行：  文本中任意位置出现的上述标签
    /// </summary>
    public static class ToolInvocationParser
    {
        // 匹配 <tool_invocation name="..." arguments={...} />
        // arguments 是 JSON 对象，以 { 开头 } 结尾，内部可含一层嵌套 { }
        // 注意：该 XML 并非标准 XML（JSON 内的 " 未转义），故用纯文本正则匹配
        private static readonly Regex ToolInvocationRegex = new Regex(
            @"<tool_invocation\s+" +
            @"name\s*=\s*""([^""]*)""\s+" +
            @"arguments\s*=\s*" +
            @"(\{(?:[^{}]|\{[^{}]*\})*\})" +  // JSON 对象: {...} 或 {...{...}...}
            @"\s*/?\s*>",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        /// <summary>
        /// 从文本中提取所有 XML 格式的工具调用
        /// </summary>
        /// <param name="text">LLM 返回的文本（可能混合普通文本和 XML 标签）</param>
        /// <returns>提取到的工具调用列表</returns>
        public static List<ToolCall> ExtractToolCalls(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<ToolCall>(0);

            var results = new List<ToolCall>();
            var matches = ToolInvocationRegex.Matches(text);

            foreach (Match match in matches)
            {
                if (!match.Success) continue;

                string name = match.Groups[1].Value?.Trim();
                string arguments = match.Groups[2].Value;

                if (string.IsNullOrEmpty(name)) continue;

                // 检查工具名是否合法（只含字母、数字、下划线）
                if (!IsValidToolName(name)) continue;

                // 尝试规范化 arguments JSON
                string normalizedArgs = NormalizeArguments(arguments);

                results.Add(new ToolCall
                {
                    Id = $"xml_{Guid.NewGuid():N}",
                    Name = name,
                    Arguments = normalizedArgs
                });
            }

            return results;
        }

        /// <summary>
        /// 从文本中移除 XML 工具调用标签，保留纯文本
        /// </summary>
        public static string StripToolInvocationTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return ToolInvocationRegex.Replace(text, "").Trim();
        }

        /// <summary>
        /// 检查文本是否包含任何工具调用标签
        /// </summary>
        public static bool ContainsToolInvocation(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            return ToolInvocationRegex.IsMatch(text);
        }

        private static bool IsValidToolName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            // 只允许字母、数字、下划线、连字符（防止注入）
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 对 arguments JSON 进行基本规范化（去除多余空白）
        /// 若解析失败，原样返回
        /// </summary>
        private static string NormalizeArguments(string json)
        {
            if (string.IsNullOrEmpty(json)) return "{}";

            try
            {
                // 尝试用 Newtonsoft.Json 解析并重新序列化（自动规范化）
                var obj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                if (obj != null)
                    return Newtonsoft.Json.JsonConvert.SerializeObject(obj);
            }
            catch
            {
                // 某些模型输出的 JSON 可能不标准，保持原样
            }

            return json;
        }
    }
}
