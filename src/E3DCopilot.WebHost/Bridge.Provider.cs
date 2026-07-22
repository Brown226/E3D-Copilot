using System;
using System.Text.Json;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Messaging;
using E3DCopilot.Core.Providers;

namespace E3DCopilot.WebHost
{
    /// <summary>
    /// Bridge — Provider / Model 管理
    /// </summary>
    public partial class Bridge
    {
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
