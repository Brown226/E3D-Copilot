using System;
using System.Text.Json;
using System.Threading.Tasks;
using E3DCopilot.Core;
using E3DCopilot.Core.Events;
using Microsoft.Web.WebView2.WinForms;

namespace E3DCopilot.WebHost
{
    /// <summary>
    /// C# ↔ JavaScript 双向通信桥
    ///
    /// 接收 WebView2 前端消息 → 路由到 CopilotController
    /// 推送 C# 后端事件 → 转发到前端
    ///
    /// 消息格式: JSON { type: string, payload: any }
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
                    case "user:message":
                        HandleUserMessage(payload);
                        break;

                    case "user:cancel":
                        _controller.Cancel();
                        break;

                    case "user:new_session":
                        _controller.NewSession();
                        break;

                    case "user:approve":
                        HandleApproval(payload);
                        break;

                    case "ping":
                        SendToFrontend("pong", new { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
                        break;

                    default:
                        // 未知消息类型 — 静默忽略
                        break;
                }
            }
            catch (JsonException ex)
            {
                SendToFrontend("error", new { message = $"JSON 解析错误: {ex.Message}" });
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
                return;

            // 异步发送，不阻塞 UI 线程
            _ = Task.Run(async () =>
            {
                try
                {
                    await _controller.SendAsync(text);
                }
                catch (Exception ex)
                {
                    SendToFrontend("error", new { message = ex.Message });
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
        /// 推送事件到前端（可从任意线程调用）
        /// </summary>
        public void SendToFrontend(string type, object payload)
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
    }
}
