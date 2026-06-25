using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace E3DCopilot.Core.Skills
{
    /// <summary>
    /// 技能管理器 — 扫描/加载/启禁技能
    ///
    /// 技能目录结构：
    ///   skillSources/
    ///     my-skill/
    ///       SKILL.md          ← 技能定义文件（frontmatter + markdown body）
    ///     another-skill/
    ///       SKILL.md
    ///
    /// SKILL.md 格式：
    ///   ---
    ///   name: my-skill
    ///   description: 这是一个示例技能
    ///   runAs: inline
    ///   tags: [示例, 工具]
    ///   ---
    ///   # 技能内容
    ///   这里是技能的详细说明...
    /// </summary>
    public class SkillManager
    {
        private readonly List<string> _sourcePaths = new List<string>();
        private readonly Dictionary<string, bool> _enabledState = new Dictionary<string, bool>();
        private readonly string _stateFilePath;

        // Frontmatter 正则
        private static readonly Regex FrontmatterRegex = new Regex(
            @"^---\s*\n(.*?)\n---\s*\n",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex YamlKVRegex = new Regex(
            @"^(\w+)\s*:\s*(.+)$",
            RegexOptions.Compiled);

        private static readonly Regex YamlArrayRegex = new Regex(
            @"\[(.*?)\]",
            RegexOptions.Compiled);

        public SkillManager(string stateFilePath = null)
        {
            _stateFilePath = stateFilePath
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "E3DCopilot", "skills-state.json");

            LoadState();

            // 默认包含应用目录下的 skills 文件夹
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var defaultPath = Path.Combine(appDir, "skills");
            if (Directory.Exists(defaultPath))
                _sourcePaths.Add(defaultPath);
        }

        /// <summary>
        /// 获取所有技能（合并所有来源）
        /// </summary>
        public List<SkillInfo> ListSkills()
        {
            var skills = new List<SkillInfo>();

            foreach (var sourcePath in _sourcePaths)
            {
                if (!Directory.Exists(sourcePath)) continue;

                foreach (var skillDir in Directory.GetDirectories(sourcePath))
                {
                    var skillFile = Path.Combine(skillDir, "SKILL.md");
                    if (!File.Exists(skillFile)) continue;

                    var skill = ParseSkillFile(skillFile);
                    if (skill == null) continue;

                    // 应用启禁状态
                    if (_enabledState.TryGetValue(skill.Name, out var enabled))
                        skill.Enabled = enabled;

                    skills.Add(skill);
                }
            }

            return skills;
        }

        /// <summary>
        /// 根据名称查找单个技能
        /// </summary>
        public SkillInfo Read(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return ListSkills().Find(s =>
                string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 读取技能 SKILL.md 的 body 内容（去掉 frontmatter）
        /// </summary>
        public string ReadContent(string name)
        {
            var skill = Read(name);
            if (skill == null || string.IsNullOrEmpty(skill.FilePath))
                return null;

            try
            {
                var content = File.ReadAllText(skill.FilePath);
                var match = FrontmatterRegex.Match(content);
                if (match.Success)
                    return content.Substring(match.Length).Trim();
                return content.Trim();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取所有技能来源
        /// </summary>
        public List<SkillSource> ListSources()
        {
            var sources = new List<SkillSource>();
            foreach (var path in _sourcePaths)
            {
                var skillCount = 0;
                var status = "active";

                if (!Directory.Exists(path))
                {
                    status = "missing";
                }
                else
                {
                    try
                    {
                        skillCount = Directory.GetDirectories(path)
                            .Count(d => File.Exists(Path.Combine(d, "SKILL.md")));
                    }
                    catch
                    {
                        status = "error";
                    }
                }

                sources.Add(new SkillSource
                {
                    Path = path,
                    Status = status,
                    SkillCount = skillCount,
                    Removable = true,
                });
            }

            return sources;
        }

        /// <summary>
        /// 切换技能启用/禁用
        /// </summary>
        public bool ToggleSkill(string skillName)
        {
            if (!_enabledState.ContainsKey(skillName))
                _enabledState[skillName] = true; // 默认启用，首次切换为禁用

            _enabledState[skillName] = !_enabledState[skillName];
            SaveState();
            return _enabledState[skillName];
        }

        /// <summary>
        /// 设置技能启用状态
        /// </summary>
        public void SetSkillEnabled(string skillName, bool enabled)
        {
            _enabledState[skillName] = enabled;
            SaveState();
        }

        /// <summary>
        /// 添加技能来源路径
        /// </summary>
        public bool AddSource(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (_sourcePaths.Contains(path)) return false;

            _sourcePaths.Add(path);
            return true;
        }

        /// <summary>
        /// 移除技能来源路径
        /// </summary>
        public bool RemoveSource(string path)
        {
            return _sourcePaths.Remove(path);
        }

        /// <summary>
        /// 刷新（重新扫描）
        /// </summary>
        public void Refresh()
        {
            // 清除不存在的来源
            _sourcePaths.RemoveAll(p => !Directory.Exists(p));
        }

        // ════════════════════════════════════════
        //  SKILL.md 解析
        // ════════════════════════════════════════

        private SkillInfo ParseSkillFile(string filePath)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var match = FrontmatterRegex.Match(content);

                var name = Path.GetFileName(Path.GetDirectoryName(filePath));
                var description = "";
                var runAs = "inline";
                var tags = new string[0];

                if (match.Success)
                {
                    var frontmatter = match.Groups[1].Value;
                    var body = content.Substring(match.Length).Trim();

                    foreach (var line in frontmatter.Split('\n'))
                    {
                        var kvMatch = YamlKVRegex.Match(line.Trim());
                        if (!kvMatch.Success) continue;

                        var key = kvMatch.Groups[1].Value.Trim().ToLower();
                        var value = kvMatch.Groups[2].Value.Trim().Trim('"', '\'');

                        switch (key)
                        {
                            case "name":
                                name = value;
                                break;
                            case "description":
                                description = value;
                                break;
                            case "runas":
                                runAs = value;
                                break;
                            case "tags":
                                var arrMatch = YamlArrayRegex.Match(value);
                                if (arrMatch.Success)
                                {
                                    tags = arrMatch.Groups[1].Value
                                        .Split(',')
                                        .Select(t => t.Trim().Trim('"', '\''))
                                        .Where(t => !string.IsNullOrEmpty(t))
                                        .ToArray();
                                }
                                break;
                        }
                    }

                    // body 作为补充描述
                    if (string.IsNullOrEmpty(description) && !string.IsNullOrEmpty(body))
                    {
                        description = body.Length > 200 ? body.Substring(0, 200) + "..." : body;
                    }
                }

                return new SkillInfo
                {
                    Name = name,
                    Description = description,
                    Scope = "project",
                    RunAs = runAs,
                    Enabled = true,
                    FilePath = filePath,
                    Tags = tags,
                };
            }
            catch
            {
                return null;
            }
        }

        // ════════════════════════════════════════
        //  状态持久化
        // ════════════════════════════════════════

        private void LoadState()
        {
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    var json = File.ReadAllText(_stateFilePath);
                    var state = JsonConvert.DeserializeObject<SkillState>(json);
                    if (state?.EnabledSkills != null)
                    {
                        foreach (var kv in state.EnabledSkills)
                            _enabledState[kv.Key] = kv.Value;
                    }
                }
            }
            catch { /* ignore */ }
        }

        private void SaveState()
        {
            try
            {
                var dir = Path.GetDirectoryName(_stateFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var state = new SkillState { EnabledSkills = new Dictionary<string, bool>(_enabledState) };
                File.WriteAllText(_stateFilePath, JsonConvert.SerializeObject(state, Formatting.Indented));
            }
            catch { /* ignore */ }
        }
    }
}
