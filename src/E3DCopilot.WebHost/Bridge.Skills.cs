using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using E3DCopilot.Core.Messaging;

namespace E3DCopilot.WebHost
{
    /// <summary>
    /// Bridge — Skills 管理
    /// </summary>
    public partial class Bridge
    {
        private void HandleSkillsList(string requestId)
        {
            try
            {
                var skills = _controller.Skills.ListSkills();
                var sources = _controller.Skills.ListSources();

                // 合并内置工具作为技能条目（从 ToolExecutor 注册的工具自动生成）
                var existingNames = new HashSet<string>(
                    skills.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);

                foreach (var handler in _controller.Executor.GetAllHandlers())
                {
                    if (existingNames.Contains(handler.Name)) continue;

                    // 跳过内部工具（不暴露给用户的）
                    if (handler.Name == "todo_write" || handler.Name == "complete_step" || handler.Name == "memory") continue;

                    skills.Add(new Core.Skills.SkillInfo
                    {
                        Name = handler.Name,
                        Description = handler.Description,
                        Scope = "builtin",
                        RunAs = "inline",
                        Enabled = true,
                        Tags = new[] { "内置", "工具" }
                    });
                }

                SendToFrontend(MessageTypes.SkillsList, new { skills, sources }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"获取技能列表失败: {ex.Message}" }, requestId);
            }
        }

        private void HandleSkillsToggle(JsonElement? payload, string requestId)
        {
            try
            {
                string name = null;
                if (payload.HasValue && payload.Value.TryGetProperty("name", out var n))
                    name = n.GetString();

                if (string.IsNullOrEmpty(name))
                {
                    SendToFrontend(MessageTypes.Error, new { message = "缺少技能名称" }, requestId);
                    return;
                }

                var enabled = _controller.Skills.ToggleSkill(name);
                SendToFrontend(MessageTypes.SkillsToggle, new { name, enabled }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"切换技能失败: {ex.Message}" }, requestId);
            }
        }

        private void HandleSkillsAddSource(JsonElement? payload, string requestId)
        {
            try
            {
                string path = null;
                if (payload.HasValue && payload.Value.TryGetProperty("path", out var p))
                    path = p.GetString();

                if (string.IsNullOrEmpty(path))
                {
                    SendToFrontend(MessageTypes.Error, new { message = "缺少路径" }, requestId);
                    return;
                }

                var added = _controller.Skills.AddSource(path);
                var sources = _controller.Skills.ListSources();
                SendToFrontend(MessageTypes.SkillsAddSource, new { added, sources }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"添加来源失败: {ex.Message}" }, requestId);
            }
        }

        private void HandleSkillsRemoveSource(JsonElement? payload, string requestId)
        {
            try
            {
                string path = null;
                if (payload.HasValue && payload.Value.TryGetProperty("path", out var p))
                    path = p.GetString();

                var removed = _controller.Skills.RemoveSource(path ?? "");
                var sources = _controller.Skills.ListSources();
                SendToFrontend(MessageTypes.SkillsRemoveSource, new { removed, sources }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"移除来源失败: {ex.Message}" }, requestId);
            }
        }

        private void HandleSkillsRefresh(string requestId)
        {
            try
            {
                _controller.Skills.Refresh();
                var skills = _controller.Skills.ListSkills();
                var sources = _controller.Skills.ListSources();
                SendToFrontend(MessageTypes.SkillsList, new { skills, sources }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"刷新技能失败: {ex.Message}" }, requestId);
            }
        }
    }
}
