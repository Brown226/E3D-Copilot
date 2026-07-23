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
        public string DefaultProvider { get; set; } = "local-proxy";
        
        /// <summary>
        /// Default model name (format: provider/model or plain model name)
        /// </summary>
        public string DefaultModel { get; set; } = "local-proxy/cbcn/deepseek-v4-pro";
        
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
        /// <summary>只读 MCP server 列表（B2 知识扩展，仅 resources/prompts）</summary>
        public List<McpServerConfig> McpServers { get; set; } = new List<McpServerConfig>();
        public LoggingConfig Logging { get; set; } = new LoggingConfig();
        public List<SpecializedAgentConfig> SpecializedAgents { get; set; } = new List<SpecializedAgentConfig>();
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
            
            /// <summary>推理强度 (low/medium/high/max/adaptive)，仅 DeepSeek Reasoner / Claude 等生效</summary>
            public string Effort { get; set; } = "high";
            
            /// <summary>推理协议 (deepseek/openai/none)，决定 reasoning 流的解析方式</summary>
            public string ReasoningProtocol { get; set; } = "deepseek";
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
            /// <summary>Context Compaction 触发比（0~1，当前 token 窗口用满此比例时触发压缩，0 = 禁用）</summary>
            public double CompactRatio { get; set; } = 0.8;
            /// <summary>Context Compaction 触发阈值：assistant 消息数超过此值才检查压缩（硬阈值，避免短对话触发）</summary>
            public int CompactTriggerMessages { get; set; } = 15;
            /// <summary>Soft Notice 比例（达到此比例时仅通知，不修改上下文）</summary>
            public double SoftCompactRatio { get; set; } = 0.5;
            /// <summary>Tool Result Snip 比例（达到此比例时截断过期工具结果）</summary>
            public double ToolResultSnipRatio { get; set; } = 0.6;
            /// <summary>Force Compact 比例（达到此比例时强制压缩，跳过经济性检查）</summary>
            public double CompactForceRatio { get; set; } = 0.9;
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

        /// <summary>只读 MCP server 配置（B2）</summary>
        /// <summary>
        /// MCP server 配置（对齐 Reasonix plugin.Spec）
        /// 支持 stdio / http / sse 三种传输方式
        /// </summary>
        public class McpServerConfig
        {
            public string Name { get; set; }
            /// <summary>传输方式：stdio | http | streamable-http | sse</summary>
            public string Type { get; set; } = "stdio";
            /// <summary>兼容旧配置: Transport 别名</summary>
            public string Transport { get { return Type; } set { Type = value; } }
            /// <summary>stdio 启动命令（如 npx）</summary>
            public string Command { get; set; }
            /// <summary>stdio 启动参数</summary>
            public List<string> Args { get; set; } = new List<string>();
            /// <summary>环境变量（stdio 进程注入）</summary>
            public Dictionary<string, string> Env { get; set; }
            /// <summary>http/sse 端点 URL</summary>
            public string Url { get; set; }
            /// <summary>兼容旧配置: Endpoint 别名</summary>
            public string Endpoint { get { return Url; } set { Url = value; } }
            /// <summary>http 自定义 Headers</summary>
            public Dictionary<string, string> Headers { get; set; }
            /// <summary>stdio 工作目录（对齐 Reasonix Spec.Dir）</summary>
            public string Dir { get; set; }
            /// <summary>per-server 调用超时（毫秒，0=用默认 300s）</summary>
            public int CallTimeoutMs { get; set; }
            /// <summary>兼容旧配置: TimeoutMs 别名</summary>
            public int TimeoutMs { get { return CallTimeoutMs; } set { CallTimeoutMs = value; } }
            /// <summary>per-tool 超时覆盖（key=工具原始名, value=毫秒）</summary>
            public Dictionary<string, int> ToolTimeouts { get; set; }
            /// <summary>强制标记为只读的工具名列表（对齐 Reasonix ReadOnlyToolNames）</summary>
            public List<string> ReadOnlyToolNames { get; set; }
            /// <summary>去除工具名前缀（对齐 Reasonix StripRawPrefix）</summary>
            public string StripRawPrefix { get; set; }
        }

        public class LoggingConfig
        {
            public string Level { get; set; } = "info";
            public int FileMaxMb { get; set; } = 10;
            public int FileMaxCount { get; set; } = 5;
            /// <summary>是否启用对话轨迹记录（开发调试用，记录完整思考链+工具执行+Token统计）</summary>
            public bool TraceEnabled { get; set; } = true;
            /// <summary>Trace 文件保留天数（超期自动清理）</summary>
            public int TraceRetentionDays { get; set; } = 7;
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
        /// 如果 config.json 不存在，自动生成模板配置文件
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

                // 如果全局配置不存在，自动生成模板配置文件
                if (_globalConfig == null)
                {
                    _globalConfig = new CopilotConfig();
                    _globalConfig.InitDefaultProviders();
                    GenerateDefaultConfigFile(globalPath);
                }

                // Step 2: 加载用户配置（%LOCALAPPDATA%）
                string userPath = GetUserConfigPath();
                var userConfig = LoadFromFile(userPath);

                // Step 3: 合并 — 用户配置覆盖全局配置
                _instance = MergeConfigs(_globalConfig, userConfig);

                return _instance;
            }
        }

        /// <summary>
        /// 首次启动时自动生成 config.json 模板
        /// </summary>
        private static void GenerateDefaultConfigFile(string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var template = new CopilotConfig();
                template.Providers = new List<ProviderConfig>
                {
                    new ProviderConfig
                    {
                        Name = "local-proxy",
                        Kind = "openai",
                        BaseUrl = "http://localhost:20128/v1",
                        ApiKey = "",
                        Models = new List<string> { "cbcn/deepseek-v4-pro", "cbcn/kimi-k2.7", "mimo-v2.5" },
                        DefaultModel = "cbcn/deepseek-v4-pro",
                        Temperature = 0.1,
                        MaxTokens = 8192
                    }
                };
                template.DefaultProvider = "local-proxy";
                template.DefaultModel = "local-proxy/cbcn/deepseek-v4-pro";

                File.WriteAllText(path, JsonConvert.SerializeObject(template, Formatting.Indented));
                E3DCopilot.Core.Logging.CopilotLogger.Info("已自动生成配置模板: {0}", path);
            }
            catch (Exception ex)
            {
                E3DCopilot.Core.Logging.CopilotLogger.Error(ex, "生成默认配置文件失败");
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
        /// 专长 Agent 配置（E7 Coordinator）
        /// </summary>
        public class SpecializedAgentConfig
        {
            public string Name { get; set; }
            public string SystemPrompt { get; set; }
            public bool ReadOnly { get; set; } = true;
            public string DefaultProvider { get; set; }
        }

        /// <summary>
        /// Initialize default Provider list — 不再硬编码，改为自动生成配置文件
        /// </summary>
        internal void InitDefaultProviders()
        {
            // 不再硬编码 Provider，全部由配置文件管理
            Providers = new List<ProviderConfig>();
        }

        /// <summary>
        /// Migrate from old config to new format
        /// </summary>
        private void MigrateFromLegacy()
        {
            // If Providers is empty, try to migrate from old Llm config
            if (Providers == null || Providers.Count == 0)
            {
                Providers = new List<ProviderConfig>();
                
                // Create Provider from old Llm config if available
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
                // 不再回退到硬编码 Provider，留空由配置文件管理
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
