using E3DCopilot.Core.Agents;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class OrchestratorTests
    {
        // ── ExecutionPlan 数据结构（Phase 2 任务 1）──

        [Test]
        public void ExecutionPlan_Validate_ValidPlan_ReturnsNull()
        {
            var plan = new ExecutionPlan
            {
                Title = "Test Plan",
                Steps =
                {
                    new PlanStep { Id = "s1", Name = "Step 1", Task = "Do thing 1" },
                    new PlanStep { Id = "s2", Name = "Step 2", Task = "Do thing 2", DependsOn = { "s1" } }
                }
            };
            Assert.IsNull(plan.Validate());
        }

        [Test]
        public void ExecutionPlan_Validate_MissingTitle_ReturnsError()
        {
            var plan = new ExecutionPlan
            {
                Steps = { new PlanStep { Id = "s1", Name = "S1", Task = "T1" } }
            };
            Assert.That(plan.Validate(), Does.Contain("标题"));
        }

        [Test]
        public void ExecutionPlan_Validate_NoSteps_ReturnsError()
        {
            var plan = new ExecutionPlan { Title = "Empty" };
            Assert.That(plan.Validate(), Does.Contain("步骤"));
        }

        [Test]
        public void ExecutionPlan_Validate_MissingDep_ReturnsError()
        {
            var plan = new ExecutionPlan
            {
                Title = "Bad Deps",
                Steps =
                {
                    new PlanStep { Id = "s1", Name = "S1", Task = "T1", DependsOn = { "s-nonexistent" } }
                }
            };
            Assert.That(plan.Validate(), Does.Contain("依赖"));
        }

        [Test]
        public void ExecutionPlan_FromJson_Roundtrip()
        {
            var plan = new ExecutionPlan
            {
                Title = "Roundtrip",
                Steps =
                {
                    new PlanStep { Id = "s1", Name = "Query", Task = "Query pipes", ReadOnly = true, ParallelGroup = 0 },
                    new PlanStep { Id = "s2", Name = "Modify", Task = "Modify pipes", ReadOnly = false, ParallelGroup = 1, DependsOn = { "s1" } }
                }
            };
            var json = plan.ToJson();
            var restored = ExecutionPlan.FromJson(json);
            Assert.That(restored.Title, Is.EqualTo("Roundtrip"));
            Assert.That(restored.Steps.Count, Is.EqualTo(2));
            Assert.That(restored.Steps[0].Id, Is.EqualTo("s1"));
            Assert.That(restored.Steps[1].DependsOn, Contains.Item("s1"));
        }
    }
}
