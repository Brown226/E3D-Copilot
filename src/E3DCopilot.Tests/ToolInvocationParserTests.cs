using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using E3DCopilot.Core.Providers;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class ToolInvocationParserTests
    {
        [Test]
        public void ExtractToolCalls_EmptyText_ReturnsEmpty()
        {
            var result = ToolInvocationParser.ExtractToolCalls("");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ExtractToolCalls_NullText_ReturnsEmpty()
        {
            var result = ToolInvocationParser.ExtractToolCalls(null);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ExtractToolCalls_NoToolTags_ReturnsEmpty()
        {
            var result = ToolInvocationParser.ExtractToolCalls("你好，这是普通对话");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ExtractToolCalls_SingleSelfClosingTag_ReturnsOneCall()
        {
            string text = @"<tool_invocation name=""query"" arguments={""type"": ""pipe"", ""filter"": ""BORE = '100'""} />";
            var result = ToolInvocationParser.ExtractToolCalls(text);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("query", result[0].Name);
            Assert.IsTrue(result[0].Arguments.Contains("pipe"));
            Assert.IsTrue(result[0].Arguments.Contains("BORE"));
        }

        [Test]
        public void ExtractToolCalls_TagWithTrailingContent_ReturnsCall()
        {
            string text = @"我需要查询管道<tool_invocation name=""query"" arguments={""type"": ""pipe""} />以上是查询结果。";
            var result = ToolInvocationParser.ExtractToolCalls(text);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("query", result[0].Name);
        }

        [Test]
        public void ExtractToolCalls_MultipleTags_ReturnsAllCalls()
        {
            string text = @"第一步查询<tool_invocation name=""query"" arguments={""type"": ""pipe""} />
第二步修改<tool_invocation name=""modify"" arguments={""property"": ""BORE"", ""value"": ""150""} />";
            var result = ToolInvocationParser.ExtractToolCalls(text);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("query", result[0].Name);
            Assert.AreEqual("modify", result[1].Name);
        }

        [Test]
        public void ExtractToolCalls_ArgumentsWithNestedBraces_ParsedCorrectly()
        {
            string text = @"<tool_invocation name=""query"" arguments={""type"": ""pipe"", ""data"": {""key"": ""val""}} />";
            var result = ToolInvocationParser.ExtractToolCalls(text);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("query", result[0].Name);
        }

        [Test]
        public void ContainsToolInvocation_HasTag_ReturnsTrue()
        {
            string text = @"之前<tool_invocation name=""query"" arguments={""type"":""pipe""} />之后";
            Assert.IsTrue(ToolInvocationParser.ContainsToolInvocation(text));
        }

        [Test]
        public void ContainsToolInvocation_NoTag_ReturnsFalse()
        {
            Assert.IsFalse(ToolInvocationParser.ContainsToolInvocation("普通对话"));
        }

        [Test]
        public void StripToolInvocationTags_RemovesTags()
        {
            string text = @"开始<tool_invocation name=""query"" arguments={""type"":""pipe""} />结束";
            string result = ToolInvocationParser.StripToolInvocationTags(text);
            Assert.AreEqual("开始结束", result);
        }

        [Test]
        public void StripToolInvocationTags_MultipleTags_RemovesAll()
        {
            string text = @"查询<tool_invocation name=""query"" arguments={""type"":""pipe""} />修改<tool_invocation name=""modify"" arguments={""prop"":""x""} />完成";
            string result = ToolInvocationParser.StripToolInvocationTags(text);
            Assert.AreEqual("查询修改完成", result);
        }

        [Test]
        public void ExtractToolCalls_InvalidToolName_IsSkipped()
        {
            // XML 注入尝试
            string text = @"<tool_invocation name=""malicious<script>alert('xss')</script>"" arguments={} />";
            var result = ToolInvocationParser.ExtractToolCalls(text);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void NormalizeArguments_MinifiesJson()
        {
            string text = @"<tool_invocation name=""query"" arguments={""type"":   ""pipe"",    ""filter"" : ""BORE='100'""} />";
            var result = ToolInvocationParser.ExtractToolCalls(text);
            Assert.AreEqual(1, result.Count);
            // 应被标准化为紧凑 JSON（无多余空格）
            Assert.IsFalse(result[0].Arguments.Contains("   "));
        }
    }
}
