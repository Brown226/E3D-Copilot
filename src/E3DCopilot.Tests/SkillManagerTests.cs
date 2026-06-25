using System.IO;
using E3DCopilot.Core.Skills;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class SkillManagerTests
    {
        private string _tempDir;
        private string _skillSourceDir;
        private string _stateFilePath;
        private SkillManager _mgr;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "e3dcopilot_skill_test_" + Path.GetRandomFileName());
            _skillSourceDir = Path.Combine(_tempDir, "skills");
            _stateFilePath = Path.Combine(_tempDir, "state.json");
            Directory.CreateDirectory(_skillSourceDir);

            _mgr = new SkillManager(_stateFilePath);
            _mgr.AddSource(_skillSourceDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private void CreateSkill(string dirName, string frontmatter)
        {
            var skillDir = Path.Combine(_skillSourceDir, dirName);
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), frontmatter);
        }

        // ====== ListSkills ======

        [Test]
        public void ListSkills_EmptySource_ReturnsEmpty()
        {
            var skills = _mgr.ListSkills();
            Assert.AreEqual(0, skills.Count);
        }

        [Test]
        public void ListSkills_WithSkillFiles_ReturnsAll()
        {
            CreateSkill("skill-a", "---\nname: skill-a\ndescription: Skill A\n---\nContent A");
            CreateSkill("skill-b", "---\nname: skill-b\ndescription: Skill B\n---\nContent B");

            var skills = _mgr.ListSkills();
            Assert.AreEqual(2, skills.Count);
        }

        [Test]
        public void ListSkills_ParsesFrontmatter()
        {
            CreateSkill("my-skill", "---\nname: my-skill\ndescription: A test skill\nrunAs: subagent\ntags: [test, demo]\n---\nBody content");

            var skills = _mgr.ListSkills();
            Assert.AreEqual(1, skills.Count);

            var skill = skills[0];
            Assert.AreEqual("my-skill", skill.Name);
            Assert.AreEqual("A test skill", skill.Description);
            Assert.AreEqual("subagent", skill.RunAs);
            Assert.AreEqual(2, skill.Tags.Length);
        }

        [Test]
        public void ListSkills_NoFrontmatter_UsesDirName()
        {
            CreateSkill("fallback-name", "# Just markdown\nNo frontmatter here");

            var skills = _mgr.ListSkills();
            Assert.AreEqual(1, skills.Count);
            Assert.AreEqual("fallback-name", skills[0].Name);
        }

        [Test]
        public void ListSkills_NoSkillMd_SkipsDirectory()
        {
            var dir = Path.Combine(_skillSourceDir, "no-skill");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "README.md"), "not a skill");

            var skills = _mgr.ListSkills();
            Assert.AreEqual(0, skills.Count);
        }

        // ====== Read ======

        [Test]
        public void Read_ExistingSkill_ReturnsSkill()
        {
            CreateSkill("test-skill", "---\nname: test-skill\ndescription: Test\n---\nBody");

            var skill = _mgr.Read("test-skill");
            Assert.IsNotNull(skill);
            Assert.AreEqual("test-skill", skill.Name);
        }

        [Test]
        public void Read_NonExistent_ReturnsNull()
        {
            var skill = _mgr.Read("nonexistent");
            Assert.IsNull(skill);
        }

        [Test]
        public void Read_NullName_ReturnsNull()
        {
            var skill = _mgr.Read(null);
            Assert.IsNull(skill);
        }

        [Test]
        public void Read_CaseInsensitive()
        {
            CreateSkill("My-Skill", "---\nname: My-Skill\ndescription: Test\n---\nBody");

            var skill = _mgr.Read("my-skill");
            Assert.IsNotNull(skill);
        }

        // ====== ReadContent ======

        [Test]
        public void ReadContent_WithFrontmatter_ReturnsBodyOnly()
        {
            CreateSkill("content-skill", "---\nname: content-skill\ndescription: Test\n---\n# Hello World\nThis is the body.");

            var content = _mgr.ReadContent("content-skill");
            Assert.IsNotNull(content);
            Assert.IsTrue(content.Contains("Hello World"));
            Assert.IsFalse(content.Contains("name: content-skill"));
        }

        [Test]
        public void ReadContent_NoFrontmatter_ReturnsFullContent()
        {
            CreateSkill("raw-skill", "# Just raw content\nNo frontmatter.");

            var content = _mgr.ReadContent("raw-skill");
            Assert.IsNotNull(content);
            Assert.IsTrue(content.Contains("raw content"));
        }

        [Test]
        public void ReadContent_NonExistent_ReturnsNull()
        {
            var content = _mgr.ReadContent("nonexistent");
            Assert.IsNull(content);
        }

        // ====== ToggleSkill ======

        [Test]
        public void ToggleSkill_FirstToggle_DisablesSkill()
        {
            // Default is enabled, first toggle makes it disabled
            var result = _mgr.ToggleSkill("some-skill");
            Assert.IsFalse(result);
        }

        [Test]
        public void ToggleSkill_Twice_EnablesSkill()
        {
            _mgr.ToggleSkill("some-skill"); // disable
            var result = _mgr.ToggleSkill("some-skill"); // enable
            Assert.IsTrue(result);
        }

        [Test]
        public void ToggleSkill_PersistsState()
        {
            _mgr.ToggleSkill("my-skill"); // disable

            // Create new manager with same state file
            var mgr2 = new SkillManager(_stateFilePath);
            mgr2.AddSource(_skillSourceDir);
            CreateSkill("my-skill", "---\nname: my-skill\ndescription: Test\n---\nBody");

            var skills = mgr2.ListSkills();
            Assert.AreEqual(1, skills.Count);
            Assert.IsFalse(skills[0].Enabled);
        }

        // ====== SetSkillEnabled ======

        [Test]
        public void SetSkillEnabled_SetsExplicitState()
        {
            _mgr.SetSkillEnabled("skill-x", false);
            _mgr.ToggleSkill("skill-x"); // false → true
            // Actually, ToggleSkill checks if key exists, if not, sets to false then toggles to true
            // But SetSkillEnabled set it to false, so toggle should make it true
        }

        // ====== AddSource / RemoveSource ======

        [Test]
        public void AddSource_DuplicatePath_ReturnsFalse()
        {
            var result = _mgr.AddSource(_skillSourceDir);
            Assert.IsFalse(result); // Already added in SetUp
        }

        [Test]
        public void AddSource_NewPath_ReturnsTrue()
        {
            var newPath = Path.Combine(_tempDir, "new-source");
            Directory.CreateDirectory(newPath);
            var result = _mgr.AddSource(newPath);
            Assert.IsTrue(result);
        }

        [Test]
        public void AddSource_NullPath_ReturnsFalse()
        {
            Assert.IsFalse(_mgr.AddSource(null));
        }

        [Test]
        public void RemoveSource_ExistingPath_ReturnsTrue()
        {
            var result = _mgr.RemoveSource(_skillSourceDir);
            Assert.IsTrue(result);
        }

        [Test]
        public void RemoveSource_NonExistingPath_ReturnsFalse()
        {
            var result = _mgr.RemoveSource("/nonexistent/path");
            Assert.IsFalse(result);
        }

        // ====== ListSources ======

        [Test]
        public void ListSources_ReturnsAllSources()
        {
            var sources = _mgr.ListSources();
            // At least the one added in SetUp
            Assert.GreaterOrEqual(sources.Count, 1);
        }

        [Test]
        public void ListSources_ActiveSource_HasCorrectStatus()
        {
            var sources = _mgr.ListSources();
            var source = sources.Find(s => s.Path == _skillSourceDir);
            Assert.IsNotNull(source);
            Assert.AreEqual("active", source.Status);
        }

        [Test]
        public void ListSources_CountsSkills()
        {
            CreateSkill("s1", "---\nname: s1\n---\nBody");
            CreateSkill("s2", "---\nname: s2\n---\nBody");

            var sources = _mgr.ListSources();
            var source = sources.Find(s => s.Path == _skillSourceDir);
            Assert.AreEqual(2, source.SkillCount);
        }

        // ====== Refresh ======

        [Test]
        public void Refresh_RemovesNonExistentSources()
        {
            var ghostPath = Path.Combine(_tempDir, "ghost");
            _mgr.AddSource(ghostPath);
            // ghostPath doesn't exist

            _mgr.Refresh();

            var sources = _mgr.ListSources();
            Assert.IsNull(sources.Find(s => s.Path == ghostPath));
        }
    }
}
