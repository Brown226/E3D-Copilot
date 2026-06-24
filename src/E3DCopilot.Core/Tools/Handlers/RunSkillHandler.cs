using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Skills;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// RunSkill — 让 LLM 按名称调用技能 playbook
    /// 
    /// 对齐 Reasonix skill/tools.go runSkillTool：
    ///   - 加载 SKILL.md 内容
    ///   - inline 技能：返回 body 作为 tool result（LLM 直接阅读）
    ///   - 技能体包装在 <skill-pin> sentinel 中保护压缩
    /// </summary>
    public class RunSkillHandler : IToolHandler
    {
        private readonly SkillManager _skillManager;
        private readonly IEventSink _sink;

        public RunSkillHandler(SkillManager skillManager, IEventSink sink = null)
        {
            _skillManager = skillManager ?? throw new ArgumentNullException(nameof(skillManager));
            _sink = sink;
        }

        public string Name => "run_skill";
        public string Description => "Invoke a playbook from the Skills index pinned in the system prompt. " +
            "Pass `name` as the BARE identifier (e.g. 'aveva-pdms-piping'), NOT any [🧬 subagent] tag. " +
            "The skill body returns as a tool result — read and follow its instructions. " +
            "Use for domain-specific E3D guidance (PML macros, piping standards, design specs, API reference).";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""name"": {
      ""type"": ""string"",
      ""description"": ""Skill identifier as it appears in the Skills index (e.g. 'aveva-pdms-piping'). Case-sensitive. Just the identifier, not any tag.""
    },
    ""arguments"": {
      ""type"": ""string"",
      ""description"": ""Optional free-form arguments — appended after the skill body for context.""
    }
  },
  ""required"": [""name""]
}";

        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            await Task.CompletedTask;

            try
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(args);
                string name = (json.Value<string>("name") ?? "").Trim();
                string arguments = (json.Value<string>("arguments") ?? "").Trim();

                if (string.IsNullOrEmpty(name))
                    return ToolResult.Fail("run_skill requires a 'name' argument");

                // 清除 [tag] 后缀（模型有时会复制索引中的标记）
                int bracketIdx = name.IndexOf('[');
                if (bracketIdx > 0)
                    name = name.Substring(0, bracketIdx).Trim();

                // 查找技能
                var skill = _skillManager.Read(name);
                if (skill == null)
                {
                    var available = _skillManager.ListSkills();
                    var names = string.Join(", ", available.ConvertAll(s => s.Name));
                    return ToolResult.Fail($"Unknown skill \"{name}\" — available: {(string.IsNullOrEmpty(names) ? "(none)" : names)}");
                }

                if (!skill.Enabled)
                    return ToolResult.Fail($"Skill \"{name}\" is disabled");

                // 加载 SKILL.md 内容
                string body = _skillManager.ReadContent(name);
                if (string.IsNullOrEmpty(body))
                    return ToolResult.Fail($"Skill \"{name}\" has no content");

                // 渲染（对齐 Reasonix renderInline：<skill-pin> sentinel）
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"<skill-pin name=\"{name}\">");
                sb.AppendLine($"# Skill: {skill.Name}");
                if (!string.IsNullOrEmpty(skill.Description))
                    sb.AppendLine($"> {skill.Description}");
                sb.AppendLine($"(scope: {skill.Scope} · {skill.FilePath})");
                sb.AppendLine();
                sb.Append(body);
                if (!string.IsNullOrEmpty(arguments))
                {
                    sb.AppendLine();
                    sb.AppendLine($"Arguments: {arguments}");
                }
                sb.AppendLine("</skill-pin>");

                string result = sb.ToString();
                _sink?.Emit(CopilotEvent.Notice($"Loaded skill: {name} ({result.Length} chars)"));

                return ToolResult.Ok(result, new { skill = name, length = result.Length });
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                return ToolResult.Fail($"Invalid JSON arguments: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"run_skill failed: {ex.Message}");
            }
        }
    }
}
