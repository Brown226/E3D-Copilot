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
    ///
    /// 拆分说明：Provider/Skills/Memory/Settings/Sessions 处理方法位于对应 partial 文件
    /// </summary>
    public partial class Bridge
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

        // 过期条目清理 Timer
        private readonly System.Threading.Timer _cleanupTimer;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public Bridge(WebView2 webView, CopilotController controller)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _cleanupTimer = new System.Threading.Timer(CleanupStaleEntries, null, 60000, 60000);
        }

        /// <summary>清理超过 60 秒的过期条目，防止内存泄漏</summary>
        private void CleanupStaleEntries(object _)
        {
            var cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 60000;
            foreach (var kv in _toolStartTimes)
            {
                if (kv.Value < cutoff)
                    _toolStartTimes.TryRemove(kv.Key, out long _);
            }
        }

        private static void Log(string message)
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "E3DCopilot", "bridge.log");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                System.IO.File.AppendAllText(logPath, $"[{timestamp}] {message}\r\n");
            }
            catch
            {
                // 忽略日志写入错误
            }
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

                    case MessageTypes.UserSetApprovalMode:
                        HandleSetApprovalMode(payload);
                        break;

                    case MessageTypes.UserCloseTab:
                        HandleCloseTab(payload);
                        break;

                    case MessageTypes.UserSteer:
                        HandleUserSteer(payload);
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

                    // ── Trace 诊断日志 ──
                    case MessageTypes.TraceLatest:
                        HandleTraceLatest(requestId);
                        break;
                    case MessageTypes.TraceList:
                        HandleTraceList(requestId);
                        break;
                    case MessageTypes.TraceRead:
                        HandleTraceRead(payload, requestId);
                        break;

                    case "devtools:open":
                        _webView.CoreWebView2.OpenDevToolsWindow();
                        break;

                    // ── 原生对话框 ──
                    case "dialog:open_file":
                        HandleOpenFileDialog(payload, requestId);
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
        /// </summary>
        private void HandleUserMessage(JsonElement? payload)
        {
            string text = null;
            string tabId = null;
            string[] images = null;
            
            if (payload.HasValue)
            {
                if (payload.Value.TryGetProperty("text", out var textProp))
                    text = textProp.GetString();
                if (payload.Value.TryGetProperty("tabId", out var tabIdProp))
                    tabId = tabIdProp.GetString();
                if (payload.Value.TryGetProperty("images", out var imagesProp) && imagesProp.ValueKind == JsonValueKind.Array)
                {
                    var imgList = new List<string>();
                    foreach (var img in imagesProp.EnumerateArray())
                    {
                        if (img.ValueKind == JsonValueKind.String)
                            imgList.Add(img.GetString());
                    }
                    images = imgList.Count > 0 ? imgList.ToArray() : null;
                }
            }

            if (string.IsNullOrWhiteSpace(text) && (images == null || images.Length == 0))
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
                    await _controller.SendAsync(text, images);
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
        /// 处理用户对 ask 问题的回答（对齐 Reasonix AnswerQuestion）
        /// </summary>
        private void HandleAskResponse(JsonElement? payload)
        {
            if (!payload.HasValue) return;

            var json = payload.Value;

            var askId = json.TryGetProperty("id", out var idProp)
                ? idProp.GetString() : null;
            if (string.IsNullOrEmpty(askId)) return;

            var answers = new List<AskAnswer>();

            // answers 数组 [{questionId, selected:[]}]
            if (json.TryGetProperty("answers", out var answersProp) && answersProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var ans in answersProp.EnumerateArray())
                {
                    var qId = ans.TryGetProperty("questionId", out var q) ? q.GetString() : "";
                    var sel = new List<string>();
                    if (ans.TryGetProperty("selected", out var s) && s.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in s.EnumerateArray())
                            sel.Add(item.GetString());
                    }
                    if (!string.IsNullOrEmpty(qId) && sel.Count > 0)
                        answers.Add(new AskAnswer { QuestionId = qId, Selected = sel });
                }
            }

            Log($"[Bridge] Received user:ask_response: id={askId}, answers={answers.Count}");

            if (answers.Count > 0)
                _controller.AnswerQuestion(askId, answers);
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
        /// 处理工具审批模式切换（ask / auto / yolo）
        /// </summary>
        private void HandleSetApprovalMode(JsonElement? payload)
        {
            string mode = "auto";
            if (payload.HasValue && payload.Value.TryGetProperty("mode", out var modeProp))
                mode = modeProp.GetString() ?? "auto";

            _controller.SetApprovalMode(mode);

            // 通知前端模式已切换
            SendToFrontend(MessageTypes.UserSetApprovalMode, new { mode });
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
        /// 处理中途干预消息 — 将引导文本放入 SteerQueue，AgentLoop 下一步自动注入
        /// </summary>
        private void HandleUserSteer(JsonElement? payload)
        {
            string text = null;
            if (payload.HasValue && payload.Value.TryGetProperty("text", out var textProp))
                text = textProp.GetString();

            if (!string.IsNullOrWhiteSpace(text))
                _controller.EnqueueSteer(text);
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
                catch (Exception ex) { Log($"[Bridge] PostWebMessage failed: {ex.Message}"); }
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

                case EventKind.Message:
                    SendToFrontend(MessageTypes.LlmMessage, new { text = evt.Text, reasoning = evt.Data, tabId });
                    break;

                case EventKind.StreamEnd:
                    SendToFrontend(MessageTypes.LlmStreamEnd, new { usage = evt.Data, error = evt.Text, tabId });
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

                case EventKind.AskRequest:
                    {
                        if (evt.Ask != null)
                        {
                            var askData = evt.Ask;
                            Log($"[Bridge] Dispatching AskRequest: id={askData.Id}, questions={askData.Questions?.Count ?? 0}");
                            SendToFrontend(MessageTypes.AskRequest, new
                            {
                                id = askData.Id,
                                questions = askData.Questions?.Select(q => new
                                {
                                    id = q.Id,
                                    header = q.Header,
                                    prompt = q.Prompt,
                                    options = q.Options?.Select(o => new { label = o.Label, description = o.Description }),
                                    multi = q.Multi
                                }),
                                tabId
                            });
                        }
                        else
                        {
                            Log($"[Bridge] AskRequest with null Ask data, ignoring");
                        }
                    }
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

        // ════════════════════════════════════════
        //  原生对话框
        // ════════════════════════════════════════

        /// <summary>
        /// 处理前端请求打开文件对话框（用于 DWG/DXF 文件选择）
        /// 必须在 STA 线程上运行（WinForms 要求）
        /// </summary>
        private void HandleOpenFileDialog(JsonElement? payload, string requestId)
        {
            string title = "选择文件";
            string filter = "所有文件|*.*";

            if (payload.HasValue)
            {
                if (payload.Value.TryGetProperty("title", out var t))
                    title = t.GetString() ?? title;
                if (payload.Value.TryGetProperty("filter", out var f))
                    filter = f.GetString() ?? filter;
            }

            // WebView2 的 WebMessageReceived 已在 UI 线程上，可直接弹对话框
            try
            {
                using (var dlg = new System.Windows.Forms.OpenFileDialog())
                {
                    dlg.Title = title;
                    dlg.Filter = filter;
                    dlg.CheckFileExists = true;
                    dlg.Multiselect = false;

                    var result = dlg.ShowDialog();
                    if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrEmpty(dlg.FileName))
                    {
                        SendToFrontend("dialog:open_file", new { path = dlg.FileName }, requestId);
                    }
                    else
                    {
                        // 用户取消 — 返回空 path
                        SendToFrontend("dialog:open_file", new { path = (string)null }, requestId);
                    }
                }
            }
            catch (Exception ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"打开文件对话框失败: {ex.Message}" }, requestId);
            }
        }
    }
}
