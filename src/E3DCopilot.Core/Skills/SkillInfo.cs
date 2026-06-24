using Newtonsoft.Json;

namespace E3DCopilot.Core.Skills
{
    /// <summary>
    /// 技能元数据 — 对应前端 Skill 接口
    /// </summary>
    public class SkillInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("scope")]
        public string Scope { get; set; } = "builtin"; // builtin | project | global | custom

        [JsonProperty("runAs")]
        public string RunAs { get; set; } = "inline"; // inline | subagent

        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("filePath")]
        public string FilePath { get; set; }

        [JsonProperty("tags")]
        public string[] Tags { get; set; } = new string[0];

        /// <summary>
        /// SKILL.md 文件的原始内容（Markdown body）
        /// </summary>
        [JsonProperty("content")]
        public string Content { get; set; }
    }

    /// <summary>
    /// 技能来源（扫描路径）
    /// </summary>
    public class SkillSource
    {
        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("status")]
        public string Status { get; set; } = "active"; // active | missing | error

        [JsonProperty("skillCount")]
        public int SkillCount { get; set; }

        [JsonProperty("removable")]
        public bool Removable { get; set; } = true;
    }

    /// <summary>
    /// 技能启禁状态持久化
    /// </summary>
    public class SkillState
    {
        [JsonProperty("enabledSkills")]
        public System.Collections.Generic.Dictionary<string, bool> EnabledSkills { get; set; }
            = new System.Collections.Generic.Dictionary<string, bool>();
    }
}
