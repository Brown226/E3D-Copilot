using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Messaging;

namespace E3DCopilot.Core.Providers
{
    /// <summary>
    /// Provider 管理服务（参考 Reasonix 设计）
    /// 提供：列出/添加/删除 provider、拉取模型列表、切换当前模型
    /// </summary>
    public class ProvidersService
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        /// <summary>
        /// 获取所有 provider 列表（前端格式）
        /// </summary>
        public static ProvidersListResultPayload ListProviders(CopilotConfig config)
        {
            var (currentProv, currentModel) = config.ResolveModel(config.DefaultModel);

            var providers = config.Providers
                .Select(p => new ProviderInfo
                {
                    Name = p.Name,
                    Kind = p.Kind ?? "openai",
                    BaseUrl = p.BaseUrl ?? "",
                    ApiKey = MaskKey(p.ApiKey),
                    KeySet = !string.IsNullOrEmpty(p.ApiKey),
                    Models = p.Models != null ? p.Models.ToArray() : new string[0],
                    Default = p.DefaultModel ?? "",
                    Enabled = true,
                    BuiltIn = IsBuiltIn(p.Name),
                    ContextWindow = p.ContextWindow,
                    VisionModels = p.VisionModels != null ? p.VisionModels.ToArray() : new string[0]
                })
                .ToArray();

            return new ProvidersListResultPayload
            {
                Providers = providers,
                CurrentProvider = currentProv?.Name ?? "",
                CurrentModel = currentModel ?? ""
            };
        }

        /// <summary>
        /// 获取所有可用模型（含当前激活标记）
        /// </summary>
        public static ModelsListResultPayload ListModels(CopilotConfig config)
        {
            var (currentProv, currentModel) = config.ResolveModel(config.DefaultModel);
            var currentRef = $"{currentProv?.Name}/{currentModel}";

            var models = new List<ModelInfo>();
            foreach (var p in config.Providers)
            {
                if (p.Models == null) continue;
                foreach (var m in p.Models)
                {
                    var ref_ = $"{p.Name}/{m}";
                    models.Add(new ModelInfo
                    {
                        Ref = ref_,
                        Provider = p.Name,
                        Model = m,
                        Current = ref_ == currentRef
                    });
                }
            }

            return new ModelsListResultPayload
            {
                Models = models.ToArray(),
                CurrentProvider = currentProv?.Name ?? "",
                CurrentModel = currentModel ?? ""
            };
        }

        /// <summary>
        /// 切换当前激活的模型（"provider/model"）
        /// </summary>
        public static bool SwitchModel(CopilotConfig config, string modelRef)
        {
            if (string.IsNullOrWhiteSpace(modelRef)) return false;
            var (prov, model) = config.ResolveModel(modelRef);
            if (prov == null || string.IsNullOrEmpty(model)) return false;

            config.DefaultProvider = prov.Name;
            config.DefaultModel = $"{prov.Name}/{model}";
            Save(config);
            return true;
        }

        /// <summary>
        /// 保存（新增或更新）provider
        /// </summary>
        public static bool SaveProvider(CopilotConfig config, ProviderSavePayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Name)) return false;

            var existing = config.Providers.Find(p => p.Name == payload.Name);
            if (existing == null)
            {
                existing = new CopilotConfig.ProviderConfig
                {
                    Name = payload.Name,
                    Kind = payload.Kind ?? "openai",
                    BaseUrl = payload.BaseUrl ?? "",
                    Models = new List<string>(payload.Models ?? new string[0]),
                    DefaultModel = payload.Default ?? "",
                    ContextWindow = payload.ContextWindow,
                    VisionModels = new List<string>(payload.VisionModels ?? new string[0])
                };
                config.Providers.Add(existing);
            }
            else
            {
                existing.Kind = payload.Kind ?? existing.Kind;
                if (!string.IsNullOrEmpty(payload.BaseUrl)) existing.BaseUrl = payload.BaseUrl;
                if (payload.Models != null) existing.Models = payload.Models.ToList();
                if (!string.IsNullOrEmpty(payload.Default)) existing.DefaultModel = payload.Default;
                existing.ContextWindow = payload.ContextWindow;
                if (payload.VisionModels != null) existing.VisionModels = payload.VisionModels.ToList();
            }

            // 如果前端传了 apiKey（非空字符串），更新
            if (!string.IsNullOrEmpty(payload.ApiKey))
                existing.ApiKey = payload.ApiKey;

            Save(config);
            return true;
        }

        /// <summary>
        /// 删除 provider
        /// </summary>
        public static bool DeleteProvider(CopilotConfig config, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            var removed = config.Providers.RemoveAll(p => p.Name == name);
            if (removed > 0)
            {
                if (config.DefaultProvider == name)
                {
                    config.DefaultProvider = config.Providers.FirstOrDefault()?.Name ?? "";
                    config.DefaultModel = config.Providers.FirstOrDefault()?.DefaultModel ?? "";
                }
                Save(config);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 设置 provider 的 API Key
        /// </summary>
        public static bool SetProviderKey(CopilotConfig config, string name, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var p = config.Providers.Find(x => x.Name == name);
            if (p == null) return false;
            p.ApiKey = apiKey ?? "";
            Save(config);
            return true;
        }

        /// <summary>
        /// 拉取 provider 的模型列表（调用 /v1/models）
        /// 异步版本：避免同步阻塞 UI 线程
        /// </summary>
        public static async Task<ProviderFetchResultPayload> FetchProviderModelsAsync(CopilotConfig config, string name)
        {
            var p = config.Providers.Find(x => x.Name == name);
            if (p == null)
                return new ProviderFetchResultPayload { ProviderName = name, Success = false, Error = "Provider not found" };

            try
            {
                var baseUrl = (p.BaseUrl ?? "").TrimEnd('/');
                var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/models");
                if (!string.IsNullOrEmpty(p.ApiKey))
                    req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", p.ApiKey);

                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                {
                    return new ProviderFetchResultPayload
                    {
                        ProviderName = name,
                        Success = false,
                        Error = $"HTTP {resp.StatusCode}"
                    };
                }

                var body = await resp.Content.ReadAsStringAsync();
                var json = JObject.Parse(body);
                var data = json["data"] as JArray;
                var models = new List<string>();
                if (data != null)
                {
                    foreach (var item in data)
                    {
                        var id = item["id"]?.ToString();
                        if (!string.IsNullOrEmpty(id)) models.Add(id);
                    }
                }

                // 更新到 provider 配置
                p.Models = models;
                if (models.Count > 0 && string.IsNullOrEmpty(p.DefaultModel))
                    p.DefaultModel = models[0];
                Save(config);

                return new ProviderFetchResultPayload
                {
                    ProviderName = name,
                    Success = true,
                    Models = models.ToArray()
                };
            }
            catch (Exception ex)
            {
                return new ProviderFetchResultPayload
                {
                    ProviderName = name,
                    Success = false,
                    Error = ex.GetType().Name + ": " + ex.Message
                };
            }
        }

        // ── 辅助方法 ──

        private static bool IsBuiltIn(string name)
        {
            // 只保留核心的三个内置 Provider：mimo、qwen37、local
            // deepseek 系列需要用户自行添加（支持 DeepSeek 特有的 effort/reasoning 配置）
            return name == "mimo" || name == "qwen37" || name == "local";
        }

        private static string MaskKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (key.Length <= 8) return "********";
            return key.Substring(0, 4) + "..." + key.Substring(key.Length - 4);
        }

        private static void Save(CopilotConfig config)
        {
            // 统一走 CopilotConfig.Save()，避免写入路径不一致
            config.Save();
        }
    }
}
