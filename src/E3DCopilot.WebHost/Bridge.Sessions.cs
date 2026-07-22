using System;
using System.Linq;
using System.Text.Json;
using E3DCopilot.Core.Messaging;

namespace E3DCopilot.WebHost
{
    /// <summary>
    /// Bridge — Sessions 管理
    /// </summary>
    public partial class Bridge
    {
        private void HandleSessionsList(string requestId)
        {
            try
            {
                var tabInfos = _controller.GetTabSessionInfos();
                var sessions = tabInfos.Select(t => new
                {
                    id = t.SessionId,
                    tabId = t.TabId,
                    title = $"会话 {t.TabId.Substring(0, Math.Min(8, t.TabId.Length))}",
                    messageCount = t.MessageCount,
                    isPlanMode = t.IsPlanMode,
                    isActive = t.TabId == _controller.ActiveTabId,
                }).ToList();

                // 如果没有 tab session，至少返回当前 session
                if (sessions.Count == 0 && _controller.Session != null)
                {
                    sessions.Add(new
                    {
                        id = _controller.Session.SessionId,
                        tabId = _controller.ActiveTabId ?? "default",
                        title = "当前会话",
                        messageCount = _controller.Session.MessageCount,
                        isPlanMode = _controller.Session.IsPlanMode,
                        isActive = true,
                    });
                }

                SendToFrontend(MessageTypes.SessionsList, new { sessions }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"获取会话列表失败: {ex.Message}" }, requestId);
            }
        }

        private void HandleSessionsDelete(JsonElement? payload, string requestId)
        {
            try
            {
                string id = null;
                if (payload.HasValue && payload.Value.TryGetProperty("id", out var idProp))
                    id = idProp.GetString();

                // 当前实现只支持清空当前会话
                _controller.NewSession();
                SendToFrontend(MessageTypes.SessionsDelete, new { id, deleted = true }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"删除会话失败: {ex.Message}" }, requestId);
            }
        }
    }
}
