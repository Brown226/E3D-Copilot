using E3DCopilot.Core.Security;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class ToolPolicyTests
    {
        [Test]
        public void Set_And_GetMode_ReturnsCorrectMode()
        {
            var policy = new ToolPolicy();
            policy.Set("query", ApprovalMode.Auto);
            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("query"));
        }

        [Test]
        public void GetMode_UnknownTool_DefaultsToAsk()
        {
            var policy = new ToolPolicy();
            Assert.AreEqual(ApprovalMode.Ask, policy.GetMode("unknown_tool"));
        }

        [Test]
        public void Set_Disabled_FallsBackToAsk()
        {
            var policy = new ToolPolicy();
            policy.Set("query", ApprovalMode.Auto, enabled: false);
            Assert.AreEqual(ApprovalMode.Ask, policy.GetMode("query"));
        }

        [Test]
        public void ApplyPreset_Confirm_SetsCorrectModes()
        {
            var policy = new ToolPolicy();
            policy.ApplyPreset(ToolPreset.Confirm);

            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("query"));
            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("get_attributes"));
            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("check"));
            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("calculate"));
            Assert.AreEqual(ApprovalMode.Ask, policy.GetMode("modify"));
            Assert.AreEqual(ApprovalMode.Ask, policy.GetMode("export"));
            Assert.AreEqual(ApprovalMode.Ask, policy.GetMode("execute_pml"));
        }

        [Test]
        public void ApplyPreset_Observe_SetsPlanOnlyForWriteTools()
        {
            var policy = new ToolPolicy();
            policy.ApplyPreset(ToolPreset.Observe);

            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("query"));
            Assert.AreEqual(ApprovalMode.PlanOnly, policy.GetMode("modify"));
            Assert.AreEqual(ApprovalMode.PlanOnly, policy.GetMode("export"));
            Assert.AreEqual(ApprovalMode.PlanOnly, policy.GetMode("execute_pml"));
        }

        [Test]
        public void ApplyPreset_Auto_SetsAllAuto()
        {
            var policy = new ToolPolicy();
            policy.ApplyPreset(ToolPreset.Auto);

            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("query"));
            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("modify"));
            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("execute_pml"));
        }

        [Test]
        public void IsAllowed_AutoMode_ReturnsTrue()
        {
            var policy = new ToolPolicy();
            policy.Set("query", ApprovalMode.Auto);
            Assert.IsTrue(policy.IsAllowed("query", isPlanMode: false));
        }

        [Test]
        public void IsAllowed_PlanOnly_InPlanMode_ReturnsTrue()
        {
            var policy = new ToolPolicy();
            policy.Set("modify", ApprovalMode.PlanOnly);
            Assert.IsTrue(policy.IsAllowed("modify", isPlanMode: true));
        }

        [Test]
        public void IsAllowed_PlanOnly_NotInPlanMode_ReturnsFalse()
        {
            var policy = new ToolPolicy();
            policy.Set("modify", ApprovalMode.PlanOnly);
            Assert.IsFalse(policy.IsAllowed("modify", isPlanMode: false));
        }

        [Test]
        public void IsAllowed_AskMode_ReturnsTrue()
        {
            // Ask mode returns true but needs PermissionGate for further check
            var policy = new ToolPolicy();
            policy.Set("modify", ApprovalMode.Ask);
            Assert.IsTrue(policy.IsAllowed("modify", isPlanMode: false));
        }

        [Test]
        public void Set_Overwrite_UpdatesPolicy()
        {
            var policy = new ToolPolicy();
            policy.Set("query", ApprovalMode.Ask);
            Assert.AreEqual(ApprovalMode.Ask, policy.GetMode("query"));

            policy.Set("query", ApprovalMode.Auto);
            Assert.AreEqual(ApprovalMode.Auto, policy.GetMode("query"));
        }
    }

    [TestFixture]
    public class ToolPresetTests
    {
        [Test]
        public void Observe_HasCorrectName()
        {
            Assert.AreEqual("observe", ToolPreset.Observe.Name);
        }

        [Test]
        public void Confirm_HasCorrectName()
        {
            Assert.AreEqual("confirm", ToolPreset.Confirm.Name);
        }

        [Test]
        public void Auto_HasCorrectName()
        {
            Assert.AreEqual("auto", ToolPreset.Auto.Name);
        }

        [Test]
        public void Observe_ContainsReadOnlyTools()
        {
            var policies = ToolPreset.Observe.Policies;
            Assert.IsTrue(policies.ContainsKey("query"));
            Assert.IsTrue(policies.ContainsKey("check"));
            Assert.IsTrue(policies.ContainsKey("calculate"));
        }

        [Test]
        public void Confirm_WriteToolsAreAskMode()
        {
            var policies = ToolPreset.Confirm.Policies;
            Assert.AreEqual(ApprovalMode.Ask, policies["modify"]);
            Assert.AreEqual(ApprovalMode.Ask, policies["export"]);
            Assert.AreEqual(ApprovalMode.Ask, policies["execute_pml"]);
        }
    }
}
