using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace E3DCopilot.Core.Config
{
    /// <summary>
    /// Global config — auto-generates default config.json on first start
    /// Supports multi-Provider setup, inspired by Reasonix design
    /// </summary>
    public class CopilotConfig
    {
        /// <summary>
        /// Default Provider name (corresponds to Name in Providers list)
        /// </summary>
        public string DefaultProvider { get; set; } = "qwen37";
        
        /// <summary>
        /// Default model name (format: provider/model or plain model name)
        /// </summary>
        public string DefaultModel { get; set; } = "qwen37/Qwen3.7-Plus";
        
        /// <summary>
        /// Provider list, supports multiple LLM services
        /// </summary>
        public List<ProviderConfig> Providers { get; set; } = new List<ProviderConfig>();
        
        /// <summary>
        /// Backward compat: old single LLM config (deprecated, kept for migration)
        /// </summary>
        [JsonIgnore]
        public LlmConfig Llm { get; set; } = new LlmConfig();
        
        public UiConfig Ui { get; set; } = new UiConfig();
        public SafetyConfig Safety { get; set; } = new SafetyConfig();
        public MemoryConfig Memory { get; set; } = new MemoryConfig();
        public LoggingConfig Logging { get; set; } = new LoggingConfig();

        /// <summary>
        /// Provider configuration
        /// </summary>
        public class ProviderConfig
        {
            /// <summary>Provider unique identifier</summary>
            public string Name { get; set; }
            
            /// <summary>Provider type: openai (OpenAI-compatible API) or anthropic</summary>
            public string Kind { get; set; } = "openai";
            
            /// <summary>API base URL</summary>
            public string BaseUrl { get; set; }
            
            /// <summary>API Key</summary>
            public string ApiKey { get; set; } = "";
            
            /// <summary>List of available models under this Provider</summary>
            public List<string> Models { get; set; } = new List<string>();
            
            /// <summary>Default model name</summary>
            public string DefaultModel { get; set; }
            
            /// <summary>Request timeout (milliseconds)</summary>
            public int TimeoutMs { get; set; } = 120000;
            
            /// <summary>Temperature parameter</summary>
            public double Temperature { get; set; } = 0.1;
            
            /// <summary>Maximum tokens</summary>
            public int MaxTokens { get; set; } = 8192;
        }

        /// <summary>
        /// Backward compat: old single LLM config (deprecated)
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
            /// <summary>默认模式：act / plan</summary>
            public string DefaultMode { get; set; } = "act";
            /// <summary>桌面通知</summary>
            public bool Notifications { get; set; } = true;
            /// <summary>提示音</summary>
            public bool SoundEnabled { get; set; } = false;
            /// <summary>字体族：default / mono</summary>
            public string FontFamily { get; set; } = "default";
        }

        public class SafetyConfig
        {
            public bool AutoApproveReadonly { get; set; } = true;
            public int ConfirmBatchThreshold { get; set; } = 10;
            public bool ConfirmDelete { get; set; } = true;
            public bool LogAllActions { get; set; } = true;
            /// <summary>自动批准工具调用</summary>
            public bool AutoApproveTools { get; set; } = false;
            /// <summary>自动批准文件编辑</summary>
            public bool AutoApproveEdits { get; set; } = false;
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
        /// 保存配置到文件
        /// </summary>
        public void Save(string configPath = null)
        {
            string path = configPath
                ?? Path.Combine(GetDataDir(), "config.json");

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        /// <summary>
        /// Load config, create default config.json if not exists
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
                    
                    // Migrate old config to new format
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
        /// Initialize default Provider list
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
                    ApiKey = "",  // 运行环境变量或内网部署时配置
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
                    ApiKey = "",  // 本地 API，无需密钥
                    Models = new List<string> { "Qwen3.5-32B", "Qwen3-32B" },
                    DefaultModel = "Qwen3.5-32B",
                    Temperature = 0.1,
                    MaxTokens = 8192
                },
                new ProviderConfig
                {
                    Name = "qwen37",
                    Kind = "openai",
                    BaseUrl = "https://opencode.ai/zen/go/v1",
                    ApiKey = "",  // 运行环境变量或内网部署时配置
                    Models = new List<string> { "Qwen3.7-Plus" },
                    DefaultModel = "Qwen3.7-Plus",
                    Temperature = 0.1,
                    MaxTokens = 8192
                }
            };
            
            DefaultProvider = "local";  // 内网优先使用 local
            DefaultModel = "local/Qwen3.5-32B";
        }

        /// <summary>
        /// Migrate from old config to new format
        /// </summary>
        private void MigrateFromLegacy()
        {
            // If Providers is empty, it's old config, need migration
            if (Providers == null || Providers.Count == 0)
            {
                Providers = new List<ProviderConfig>();
                
                // Create default Provider from old Llm config
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
        /// 按名称获取 Provider 配置
        /// </summary>
        public ProviderConfig GetProvider(string name)
        {
            if (string.IsNullOrEmpty(name))
                return Providers.Find(p => p.Name == DefaultProvider) ?? Providers.FirstOrDefault();
            return Providers.Find(p => p.Name == name);
        }

        /// <summary>
        /// Resolve model reference (format: provider/model or plain model name)
        /// </summary>
        /// <returns>ProviderConfig and model name</returns>
        public (ProviderConfig provider, string modelName) ResolveModel(string modelRef)
        {
            if (string.IsNullOrEmpty(modelRef))
            {
                modelRef = DefaultModel;
            }

            // Parse provider/model format
            string providerName = null;
            string modelName = modelRef;
            
            int slashIndex = modelRef.IndexOf('/');
            if (slashIndex > 0)
            {
                providerName = modelRef.Substring(0, slashIndex);
                modelName = modelRef.Substring(slashIndex + 1);
            }

            // Find Provider
            ProviderConfig provider = null;
            if (!string.IsNullOrEmpty(providerName))
            {
                provider = Providers.Find(p => p.Name == providerName);
            }
            
            // If not found or not specified, use default Provider
            if (provider == null)
            {
                provider = Providers.Find(p => p.Name == DefaultProvider);
            }
            
            // If still not found, use first Provider
            if (provider == null && Providers.Count > 0)
            {
                provider = Providers[0];
            }

            return (provider, modelName);
        }

        /// <summary>
        /// Runtime data directory .e3dcopilot/
        /// </summary>
        public static string GetDataDir()
        {
            // Prefer directory from environment variable, otherwise use LocalApplicationData
            string envDir = System.Environment.GetEnvironmentVariable("E3DCOPILOT_DATA");
            if (!string.IsNullOrEmpty(envDir))
                return envDir;

            return Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "E3DCopilot");
        }
    }
}
