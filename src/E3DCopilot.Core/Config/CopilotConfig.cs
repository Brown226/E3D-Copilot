using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace E3DCopilot.Core.Config
{
    /// <summary>
    /// 全局配置 — 首次启动自动生成默认 config.json
    /// 支持多 Provider 配置，参考 Reasonix 设计
    /// </summary>
    public class CopilotConfig
    {
        /// <summary>
        /// 默认使用的 Provider 名称（对应 Providers 列表中的 Name）
        /// </summary>
        public string DefaultProvider { get; set; } = "mimo";
        
        /// <summary>
        /// 默认使用的模型名称（格式：provider/model 或纯模型名）
        /// </summary>
        public string DefaultModel { get; set; } = "mimo/mimo-v2.5";
        
        /// <summary>
        /// Provider 列表，支持多个 LLM 服务
        /// </summary>
        public List<ProviderConfig> Providers { get; set; } = new List<ProviderConfig>();
        
        /// <summary>
        /// 向后兼容：旧的单 LLM 配置（已废弃，保留用于迁移）
        /// </summary>
        [JsonIgnore]
        public LlmConfig Llm { get; set; } = new LlmConfig();
        
        public UiConfig Ui { get; set; } = new UiConfig();
        public SafetyConfig Safety { get; set; } = new SafetyConfig();
        public MemoryConfig Memory { get; set; } = new MemoryConfig();
        public LoggingConfig Logging { get; set; } = new LoggingConfig();

        /// <summary>
        /// Provider 配置
        /// </summary>
        public class ProviderConfig
        {
            /// <summary>Provider 唯一标识名</summary>
            public string Name { get; set; }
            
            /// <summary>Provider 类型：openai（兼容 OpenAI API）或 anthropic</summary>
            public string Kind { get; set; } = "openai";
            
            /// <summary>API 基础 URL</summary>
            public string BaseUrl { get; set; }
            
            /// <summary>API Key</summary>
            public string ApiKey { get; set; } = "";
            
            /// <summary>该 Provider 下可用的模型列表</summary>
            public List<string> Models { get; set; } = new List<string>();
            
            /// <summary>默认模型名称</summary>
            public string DefaultModel { get; set; }
            
            /// <summary>请求超时时间（毫秒）</summary>
            public int TimeoutMs { get; set; } = 120000;
            
            /// <summary>温度参数</summary>
            public double Temperature { get; set; } = 0.1;
            
            /// <summary>最大 Token 数</summary>
            public int MaxTokens { get; set; } = 8192;
        }

        /// <summary>
        /// 向后兼容：旧的单 LLM 配置（已废弃）
        /// </summary>
        public class LlmConfig
        {
            public string BaseUrl { get; set; } = "https://token-plan-cn.xiaomimimo.com/v1";
            public string Model { get; set; } = "mimo-v2.5";
            public string ApiKey { get; set; } = "tp-c6vbxwk3ttizyn5z97ua2to1szxz3eso49r11x65nwoi4r2e";
            public double Temperature { get; set; } = 0.1;
            public int MaxTokens { get; set; } = 8192;
            public int TimeoutMs { get; set; } = 120000;
        }

        public class UiConfig
        {
            public string Language { get; set; } = "zh-CN";
            public string Theme { get; set; } = "system";
            public int FontSize { get; set; } = 12;
        }

        public class SafetyConfig
        {
            public bool AutoApproveReadonly { get; set; } = true;
            public int ConfirmBatchThreshold { get; set; } = 10;
            public bool ConfirmDelete { get; set; } = true;
            public bool LogAllActions { get; set; } = true;
        }

        public class MemoryConfig
        {
            public bool Enabled { get; set; } = false;
            public int MaxSessions { get; set; } = 100;
            public bool AutoSuggest { get; set; } = true;
        }

        public class LoggingConfig
        {
            public string Level { get; set; } = "info";
            public int FileMaxMb { get; set; } = 10;
            public int FileMaxCount { get; set; } = 5;
        }

        private static CopilotConfig _instance;
        private static readonly object LockObj = new object();

        /// <summary>
        /// 加载配置，文件不存在则创建默认配置
        /// </summary>
        public static CopilotConfig Load(string configPath = null)
        {
            if (_instance != null) return _instance;

            lock (LockObj)
            {
                if (_instance != null) return _instance;

                string path = configPath
                    ?? Path.Combine(GetDataDir(), "config.json");

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    _instance = JsonConvert.DeserializeObject<CopilotConfig>(json)
                        ?? new CopilotConfig();
                    
                    // 迁移旧配置到新格式
                    _instance.MigrateFromLegacy();
                }
                else
                {
                    _instance = new CopilotConfig();
                    _instance.InitDefaultProviders();
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, JsonConvert.SerializeObject(_instance, Formatting.Indented));
                }

                return _instance;
            }
        }

        /// <summary>
        /// 初始化默认 Provider 列表
        /// </summary>
        private void InitDefaultProviders()
        {
            Providers = new List<ProviderConfig>
            {
                new ProviderConfig
                {
                    Name = "mimo",
                    Kind = "openai",
                    BaseUrl = "https://token-plan-cn.xiaomimimo.com/v1",
                    ApiKey = "tp-c6vbxwk3ttizyn5z97ua2to1szxz3eso49r11x65nwoi4r2e",
                    Models = new List<string> { "mimo-v2.5", "mimo-v2-flash" },
                    DefaultModel = "mimo-v2.5",
                    Temperature = 0.1,
                    MaxTokens = 8192
                },
                new ProviderConfig
                {
                    Name = "local",
                    Kind = "openai",
                    BaseUrl = "http://localhost:8000/v1",
                    ApiKey = "",
                    Models = new List<string> { "Qwen3.5-32B", "Qwen3-32B" },
                    DefaultModel = "Qwen3.5-32B",
                    Temperature = 0.1,
                    MaxTokens = 8192
                }
            };
            
            DefaultProvider = "mimo";
            DefaultModel = "mimo/mimo-v2.5";
        }

        /// <summary>
        /// 从旧配置迁移到新格式
        /// </summary>
        private void MigrateFromLegacy()
        {
            // 如果 Providers 为空，说明是旧配置，需要迁移
            if (Providers == null || Providers.Count == 0)
            {
                Providers = new List<ProviderConfig>();
                
                // 从旧的 Llm 配置创建默认 Provider
                if (Llm != null && !string.IsNullOrEmpty(Llm.BaseUrl))
                {
                    Providers.Add(new ProviderConfig
                    {
                        Name = "default",
                        Kind = "openai",
                        BaseUrl = Llm.BaseUrl,
                        ApiKey = Llm.ApiKey ?? "",
                        Models = new List<string> { Llm.Model },
                        DefaultModel = Llm.Model,
                        Temperature = Llm.Temperature,
                        MaxTokens = Llm.MaxTokens,
                        TimeoutMs = Llm.TimeoutMs
                    });
                    
                    DefaultProvider = "default";
                    DefaultModel = $"default/{Llm.Model}";
                }
                else
                {
                    InitDefaultProviders();
                }
            }
        }

        /// <summary>
        /// 解析模型引用（格式：provider/model 或纯模型名）
        /// </summary>
        /// <returns>ProviderConfig 和模型名称</returns>
        public (ProviderConfig provider, string modelName) ResolveModel(string modelRef)
        {
            if (string.IsNullOrEmpty(modelRef))
            {
                modelRef = DefaultModel;
            }

            // 解析 provider/model 格式
            string providerName = null;
            string modelName = modelRef;
            
            int slashIndex = modelRef.IndexOf('/');
            if (slashIndex > 0)
            {
                providerName = modelRef.Substring(0, slashIndex);
                modelName = modelRef.Substring(slashIndex + 1);
            }

            // 查找 Provider
            ProviderConfig provider = null;
            if (!string.IsNullOrEmpty(providerName))
            {
                provider = Providers.Find(p => p.Name == providerName);
            }
            
            // 如果找不到或没指定，使用默认 Provider
            if (provider == null)
            {
                provider = Providers.Find(p => p.Name == DefaultProvider);
            }
            
            // 如果还是找不到，使用第一个 Provider
            if (provider == null && Providers.Count > 0)
            {
                provider = Providers[0];
            }

            return (provider, modelName);
        }

        /// <summary>
        /// 运行时数据目录 .e3dcopilot/
        /// </summary>
        public static string GetDataDir()
        {
            // 优先使用环境变量指定的目录，否则用 LocalApplicationData
            string envDir = System.Environment.GetEnvironmentVariable("E3DCOPILOT_DATA");
            if (!string.IsNullOrEmpty(envDir))
                return envDir;

            return Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "E3DCopilot");
        }
    }
}
