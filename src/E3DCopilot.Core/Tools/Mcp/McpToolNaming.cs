using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace E3DCopilot.Core.Tools.Mcp
{
    /// <summary>
    /// MCP 工具名规范化 — 对齐 Reasonix normalizeName + toolName + ToolPrefix。
    /// 生成模型可见的命名空间: mcp__&lt;server&gt;__&lt;tool&gt;
    /// </summary>
    public static class McpToolNaming
    {
        private static readonly Regex InvalidNameChars = new Regex(@"[^a-zA-Z0-9_-]+", RegexOptions.Compiled);

        /// <summary>
        /// 构建模型可见的工具全名: mcp__&lt;server&gt;__&lt;tool&gt;
        /// 对齐 Reasonix toolName(server, raw)
        /// </summary>
        public static string ToolName(string server, string rawTool)
        {
            return ToolPrefix(server) + NormalizeName(rawTool);
        }

        /// <summary>
        /// 工具命名空间前缀: mcp__&lt;server&gt;__
        /// 对齐 Reasonix ToolPrefix(server)
        /// </summary>
        public static string ToolPrefix(string server)
        {
            return "mcp__" + NormalizeName(server) + "__";
        }

        /// <summary>
        /// 规范化名称：非法字符替换为下划线，冲突时追加哈希后缀。
        /// 对齐 Reasonix normalizeName(s)
        /// </summary>
        public static string NormalizeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "unnamed";

            string raw = s;
            s = InvalidNameChars.Replace(s, "_").Trim('_');
            if (string.IsNullOrEmpty(s)) s = "unnamed";

            // 如果名称被修改过，追加短哈希避免冲突
            if (s != raw)
                s += "_" + ShortNameHash(raw);

            return s;
        }

        /// <summary>
        /// 应用 StripRawPrefix：去除工具原始名称的前缀。
        /// 对齐 Reasonix Spec.StripRawPrefix
        /// </summary>
        public static string ApplyStripPrefix(string rawName, string stripPrefix)
        {
            if (string.IsNullOrEmpty(stripPrefix)) return rawName;
            if (rawName.StartsWith(stripPrefix, StringComparison.Ordinal))
                return rawName.Substring(stripPrefix.Length);
            return rawName;
        }

        /// <summary>
        /// 判断工具名是否属于 MCP 命名空间
        /// </summary>
        public static bool IsMcpTool(string toolName)
        {
            return !string.IsNullOrEmpty(toolName) && toolName.StartsWith("mcp__", StringComparison.Ordinal);
        }

        /// <summary>
        /// 从 mcp__server__tool 格式中提取 server 名称
        /// </summary>
        public static string ExtractServerName(string mcpToolName)
        {
            if (!IsMcpTool(mcpToolName)) return null;
            // mcp__<server>__<tool> → 找第二个 __
            int firstSep = 5; // "mcp__".Length
            int secondSep = mcpToolName.IndexOf("__", firstSep, StringComparison.Ordinal);
            if (secondSep < 0) return null;
            return mcpToolName.Substring(firstSep, secondSep - firstSep);
        }

        /// <summary>
        /// FNV-1a 短哈希（6字符），对齐 Reasonix shortNameHash
        /// </summary>
        private static string ShortNameHash(string s)
        {
            // FNV-1a 32-bit
            uint hash = 2166136261;
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            foreach (byte b in bytes)
            {
                hash ^= b;
                hash *= 16777619;
            }
            return hash.ToString("x8").Substring(0, 6);
        }
    }
}
