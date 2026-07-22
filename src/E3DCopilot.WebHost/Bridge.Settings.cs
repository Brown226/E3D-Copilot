using System;
using System.Text.Json;
using E3DCopilot.Core.Messaging;

namespace E3DCopilot.WebHost
{
    /// <summary>
    /// Bridge — Settings 管理
    /// </summary>
    public partial class Bridge
    {
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
                    case "maxSteps":
                        if (int.TryParse(value, out var maxSteps))
                            config.Ui.MaxSteps = maxSteps;
                        break;
                    case "version":
                        config.Ui.Version = value ?? "2.0.0";
                        break;
                    case "aboutUrl":
                        config.Ui.AboutUrl = value ?? "";
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
    }
}
