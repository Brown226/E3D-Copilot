using E3DCopilot.Core;
using E3DCopilot.Core.Security;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    /// <summary>
    /// 四档权限模式的策略映射测试。
    ///
    /// 历史缺陷（已修）：
    ///   1. yolo（完全访问）分支只显式列了十几个工具，design_workflow / dwg_diff /
    ///      MCP 平台工具全部落到 FallbackMode=Ask，用户选了完全访问仍会被卡在
    ///      2 分钟审批超时。
    ///   2. AgentLoop 只把 Ask 当作需要审批，从未拦截 Deny，导致计划模式的
    ///      "写操作一律拒绝"形同虚设。
    /// </summary>
    [TestFixture]
    public class ApprovalModePolicyTests
    {
        [Test]
        public void Plan_ReadOnlyTools_AreAuto()
        {
            var policy = AgentLoop.BuildToolPolicy("plan");

            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("query"));
            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("get_attributes"));
            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("grep"));
            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("read_file"));
        }

        [Test]
        public void Plan_WriteTools_AreDenied()
        {
            var policy = AgentLoop.BuildToolPolicy("plan");

            Assert.AreEqual(ApprovalMode.Deny, policy.GetMode("write_file"));
            Assert.AreEqual(ApprovalMode.Deny, policy.GetMode("modify"));
            Assert.AreEqual(ApprovalMode.Deny, policy.GetMode("execute_pml"));
            Assert.AreEqual(ApprovalMode.Deny, policy.GetMode("design_workflow"));
            Assert.AreEqual(ApprovalMode.Deny, policy.GetMode("cad_import"));
        }

        [Test]
        public void Plan_UnlistedTool_DefaultsToDeny()
        {
            var policy = AgentLoop.BuildToolPolicy("plan");
            Assert.AreEqual(ApprovalMode.Deny, policy.GetMode("some_future_write_tool"));
        }

        [Test]
        public void Yolo_DesignWorkflow_IsAuto()
        {
            var policy = AgentLoop.BuildToolPolicy("yolo");
            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("design_workflow"));
            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("dwg_diff"));
        }

        [Test]
        public void Yolo_UnlistedTool_IsAuto_NotEmptyAsk()
        {
            // 这是导致「完全访问仍然弹审批」的核心回归点
            var policy = AgentLoop.BuildToolPolicy("yolo");

            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("mcp__e3d-data-platform__record_drawing"));
            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("any_unlisted_tool"));
        }

        [Test]
        public void Auto_WriteTools_StillAsk()
        {
            var policy = AgentLoop.BuildToolPolicy("auto");

            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("query"));
            Assert.AreEqual(ApprovalMode.Ask, policy.GetMode("write_file"));
            Assert.AreEqual(ApprovalMode.Ask, policy.GetMode("modify"));
        }

        [Test]
        public void Ask_ExecutePml_Asks()
        {
            // 回归点：execute_pml 是唯一能直接改 E3D 数据库的工具，
            // 「变更前确认」档必须真的询问，不能因为是"脚本"就放行。
            // 历史缺陷：ask 档曾把它设为 Auto，等于最高危的写操作完全不拦截。
            var policy = AgentLoop.BuildToolPolicy("ask");
            Assert.AreEqual(ApprovalMode.Ask, policy.GetMode("execute_pml"));
        }

        [Test]
        public void Auto_ExecutePml_Asks()
        {
            // 自主模式的语义是「只读自动、写操作先问」，execute_pml 属于写操作
            var policy = AgentLoop.BuildToolPolicy("auto");
            Assert.AreEqual(ApprovalMode.Ask, policy.GetMode("execute_pml"));
        }

        [Test]
        public void Yolo_ExecutePml_IsAuto()
        {
            // 完全访问档下不再打断，这是用户显式选择
            var policy = AgentLoop.BuildToolPolicy("yolo");
            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("execute_pml"));
        }

        [Test]
        public void Ask_ReadTools_AlsoAsk()
        {
            var policy = AgentLoop.BuildToolPolicy("ask");

            Assert.AreEqual(ApprovalMode.Ask, policy.GetMode("query"));
            Assert.AreEqual(ApprovalMode.Ask, policy.GetMode("get_attributes"));
        }

        [Test]
        public void UnknownMode_FallsBackToAutoBehaviour()
        {
            // 传入未知模式时按 auto 处理，不能默默变成"全部要审批"
            var policy = AgentLoop.BuildToolPolicy("something_else");

            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("query"));
            Assert.AreEqual(ApprovalMode.Ask, policy.GetMode("write_file"));
        }

        [Test]
        public void NullMode_TreatedAsAuto()
        {
            var policy = AgentLoop.BuildToolPolicy(null);
            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("query"));
        }
    }
}
