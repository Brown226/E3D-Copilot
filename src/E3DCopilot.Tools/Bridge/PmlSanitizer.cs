using System;
using System.Text.RegularExpressions;

namespace E3DCopilot.Tools.Bridge
{
    /// <summary>
    /// PML 输入安全过滤器
    /// 防止 PML 注入攻击：用户输入直接拼接到 PML 脚本可能导致非预期命令执行
    /// 
    /// 安全策略：
    /// 1. 字符串值转义（单引号 doubling）
    /// 2. 元素名/属性名白名单验证
    /// 3. 危险关键字拦截
    /// </summary>
    public static class PmlSanitizer
    {
        // ── 合法元素名模式：字母开头，允许字母/数字/连字符/下划线/斜杠 ──
        // E3D 元素名示例: PIPE-001, /ZONE-01/SITE-02, EQUI_001A
        private static readonly Regex ValidElementNamePattern = new Regex(
            @"^/?[A-Za-z][A-Za-z0-9\-_/]*$",
            RegexOptions.Compiled);

        // ── 合法属性名模式：字母开头，允许字母/数字/下划线 ──
        // E3D 属性示例: NAME, WTHK, DIA, SPEC, ROOM_NO
        private static readonly Regex ValidAttributeNamePattern = new Regex(
            @"^[A-Za-z][A-Za-z0-9_]*$",
            RegexOptions.Compiled);

        // ── 合法类型名模式：全大写字母，2-6 个字符 ──
        // E3D 类型示例: PIPE, EQUI, VALV, STRU, ZONE, SITE, BRAN, NOZZ
        private static readonly Regex ValidTypeNamePattern = new Regex(
            @"^[A-Z]{2,6}$",
            RegexOptions.Compiled);

        // ── 危险 PML 关键字（在用户输入值中拦截）──
        private static readonly string[] DangerousKeywords = new[]
        {
            "DELETE", "REMOVE", "DESTROY", "PURGE",
            "EXECUTE", "RUN", "SHELL", "SYSTEM",
            "IMPORT", "EXPORT", "WRITE", "OVERWRITE",
            "FILE", "DIRECTORY", "PATH"
        };

        /// <summary>
        /// 转义 PML 字符串值（用于引号内的值）
        /// PML 使用单引号 doubling 来转义：' → ''
        /// </summary>
        /// <param name="value">原始值</param>
        /// <returns>转义后的安全值</returns>
        public static string EscapeString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            // 1. 单引号 doubling（PML 标准转义）
            var escaped = value.Replace("'", "''");

            // 2. 移除分号（防止语句注入）
            escaped = escaped.Replace(";", "");

            // 3. 移除换行符（防止多行注入）
            escaped = escaped.Replace("\r", "").Replace("\n", "");

            return escaped;
        }

        /// <summary>
        /// 验证并清理元素名
        /// </summary>
        /// <param name="elementName">元素名</param>
        /// <returns>清理后的元素名</returns>
        /// <exception cref="ArgumentException">元素名不合法时抛出</exception>
        public static string SanitizeElementName(string elementName)
        {
            if (string.IsNullOrWhiteSpace(elementName))
                throw new ArgumentException("元素名不能为空");

            var trimmed = elementName.Trim();

            // 允许通配符模式（用于查询）
            if (trimmed.Contains("*") || trimmed.Contains("?"))
            {
                return SanitizePattern(trimmed);
            }

            if (!ValidElementNamePattern.IsMatch(trimmed))
            {
                throw new ArgumentException(
                    $"非法元素名: '{elementName}'。元素名只能包含字母、数字、连字符、下划线和斜杠。");
            }

            return trimmed.ToUpperInvariant();
        }

        /// <summary>
        /// 验证并清理通配符模式（用于 Matchwild 查询）
        /// </summary>
        /// <param name="pattern">通配符模式</param>
        /// <returns>清理后的模式</returns>
        public static string SanitizePattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return "*";

            var trimmed = pattern.Trim();

            // 只允许：字母、数字、连字符、下划线、斜杠、星号、问号
            var sanitized = Regex.Replace(trimmed, @"[^A-Za-z0-9\-_/*?]", "");

            if (string.IsNullOrEmpty(sanitized))
                return "*";

            return EscapeString(sanitized);
        }

        /// <summary>
        /// 验证并清理属性名
        /// </summary>
        /// <param name="attributeName">属性名</param>
        /// <returns>清理后的属性名</returns>
        /// <exception cref="ArgumentException">属性名不合法时抛出</exception>
        public static string SanitizeAttributeName(string attributeName)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
                throw new ArgumentException("属性名不能为空");

            var trimmed = attributeName.Trim().ToUpperInvariant();

            if (!ValidAttributeNamePattern.IsMatch(trimmed))
            {
                throw new ArgumentException(
                    $"非法属性名: '{attributeName}'。属性名只能包含字母、数字和下划线。");
            }

            return trimmed;
        }

        /// <summary>
        /// 验证并清理元素类型名
        /// </summary>
        /// <param name="typeName">类型名</param>
        /// <returns>清理后的类型名</returns>
        /// <exception cref="ArgumentException">类型名不合法时抛出</exception>
        public static string SanitizeTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException("类型名不能为空");

            var trimmed = typeName.Trim().ToUpperInvariant();

            if (!ValidTypeNamePattern.IsMatch(trimmed))
            {
                throw new ArgumentException(
                    $"非法类型名: '{typeName}'。类型名必须是 2-6 个大写字母（如 PIPE, EQUI, VALV）。");
            }

            return trimmed;
        }

        /// <summary>
        /// 验证属性值（用于修改操作）
        /// 拦截包含危险关键字的值
        /// </summary>
        /// <param name="value">属性值</param>
        /// <returns>转义后的安全值</returns>
        /// <exception cref="ArgumentException">值包含危险内容时抛出</exception>
        public static string SanitizeValue(string value)
        {
            if (value == null)
                return "";

            var upperValue = value.ToUpperInvariant();

            // 检查是否包含危险关键字（作为独立单词）
            foreach (var keyword in DangerousKeywords)
            {
                // 使用单词边界匹配，避免误杀（如 "PIPELINE" 不应被 "PIPE" 拦截）
                if (Regex.IsMatch(upperValue, $@"\b{keyword}\b"))
                {
                    throw new ArgumentException(
                        $"属性值包含不允许的关键字: '{keyword}'。如确需此操作，请使用 execute_pml 工具。");
                }
            }

            return EscapeString(value);
        }

        /// <summary>
        /// 验证范围/作用域名（scope）
        /// </summary>
        /// <param name="scope">作用域</param>
        /// <returns>清理后的作用域，null 表示全局</returns>
        public static string SanitizeScope(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
                return null;

            var trimmed = scope.Trim();

            // CE 表示当前元素
            if (trimmed.Equals("CE", StringComparison.OrdinalIgnoreCase))
                return null; // CE 是默认行为，不需要 for 子句

            return SanitizeElementName(trimmed);
        }

        /// <summary>
        /// 综合验证：检查输入是否安全（不抛出异常，返回布尔值）
        /// </summary>
        public static bool IsSafe(string input)
        {
            if (string.IsNullOrEmpty(input))
                return true;

            var upper = input.ToUpperInvariant();

            // 检查危险关键字
            foreach (var keyword in DangerousKeywords)
            {
                if (upper.Contains(keyword))
                    return false;
            }

            // 检查语句分隔符
            if (input.Contains(";") || input.Contains("\n"))
                return false;

            return true;
        }
    }
}
