using System.IO;
using E3DCopilot.Core.Memory;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class MemoryManagerProfileTests
    {
        private string _db;

        [SetUp]
        public void Setup()
        {
            _db = Path.Combine(Path.GetTempPath(), "mem_profile_test_" + System.Guid.NewGuid().ToString("N") + ".db");
        }

        [TearDown]
        public void Teardown()
        {
            try { File.Delete(_db); } catch { }
            try { File.Delete(Path.Combine(Path.GetDirectoryName(_db), "profile.json")); } catch { }
        }

        [Test]
        public void UpdateProfileFromToolUse_AccumulatesToolUsage()
        {
            using (var m = new MemoryManager(_db))
            {
                m.UpdateProfileFromToolUse("execute_pml", "{\"element\":\"PIPE\"}", true);
                m.UpdateProfileFromToolUse("execute_pml", "{}", true);
                m.UpdateProfileFromToolUse("query", "{\"element\":\"VALVE\"}", false);
            }
            using (var m2 = new MemoryManager(_db))
            {
                Assert.AreEqual(2, m2.Profile.ToolUsage["execute_pml"]);
                Assert.AreEqual(1, m2.Profile.ToolUsage["query"]);
                Assert.IsTrue(m2.Profile.PreferredElements.Contains("PIPE"));
                Assert.IsTrue(m2.Profile.PreferredElements.Contains("VALVE"));
            }
        }

        [Test]
        public void GetSystemPromptContext_IncludesProfile()
        {
            using (var m = new MemoryManager(_db))
            {
                m.UpdateProfileFromToolUse("execute_pml", "{\"element\":\"PIPE\"}", true);
                var ctx = m.GetSystemPromptContext();
                Assert.IsTrue(ctx.Contains("<user_profile>"));
                Assert.IsTrue(ctx.Contains("PIPE"));
            }
        }
    }
}
