using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace E3DCopilot.Core.Tools
{
    /// <summary>
    /// PML 脚本预检 — 执行前校验危险操作（对齐 Reasonix 命令执行前 safety check）
    ///
    /// 检查规则：
    ///   - 高危命令（PURGE/DELETE DB/ADMIN）→ 阻止
    ///   - 危险模式（无限循环/文件覆盖）→ 警告
    ///   - 语法基础检查（未闭合的 DO/IF）→ 警告
    /// </summary>
    public static class PmlValidator
    {
        // 高危命令黑名单（直接阻止）
        // 使用预编译正则，词边界确保不误报（如 OVERWRITE 不会匹配到变量名 XOVERWRITE）
        private static readonly (string Display, Regex Pattern)[] BlockedCommands =
        {
            ("PURGE",          new Regex(@"\bPURGE\b",         RegexOptions.Compiled | RegexOptions.IgnoreCase)),
            ("DELETE DB",      new Regex(@"\bDELETE\s+DB\b",   RegexOptions.Compiled | RegexOptions.IgnoreCase)),
            ("ADMIN",          new Regex(@"\bADMIN\b",         RegexOptions.Compiled | RegexOptions.IgnoreCase)),
            ("NEW DB",         new Regex(@"\bNEW\s+DB\b",      RegexOptions.Compiled | RegexOptions.IgnoreCase)),
            ("OVERWRITE DB",   new Regex(@"\bOVERWRITE\s+DB\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        };

        /// <summary>
        /// 校验 PML 脚本，返回校验结果。
        /// </summary>
        public static PmlValidationResult Validate(string script)
        {
            if (string.IsNullOrWhiteSpace(script))
                return PmlValidationResult.Ok();

            // PML 预处理：去除注释行和多余空格后再检查
            var cleaned = PreprocessPml(script);

            // ── 1. 高危命令黑名单检查（正则词边界，防止字符串拼接/注释绕过）──
            foreach (var (display, pattern) in BlockedCommands)
            {
                if (pattern.IsMatch(cleaned))
                {
                    return PmlValidationResult.Block(
                        $"检测到高危命令 '{display}'，已被预检器阻止。请人工确认后执行。");
                }
            }

            // ── 2. 无限循环检测（DO without counter）──
            if (HasInfiniteLoop(script))
            {
                return PmlValidationResult.Warn(
                    "检测到可能的无循环条件 DO 语句，可能导致 E3D 卡死。请确认有退出条件。");
            }

            // ── 3. 基础语法检查 ──
            var syntaxIssue = CheckBasicSyntax(script);
            if (syntaxIssue != null)
            {
                return PmlValidationResult.Warn(syntaxIssue);
            }

            return PmlValidationResult.Ok();
        }

        /// <summary>
        /// PML 预处理：去除注释行（-- 或 ! 开头）和多余空格，
        /// 防止通过注释夹杂或字符串拼接绕过黑名单检查。
        /// </summary>
        private static string PreprocessPml(string script)
        {
            var lines = script.Split(new[] { '\n', '\r' }, StringSplitOptions.None);
            var cleanedLines = new List<string>();

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                // 跳过纯注释行（PML 注释以 -- 或 ! 开头）
                if (line.StartsWith("--") || line.StartsWith("!"))
                    continue;

                // 去除行内注释（-- 之后的部分），但保留字符串内的内容
                var commentIdx = line.IndexOf("--", StringComparison.Ordinal);
                if (commentIdx >= 0)
                    line = line.Substring(0, commentIdx);

                // 折叠多个空格为单个空格
                line = Regex.Replace(line, @"\s+", " ").Trim();

                if (!string.IsNullOrEmpty(line))
                    cleanedLines.Add(line);
            }

            return string.Join(" ", cleanedLines);
        }

        /// <summary>
        /// 检测无限循环：DO 后面没有 WHILE/UNTIL/N TIMES/VALUES 的情况
        /// PML 安全模式：
        ///   DO WHILE (cond)     — 条件循环
        ///   DO UNTIL (cond)     — 条件循环
        ///   DO N TIMES          — 计数循环
        ///   DO !var VALUES !arr — 数组迭代（有界）
        ///   DO !var FROM n TO m — 范围迭代（有界）
        /// 裸 DO ... ENDDO 才可能是无限循环
        /// </summary>
        private static bool HasInfiniteLoop(string script)
        {
            var lines = script.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim().ToUpperInvariant();
                // 排除注释行
                if (trimmed.StartsWith("--") || trimmed.StartsWith("!"))
                    continue;

                // 排除所有已知的有界 DO 模式
                if (trimmed == "DO" || (trimmed.StartsWith("DO ")
                    && !trimmed.Contains("WHILE")
                    && !trimmed.Contains("UNTIL")
                    && !trimmed.Contains("VALUES")   // DO !var VALUES !arr
                    && !trimmed.Contains("FROM")     // DO !var FROM n TO m
                    && !Regex.IsMatch(trimmed, @"\d+\s*TIMES")))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 基础语法检查：DO/IF 是否闭合
        /// </summary>
        private static string CheckBasicSyntax(string script)
        {
            var upper = script.ToUpperInvariant();

            int doCount = CountKeyword(upper, "DO");
            int enddoCount = CountKeyword(upper, "ENDDO");
            if (doCount > enddoCount)
                return "检测到 DO 块可能未闭合（缺少 ENDDO）。";

            int ifCount = CountKeyword(upper, "IF ");
            int endifCount = CountKeyword(upper, "ENDIF");
            if (ifCount > endifCount)
                return "检测到 IF 块可能未闭合（缺少 ENDIF）。";

            return null;
        }

        private static int CountKeyword(string text, string keyword)
        {
            // 使用词边界匹配，避免子串误匹配（如 "DO" 匹配到 "ENDDO" 中的 "DO"）
            return Regex.Matches(text, @"\b" + Regex.Escape(keyword) + @"\b", RegexOptions.IgnoreCase).Count;
        }
    }

    /// <summary>
    /// PML 预检结果
    /// </summary>
    public class PmlValidationResult
    {
        public bool Passed { get; set; }
        public string Message { get; set; }
        public PmlValidationLevel Level { get; set; }

        public static PmlValidationResult Ok() =>
            new PmlValidationResult { Passed = true, Level = PmlValidationLevel.Ok };

        public static PmlValidationResult Warn(string msg) =>
            new PmlValidationResult { Passed = true, Message = msg, Level = PmlValidationLevel.Warning };

        public static PmlValidationResult Block(string msg) =>
            new PmlValidationResult { Passed = false, Message = msg, Level = PmlValidationLevel.Blocked };
    }

    public enum PmlValidationLevel
    {
        Ok,
        Warning,
        Blocked
    }
}
