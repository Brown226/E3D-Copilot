using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace E3DCopilot.Core.Security
{
    /// <summary>
    /// 密钥 / 敏感信息脱敏器（规则式，参考 Reasonix internal/secrets）。
    /// 用于 LLM 输出与日志中自动遮蔽密钥、连接串、令牌、私钥等，避免泄露。
    /// net48 / C# 7.3 兼容：仅使用正则与基础字符串操作。
    /// </summary>
    public sealed class SecretRedactor
    {
        private static readonly List<RedactRule> Rules = BuildRules();

        private static List<RedactRule> BuildRules()
        {
            var list = new List<RedactRule>();

            // 1. OpenAI / 通用 sk- 密钥
            list.Add(new RedactRule(
                new Regex(@"\bsk-(?:sk-)?[A-Za-z0-9_\-]{20,}\b", RegexOptions.IgnoreCase),
                m => "sk-" + MaskTail(m.Value.Substring(3))));

            // 2. Anthropic 密钥
            list.Add(new RedactRule(
                new Regex(@"\bsk-ant-[A-Za-z0-9_\-]{20,}\b", RegexOptions.IgnoreCase),
                m => "sk-ant-" + MaskTail(m.Value.Substring(7))));

            // 3. AWS Access Key
            list.Add(new RedactRule(
                new Regex(@"\bAKIA[0-9A-Z]{16}\b"),
                m => MaskToken(m.Value)));

            // 4. JWT
            list.Add(new RedactRule(
                new Regex(@"\beyJ[A-Za-z0-9_\-]+\.eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\b"),
                m => "[REDACTED JWT]"));

            // 5. 带凭据的连接串
            list.Add(new RedactRule(
                new Regex(@"\b(mongodb(\+srv)?|postgres(?:ql)?|mysql|redis|amqp|ftp|https?)://[^\s'""<>]+", RegexOptions.IgnoreCase),
                m => "[REDACTED CONNECTION STRING]"));

            // 6. URL 中的 user:pass@
            list.Add(new RedactRule(
                new Regex(@"(://)([^:@/\s]+):([^@/\s]+)@"),
                m => m.Groups[1].Value + m.Groups[2].Value + ":****@"));

            // 7. 密码 / 密钥赋值
            list.Add(new RedactRule(
                new Regex(@"\b(password|passwd|pwd|secret|token|api[_-]?key|apiKey|access[_-]?token)\s*[:=]\s*[""']?[^\s""'<>]{6,}[""']?", RegexOptions.IgnoreCase),
                m => MaskAssignment(m.Value)));

            // 8. 私钥块
            list.Add(new RedactRule(
                new Regex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----"),
                m => "[REDACTED PRIVATE KEY]"));

            return list;
        }

        /// <summary>对文本执行全部脱敏规则，返回脱敏后文本（无匹配则原样返回）。</summary>
        public static string Redact(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var result = text;
            foreach (var rule in Rules)
                result = rule.Rx.Replace(result, rule.Mask);
            return result;
        }

        /// <summary>是否包含敏感信息。</summary>
        public static bool ContainsSecret(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var rule in Rules)
                if (rule.Rx.IsMatch(text)) return true;
            return false;
        }

        /// <summary>尝试脱敏，返回是否发生了替换。</summary>
        public static bool TryRedact(string text, out string redacted)
        {
            redacted = Redact(text);
            return redacted != text;
        }

        // ── 辅助 ──

        private static string MaskToken(string s)
        {
            if (s.Length <= 8) return "********";
            return s.Substring(0, 4) + "********" + s.Substring(s.Length - 4);
        }

        private static string MaskTail(string s)
        {
            if (s.Length <= 6) return "********";
            return s.Substring(0, 3) + "********" + s.Substring(s.Length - 3);
        }

        private static string MaskAssignment(string value)
        {
            var eq = value.IndexOfAny(new[] { '=', ':' });
            if (eq < 0) return "[REDACTED]";
            return value.Substring(0, eq + 1) + " ********";
        }

        private class RedactRule
        {
            public Regex Rx;
            public MatchEvaluator Mask;
            public RedactRule(Regex rx, MatchEvaluator mask)
            {
                Rx = rx;
                Mask = mask;
            }
        }
    }
}
