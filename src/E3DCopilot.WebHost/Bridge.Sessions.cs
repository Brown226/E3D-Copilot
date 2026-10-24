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
                    // 标题优先用预览内容，否则回落到 TabId 短码
                    title = string.IsNullOrWhiteSpace(t.Preview)
                        ? $"会话 {t.TabId.Substring(0, Math.Min(8, t.TabId.Length))}"
                        : t.Preview,
                    preview = t.Preview ?? "",
                    messageCount = t.MessageCount,
                    createdAt = t.CreatedAt,
                    lastActivityAt = t.LastActivityAt,
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
                        preview = "",
                        messageCount = _controller.Session.MessageCount,
                        createdAt = 0L,
                        lastActivityAt = 0L,
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

                // 按 SessionId 精确定位要删的 tab。原实现不看 id 就直接 NewSession()，
                // 结果删除任意历史条目都会把「当前正在进行的会话」清空。
                string tabId = null;
                if (!string.IsNullOrEmpty(id))
                {
                    tabId = _controller.GetTabSessionInfos()
                        .FirstOrDefault(t => t.SessionId == id)?.TabId;
                }

                if (!string.IsNullOrEmpty(tabId))
                    _controller.RemoveTabSession(tabId);
                else
                    _controller.NewSession();   // 找不到对应 tab（例如当前会话）时才回退
                SendToFrontend(MessageTypes.SessionsDelete, new { id, deleted = true }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"删除会话失败: {ex.Message}" }, requestId);
            }
        }
    }
}
