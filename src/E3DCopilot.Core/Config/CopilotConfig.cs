using System.IO;
using Newtonsoft.Json;

namespace E3DCopilot.Core.Config
{
    /// <summary>
    /// 全局配置 — 首次启动自动生成默认 config.json
    /// </summary>
    public class CopilotConfig
    {
        public LlmConfig Llm { get; set; } = new LlmConfig();
        public UiConfig Ui { get; set; } = new UiConfig();
        public SafetyConfig Safety { get; set; } = new SafetyConfig();
        public MemoryConfig Memory { get; set; } = new MemoryConfig();
        public LoggingConfig Logging { get; set; } = new LoggingConfig();

        public class LlmConfig
        {
            public string BaseUrl { get; set; } = "http://localhost:8000/v1";
            public string Model { get; set; } = "Qwen3.5-32B";
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
                }
                else
                {
                    _instance = new CopilotConfig();
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, JsonConvert.SerializeObject(_instance, Formatting.Indented));
                }

                return _instance;
            }
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
