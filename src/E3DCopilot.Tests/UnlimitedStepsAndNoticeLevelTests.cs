using NUnit.Framework;
using E3DCopilot.Core.Agents;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Events;

namespace E3DCopilot.Tests
{
    /// <summary>
    /// 回归：步数不设上限 + Notice 分级。
    ///
    /// 背景：真实建模任务（解析图纸 → 多批建模 → 校验 → 回写）常常几十步，
    /// 20 步上限会在建模尚未执行时就把任务截断，用户看到的就是
    /// 「因步骤上限中止，建模尚未执行」。
    /// </summary>
    [TestFixture]
    public class UnlimitedStepsAndNoticeLevelTests
    {
        // ═══════════════════════════════════════════════════
        //  步数上限：默认必须为 0（不限）
        // ═══════════════════════════════════════════════════

        [Test]
        public void UiConfig_MaxSteps_默认值是不限()
        {
            var ui = new CopilotConfig.UiConfig();
            Assert.AreEqual(0, ui.MaxSteps,
                "默认必须为 0（不限），否则长任务会被步数截断");
        }

        [Test]
        public void UiConfig_MaxSteps_可显式配置上限()
        {
            var ui = new CopilotConfig.UiConfig { MaxSteps = 50 };
            Assert.AreEqual(50, ui.MaxSteps, "显式配置的边限必须保留，供想限制的用户使用");
        }

        [Test]
        public void MaxSteps_为零时语义是不限而非默认20()
        {
            // 回归：旧实现是 `> 0 ? value : 20`，会把 0 变成 20，
            // 与注释「0 = 不限」矛盾，导致用户无法关闭上限。
            var ui = new CopilotConfig.UiConfig { MaxSteps = 0 };
            int effective = ui.MaxSteps;

            Assert.AreEqual(0, effective);
            Assert.IsTrue(effective <= 0, "0 必须表示不设上限");
        }

        // ═══════════════════════════════════════════════════
        //  进度守卫：去掉步数上限后，必须能真正终止原地打转
        // ═══════════════════════════════════════════════════

        [Test]
        public void ProgressGuard_无todo时也能检测原地打转()
        {
            // 回归：旧实现第一行是 `if (!_hasActiveTodo) return null;`，
            // 模型不调 todo_write 时守卫完全失效。去掉步数上限后这是危险缺口。
            var guard = new ProgressGuard();

            bool gotNudge = false;
            for (int i = 0; i < 10; i++)
            {
                if (guard.RecordToolCompletion("cad_import", "same-signature") != null)
                    gotNudge = true;
            }

            // nudge 只发一次（_nudgeSent 去重），所以这里验证「发过」而不是「最后一次非空」
            Assert.IsTrue(gotNudge, "不依赖 todo 也应能检测到重复动作并注入 nudge");
            Assert.Greater(guard.NoProgressRounds, 0, "无进度轮数必须累积");
        }

        [Test]
        public void ProgressGuard_连续无进度达阈值后进入暂停()
        {
            var guard = new ProgressGuard();

            for (int i = 0; i < 20; i++)
                guard.RecordToolCompletion("query", "identical-result");

            Assert.IsTrue(guard.IsPaused, "连续 16 轮无新进度必须进入暂停状态");
            Assert.GreaterOrEqual(guard.NoProgressRounds, 16);
        }

        [Test]
        public void ProgressGuard_有新进度时续约不暂停()
        {
            var guard = new ProgressGuard();

            for (int i = 0; i < 30; i++)
                guard.RecordToolCompletion("query", "result-" + i);   // 每轮结果都不同

            Assert.IsFalse(guard.IsPaused, "每次都有新结果时不应被判定为原地打转");
            Assert.AreEqual(0, guard.NoProgressRounds);
        }

        [Test]
        public void ProgressGuard_Resume后恢复执行()
        {
            var guard = new ProgressGuard();
            for (int i = 0; i < 20; i++) guard.RecordToolCompletion("query", "same");
            Assert.IsTrue(guard.IsPaused);

            guard.Resume();

            Assert.IsFalse(guard.IsPaused);
            Assert.AreEqual(0, guard.NoProgressRounds);
        }

        // ═══════════════════════════════════════════════════
        //  Notice 分级：右上角横幅泛滥的根因是前端一律弹 Toast
        // ═══════════════════════════════════════════════════

        [Test]
        public void Notice_默认级别是info()
        {
            var evt = CopilotEvent.Notice("普通提示");
            Assert.AreEqual(EventKind.Notice, evt.Kind);
            Assert.AreEqual(NoticeLevel.Info, evt.Data as string);
        }

        [Test]
        public void NoticeSilent_级别是silent且前端应丢弃()
        {
            var evt = CopilotEvent.NoticeSilent("已注册工具 query");
            Assert.AreEqual(EventKind.Notice, evt.Kind);
            Assert.AreEqual(NoticeLevel.Silent, evt.Data as string);
        }

        [Test]
        public void NoticeWarn_级别是warn并弹醒目样式()
        {
            var evt = CopilotEvent.NoticeWarn("审批超时");
            Assert.AreEqual(EventKind.Notice, evt.Kind);
            Assert.AreEqual(NoticeLevel.Warn, evt.Data as string);
        }

        [Test]
        public void Notice三档_级别两两不同()
        {
            var info = CopilotEvent.Notice("a").Data as string;
            var silent = CopilotEvent.NoticeSilent("b").Data as string;
            var warn = CopilotEvent.NoticeWarn("c").Data as string;

            Assert.AreNotEqual(info, silent);
            Assert.AreNotEqual(info, warn);
            Assert.AreNotEqual(silent, warn);
        }
    }
}
