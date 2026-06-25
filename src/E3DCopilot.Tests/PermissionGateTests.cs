using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Security;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class PermissionGateTests
    {
        [Test]
        public void NeedsApproval_AskMode_ReturnsTrue()
        {
            var policy = new ToolPolicy();
            policy.Set("modify", ApprovalMode.Ask);
            var gate = new PermissionGate(policy);

            Assert.IsTrue(gate.NeedsApproval("modify"));
        }

        [Test]
        public void NeedsApproval_AutoMode_ReturnsFalse()
        {
            var policy = new ToolPolicy();
            policy.Set("query", ApprovalMode.Auto);
            var gate = new PermissionGate(policy);

            Assert.IsFalse(gate.NeedsApproval("query"));
        }

        [Test]
        public void NeedsApproval_PlanOnlyMode_ReturnsFalse()
        {
            var policy = new ToolPolicy();
            policy.Set("export", ApprovalMode.PlanOnly);
            var gate = new PermissionGate(policy);

            Assert.IsFalse(gate.NeedsApproval("export"));
        }

        [Test]
        public void NeedsApproval_UnknownTool_DefaultsToAsk()
        {
            var policy = new ToolPolicy();
            var gate = new PermissionGate(policy);

            Assert.IsTrue(gate.NeedsApproval("unknown_tool"));
        }
    }

    [TestFixture]
    public class PendingApprovalTests
    {
        [Test]
        public void Id_IsGenerated()
        {
            var approval = new PendingApproval
            {
                ToolName = "modify",
                Args = "{}",
                Description = "test"
            };

            Assert.IsNotNull(approval.Id);
            Assert.IsNotEmpty(approval.Id);
        }

        [Test]
        public void Id_IsUnique()
        {
            var a1 = new PendingApproval { ToolName = "modify" };
            var a2 = new PendingApproval { ToolName = "modify" };

            Assert.AreNotEqual(a1.Id, a2.Id);
        }

        [Test]
        public async Task Complete_Allow_ReturnsAllowResult()
        {
            var approval = new PendingApproval { ToolName = "modify" };

            approval.Complete(allow: true, sessionPersist: false);

            var result = await approval.WaitAsync();
            Assert.IsTrue(result.Allow);
            Assert.IsFalse(result.SessionPersist);
        }

        [Test]
        public async Task Complete_Deny_ReturnsDenyResult()
        {
            var approval = new PendingApproval { ToolName = "modify" };

            approval.Complete(allow: false);

            var result = await approval.WaitAsync();
            Assert.IsFalse(result.Allow);
        }

        [Test]
        public async Task Complete_WithSessionPersist_ReturnsPersistTrue()
        {
            var approval = new PendingApproval { ToolName = "execute_pml" };

            approval.Complete(allow: true, sessionPersist: true);

            var result = await approval.WaitAsync();
            Assert.IsTrue(result.Allow);
            Assert.IsTrue(result.SessionPersist);
        }

        [Test]
        public void WaitAsync_Cancellation_ThrowsTaskCanceled()
        {
            var approval = new PendingApproval { ToolName = "modify" };
            var cts = new CancellationTokenSource();

            cts.Cancel();

            Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await approval.WaitAsync(cts.Token);
            });
        }

        [Test]
        public async Task Complete_CalledTwice_FirstResultWins()
        {
            var approval = new PendingApproval { ToolName = "modify" };

            approval.Complete(allow: true);
            approval.Complete(allow: false); // should be ignored

            var result = await approval.WaitAsync();
            Assert.IsTrue(result.Allow);
        }
    }
}
