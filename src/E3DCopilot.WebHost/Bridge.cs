using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using E3DCopilot.Core;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Messaging;
using E3DCopilot.Core.Providers;
using Microsoft.Web.WebView2.WinForms;

namespace E3DCopilot.WebHost
{
    /// <summary>
    /// C# ↔ JavaScript 双向通信桥（v2 修复版）
    ///
    /// 关键修复：
    /// 1. 读取前端 _requestId 并在响应中回带，修复 sendAndWait 协议
    /// 2. 移除 HandleUserMessage 中的诊断 Notice，避免污染消息流
    /// 3. 新增 TurnDone 分发，驱动前端 UI 状态机重置
    /// 4. 新增 UserSetPlanMode 处理，修复 Plan/Act 模式切换
    /// 5. HandleModelSwitch 成功后发 ModelsListResult 而非 ModelSwitch
    /// </summary>
    public class Bridge
    {
        private readonly WebView2 _webView;
        private readonly CopilotController _controller;

        /// <summary>
        /// 当前请求的 _requestId（按消息类型暂存，响应时回带）
        /// 同一时刻同一类型只会有一个 pending 请求（前端 UI 串行调用）
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _pendingRequestIds
            = new ConcurrentDictionary<string, string>();

        // 工具执行耗时追踪
        private readonly ConcurrentDictionary<string, long> _toolStartTimes
            = new ConcurrentDictionary<string, long>();

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public Bridge(WebView2 webView, CopilotController controller)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        /// <summary>
        /// 处理前端发来的消息（在 UI 线程上由 WebMessageReceived 触发）
        /// </summary>
        public void HandleMessage(string raw)
        {
            string requestId = null;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var typeProp))
                    return;

                var type = typeProp.GetString();

                // 读取 _requestId（sendAndWait 协议）
                if (root.TryGetProperty("_requestId", out var ridProp))
                    requestId = ridProp.GetString();

                // 暂存 requestId，供后续 SendToFrontend 响应时回带
                if (!string.IsNullOrEmpty(requestId))
                    _pendingRequestIds[type] = requestId;

                var payload = root.TryGetProperty("payload", out var p) ? p : (JsonElement?)null;

