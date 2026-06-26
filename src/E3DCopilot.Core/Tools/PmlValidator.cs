using System;
using System.Collections.Generic;
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
        private static readonly string[] BlockedCommands =
        {
            "PURGE",          // 批量删除元素
            "DELETE DB",      // 删除数据库
            "ADMIN",          // 管理员操作
            "NEW DB",         // 新建数据库（会清空当前）
            "OVERWRITE DB",   // 覆盖数据库
        };

        /// <summary>
        /// 校验 PML 脚本，返回校验结果。
        /// </summary>
        public static PmlValidationResult Validate(string script)
        {
            if (string.IsNullOrWhiteSpace(script))
                return PmlValidationResult.Ok();

            var upper = script.ToUpperInvariant();

            // ── 1. 高危命令黑名单检查 ──
            foreach (var cmd in BlockedCommands)
            {
                if (upper.Contains(cmd))
                {
                    return PmlValidationResult.Block(
                        $"检测到高危命令 '{cmd}'，已被预检器阻止。请人工确认后执行。");
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
        /// 检测无限循环：DO 后面没有 WHILE/UNTIL/N 次 的情况
        /// </summary>
        private static bool HasInfiniteLoop(string script)
        {
            // 匹配单独的 DO 行（没有跟随循环条件）
            // PML 中 DO WHILE / DO UNTIL / DO 10 TIMES 是安全的
            // 裸 DO ... ENDDO 可能是无限循环
            var lines = script.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim().ToUpperInvariant();
                if (trimmed == "DO" || trimmed.StartsWith("DO ") && !trimmed.Contains("WHILE") && !trimmed.Contains("UNTIL") && !Regex.IsMatch(trimmed, @"\d+\s*TIMES"))
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
            int count = 0;
            int idx = 0;
            while ((idx = text.IndexOf(keyword, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                idx += keyword.Length;
            }
            return count;
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
