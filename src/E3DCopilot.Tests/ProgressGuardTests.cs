using E3DCopilot.Core.Agents;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class ProgressGuardTests
    {
        [Test]
        public void RecordToolCompletion_NoActiveTodo_ReturnsNull()
        {
            // 无活跃 todo 时不监控
            var guard = new ProgressGuard();
            var msg = guard.RecordToolCompletion("query", "result1");
            Assert.IsNull(msg);
        }

        [Test]
        public void RecordToolCompletion_ActiveTodo_NewAction_ReturnsNull()
        {
            var guard = new ProgressGuard();
            guard.SetActiveTodo(true);
            // 首次动作,无历史 → 有进度
            var msg = guard.RecordToolCompletion("query", "result1");
            Assert.IsNull(msg);
        }

        [Test]
        public void RecordToolCompletion_ActiveTodo_ExactRepeat_DoesNotRenew()
        {
            var guard = new ProgressGuard();
            guard.SetActiveTodo(true);
            guard.RecordToolCompletion("query", "result1");
            // 精确重复 → 不算进度
            var msg = guard.RecordToolCompletion("query", "result1");
            // 1 次重复,不到 8 轮阈值 → null
            Assert.IsNull(msg);
        }

        [Test]
        public void RecordToolCompletion_AtNudgeThreshold_ReturnsNudgeMessage()
        {
            var guard = new ProgressGuard();
            guard.SetActiveTodo(true);
            // 首次动作有进度,后续 8 次精确重复 → 第 8 次触发 nudge
            guard.RecordToolCompletion("query", "result1");
            string msg = null;
            for (int i = 0; i < 8; i++)
                msg = guard.RecordToolCompletion("query", "result1");
            Assert.IsNotNull(msg);
            Assert.IsTrue(msg.Contains("progress guard") || msg.Contains("Reassess") || msg.Contains("reassess"));
        }

        [Test]
        public void RecordToolCompletion_AtPauseThreshold_ReturnsPauseMessage()
        {
            var guard = new ProgressGuard();
            guard.SetActiveTodo(true);
            guard.RecordToolCompletion("query", "result1");
            string msg = null;
            for (int i = 0; i < 16; i++)
                msg = guard.RecordToolCompletion("query", "result1");
            // 第 16 次触发 pause
            Assert.IsNotNull(msg);
            Assert.IsTrue(msg.Contains("PAUSED") || msg.Contains("paused") || msg.Contains("Pause"));
            Assert.IsTrue(guard.IsPaused);
        }

        [Test]
        public void RecordToolCompletion_NewActionAfterRepeats_RenewsLease()
        {
            var guard = new ProgressGuard();
            guard.SetActiveTodo(true);
            guard.RecordToolCompletion("query", "result1");
            // 5 次重复(不到 nudge 阈值)
            for (int i = 0; i < 5; i++)
                guard.RecordToolCompletion("query", "result1");
            // 新动作 → 续约
            var msg = guard.RecordToolCompletion("modify", "different-result");
            Assert.IsNull(msg);
            // 计数器应重置:再走 7 次重复不应触发 nudge(总共 7 < 8)
            for (int i = 0; i < 7; i++)
            {
                msg = guard.RecordToolCompletion("modify", "different-result");
                Assert.IsNull(msg, $"after renew step {i} should not nudge");
            }
        }

        [Test]
        public void FullReset_ClearsAllState()
        {
            var guard = new ProgressGuard();
            guard.SetActiveTodo(true);
            guard.RecordToolCompletion("query", "result1");
            for (int i = 0; i < 10; i++)
                guard.RecordToolCompletion("query", "result1");
            guard.FullReset();
            Assert.IsFalse(guard.IsPaused);
            // 重置后首次动作应正常(无进度历史)
            var msg = guard.RecordToolCompletion("query", "result1");
            Assert.IsNull(msg);
        }

        [Test]
        public void SetActiveTodo_False_StopsMonitoring()
        {
            var guard = new ProgressGuard();
            guard.SetActiveTodo(true);
            guard.SetActiveTodo(false);
            // 无活跃 todo → 不监控
            var msg = guard.RecordToolCompletion("query", "result1");
            Assert.IsNull(msg);
        }

        [Test]
        public void Resume_AfterPause_ClearsPausedState()
        {
            var guard = new ProgressGuard();
            guard.SetActiveTodo(true);
            guard.RecordToolCompletion("query", "result1");
            for (int i = 0; i < 16; i++)
                guard.RecordToolCompletion("query", "result1");
            Assert.IsTrue(guard.IsPaused);
            guard.Resume();
            Assert.IsFalse(guard.IsPaused);
        }
    }
}
