using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core;
using E3DCopilot.Core.Agents;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Providers;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Security;
using E3DCopilot.Core.Tools;
using E3DCopilot.Core.Tools.Handlers;
using E3DCopilot.Tools.Bridge;
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

        // ── Orchestrator.ExtractJson（测试辅助方法）──

        [Test]
        public void ExtractJson_FromMarkdownBlock()
        {
            var text = "Here is the plan:\n```json\n{\"title\":\"Test\"}\n```\nDone.";
            var json = Orchestrator.ExtractJsonForTest(text);
            Assert.That(json, Is.EqualTo("{\"title\":\"Test\"}"));
        }

        [Test]
        public void ExtractJson_PlainBraces()
        {
            var text = "Plan: {\"title\":\"Plain\"} end.";
            var json = Orchestrator.ExtractJsonForTest(text);
            Assert.That(json, Is.EqualTo("{\"title\":\"Plain\"}"));
        }

        [Test]
        public void ExtractJson_NoJson_ReturnsOriginal()
        {
            var text = "No JSON here.";
            var json = Orchestrator.ExtractJsonForTest(text);
            Assert.That(json, Is.EqualTo("No JSON here."));
        }

        // ── ExecutionPlan + ExtractJson 验收 ──

        [Test]
        public void FullPlan_Roundtrip_WithAllFields()
        {
            var plan = new ExecutionPlan
            {
                Title = "Full",
                Steps =
                {
                    new PlanStep { Id = "s1", Name = "Q", Task = "Query", ReadOnly = true, ParallelGroup = 0, Provider = "default" },
                    new PlanStep { Id = "s2", Name = "W", Task = "Write", ReadOnly = false, ParallelGroup = 1, DependsOn = { "s1" } }
                }
            };
            var json = plan.ToJson();
            var restored = ExecutionPlan.FromJson(json);
            Assert.That(restored.Steps[0].ParallelGroup, Is.EqualTo(0));
            Assert.That(restored.Steps[0].ReadOnly, Is.True);
            Assert.That(restored.Steps[1].DependsOn, Contains.Item("s1"));
            Assert.That(restored.Steps[0].Provider, Is.EqualTo("default"));
        }
    }
}
