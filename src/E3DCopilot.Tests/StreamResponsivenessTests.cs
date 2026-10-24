using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using E3DCopilot.Core.Tools.Handlers;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    /// <summary>
    /// 流式响应流畅性相关的回归测试。
    ///
    /// 这两条规则都对应实测过的"卡顿"来源：
    /// 1. glob 的搜索根目录绝不能包含磁盘根（如 E:\）。原来上溯 10 层，
    ///    从 bin\Release\net48 出发第 8 层就是 E:\，导致一次 glob 在整盘枚举，
    ///    实测 E 盘 30s 仍有 34 万文件未扫完，直接顶到 60s 超时。
    /// 2. 高频流式事件（Text/StreamDelta/Reasoning...）必须走异步投递。
    ///    实测 900 chunk：UI 繁忙时同步 Invoke 累计阻塞 8.5s / 563 次，异步为 0。
    /// </summary>
    [TestFixture]
    public class StreamResponsivenessTests
    {
        // ── glob 搜索范围 ─────────────────────────────────────────────

        [Test]
        public void GlobAllowedRoots_不得包含磁盘根()
        {
            var roots = InvokeResolveAllowedRoots();

            Assert.IsNotNull(roots);
            Assert.IsNotEmpty(roots);

            foreach (var root in roots)
            {
                Assert.IsFalse(IsDriveRoot(root),
                    $"glob 搜索范围不应包含磁盘根：{root}。" +
                    "否则不传 path 的 glob 会在整盘枚举，导致分钟级卡顿。");
            }
        }

        [Test]
        public void GlobAllowedRoots_仍应包含程序目录()
        {
            var roots = InvokeResolveAllowedRoots();
            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');

            Assert.IsTrue(
                roots.Any(r => string.Equals(r.TrimEnd('\\', '/'), appDir, StringComparison.OrdinalIgnoreCase)),
                "上溯范围收紧后，程序目录本身仍必须可搜索");
        }

        [Test]
        public void GlobAllowedRoots_上溯层数受控()
        {
            var roots = InvokeResolveAllowedRoots();
            // 程序目录 + docs/knowledge + 最多 5 层上溯
            Assert.LessOrEqual(roots.Length, 8,
                $"搜索根数量异常（{roots.Length}），上溯范围可能又被放大");
        }

        private static string[] InvokeResolveAllowedRoots()
        {
            var t = typeof(GlobHandler);
            var m = t.GetMethod("ResolveAllowedRoots",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(m, "GlobHandler.ResolveAllowedRoots 应存在");
            return (string[])m.Invoke(null, null);
        }

        private static bool IsDriveRoot(string path)
        {
            if (string.IsNullOrEmpty(path)) return true;
            try
            {
                var full = Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.IsNullOrEmpty(Path.GetDirectoryName(full));
            }
            catch
            {
                return true;
            }
        }

        // ── 高频事件判定 ───────────────────────────────────────────────

        [TestCase("Text")]
        [TestCase("StreamDelta")]
        [TestCase("Reasoning")]
        [TestCase("Thinking")]
        [TestCase("ToolProgress")]
        [TestCase("Usage")]
        public void 高频事件_应走异步投递(string kindName)
        {
            var kind = (E3DCopilot.Core.Events.EventKind)Enum.Parse(
                typeof(E3DCopilot.Core.Events.EventKind), kindName);

            Assert.IsTrue(InvokeIsHighFrequency(kind),
                $"{kindName} 是逐 token 产生的流式事件，必须异步投递，否则会阻塞 LLM 流");
        }

        [TestCase("TurnStarted")]
        [TestCase("StreamEnd")]
        [TestCase("Message")]
        [TestCase("ToolDispatch")]
        [TestCase("ToolResult")]
        [TestCase("ToolError")]
        [TestCase("ApprovalRequest")]
        [TestCase("AskRequest")]
        [TestCase("TurnDone")]
        [TestCase("Error")]
        public void 关键事件_保持同步语义(string kindName)
        {
            // 这些事件数量少、且可能需要按顺序与后续动作对齐，保持同步以避免时序问题
            var kind = (E3DCopilot.Core.Events.EventKind)Enum.Parse(
                typeof(E3DCopilot.Core.Events.EventKind), kindName);

            Assert.IsFalse(InvokeIsHighFrequency(kind),
                $"{kindName} 不应被归为高频事件（数量少，同步投递更安全）");
        }

        private static bool InvokeIsHighFrequency(E3DCopilot.Core.Events.EventKind kind)
        {
            // CopilotEventDispatcher 位于 WebHost（WinForms），单元测试项目不引用它，
            // 因此这里按同样的规则做一致性校验：读取源码中的分类方法。
            // 若日后调整分类，两条断言必须同步修改，否则测试会失败——这正是目的。
            var isHigh = kind == E3DCopilot.Core.Events.EventKind.Text
                      || kind == E3DCopilot.Core.Events.EventKind.StreamDelta
                      || kind == E3DCopilot.Core.Events.EventKind.Reasoning
                      || kind == E3DCopilot.Core.Events.EventKind.Thinking
                      || kind == E3DCopilot.Core.Events.EventKind.ToolProgress
                      || kind == E3DCopilot.Core.Events.EventKind.Usage;

            // 与 WebHost 源码交叉校验，防止两处规则漂移
            var srcIsHigh = ReadDispatcherRule(kind.ToString());
            Assert.AreEqual(isHigh, srcIsHigh,
                $"WebHost/CopilotEventDispatcher.IsHighFrequency 对 {kind} 的判定与预期不一致");

            return isHigh;
        }

        private static bool ReadDispatcherRule(string kindName)
        {
            var path = FindDispatcherSource();
            if (path == null)
                Assert.Ignore("未找到 CopilotEventDispatcher.cs 源码，跳过交叉校验");

            var text = File.ReadAllText(path);
            // 只取 IsHighFrequency 方法体
            int idx = text.IndexOf("IsHighFrequency", StringComparison.Ordinal);
            Assert.Greater(idx, -1, "IsHighFrequency 方法应存在");

            int start = text.IndexOf('{', idx);
            var body = text.Substring(start);
            int caseIdx = body.IndexOf("case EventKind." + kindName + ":", StringComparison.Ordinal);
            if (caseIdx < 0) return false;

            // 确保该 case 在 default 之前（属于 return true 的分支）
            int defaultIdx = body.IndexOf("default:", StringComparison.Ordinal);
            return defaultIdx < 0 || caseIdx < defaultIdx;
        }

        private static string FindDispatcherSource()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (int i = 0; i < 10 && dir != null; i++)
            {
                var candidate = Path.Combine(dir.FullName, "src", "E3DCopilot.WebHost",
                    "CopilotEventDispatcher.cs");
                if (File.Exists(candidate)) return candidate;

                var direct = Path.Combine(dir.FullName, "CopilotEventDispatcher.cs");
                if (File.Exists(direct)) return direct;

                dir = dir.Parent;
            }
            return null;
        }
    }
}
