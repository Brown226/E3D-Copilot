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

            // 注入隔离的源路径，避免扫描到真实环境（应用目录 skills / 用户目录 skills）
            _mgr = new SkillManager(_stateFilePath, new[] { _skillSourceDir });
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
            var mgr2 = new SkillManager(_stateFilePath, new[] { _skillSourceDir });
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

    /// <summary>
    /// 内置 skills 的部署回归。
    ///
    /// 背景：源码里存在 skills/ 目录，但曾经因为 csproj 只复制 src/E3DCopilot.Core/skills/，
    /// 导致 12 个手工整理的 skill 从未进入部署目录，模型在系统提示词里根本看不到它们。
    /// 这类缺陷不会报错、不会失败编译，只会静默地"技能不存在"，因此必须用测试锁住。
    /// </summary>
    [TestFixture]
    public class BuiltinSkillsDeploymentTests
    {
        private static readonly string[] RequiredSkills =
        {
            "pml-authoring",
            "cad-e3d-modeling",
            "e3d-element-ops",
            "iso-and-standards",
        };

        private static string BuiltinSkillsDir
        {
            get
            {
                // 测试程序集输出目录下的 skills/，即 SkillManager 在运行时扫描的位置
                return Path.Combine(
                    Path.GetDirectoryName(typeof(BuiltinSkillsDeploymentTests).Assembly.Location),
                    "skills");
            }
        }

        [Test]
        public void CoreSkills_AreCopiedToOutputDirectory()
        {
            Assert.IsTrue(Directory.Exists(BuiltinSkillsDir),
                "内置 skills 未复制到输出目录: " + BuiltinSkillsDir);

            foreach (var name in RequiredSkills)
            {
                string file = Path.Combine(BuiltinSkillsDir, name, "SKILL.md");
                Assert.IsTrue(File.Exists(file), "缺少 SKILL.md: " + name);
            }
        }

        [Test]
        public void CoreSkills_LoadWithNonEmptyBodyAndNoFrontmatter()
        {
            var mgr = new SkillManager(null, new[] { BuiltinSkillsDir });
            var loaded = mgr.ListSkills();

            foreach (var name in RequiredSkills)
            {
                var skill = loaded.Find(s => s.Name == name);
                Assert.IsNotNull(skill, "SkillManager 未能加载: " + name);
                Assert.IsTrue(skill.Enabled, "技能被禁用: " + name);

                string body = mgr.ReadContent(name);
                Assert.IsNotNull(body, "读不到正文: " + name);
                Assert.Greater(body.Length, 1500, "正文过短，可能复制不完整: " + name);
                Assert.IsFalse(body.TrimStart().StartsWith("---"),
                    "frontmatter 未剥离，会污染模型上下文: " + name);
            }
        }

        [Test]
        public void CoreSkills_ContainTheirCriticalGuidance()
        {
            var mgr = new SkillManager(null, new[] { BuiltinSkillsDir });

            // 每份 skill 里最容易丢失、且丢了就会误导模型的那一句
            Assert.IsTrue(mgr.ReadContent("pml-authoring").Contains("执行约束"));
            Assert.IsTrue(mgr.ReadContent("cad-e3d-modeling").Contains("图层过滤"));
            Assert.IsTrue(mgr.ReadContent("e3d-element-ops").Contains("尚未实现"));
            Assert.IsTrue(mgr.ReadContent("iso-and-standards").Contains("GBK"));
        }

        [Test]
        public void CoreSkills_CrossReferencesPointToExistingSkills()
        {
            var mgr = new SkillManager(null, new[] { BuiltinSkillsDir });
            var loaded = new System.Collections.Generic.HashSet<string>();
            foreach (var s in mgr.ListSkills()) loaded.Add(s.Name);

            foreach (var name in RequiredSkills)
            {
                string body = mgr.ReadContent(name);
                foreach (System.Text.RegularExpressions.Match m in
                    System.Text.RegularExpressions.Regex.Matches(body, @"run_skill\(name='([a-z0-9-]+)'\)"))
                {
                    string target = m.Groups[1].Value;
                    Assert.IsTrue(loaded.Contains(target),
                        name + " 引用了不存在的 skill: " + target);
                }
            }
        }
    }
}
