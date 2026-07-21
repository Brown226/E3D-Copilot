using System;
using System.Text;
using System.Text.RegularExpressions;

namespace E3DCopilot.Core.Tools
{
    /// <summary>
    /// PML 预处理器 — 自动修正 LLM 常犯的 PML 语法错误。
    /// 在 PML 脚本发送到 E3D 执行前进行「安全增强」。
    ///
    /// 设计原则（对齐 P2 修复）：
    ///   1. 只做可证明安全的替换，绝不修改字符串字面量（'...' / |...|）与 {..} 插值内容；
    ///   2. 单趟扫描 + 状态机保护，避免旧版「全行正则」误伤字符串里的 == / && / ; 等；
    ///   3. 无法确定的改写宁可不改（留给 RunInPdms + PmlErrorMapper 回传真实错误）。
    ///
    /// 修正规则：
    ///   - // 注释 → -- 注释；行首 # → --（字符串内的 // 不动）
    ///   - == → EQ, != / &lt;&gt; → NE, &gt;= → GE, &lt;= → LE, &gt; → GT, &lt; → LT
    ///   - &amp;&amp; → AND, || → OR
    ///   - print()/echo()/console.log() → $P
    ///   - $P 行中 CODE 段的 + 拼接 → 逗号（{..} 内的算术 + 保留）
    ///   - 移除行尾分号（字符串内的 ; 不动）
    /// </summary>
    public static class PmlPreProcessor
    {
        /// <summary>
        /// 预处理 PML 脚本，修正常见 LLM 语法错误。
        /// 返回修正后的脚本。如果无修改则返回原文（保持原始换行/空白）。
        /// </summary>
        public static string Process(string script)
        {
            if (string.IsNullOrWhiteSpace(script))
                return script;

            var lines = script.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var sb = new StringBuilder();
            bool modified = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string fixed_ = FixLine(line);
                if (fixed_ != line) modified = true;
                sb.AppendLine(fixed_);
            }

            // 移除末尾多余空行
            string result = sb.ToString().TrimEnd('\r', '\n');
            return modified ? result : script;
        }

        /// <summary>
        /// 修正单行 PML 代码
        /// </summary>
        private static string FixLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return line;

            string indent = line.Substring(0, line.Length - line.TrimStart().Length);
            string trimmed = line.TrimStart();

            // 已经是 PML 注释（-- 开头），不处理
            if (trimmed.StartsWith("--"))
                return line;

            // 行首 # 注释（Python 风格）→ 整行转注释
            if (trimmed.StartsWith("#"))
                return indent + "--" + trimmed.Substring(1);

            // print/echo/console.log(...) → $P（行级，仅当整行是打印语句）
            trimmed = FixPrintStatements(trimmed);

            // 单趟扫描修正（字符串/竖线串/花括号插值内一律不动）
            bool isDollarP = trimmed.StartsWith("$P ", StringComparison.OrdinalIgnoreCase)
                             || trimmed.Equals("$P", StringComparison.OrdinalIgnoreCase)
                             || trimmed.StartsWith("$p", StringComparison.Ordinal);
            string scanned = ScanAndFix(trimmed, isDollarP);

