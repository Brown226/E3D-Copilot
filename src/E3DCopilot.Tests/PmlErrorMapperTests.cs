using NUnit.Framework;
using E3DCopilot.Tools.Bridge;

namespace E3DCopilot.Tests
{
    /// <summary>
    /// PmlErrorMapper 单元测试 — 验证 PML 错误映射
    /// </summary>
    [TestFixture]
    public class PmlErrorMapperTests
    {
        // ═══════════════════════════════════════════
        //  按错误码映射
        // ═══════════════════════════════════════════

        [Test]
        public void MapError_KnownCode_ShouldReturnDescription()
        {
            var result = PmlErrorMapper.MapError("some error", 100);
            Assert.IsTrue(result.Contains("元素不存在"));
            Assert.IsTrue(result.Contains("修复建议"));
        }

        [Test]
        public void MapError_SyntaxErrorCode_ShouldReturnSyntaxInfo()
        {
            var result = PmlErrorMapper.MapError("unexpected token", 1);
            Assert.IsTrue(result.Contains("语法错误"));
        }

        [Test]
        public void MapError_UnknownCode_ShouldReturnOriginal()
        {
            var result = PmlErrorMapper.MapError("mysterious error", 9999);
            Assert.IsTrue(result.Contains("mysterious error"));
        }

        // ═══════════════════════════════════════════
        //  从错误消息中提取错误码
        // ═══════════════════════════════════════════

        [Test]
        public void MapError_MessageWithCode_ShouldExtractAndMap()
        {
            var result = PmlErrorMapper.MapError("Error 100: element not found");
            Assert.IsTrue(result.Contains("元素不存在"));
        }

        [Test]
        public void MapError_MessageWithCodeFormat2_ShouldExtract()
        {
            var result = PmlErrorMapper.MapError("Error: 105");
            Assert.IsTrue(result.Contains("元素已锁定"));
        }

        // ═══════════════════════════════════════════
        //  模式匹配
        // ═══════════════════════════════════════════

        [Test]
        public void MapError_NotExistPattern_ShouldMapTo100()
        {
            var result = PmlErrorMapper.MapError("Element PIPE-999 does not exist");
            Assert.IsTrue(result.Contains("元素不存在"));
        }

        [Test]
        public void MapError_SyntaxErrorPattern_ShouldMapTo1()
        {
            var result = PmlErrorMapper.MapError("syntax error at line 5");
            Assert.IsTrue(result.Contains("语法错误"));
        }

        [Test]
        public void MapError_PermissionPattern_ShouldMapTo106()
        {
            var result = PmlErrorMapper.MapError("permission denied for user");
            Assert.IsTrue(result.Contains("权限不足"));
        }

        [Test]
        public void MapError_TimeoutPattern_ShouldMapTo502()
        {
            var result = PmlErrorMapper.MapError("operation timed out after 30s");
            Assert.IsTrue(result.Contains("超时"));
        }

        [Test]
        public void MapError_FileNotFoundPattern_ShouldMapTo300()
        {
            var result = PmlErrorMapper.MapError("file not found: C:\\test.pml");
            Assert.IsTrue(result.Contains("文件不存在"));
        }

        // ═══════════════════════════════════════════
        //  边界情况
        // ═══════════════════════════════════════════

        [Test]
        public void MapError_NullMessage_ShouldReturnDefault()
        {
            var result = PmlErrorMapper.MapError(null);
            Assert.IsTrue(result.Contains("未知错误"));
        }

        [Test]
        public void MapError_EmptyMessage_ShouldReturnDefault()
        {
            var result = PmlErrorMapper.MapError("");
            Assert.IsTrue(result.Contains("未知错误"));
        }

        [Test]
        public void MapError_UnrecognizedMessage_ShouldReturnOriginalWithSuggestion()
        {
            var result = PmlErrorMapper.MapError("something completely unexpected happened");
            Assert.IsTrue(result.Contains("something completely unexpected"));
            Assert.IsTrue(result.Contains("建议"));
        }

        // ═══════════════════════════════
        //  P4：错误回环——引导检索知识库黄金范式后重试
        // ═══════════════════════════════

        [Test]
        public void MapError_SyntaxError_ShouldHintKnowledgeGrepAndRetry()
        {
            var result = PmlErrorMapper.MapError("syntax error at line 5");
            Assert.IsTrue(result.Contains("grep(knowledge=true"));
            Assert.IsTrue(result.Contains("重试") || result.Contains("重写"));
        }

        [Test]
        public void MapError_UnrecognizedMessage_ShouldHintKnowledgeGrep()
        {
            var result = PmlErrorMapper.MapError("something completely unexpected happened");
            Assert.IsTrue(result.Contains("grep(knowledge=true"));
        }

        // ═══════════════════════════════════════════
        //  GetAllErrors
        // ═══════════════════════════════════════════

        [Test]
        public void GetAllErrors_ShouldReturnNonEmpty()
        {
            var errors = PmlErrorMapper.GetAllErrors();
            Assert.IsTrue(errors.Count > 20);
        }

        [Test]
        public void GetAllErrors_ShouldContainCommonCodes()
        {
            var errors = PmlErrorMapper.GetAllErrors();
            Assert.IsTrue(errors.ContainsKey(1));   // 语法错误
            Assert.IsTrue(errors.ContainsKey(100)); // 元素不存在
            Assert.IsTrue(errors.ContainsKey(502)); // 超时
        }
    }
}
