using System;
using NUnit.Framework;
using E3DCopilot.Tools.Bridge;

namespace E3DCopilot.Tests
{
    /// <summary>
    /// PmlSanitizer 单元测试 — 验证 PML 注入防护
    /// </summary>
    [TestFixture]
    public class PmlSanitizerTests
    {
        // ═══════════════════════════════════════════
        //  EscapeString 测试
        // ═══════════════════════════════════════════

        [Test]
        public void EscapeString_SingleQuote_ShouldDouble()
        {
            var result = PmlSanitizer.EscapeString("it's a test");
            Assert.AreEqual("it''s a test", result);
        }

        [Test]
        public void EscapeString_Semicolon_ShouldRemove()
        {
            var result = PmlSanitizer.EscapeString("value; DELETE ALL");
            Assert.AreEqual("value DELETE ALL", result);
        }

        [Test]
        public void EscapeString_Newline_ShouldRemove()
        {
            var result = PmlSanitizer.EscapeString("line1\nline2\r\nline3");
            Assert.AreEqual("line1line2line3", result);
        }

        [Test]
        public void EscapeString_Empty_ShouldReturnEmpty()
        {
            Assert.AreEqual("", PmlSanitizer.EscapeString(""));
            Assert.AreEqual("", PmlSanitizer.EscapeString(null));
        }

        [Test]
        public void EscapeString_InjectionAttempt_ShouldNeutralize()
        {
            // 模拟注入攻击: '; DELETE ALL PIPE; '
            var result = PmlSanitizer.EscapeString("'; DELETE ALL PIPE; '");
            Assert.IsFalse(result.Contains(";"));
            Assert.IsFalse(result.Contains("\n"));
        }

        // ═══════════════════════════════════════════
        //  SanitizeElementName 测试
        // ═══════════════════════════════════════════

        [Test]
        public void SanitizeElementName_ValidName_ShouldUppercase()
        {
            var result = PmlSanitizer.SanitizeElementName("pipe-001");
            Assert.AreEqual("PIPE-001", result);
        }

        [Test]
        public void SanitizeElementName_PathWithSlash_ShouldPass()
        {
            var result = PmlSanitizer.SanitizeElementName("/ZONE-01/SITE-02");
            Assert.AreEqual("/ZONE-01/SITE-02", result);
        }

        [Test]
        public void SanitizeElementName_Wildcard_ShouldSanitizeAsPattern()
        {
            var result = PmlSanitizer.SanitizeElementName("*DN100*");
            Assert.IsTrue(result.Contains("*"));
        }

        [Test]
        public void SanitizeElementName_InvalidChars_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() =>
                PmlSanitizer.SanitizeElementName("PIPE; DELETE"));
        }

        [Test]
        public void SanitizeElementName_Empty_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() =>
                PmlSanitizer.SanitizeElementName(""));
        }

        // ═══════════════════════════════════════════
        //  SanitizeTypeName 测试
        // ═══════════════════════════════════════════

        [Test]
        public void SanitizeTypeName_ValidType_ShouldPass()
        {
            Assert.AreEqual("PIPE", PmlSanitizer.SanitizeTypeName("pipe"));
            Assert.AreEqual("EQUI", PmlSanitizer.SanitizeTypeName("EQUI"));
            Assert.AreEqual("VALV", PmlSanitizer.SanitizeTypeName("valv"));
        }

        [Test]
        public void SanitizeTypeName_TooLong_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() =>
                PmlSanitizer.SanitizeTypeName("PIPELONG"));
        }

        [Test]
        public void SanitizeTypeName_WithNumbers_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() =>
                PmlSanitizer.SanitizeTypeName("PIPE1"));
        }

        // ═══════════════════════════════════════════
        //  SanitizeAttributeName 测试
        // ═══════════════════════════════════════════

        [Test]
        public void SanitizeAttributeName_ValidAttr_ShouldUppercase()
        {
            Assert.AreEqual("WTHK", PmlSanitizer.SanitizeAttributeName("wthk"));
            Assert.AreEqual("ROOM_NO", PmlSanitizer.SanitizeAttributeName("room_no"));
        }

        [Test]
        public void SanitizeAttributeName_InvalidChars_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() =>
                PmlSanitizer.SanitizeAttributeName("ATTR; DELETE"));
        }

        // ═══════════════════════════════════════════
        //  SanitizeValue 测试
        // ═══════════════════════════════════════════

        [Test]
        public void SanitizeValue_NormalValue_ShouldEscape()
        {
            var result = PmlSanitizer.SanitizeValue("SCH40");
            Assert.AreEqual("SCH40", result);
        }

        [Test]
        public void SanitizeValue_DangerousKeyword_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() =>
                PmlSanitizer.SanitizeValue("DELETE ALL"));
        }

        [Test]
        public void SanitizeValue_KeywordInWord_ShouldNotThrow()
        {
            // "PIPELINE" 包含 "PIPE" 但不应被拦截（单词边界匹配）
            var result = PmlSanitizer.SanitizeValue("PIPELINE");
            Assert.AreEqual("PIPELINE", result);
        }

        // ═══════════════════════════════════════════
        //  SanitizeScope 测试
        // ═══════════════════════════════════════════

        [Test]
        public void SanitizeScope_CE_ShouldReturnNull()
        {
            Assert.IsNull(PmlSanitizer.SanitizeScope("CE"));
            Assert.IsNull(PmlSanitizer.SanitizeScope("ce"));
        }

        [Test]
        public void SanitizeScope_Empty_ShouldReturnNull()
        {
            Assert.IsNull(PmlSanitizer.SanitizeScope(""));
            Assert.IsNull(PmlSanitizer.SanitizeScope(null));
        }

        [Test]
        public void SanitizeScope_ValidElement_ShouldSanitize()
        {
            var result = PmlSanitizer.SanitizeScope("ZONE-01");
            Assert.AreEqual("ZONE-01", result);
        }

        // ═══════════════════════════════════════════
        //  IsSafe 测试
        // ═══════════════════════════════════════════

        [Test]
        public void IsSafe_NormalInput_ShouldReturnTrue()
        {
            Assert.IsTrue(PmlSanitizer.IsSafe("SCH40"));
            Assert.IsTrue(PmlSanitizer.IsSafe("DN100"));
        }

        [Test]
        public void IsSafe_DangerousInput_ShouldReturnFalse()
        {
            Assert.IsFalse(PmlSanitizer.IsSafe("DELETE ALL"));
            Assert.IsFalse(PmlSanitizer.IsSafe("value; next command"));
            Assert.IsFalse(PmlSanitizer.IsSafe("line1\nline2"));
        }
    }
}