            return indent + scanned;
        }

        /// <summary>
        /// 单趟状态机扫描：仅在 CODE 状态（非字符串、非花括号）内做替换。
        /// </summary>
        private static string ScanAndFix(string s, bool isDollarP)
        {
            var sb = new StringBuilder(s.Length + 8);
            int n = s.Length;
            int braceDepth = 0;

            for (int i = 0; i < n; i++)
            {
                char c = s[i];

                // ── 单引号字符串：原样复制，'' 视为转义 ──
                if (c == '\'')
                {
                    sb.Append(c);
                    i++;
                    while (i < n)
                    {
                        char d = s[i];
                        sb.Append(d);
                        if (d == '\'')
                        {
                            if (i + 1 < n && s[i + 1] == '\'') { sb.Append('\''); i += 2; continue; }
                            break; // 字符串结束
                        }
                        i++;
                    }
                    continue;
                }

                // ── 竖线串 |...|：原样复制 ──
                if (c == '|')
                {
                    sb.Append(c);
                    i++;
                    while (i < n)
                    {
                        char d = s[i];
                        sb.Append(d);
                        if (d == '|') break;
                        i++;
                    }
                    continue;
                }

                // ── 花括号插值 {...}：原样复制（内部算术/表达式保留）──
                if (c == '{')
                {
                    braceDepth++;
                    sb.Append(c);
                    continue;
                }
                if (braceDepth > 0)
                {
                    if (c == '}') braceDepth--;
                    sb.Append(c);
                    continue;
                }

                // ── CODE 状态：注释与运算符修正 ──
                char next = (i + 1 < n) ? s[i + 1] : '\0';

                // // 行内注释 → -- 注释，其后原样保留并结束
                if (c == '/' && next == '/')
                {
                    sb.Append("--");
                    sb.Append(s.Substring(i + 2));
                    return sb.ToString();
                }

                // 两字符运算符
                if (c == '=' && next == '=') { AppendWord(sb, "EQ", CharAt(s, i + 2)); i++; continue; }
                if (c == '!' && next == '=') { AppendWord(sb, "NE", CharAt(s, i + 2)); i++; continue; }
                if (c == '<' && next == '>') { AppendWord(sb, "NE", CharAt(s, i + 2)); i++; continue; }
                if (c == '>' && next == '=') { AppendWord(sb, "GE", CharAt(s, i + 2)); i++; continue; }
                if (c == '<' && next == '=') { AppendWord(sb, "LE", CharAt(s, i + 2)); i++; continue; }
                if (c == '&' && next == '&') { AppendWord(sb, "AND", CharAt(s, i + 2)); i++; continue; }
                if (c == '|' && next == '|') { AppendWord(sb, "OR", CharAt(s, i + 2)); i++; continue; }

                // 单字符比较运算符（CODE 段内一律视为比较）
                if (c == '>') { AppendWord(sb, "GT", next); continue; }
                if (c == '<') { AppendWord(sb, "LT", next); continue; }

                // $P 行的 + 拼接 → 逗号（{..} 内已被保护，不会到这里）
                if (c == '+' && isDollarP)
                {
                    TrimTrailingSpaces(sb);
                    sb.Append(',');
                    continue;
                }

                sb.Append(c);
            }

            // 行尾分号（CODE 状态收尾时）
            TrimTrailingSpaces(sb);
            while (sb.Length > 0 && sb[sb.Length - 1] == ';')
            {
                sb.Length--;
                TrimTrailingSpaces(sb);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 追加一个「词法运算符」，自动补齐前后空格（避免与相邻标识符黏连或产生多余空格）。
        /// </summary>
        private static void AppendWord(StringBuilder sb, string word, char nextChar)
        {
            if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
                sb.Append(' ');
            sb.Append(word);
            if (nextChar != '\0' && nextChar != ' ')
                sb.Append(' ');
        }

        private static char CharAt(string s, int idx)
        {
            return (idx >= 0 && idx < s.Length) ? s[idx] : '\0';
        }

        private static void TrimTrailingSpaces(StringBuilder sb)
        {
            while (sb.Length > 0 && sb[sb.Length - 1] == ' ')
                sb.Length--;
        }

        /// <summary>
        /// 修正 print/echo/console.log → $P（仅当整行是打印语句）
        /// </summary>
        private static string FixPrintStatements(string trimmed)
        {
            var printMatch = Regex.Match(trimmed, @"^(print|echo|console\.log)\s*\((.+)\)\s*;?\s*$", RegexOptions.IgnoreCase);
            if (printMatch.Success)
            {
                string arg = printMatch.Groups[2].Value.Trim();
                if ((arg.StartsWith("'") && arg.EndsWith("'")) ||
                    (arg.StartsWith("\"") && arg.EndsWith("\"")))
                {
                    arg = arg.Substring(1, arg.Length - 2);
                    return "$P '" + arg + "'";
                }
                return "$P " + arg;
            }
            return trimmed;
        }
    }
}