                switch (type)
                {
                    case MessageTypes.UserMessage:
                        HandleUserMessage(payload);
                        break;

                    case MessageTypes.UserCancel:
                        _controller.Cancel();
                        break;

                    case MessageTypes.UserNewSession:
                        _controller.NewSession();
                        break;

                    case MessageTypes.UserApprove:
                        HandleApproval(payload);
                        break;

                    case MessageTypes.UserAskResponse:
                        HandleAskResponse(payload);
                        break;

                    case MessageTypes.UserSetPlanMode:
                        HandleSetPlanMode(payload);
                        break;

                    case MessageTypes.UserCloseTab:
                        HandleCloseTab(payload);
                        break;

                    case MessageTypes.Ping:
                        SendToFrontend(MessageTypes.Pong, new { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }, TakeRequestId(MessageTypes.Ping));
                        break;

                    // === Provider / Model 管理 ===
                    case MessageTypes.ModelsList:
                        HandleModelsList();
                        break;

                    case MessageTypes.ModelSwitch:
                        HandleModelSwitch(payload);
                        break;

                    case MessageTypes.ProvidersList:
                        HandleProvidersList();
                        break;

                    case MessageTypes.ProviderSave:
                        HandleProviderSave(payload);
                        break;

                    case MessageTypes.ProviderDelete:
                        HandleProviderDelete(payload);
                        break;

                    case MessageTypes.ProviderFetchModels:
                        HandleProviderFetchModels(payload);
                        break;

                    case MessageTypes.ProviderSetKey:
                        HandleProviderSetKey(payload);
                        break;

                    // ── Skills 管理 ──
                    case MessageTypes.SkillsList:
                        HandleSkillsList(requestId);
                        break;
                    case MessageTypes.SkillsToggle:
                        HandleSkillsToggle(payload, requestId);
                        break;
                    case MessageTypes.SkillsAddSource:
                        HandleSkillsAddSource(payload, requestId);
                        break;
                    case MessageTypes.SkillsRemoveSource:
                        HandleSkillsRemoveSource(payload, requestId);
                        break;
                    case MessageTypes.SkillsRefresh:
                        HandleSkillsRefresh(requestId);
                        break;

                    // ── Memory 管理 ──
                    case MessageTypes.MemoryList:
                        HandleMemoryList(requestId);
                        break;
                    case MessageTypes.MemorySave:
                        HandleMemorySave(payload, requestId);
                        break;
                    case MessageTypes.MemoryDelete:
                        HandleMemoryDelete(payload, requestId);
                        break;

                    // ── Settings 管理 ──
                    case MessageTypes.SettingsSave:
                        HandleSettingsSave(payload, requestId);
                        break;

                    // ── Sessions 管理 ──
                    case MessageTypes.SessionsList:
                        HandleSessionsList(requestId);
                        break;
                    case MessageTypes.SessionsDelete:
                        HandleSessionsDelete(payload, requestId);
                        break;

                    default:
                        // 未知消息类型 — 静默忽略
                        break;
                }
            }
            catch (JsonException ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"JSON 解析错误: {ex.Message}" }, requestId);
            }
        }

        /// <summary>
        /// 处理用户文本消息
        /// 修复：移除两行诊断 Notice（避免污染 _clineMessages，破坏 messages.length===0 判断）
        /// </summary>
        private void HandleUserMessage(JsonElement? payload)
        {
            string text = null;
            string tabId = null;
            if (payload.HasValue)
            {
                if (payload.Value.TryGetProperty("text", out var textProp))
                    text = textProp.GetString();
                if (payload.Value.TryGetProperty("tabId", out var tabIdProp))
                    tabId = tabIdProp.GetString();
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                SendToFrontend(MessageTypes.Error, new { message = "[Bridge] 收到空消息，已忽略" });
                return;
            }

            // 设置活跃 tab（多 tab 支持）
            if (!string.IsNullOrEmpty(tabId))
                _controller.SetActiveTab(tabId);

            _ = Task.Run(async () =>
            {
                try
                {
                    await _controller.SendAsync(text);
                }
                catch (Exception ex)
                {
                    SendToFrontend(MessageTypes.Error, new { message = $"[Bridge] LLM 异常: {ex.GetType().Name}: {ex.Message}" });
                }
            });
        }

        /// <summary>
        /// 处理审批结果
        /// </summary>
        private void HandleApproval(JsonElement? payload)
        {
            if (!payload.HasValue) return;

            var toolId = payload.Value.TryGetProperty("id", out var idProp)
                ? idProp.GetString() : null;
            var allow = payload.Value.TryGetProperty("allow", out var allowProp)
                && allowProp.GetBoolean();

            if (!string.IsNullOrEmpty(toolId))
                _controller.Approve(toolId, allow);
        }

        /// <summary>
        /// 处理用户对 ask_user 问题的回答
        /// </summary>
        private void HandleAskResponse(JsonElement? payload)
        {
            if (!payload.HasValue) return;

            var questionId = payload.Value.TryGetProperty("questionId", out var qidProp)
                ? qidProp.GetString() : null;
            var answer = payload.Value.TryGetProperty("answer", out var ansProp)
                ? ansProp.GetString() : null;

            if (!string.IsNullOrEmpty(questionId))
                E3DCopilot.Core.Tools.Handlers.AskUserHandler.SubmitAnswer(questionId, answer ?? "");
        }

        /// <summary>
        /// 处理 Plan/Act 模式切换
        /// </summary>
        private void HandleSetPlanMode(JsonElement? payload)
        {
            string mode = "act";
            if (payload.HasValue && payload.Value.TryGetProperty("mode", out var modeProp))
                mode = modeProp.GetString() ?? "act";

            bool enabled = string.Equals(mode, "plan", StringComparison.OrdinalIgnoreCase);
            _controller.SetPlanMode(enabled);

            // 通知前端模式已切换
            SendToFrontend(MessageTypes.UserSetPlanMode, new { enabled, mode });
        }

        /// <summary>
        /// 处理 Tab 关闭 — 清理后端对应的 session
        /// </summary>
        private void HandleCloseTab(JsonElement? payload)
        {
            string tabId = null;
            if (payload.HasValue && payload.Value.TryGetProperty("tabId", out var tabIdProp))
                tabId = tabIdProp.GetString();

            if (!string.IsNullOrEmpty(tabId))
            {
                _controller.RemoveTabSession(tabId);
            }
        }

        /// <summary>
        /// 推送事件到前端（可从任意线程调用）
        /// 使用强类型消息契约
        /// </summary>
        public void SendToFrontend<T>(string type, T payload, string requestId = null)
        {
            if (_webView?.CoreWebView2 == null) return;

            string msg;
            if (!string.IsNullOrEmpty(requestId))
            {
                // 响应 sendAndWait 请求：带上 _requestId 让前端 resolve promise
                msg = JsonSerializer.Serialize(new { type, payload, _requestId = requestId }, JsonOpts);
            }
            else
            {
                msg = JsonSerializer.Serialize(new { type, payload }, JsonOpts);
            }

            void post()
            {
                try { _webView.CoreWebView2.PostWebMessageAsString(msg); }
                catch { /* 忽略发送失败 */ }
            }

            if (_webView.InvokeRequired)
                _webView.Invoke((Action)post);
            else
                post();
        }

        /// <summary>
        /// 推送事件到前端（使用 object 类型，兼容旧代码）
        /// </summary>
        public void SendToFrontend(string type, object payload, string requestId = null)
        {
            SendToFrontend<object>(type, payload, requestId);
        }

        /// <summary>
        /// 尝试取出并清除某消息类型对应的 _requestId（响应后即清）
        /// </summary>
        private string TakeRequestId(string type)
        {
            if (string.IsNullOrEmpty(type)) return null;
            if (_pendingRequestIds.TryRemove(type, out var rid))
                return rid;
            return null;
        }

        /// <summary>
        /// 从 CopilotEvent 分发到前端
        /// 使用 MessageTypes 常量
        /// </summary>
        public void DispatchEvent(CopilotEvent evt)
        {
            // 获取当前活跃 tab ID（用于多 tab 路由）
            var tabId = _controller.ActiveTabId;

            switch (evt.Kind)
            {
                case EventKind.TurnStarted:
                    SendToFrontend(MessageTypes.LlmTurnStarted, new { request = evt.Text ?? "", tabId });
                    break;

                case EventKind.Text:
                case EventKind.StreamDelta:
                    SendToFrontend(MessageTypes.LlmStreamDelta, new { delta = evt.Text, tabId });
                    break;

                case EventKind.StreamEnd:
                    SendToFrontend(MessageTypes.LlmStreamEnd, new { usage = evt.Data, tabId });
                    break;

                case EventKind.Reasoning:
                case EventKind.Thinking:
                    SendToFrontend(MessageTypes.LlmThinking, new { text = evt.Text, tabId });
                    break;

                case EventKind.TurnDone:
                    SendToFrontend(MessageTypes.TurnDone, new { tabId });
                    break;

                case EventKind.ToolDispatch:
                    _toolStartTimes[evt.ToolId] = evt.Timestamp;
                    SendToFrontend(MessageTypes.ToolDispatch, new { id = evt.ToolId, name = evt.Text, args = evt.Data, coreToolName = evt.CoreToolName, tabId });
                    break;

                case EventKind.ToolResult:
                    long durationMs = 0;
                    if (_toolStartTimes.TryRemove(evt.ToolId, out var startTime))
                        durationMs = evt.Timestamp - startTime;
                    SendToFrontend(MessageTypes.ToolResult, new { id = evt.ToolId, result = evt.Data?.ToString(), meta = evt.Meta, tabId, durationMs });
                    break;

                case EventKind.ToolError:
                    long errDurationMs = 0;
                    if (_toolStartTimes.TryRemove(evt.ToolId, out var errStartTime))
                        errDurationMs = evt.Timestamp - errStartTime;
                    SendToFrontend(MessageTypes.ToolError, new { id = evt.ToolId, error = evt.Text, tabId, durationMs = errDurationMs });
                    break;

                case EventKind.ToolProgress:
                    SendToFrontend(MessageTypes.ToolProgress, new { id = evt.ToolId, text = evt.Text, progress = evt.Data, tabId });
                    break;

                case EventKind.ApprovalRequest:
                    SendToFrontend(MessageTypes.ToolApproval, new { id = evt.ToolId, name = evt.Text, args = evt.Data?.ToString(), description = evt.Text, tabId });
                    break;

                case EventKind.AskUser:
                    var question = evt.Data is System.Text.Json.JsonElement jo
                        ? jo.GetProperty("question").GetString()
                        : evt.Text;
                    SendToFrontend(MessageTypes.AskUser, new { questionId = evt.ToolId, question = question, data = evt.Data, tabId });
                    break;

                case EventKind.PlanModeChanged:
                    SendToFrontend(MessageTypes.UserSetPlanMode, new { enabled = evt.Text?.IndexOf("enabled", StringComparison.OrdinalIgnoreCase) >= 0 });
                    break;

                // ── Token 用量 ──
                case EventKind.Usage:
                    SendToFrontend(MessageTypes.LlmUsage, new { text = evt.Text, data = evt.Data });
                    break;

                // ── 重试事件 ──
                case EventKind.Retry:
                    SendToFrontend(MessageTypes.LlmRetry, new { text = evt.Text });
                    break;

                case EventKind.Notice:
                    SendToFrontend(MessageTypes.Notice, new { text = evt.Text });
                    break;

                case EventKind.Error:
                    SendToFrontend(MessageTypes.Error, new { message = evt.Text });
                    break;

                // ── 未映射的事件 ──
                default:
                    break;
            }
        }

        // ════════════════════════════════════════════════
        //  Provider / Model 管理
        //  修复：所有响应方法在 SendToFrontend 时传入 TakeRequestId(type) 回带 _requestId
        // ════════════════════════════════════════════════

        private void HandleModelsList()
        {
            string rid = TakeRequestId(MessageTypes.ModelsList);
            try
            {
                var result = ProvidersService.ListModels(_controller.Config);
                SendToFrontend(MessageTypes.ModelsListResult, result, rid);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"列出模型失败: {ex.Message}" }, rid);
            }
        }

        private void HandleModelSwitch(JsonElement? payload)
        {
            string rid = TakeRequestId(MessageTypes.ModelSwitch);
            try
            {
                string ref_ = null;
                if (payload.HasValue && payload.Value.TryGetProperty("ref", out var prop))
                    ref_ = prop.GetString();

                bool ok = ProvidersService.SwitchModel(_controller.Config, ref_ ?? "");
                if (ok)
                {
                    // 重建 provider 指向新模型
                    _controller.SwitchProvider(BuildProviderFromConfig(_controller.Config), ref_);
                }

                // 修复：回带 _requestId 让 sendAndWait resolve；
                // 同时发 ModelsListResult 让前端 onModelsListResult 监听器更新 UI
                // 注意：C# 匿名对象字段名不能用 ref 关键字，改用 @ref 让 JSON 序列化为 "ref"
                var switchResult = new { success = ok, @ref = ref_ ?? "" };
                SendToFrontend(MessageTypes.ModelSwitch, switchResult, rid);

                // 推送最新模型列表（不带 _requestId，给监听器用）
                var listResult = ProvidersService.ListModels(_controller.Config);
                SendToFrontend(MessageTypes.ModelsListResult, listResult);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"切换模型失败: {ex.Message}" }, rid);
            }
        }

        private void HandleProvidersList()
        {
            string rid = TakeRequestId(MessageTypes.ProvidersList);
            try
            {
                var result = ProvidersService.ListProviders(_controller.Config);
                SendToFrontend(MessageTypes.ProvidersListResult, result, rid);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"列出 Provider 失败: {ex.Message}" }, rid);
            }
        }

        private void HandleProviderSave(JsonElement? payload)
        {
            string rid = TakeRequestId(MessageTypes.ProviderSave);
            try
            {
                if (!payload.HasValue)
                {
                    SendToFrontend(MessageTypes.Error, new { message = "缺少 payload" }, rid);
                    return;
                }
                var savePayload = JsonSerializer.Deserialize<ProviderSavePayload>(payload.Value.GetRawText(), JsonOpts);
                bool ok = ProvidersService.SaveProvider(_controller.Config, savePayload);
                SendToFrontend(MessageTypes.ProvidersListResult, ProvidersService.ListProviders(_controller.Config), rid);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"保存 Provider 失败: {ex.Message}" }, rid);
            }
        }

        private void HandleProviderDelete(JsonElement? payload)
        {
            string rid = TakeRequestId(MessageTypes.ProviderDelete);
            try
            {
                string name = null;
                if (payload.HasValue && payload.Value.TryGetProperty("name", out var prop))
                    name = prop.GetString();
                bool ok = ProvidersService.DeleteProvider(_controller.Config, name ?? "");
                SendToFrontend(MessageTypes.ProvidersListResult, ProvidersService.ListProviders(_controller.Config), rid);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"删除 Provider 失败: {ex.Message}" }, rid);
            }
        }

        private async void HandleProviderFetchModels(JsonElement? payload)
        {
            string rid = TakeRequestId(MessageTypes.ProviderFetchModels);
            try
            {
                string name = null;
                if (payload.HasValue && payload.Value.TryGetProperty("name", out var prop))
                    name = prop.GetString();
                var result = await ProvidersService.FetchProviderModelsAsync(_controller.Config, name ?? "");
                SendToFrontend(MessageTypes.ProviderFetchResult, result, rid);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"拉取模型失败: {ex.Message}" }, rid);
            }
        }

        private void HandleProviderSetKey(JsonElement? payload)
        {
            string rid = TakeRequestId(MessageTypes.ProviderSetKey);
            try
            {
                if (!payload.HasValue) return;
                string name = null, key = null;
                if (payload.Value.TryGetProperty("name", out var n)) name = n.GetString();
                if (payload.Value.TryGetProperty("apiKey", out var k)) key = k.GetString();
                ProvidersService.SetProviderKey(_controller.Config, name ?? "", key ?? "");
                SendToFrontend(MessageTypes.ProvidersListResult, ProvidersService.ListProviders(_controller.Config), rid);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"设置 Key 失败: {ex.Message}" }, rid);
            }
        }

        // ════════════════════════════════════════
        //  Skills 管理
        // ════════════════════════════════════════

        private void HandleSkillsList(string requestId)
        {
            try
            {
                var skills = _controller.Skills.ListSkills();
                var sources = _controller.Skills.ListSources();
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

        // ════════════════════════════════════════
        //  Memory 管理
        // ════════════════════════════════════════

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

        // ════════════════════════════════════════
        //  Settings 管理
        // ════════════════════════════════════════

        private void HandleSettingsSave(JsonElement? payload, string requestId)
        {
            try
            {
                if (!payload.HasValue)
                {
                    SendToFrontend(MessageTypes.Error, new { message = "缺少设置数据" }, requestId);
                    return;
                }

                string key = null, value = null;
                if (payload.Value.TryGetProperty("key", out var k)) key = k.GetString();
                if (payload.Value.TryGetProperty("value", out var v)) value = v.GetString();

                if (string.IsNullOrEmpty(key))
                {
                    SendToFrontend(MessageTypes.Error, new { message = "缺少设置键" }, requestId);
                    return;
                }

                // 持久化到 Config
                var config = _controller.Config;
                switch (key)
                {
                    // === UI 设置 ===
                    case "language":
                        config.Ui.Language = value ?? "zh-CN";
                        break;
                    case "theme":
                        config.Ui.Theme = value ?? "light";
                        break;
                    case "fontSize":
                        if (int.TryParse(value, out var fontSize))
                            config.Ui.FontSize = fontSize;
                        break;
                    case "fontFamily":
                    case "font":
                        config.Ui.FontFamily = value ?? "default";
                        break;
                    case "defaultMode":
                        config.Ui.DefaultMode = value ?? "act";
                        break;
                    case "notifications":
                        if (bool.TryParse(value, out var notifications))
                            config.Ui.Notifications = notifications;
                        break;
                    case "soundEnabled":
                        if (bool.TryParse(value, out var soundEnabled))
                            config.Ui.SoundEnabled = soundEnabled;
                        break;
                    // === 安全设置 ===
                    case "autoApproveTools":
                        if (bool.TryParse(value, out var autoTools))
                            config.Safety.AutoApproveTools = autoTools;
                        break;
                    case "autoApproveEdits":
                        if (bool.TryParse(value, out var autoEdits))
                            config.Safety.AutoApproveEdits = autoEdits;
                        break;
                    // === 模型参数 ===
                    case "temperature":
                        if (double.TryParse(value, out var temp))
                        {
                            var (prov, _) = config.ResolveModel(config.DefaultModel);
                            if (prov != null) prov.Temperature = temp;
                        }
                        break;
                    case "maxTokens":
                        if (int.TryParse(value, out var maxTokens))
                        {
                            var (prov, _) = config.ResolveModel(config.DefaultModel);
                            if (prov != null) prov.MaxTokens = maxTokens;
                        }
                        break;
                    default:
                        // 未知键 — 静默忽略
                        break;
                }

                config.Save();
                SendToFrontend(MessageTypes.SettingsSave, new { key, value, saved = true }, requestId);
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"保存设置失败: {ex.Message}" }, requestId);
            }
        }

        // ════════════════════════════════════════
        //  Sessions 管理
        // ════════════════════════════════════════

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
                        messageCount = (object)(_controller.Session.Messages?.Count ?? 0),
                        isPlanMode = (object)_controller.Session.IsPlanMode,
                        isActive = (object)true,
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

        /// <summary>
        /// 根据 Config 重新构建 Provider 实例（用于切换模型后让 Controller 指向新 Provider）
        /// </summary>
        private ICopilotProvider BuildProviderFromConfig(CopilotConfig config)
        {
            var (prov, modelName) = config.ResolveModel(config.DefaultModel);
            if (prov == null) return _controller.Provider;
            if (prov.Kind == "anthropic")
                throw new NotSupportedException("Anthropic Provider not yet implemented");
            return new VllmProvider(prov.BaseUrl, modelName, prov.ApiKey);
        }
    }
}
