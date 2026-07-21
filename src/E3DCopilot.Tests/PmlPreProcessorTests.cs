using NUnit.Framework;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Tests
{
    /// <summary>
    /// PmlPreProcessor 单元测试 — 验证「安全增强」：修正 LLM 常犯语法错误，
    /// 但绝不破坏字符串字面量与 {..} 插值内容。
    /// </summary>
    [TestFixture]
    public class PmlPreProcessorTests
    {
        // ── 注释修正 ──
        [Test]
        public void Comment_SlashSlash_BecomesDashDash()
        {
            Assert.AreEqual("!x = 1 -- note", PmlPreProcessor.Process("!x = 1 // note"));
        }

        [Test]
        public void Comment_LeadingHash_BecomesDashDash()
        {
            Assert.AreEqual("-- note", PmlPreProcessor.Process("# note"));
        }

        [Test]
        public void Comment_AlreadyDashDash_Unchanged()
        {
            Assert.AreEqual("-- already", PmlPreProcessor.Process("-- already"));
        }

        [Test]
        public void Comment_SlashInsideString_NotTreatedAsComment()
        {
            // 字符串里的 // 不能被当成注释
            Assert.AreEqual("!url = 'http://a//b'", PmlPreProcessor.Process("!url = 'http://a//b'"));
        }

        // ── 比较运算符 ──
        [Test]
        public void Comparison_DoubleEquals_BecomesEq()
        {
            Assert.AreEqual("IF !t EQ 'PIPE' THEN", PmlPreProcessor.Process("IF !t == 'PIPE' THEN"));
        }

        [Test]
        public void Comparison_NotEquals_BecomesNe()
        {
            Assert.AreEqual("IF !t NE 'X' THEN", PmlPreProcessor.Process("IF !t != 'X' THEN"));
        }

        [Test]
        public void Comparison_GreaterEqual_BecomesGe()
        {
            Assert.AreEqual("IF !n GE 0 THEN", PmlPreProcessor.Process("IF !n >= 0 THEN"));
        }

        [Test]
        public void Comparison_InsideStringLiteral_NotChanged()
        {
            // 单引号字符串内的 == 必须原样保留
            Assert.AreEqual("!x = 'a==b'", PmlPreProcessor.Process("!x = 'a==b'"));
        }

        // ── 逻辑运算符 ──
        [Test]
        public void Logical_AndOr_Converted()
        {
            Assert.AreEqual("IF !a EQ 'X' AND !b NE 'Y' THEN",
                PmlPreProcessor.Process("IF !a == 'X' && !b != 'Y' THEN"));
        }

        [Test]
        public void Logical_InsideString_NotChanged()
        {
            Assert.AreEqual("!x = 'a&&b'", PmlPreProcessor.Process("!x = 'a&&b'"));
        }

        // ── print/echo → $P ──
        [Test]
        public void Print_QuotedString_BecomesDollarP()
        {
            Assert.AreEqual("$P 'hi'", PmlPreProcessor.Process("print('hi')"));
        }

        // ── 行尾分号 ──
        [Test]
        public void TrailingSemicolon_Removed()
        {
            Assert.AreEqual("!x = 1", PmlPreProcessor.Process("!x = 1;"));
        }

        [Test]
        public void Semicolon_InsideString_NotRemoved()
        {
            Assert.AreEqual("!x = 'a;b'", PmlPreProcessor.Process("!x = 'a;b'"));
        }

        // ── $P 的 + 拼接 → 逗号 ──
        [Test]
        public void DollarP_PlusConcat_BecomesComma()
        {
            Assert.AreEqual("$P 'total ', !n", PmlPreProcessor.Process("$P 'total ' + !n"));
        }

        [Test]
        public void DollarP_PlusInsideBraces_Preserved()
        {
            // {..} 插值内的算术加号是合法的，禁止改成逗号
            Assert.AreEqual("$P {!a + !b}", PmlPreProcessor.Process("$P {!a + !b}"));
        }

        [Test]
        public void NonDollarP_PlusConcat_Preserved()
        {
            // 普通赋值里的 + 是合法字符串/数值拼接，不能动
            Assert.AreEqual("!msg = 'a' + 'b'", PmlPreProcessor.Process("!msg = 'a' + 'b'"));
        }

        // ── 幂等 / 无改动 ──
        [Test]
        public void ValidPml_ReturnsUnchanged()
        {
            string valid = "VAR !items COLL ALL PIPE FOR CE\r\nDO !p VALUES !items\r\n  $P {!p.Name}\r\nENDDO";
            Assert.AreEqual(valid, PmlPreProcessor.Process(valid));
        }

        [Test]
        public void Empty_ReturnsInput()
        {
            Assert.AreEqual("", PmlPreProcessor.Process(""));
            Assert.IsNull(PmlPreProcessor.Process(null));
        }
    }
}
