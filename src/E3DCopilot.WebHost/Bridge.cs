using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

                    case MessageTypes.Ping:
                        SendToFrontend(MessageTypes.Pong, new { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }, MessageTypes.Ping);
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
            if (payload.HasValue && payload.Value.TryGetProperty("text", out var textProp))
                text = textProp.GetString();

            if (string.IsNullOrWhiteSpace(text))
            {
                // 空消息只发 Error，不发 Notice（Notice 会污染消息流）
                SendToFrontend(MessageTypes.Error, new { message = "[Bridge] 收到空消息，已忽略" });
                return;
            }

            // 异步发送，不阻塞 UI 线程；不再发诊断 Notice
            _ = Task.Run(async () =>
            {
                try
                {
                    await _controller.SendAsync(text);
                    // LLM 调用完成后会通过 AgentLoop 的 TurnDone 事件通知前端
                    // 这里不再发"[Bridge] LLM 调用完成"Notice
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
            switch (evt.Kind)
            {
                // ── 轮次开始：前端据此插入 api_req_started 消息 ──
                case EventKind.TurnStarted:
                    SendToFrontend(MessageTypes.LlmTurnStarted, new { request = evt.Text ?? "" });
                    break;

                // ── 流式文本（Text 和 StreamDelta 共用） ──
                case EventKind.Text:
                case EventKind.StreamDelta:
                    SendToFrontend(MessageTypes.LlmStreamDelta, new { delta = evt.Text });
                    break;

                case EventKind.StreamEnd:
                    SendToFrontend(MessageTypes.LlmStreamEnd, new { usage = evt.Data });
                    break;

                // ── Reasoning：与 Thinking 区分，都映射到 LlmThinking ──
                case EventKind.Reasoning:
                    SendToFrontend(MessageTypes.LlmThinking, new { text = evt.Text });
                    break;

                case EventKind.Thinking:
                    SendToFrontend(MessageTypes.LlmThinking, new { text = evt.Text });
                    break;

                case EventKind.TurnDone:
                    SendToFrontend(MessageTypes.TurnDone, new { });
                    break;

                case EventKind.ToolDispatch:
                    SendToFrontend(MessageTypes.ToolDispatch, new { id = evt.ToolId, name = evt.Text, args = evt.Data, coreToolName = evt.CoreToolName });
                    break;

                case EventKind.ToolResult:
                    SendToFrontend(MessageTypes.ToolResult, new { id = evt.ToolId, result = evt.Data?.ToString(), meta = evt.Meta });
                    break;

                case EventKind.ToolError:
                    SendToFrontend(MessageTypes.ToolError, new { id = evt.ToolId, error = evt.Text });
                    break;

                // ── 工具执行进度（长时操作） ──
                case EventKind.ToolProgress:
                    SendToFrontend(MessageTypes.ToolProgress, new { id = evt.ToolId, text = evt.Text, progress = evt.Data });
                    break;

                case EventKind.ApprovalRequest:
                    SendToFrontend(MessageTypes.ToolApproval, new { id = evt.ToolId, name = evt.Text, args = evt.Data?.ToString(), description = evt.Text });
                    break;

                case EventKind.AskUser:
                    var question = evt.Data is System.Text.Json.JsonElement jo
                        ? jo.GetProperty("question").GetString()
                        : evt.Text;
                    SendToFrontend(MessageTypes.AskUser, new { questionId = evt.ToolId, question = question, data = evt.Data });
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

        private void HandleProviderFetchModels(JsonElement? payload)
        {
            string rid = TakeRequestId(MessageTypes.ProviderFetchModels);
            try
            {
                string name = null;
                if (payload.HasValue && payload.Value.TryGetProperty("name", out var prop))
                    name = prop.GetString();
                var result = ProvidersService.FetchProviderModels(_controller.Config, name ?? "");
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
