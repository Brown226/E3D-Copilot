using System;
using System.Text.Json;
using System.Threading.Tasks;
using E3DCopilot.Core;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Messaging;
using Microsoft.Web.WebView2.WinForms;

namespace E3DCopilot.WebHost
{
    /// <summary>
    /// C# ↔ JavaScript 双向通信桥（改进版）
    /// 
    /// 改进点：
    /// 1. 使用 MessageTypes 常量替代硬编码字符串
    /// 2. 使用强类型消息契约
    /// 3. 更好的错误处理和日志
    /// </summary>
    public class Bridge
    {
        private readonly WebView2 _webView;
        private readonly CopilotController _controller;

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
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var typeProp))
                    return;

                var type = typeProp.GetString();
                var payload = root.TryGetProperty("payload", out var p) ? p : default;

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

                    case MessageTypes.Ping:
                        SendToFrontend(MessageTypes.Pong, new { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
                        break;

                    default:
                        // 未知消息类型 — 静默忽略
                        break;
                }
            }
            catch (JsonException ex)
            {
                SendToFrontend(MessageTypes.Error, new { message = $"JSON 解析错误: {ex.Message}" });
            }
        }

        /// <summary>
        /// 处理用户文本消息
        /// </summary>
        private void HandleUserMessage(JsonElement? payload)
        {
            string text = null;
            if (payload.HasValue && payload.Value.TryGetProperty("text", out var textProp))
                text = textProp.GetString();

            if (string.IsNullOrWhiteSpace(text))
            {
                SendToFrontend(MessageTypes.Notice, new { text = "[Bridge] 收到空消息，已忽略" });
                return;
            }

            // 诊断：确认消息到达 Bridge
            SendToFrontend(MessageTypes.Notice, new { text = $"[Bridge] 收到消息: {text.Substring(0, Math.Min(30, text.Length))}，正在调用 LLM..." });

            // 异步发送，不阻塞 UI 线程
            _ = Task.Run(async () =>
            {
                try
                {
                    await _controller.SendAsync(text);
                }
                catch (Exception ex)
                {
                    SendToFrontend(MessageTypes.Error, new { message = $"[Bridge] LLM 调用异常: {ex.GetType().Name}: {ex.Message}" });
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
        /// 推送事件到前端（可从任意线程调用）
        /// 使用强类型消息契约
        /// </summary>
        public void SendToFrontend<T>(string type, T payload)
        {
            if (_webView?.CoreWebView2 == null) return;

            var msg = JsonSerializer.Serialize(new { type, payload }, JsonOpts);

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
        public void SendToFrontend(string type, object payload)
        {
            SendToFrontend<object>(type, payload);
        }

        /// <summary>
        /// 从 CopilotEvent 分发到前端
        /// 使用 MessageTypes 常量
        /// </summary>
        public void DispatchEvent(CopilotEvent evt)
        {
            switch (evt.Kind)
            {
                case EventKind.Text:
                case EventKind.StreamDelta:
                    SendToFrontend(MessageTypes.LlmStreamDelta, new { delta = evt.Text });
                    break;

                case EventKind.StreamEnd:
                    SendToFrontend(MessageTypes.LlmStreamEnd, new { usage = evt.Data });
                    break;

                case EventKind.Thinking:
                    SendToFrontend(MessageTypes.LlmThinking, new { text = evt.Text });
                    break;

                case EventKind.ToolDispatch:
                    SendToFrontend(MessageTypes.ToolDispatch, new 
                    { 
                        id = evt.ToolId, 
                        name = evt.Text, 
                        args = evt.Data 
                    });
                    break;

                case EventKind.ToolResult:
                    SendToFrontend(MessageTypes.ToolResult, new 
                    { 
                        id = evt.ToolId, 
                        result = evt.Data?.ToString() 
                    });
                    break;

                case EventKind.ToolError:
                    SendToFrontend(MessageTypes.ToolError, new 
                    { 
                        id = evt.ToolId, 
                        error = evt.Text 
                    });
                    break;

                case EventKind.ApprovalRequest:
                    SendToFrontend(MessageTypes.ToolApproval, new 
                    { 
                        id = evt.ToolId, 
                        name = evt.Text, 
                        args = evt.Data?.ToString(),
                        description = evt.Text
                    });
                    break;

                case EventKind.AskUser:
                    var question = evt.Data is System.Text.Json.JsonElement jo
                        ? jo.GetProperty("question").GetString()
                        : evt.Text;
                    SendToFrontend(MessageTypes.AskUser, new 
                    { 
                        questionId = evt.ToolId, 
                        question = question, 
                        data = evt.Data 
                    });
                    break;

                case EventKind.Notice:
                    SendToFrontend(MessageTypes.Notice, new { text = evt.Text });
                    break;

                case EventKind.Error:
                    SendToFrontend(MessageTypes.Error, new { message = evt.Text });
                    break;
            }
        }
    }
}
