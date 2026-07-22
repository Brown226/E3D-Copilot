using System;
using System.Collections.Generic;
using System.Text.Json;
using E3DCopilot.Core.Messaging;

namespace E3DCopilot.WebHost
{
    /// <summary>
    /// Bridge — Memory 管理
    /// </summary>
    public partial class Bridge
    {
        private void HandleMemoryList(string requestId)
        {
            try
            {
                var memories = _controller.Memory.List();
                SendToFrontend(MessageTypes.MemoryList, new { memories }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"获取记忆列表失败: {ex.Message}" }, requestId);
            }
        }

        private void HandleMemorySave(JsonElement? payload, string requestId)
        {
            try
            {
                if (!payload.HasValue)
                {
                    SendToFrontend(MessageTypes.Error, new { message = "缺少记忆数据" }, requestId);
                    return;
                }

                var entry = new E3DCopilot.Core.Memory.MemoryEntry();
                if (payload.Value.TryGetProperty("title", out var t)) entry.Title = t.GetString();
                if (payload.Value.TryGetProperty("content", out var c)) entry.Content = c.GetString();
                if (payload.Value.TryGetProperty("kind", out var k)) entry.Kind = k.GetString();
                if (payload.Value.TryGetProperty("id", out var id)) entry.Id = id.GetString();

                if (payload.Value.TryGetProperty("tags", out var tagsArr) && tagsArr.ValueKind == JsonValueKind.Array)
                {
                    var tags = new List<string>();
                    foreach (var tag in tagsArr.EnumerateArray())
                        tags.Add(tag.GetString());
                    entry.Tags = tags.ToArray();
                }

                var saved = _controller.Memory.Save(entry);
                SendToFrontend(MessageTypes.MemorySave, new { memory = saved }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"保存记忆失败: {ex.Message}" }, requestId);
            }
        }

        private void HandleMemoryDelete(JsonElement? payload, string requestId)
        {
            try
            {
                string id = null;
                if (payload.HasValue && payload.Value.TryGetProperty("id", out var idProp))
                    id = idProp.GetString();

                if (string.IsNullOrEmpty(id))
                {
                    SendToFrontend(MessageTypes.Error, new { message = "缺少记忆 ID" }, requestId);
                    return;
                }

                var deleted = _controller.Memory.Delete(id);
                SendToFrontend(MessageTypes.MemoryDelete, new { id, deleted }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"删除记忆失败: {ex.Message}" }, requestId);
            }
        }
    }
}
