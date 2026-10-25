using E3DCopilot.Core;
using E3DCopilot.Core.Providers;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    /// <summary>
    /// GetMessagesByBudget — token 预算制历史窗口（替代旧 GetRecentMessages(20) 条数窗口）
    /// </summary>
    [TestFixture]
    public class CopilotSessionWindowTests
    {
        private static CopilotSession NewSession()
        {
            return new CopilotSession();
        }

        [Test]
        public void Budget_UnderBudget_ReturnsAllMessages()
        {
            var s = NewSession();
            for (int i = 0; i < 50; i++)
                s.AddUserMessage($"msg-{i}-short"); // 50 条短消息远不足预算

            var got = s.GetMessagesByBudget(100_000);
            Assert.AreEqual(50, got.Count, "全量历史不足预算时应整段发送");
        }

        [Test]
        public void Budget_OverBudget_TruncatesFromHead()
        {
            var s = NewSession();
            for (int i = 0; i < 10; i++)
                s.AddUserMessage($"msg-{i:D2}-" + new string('x', 1000)); // 每条 ~1006 字符

            var got = s.GetMessagesByBudget(2500); // 只够装 2 条 + 至少保留规则

            Assert.LessOrEqual(got.Count, 3, "超预算应从头截断");
            Assert.GreaterOrEqual(got.Count, 2, "至少保留最近 2 条");
            // 保留的是尾部消息（msg-09 是最后一条）
            StringAssert.StartsWith("msg-09", got[got.Count - 1].Content ?? string.Empty);
        }

        [Test]
        public void Budget_AlwaysKeepsLastTwo_EvenIfOversize()
        {
            var s = NewSession();
            for (int i = 0; i < 5; i++)
                s.AddUserMessage(new string('y', 5000)); // 每条都超预算

            var got = s.GetMessagesByBudget(100); // 预算极小

            Assert.GreaterOrEqual(got.Count, 2, "预算再小也至少保留最近 2 条");
        }

        [Test]
        public void Budget_CountsToolCallArguments()
        {
            var s = NewSession();
            s.AddUserMessage("query the model");
            var assistant = new ChatMessage(MessageRole.Assistant, "") { ToolCalls = new System.Collections.Generic.List<ToolCall>() };
            assistant.ToolCalls.Add(new ToolCall { Name = "query", Arguments = new string('a', 3000) });
            s.Messages.Add(assistant);
            s.AddSystemMessage("tool result placeholder");
            s.AddUserMessage("next question");

            // 预算刚好在工具参数被计入时不该把 assistant 消息误判为小
            var got = s.GetMessagesByBudget(2000);
            Assert.Less(got.Count, 5, "工具调用参数长度计入预算，超大参数应触发截断");
        }

        [Test]
        public void Budget_DoesNotMutateSession()
        {
            var s = NewSession();
            for (int i = 0; i < 10; i++)
                s.AddUserMessage(new string('z', 800));

            int before = s.Messages.Count;
            var got = s.GetMessagesByBudget(100);
            Assert.AreEqual(before, s.Messages.Count, "窗口截取返回副本，不得修改会话历史");
        }
    }
}
