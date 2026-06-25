using System;
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
        public IsoConfig Iso { get; set; } = new IsoConfig();

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
            
            /// <summary>Context window size (0 = model default)</summary>
            public int ContextWindow { get; set; } = 0;
            
            /// <summary>Models that support vision/image input (comma-separated in config)</summary>
            public List<string> VisionModels { get; set; } = new List<string>();
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
            public int FontSize { get; set; } = 16;
            /// <summary>默认模式：act / plan</summary>
            public string DefaultMode { get; set; } = "act";
            /// <summary>桌面通知</summary>
            public bool Notifications { get; set; } = true;
            /// <summary>提示音</summary>
            public bool SoundEnabled { get; set; } = false;
            /// <summary>字体族：default / mono</summary>
            public string FontFamily { get; set; } = "default";
            /// <summary>Agent 执行轮数上限（0 = 不限）</summary>
            public int MaxSteps { get; set; } = 20;
            /// <summary>版本号（如 2.1.0）</summary>
            public string Version { get; set; } = "2.0.0";
            /// <summary>在线说明书链接</summary>
            public string AboutUrl { get; set; } = "";
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

        /// <summary>
        /// ISO出图配置
        /// </summary>
        public class IsoConfig
        {
            // ═══════════════════════════════════════
            //  管理员级别配置（全局 config.json）
            // ═══════════════════════════════════════
            
            /// <summary>默认项目编号</summary>
            public string DefaultProjectId { get; set; } = "1907";
            
            /// <summary>默认模板类型</summary>
            public string DefaultTemplateType { get; set; } = "standard";
            
            /// <summary>是否包含材料清单</summary>
            public bool IncludeMaterialList { get; set; } = true;
            
            /// <summary>AutoCAD启动超时时间（秒）</summary>
            public int AutoCadTimeoutSeconds { get; set; } = 60;
            
            /// <summary>是否自动启动AutoCAD</summary>
            public bool AutoStartAutoCad { get; set; } = true;
            
            // ═══════════════════════════════════════
            //  用户级别配置（用户 user.json）
            // ═══════════════════════════════════════
            
            /// <summary>AutoCAD可执行文件路径（每台电脑不同）</summary>
            public string AutoCadPath { get; set; } = "";
            
            /// <summary>默认输出目录（用户可自定义）</summary>
            public string DefaultOutputDir { get; set; } = "";
        }

        private static CopilotConfig _instance;
        private static CopilotConfig _globalConfig;
        private static readonly object LockObj = new object();

        // ════════════════════════════════════════
        //  双层配置：全局（管理员）+ 用户（个人偏好）
        // ════════════════════════════════════════

        /// <summary>
        /// 全局配置文件路径（插件目录，管理员维护）
        /// </summary>
        public static string GetGlobalConfigPath()
        {
            return Path.Combine(GetPluginDir(), "config.json");
        }

        /// <summary>
        /// 用户配置文件路径（%LOCALAPPDATA%，每用户独立）
        /// </summary>
        public static string GetUserConfigPath()
        {
            return Path.Combine(GetDataDir(), "user.json");
        }

        /// <summary>
        /// 插件所在目录（全局配置存放位置）
        /// </summary>
        public static string GetPluginDir()
        {
            // E3D 插件加载 DLL 的目录
            var codeBase = typeof(CopilotConfig).Assembly.CodeBase;
            if (!string.IsNullOrEmpty(codeBase))
            {
                var uri = new Uri(codeBase);
                if (uri.IsFile)
                    return Path.GetDirectoryName(uri.LocalPath);
            }
            // 降级：当前工作目录
            return Directory.GetCurrentDirectory();
        }

        /// <summary>
        /// 保存用户级配置到 user.json
        /// </summary>
        public void SaveUserConfig()
        {
            var userConfig = new UserConfig
            {
                DefaultProvider = this.DefaultProvider,
                DefaultModel = this.DefaultModel,
                Providers = this.Providers,
                Ui = this.Ui,
                Safety = this.Safety,
                // 保存用户级别的ISO配置
                Iso = new UserIsoConfig
                {
                    AutoCadPath = this.Iso?.AutoCadPath,
                    DefaultOutputDir = this.Iso?.DefaultOutputDir
                }
            };

            string path = GetUserConfigPath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, JsonConvert.SerializeObject(userConfig, Formatting.Indented));
        }

        /// <summary>
        /// 保存全局配置到 config.json（管理员维护）
        /// </summary>
        public void SaveGlobalConfig()
        {
            string path = GetGlobalConfigPath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        /// <summary>
        /// 保存配置（向后兼容：写到数据目录）
        /// </summary>
        public void Save(string configPath = null)
        {
            // 默认保存到用户配置（最常用的场景）
            SaveUserConfig();
        }

        /// <summary>
        /// 加载配置：全局 + 用户合并，用户优先覆盖
        /// </summary>
        public static CopilotConfig Load(string configPath = null)
        {
            if (_instance != null) return _instance;

            lock (LockObj)
            {
                if (_instance != null) return _instance;

                // Step 1: 加载全局配置（插件目录）
                string globalPath = configPath ?? GetGlobalConfigPath();
                _globalConfig = LoadFromFile(globalPath);

                // Step 2: 加载用户配置（%LOCALAPPDATA%）
                string userPath = GetUserConfigPath();
                var userConfig = LoadFromFile(userPath);

                // Step 3: 合并 — 用户配置覆盖全局配置
                _instance = MergeConfigs(_globalConfig, userConfig);

                return _instance;
            }
        }

        private static CopilotConfig LoadFromFile(string path)
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var config = JsonConvert.DeserializeObject<CopilotConfig>(json);
                if (config != null)
                {
                    config.MigrateFromLegacy();
                    return config;
                }
            }
            return null;
        }

        /// <summary>
        /// 合并两个配置：user 覆盖 global，null/默认值不覆盖
        /// </summary>
        private static CopilotConfig MergeConfigs(CopilotConfig global, CopilotConfig user)
        {
            // 如果没有全局配置，用默认值
            if (global == null) global = new CopilotConfig();
            // 如果没有用户配置，直接返回全局配置
            if (user == null)
            {
                // 确保 Providers 不为空
                if (global.Providers == null || global.Providers.Count == 0)
                    global.InitDefaultProviders();
                return global;
            }

            // 逐字段合并：用户有值则用用户的，否则用全局的
            var result = new CopilotConfig();

            // 顶层字段
            result.DefaultProvider = !string.IsNullOrEmpty(user.DefaultProvider) ? user.DefaultProvider : global.DefaultProvider;
            result.DefaultModel = !string.IsNullOrEmpty(user.DefaultModel) ? user.DefaultModel : global.DefaultModel;

            // Providers — 用户有自己的就用用户的，否则用全局的
            result.Providers = (user.Providers != null && user.Providers.Count > 0)
                ? user.Providers
                : global.Providers;

            // Ui — 逐字段合并
            result.Ui = MergeUiConfig(global.Ui, user.Ui);

            // Safety — 用户配置优先
            result.Safety = user.Safety ?? global.Safety;

            // Memory / Logging — 用全局的（管理员控制）
            result.Memory = global.Memory;
            result.Logging = global.Logging;
            
            // ISO 配置 — 管理员默认 + 用户覆盖
            // 从 CopilotConfig.Iso 中提取用户级别的配置
            var userIsoConfig = (user.Iso != null) ? new UserIsoConfig
            {
                AutoCadPath = user.Iso.AutoCadPath,
                DefaultOutputDir = user.Iso.DefaultOutputDir
            } : null;
            result.Iso = MergeIsoConfig(global.Iso, userIsoConfig);

            // Migrate
            result.MigrateFromLegacy();

            return result;
        }

        private static UiConfig MergeUiConfig(UiConfig global, UiConfig user)
        {
            if (global == null) global = new UiConfig();
            if (user == null) return global;

            return new UiConfig
            {
                Language = !string.IsNullOrEmpty(user.Language) ? user.Language : global.Language,
                Theme = !string.IsNullOrEmpty(user.Theme) ? user.Theme : global.Theme,
                FontSize = user.FontSize > 0 ? user.FontSize : global.FontSize,
                DefaultMode = !string.IsNullOrEmpty(user.DefaultMode) ? user.DefaultMode : global.DefaultMode,
                Notifications = user.Notifications,
                SoundEnabled = user.SoundEnabled,
                FontFamily = !string.IsNullOrEmpty(user.FontFamily) ? user.FontFamily : global.FontFamily,
                MaxSteps = user.MaxSteps > 0 ? user.MaxSteps : global.MaxSteps,
                Version = !string.IsNullOrEmpty(user.Version) ? user.Version : global.Version,
                AboutUrl = !string.IsNullOrEmpty(user.AboutUrl) ? user.AboutUrl : global.AboutUrl,
            };
        }

        /// <summary>
        /// 合并ISO配置：管理员默认 + 用户覆盖
        /// </summary>
        private static IsoConfig MergeIsoConfig(IsoConfig global, UserIsoConfig userIso)
        {
            if (global == null) global = new IsoConfig();
            
            var result = new IsoConfig
            {
                // 管理员级别配置（使用全局值）
                DefaultProjectId = global.DefaultProjectId,
                DefaultTemplateType = global.DefaultTemplateType,
                IncludeMaterialList = global.IncludeMaterialList,
                AutoCadTimeoutSeconds = global.AutoCadTimeoutSeconds,
                AutoStartAutoCad = global.AutoStartAutoCad,
                
                // 用户级别配置（用户覆盖全局）
                AutoCadPath = !string.IsNullOrEmpty(userIso?.AutoCadPath) 
                    ? userIso.AutoCadPath 
                    : global.AutoCadPath,
                DefaultOutputDir = !string.IsNullOrEmpty(userIso?.DefaultOutputDir) 
                    ? userIso.DefaultOutputDir 
                    : global.DefaultOutputDir
            };

            return result;
        }

        /// <summary>
        /// 用户配置文件结构（只保存用户可修改的部分）
        /// </summary>
        public class UserConfig
        {
            public string DefaultProvider { get; set; }
            public string DefaultModel { get; set; }
            public List<ProviderConfig> Providers { get; set; }
            public UiConfig Ui { get; set; }
            public SafetyConfig Safety { get; set; }
            
            /// <summary>用户级别的ISO配置（AutoCAD路径、输出目录等）</summary>
            public UserIsoConfig Iso { get; set; }
        }

        /// <summary>
        /// 用户级别的ISO配置
        /// </summary>
        public class UserIsoConfig
        {
            /// <summary>AutoCAD可执行文件路径（每台电脑不同）</summary>
            public string AutoCadPath { get; set; }
            
            /// <summary>默认输出目录（用户可自定义）</summary>
            public string DefaultOutputDir { get; set; }
        }

        /// <summary>
        /// Initialize default Provider list
        /// </summary>
        internal void InitDefaultProviders()
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
