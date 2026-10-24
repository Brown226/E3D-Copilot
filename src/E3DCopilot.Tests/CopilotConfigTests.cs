using System.IO;
using System.Reflection;
using E3DCopilot.Core.Config;
using Newtonsoft.Json;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class CopilotConfigTests
    {
        private string _tempDir;

        private const string DataDirEnv = "E3DCOPILOT_DATA";
        private string _originalDataDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "e3dcopilot_test_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);

            // 把用户配置目录隔离到临时目录，否则 Load() 会读到本机真实的 user.json，
            // 导致用例结果依赖运行机器
            _originalDataDir = System.Environment.GetEnvironmentVariable(DataDirEnv);
            System.Environment.SetEnvironmentVariable(DataDirEnv, Path.Combine(_tempDir, "userdata"));

            // Reset singleton via reflection for test isolation
            var field = typeof(CopilotConfig).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            // Reset singleton again
            var field = typeof(CopilotConfig).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);

            System.Environment.SetEnvironmentVariable(DataDirEnv, _originalDataDir);

            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Test]
        public void Load_NoExistingFile_CreatesDefaultConfig()
        {
            var path = Path.Combine(_tempDir, "config.json");
            var config = CopilotConfig.Load(path);

            Assert.IsNotNull(config);
            Assert.Greater(config.Providers.Count, 0);
        }

        [Test]
        public void Load_ExistingFile_LoadsCorrectly()
        {
            var path = Path.Combine(_tempDir, "config.json");
            var original = new CopilotConfig
            {
                DefaultProvider = "test",
                DefaultModel = "test/model-1"
            };
            original.Providers.Add(new CopilotConfig.ProviderConfig
            {
                Name = "test",
                BaseUrl = "http://localhost:8000/v1",
                Models = new System.Collections.Generic.List<string> { "model-1" }
            });
            File.WriteAllText(path, JsonConvert.SerializeObject(original));

            var loaded = CopilotConfig.Load(path);

            // Load merges global + user; verify global config is loaded
            Assert.IsNotNull(loaded);
            Assert.IsNotNull(loaded.Providers);
            Assert.IsTrue(loaded.Providers.Exists(p => p.Name == "test"),
                "Global config providers should be present after merge");
        }

        [Test]
        public void Save_CreatesFile()
        {
            var config = new CopilotConfig();
            config.Save();

            // Save now writes to user config path (%LOCALAPPDATA%/E3DCopilot/user.json)
            var userPath = CopilotConfig.GetUserConfigPath();
            Assert.IsTrue(File.Exists(userPath),
                $"User config should be created at {userPath}");
        }

        [Test]
        public void Save_And_Load_RoundTrip()
        {
            var path = Path.Combine(_tempDir, "roundtrip.json");
            var config = new CopilotConfig
            {
                DefaultProvider = "myprovider",
                DefaultModel = "myprovider/gpt-4"
            };
            config.Providers.Add(new CopilotConfig.ProviderConfig
            {
                Name = "myprovider",
                BaseUrl = "http://test.com",
                Models = new System.Collections.Generic.List<string> { "gpt-4" }
            });
            config.Ui.Theme = "dark";
            config.Ui.FontSize = 14;
            config.Save(path);

            // Reset singleton
            var field = typeof(CopilotConfig).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);

            var loaded = CopilotConfig.Load(path);
            Assert.AreEqual("myprovider", loaded.DefaultProvider);
            Assert.AreEqual("dark", loaded.Ui.Theme);
            Assert.AreEqual(14, loaded.Ui.FontSize);
        }

        // ====== ResolveModel ======

        [Test]
        public void ResolveModel_ProviderSlashModel_ParsesCorrectly()
        {
            var config = new CopilotConfig();
            config.Providers.Add(new CopilotConfig.ProviderConfig
            {
                Name = "openai",
                BaseUrl = "http://api.openai.com",
                Models = new System.Collections.Generic.List<string> { "gpt-4" }
            });

            var (provider, modelName) = config.ResolveModel("openai/gpt-4");

            Assert.IsNotNull(provider);
            Assert.AreEqual("openai", provider.Name);
            Assert.AreEqual("gpt-4", modelName);
        }

        [Test]
        public void ResolveModel_NoSlash_UsesDefaultProvider()
        {
            var config = new CopilotConfig
            {
                DefaultProvider = "local",
                DefaultModel = "local/Qwen3.5"
            };
            config.Providers.Add(new CopilotConfig.ProviderConfig
            {
                Name = "local",
                BaseUrl = "http://localhost:8000"
            });

            var (provider, modelName) = config.ResolveModel("Qwen3.5");

            Assert.AreEqual("local", provider.Name);
            Assert.AreEqual("Qwen3.5", modelName);
        }

        [Test]
        public void ResolveModel_EmptyString_UsesDefaultModel()
        {
            var config = new CopilotConfig
            {
                DefaultProvider = "local",
                DefaultModel = "local/Qwen3.5"
            };
            config.Providers.Add(new CopilotConfig.ProviderConfig
            {
                Name = "local",
                BaseUrl = "http://localhost:8000"
            });

            var (provider, modelName) = config.ResolveModel(null);

            Assert.AreEqual("local", provider.Name);
            Assert.AreEqual("Qwen3.5", modelName);
        }

        [Test]
        public void ResolveModel_UnknownProvider_FallsBackToDefault()
        {
            var config = new CopilotConfig
            {
                DefaultProvider = "local"
            };
            config.Providers.Add(new CopilotConfig.ProviderConfig { Name = "local" });

            var (provider, _) = config.ResolveModel("nonexistent/model");

            Assert.AreEqual("local", provider.Name);
        }

        // ====== GetProvider ======

        [Test]
        public void GetProvider_ByName_ReturnsCorrectProvider()
        {
            var config = new CopilotConfig();
            config.Providers.Add(new CopilotConfig.ProviderConfig { Name = "p1" });
            config.Providers.Add(new CopilotConfig.ProviderConfig { Name = "p2" });

            var result = config.GetProvider("p2");

            Assert.AreEqual("p2", result.Name);
        }

        [Test]
        public void GetProvider_Null_ReturnsDefaultProvider()
        {
            var config = new CopilotConfig { DefaultProvider = "p1" };
            config.Providers.Add(new CopilotConfig.ProviderConfig { Name = "p1" });
            config.Providers.Add(new CopilotConfig.ProviderConfig { Name = "p2" });

            var result = config.GetProvider(null);

            Assert.AreEqual("p1", result.Name);
        }

        [Test]
        public void GetProvider_EmptyList_ReturnsNull()
        {
            var config = new CopilotConfig();
            var result = config.GetProvider("anything");
            Assert.IsNull(result);
        }

        // ====== Default values ======

        [Test]
        public void DefaultConfig_HasExpectedDefaults()
        {
            var config = new CopilotConfig();
            Assert.AreEqual("zh-CN", config.Ui.Language);
            Assert.AreEqual("system", config.Ui.Theme);
            Assert.AreEqual(16, config.Ui.FontSize);
            Assert.AreEqual("act", config.Ui.DefaultMode);
            Assert.AreEqual(5, config.Safety.ConfirmBatchThreshold);
            Assert.AreEqual("info", config.Logging.Level);
        }

        // ====== Migration ======

        [Test]
        public void Load_LegacyConfig_MigratesProviders()
        {
            var path = Path.Combine(_tempDir, "legacy.json");
            // 真·旧版配置：没有 Providers 段，只有一个 Llm 段。
            // （此前该用例的 JSON 里根本没有 Llm 段，能通过纯属类里硬编码了默认值）
            var json = @"{
                ""Ui"": { ""Language"": ""zh-CN"" },
                ""Safety"": {},
                ""Memory"": {},
                ""Logging"": {},
                ""Llm"": {
                    ""BaseUrl"": ""http://legacy-host:8000/v1"",
                    ""Model"": ""legacy-model"",
                    ""ApiKey"": ""legacy-key""
                }
            }";
            File.WriteAllText(path, json);

            var config = CopilotConfig.Load(path);

            // 迁移后：Providers 由 Llm 段生成，且默认模型指向它
            Assert.Greater(config.Providers.Count, 0);
            Assert.AreEqual("default", config.DefaultProvider);
            Assert.AreEqual("legacy-model", config.DefaultModel.Substring("default/".Length));
            var migrated = config.Providers.Find(p => p.Name == "default");
            Assert.IsNotNull(migrated);
            Assert.AreEqual("http://legacy-host:8000/v1", migrated.BaseUrl);
        }

        [Test]
        public void Load_UserConfigWithoutProviders_DoesNotHijackDefaultModel()
        {
            // 回归用例：用户配置 Providers 为空（表示沿用全局），
            // 不得被旧版迁移逻辑注入 provider 并劫持 DefaultModel。
            var path = Path.Combine(_tempDir, "global.json");
            var global = new CopilotConfig
            {
                DefaultProvider = "myprovider",
                DefaultModel = "myprovider/my-model"
            };
            global.Providers.Add(new CopilotConfig.ProviderConfig
            {
                Name = "myprovider",
                BaseUrl = "http://127.0.0.1:9999/v1",
                DefaultModel = "my-model",
                Models = new System.Collections.Generic.List<string> { "my-model" }
            });
            File.WriteAllText(path, JsonConvert.SerializeObject(global, Formatting.Indented));

            var config = CopilotConfig.Load(path);

            Assert.AreEqual("myprovider", config.DefaultProvider);
            Assert.AreEqual("myprovider/my-model", config.DefaultModel);
            Assert.IsFalse(config.Providers.Exists(p => p.Name == "default"),
                "用户配置 Providers 为空时不应被迁移逻辑注入 default provider");
        }

        [Test]
        public void Merge_McpServers_ComesFromGlobalConfig()
        {
            // 回归用例：MCP server 由管理员在全局配置维护，
            // 合并用户配置后不能被吞掉（否则数据平台工具全部注册失败）。
            var path = Path.Combine(_tempDir, "with-mcp.json");
            var global = new CopilotConfig { DefaultProvider = "p", DefaultModel = "p/m" };
            global.Providers.Add(new CopilotConfig.ProviderConfig
            {
                Name = "p", BaseUrl = "http://127.0.0.1:1/v1", DefaultModel = "m",
                Models = new System.Collections.Generic.List<string> { "m" }
            });
            global.McpServers.Add(new CopilotConfig.McpServerConfig
            {
                Name = "test-platform", Type = "stdio", Command = "node"
            });
            File.WriteAllText(path, JsonConvert.SerializeObject(global, Formatting.Indented));

            var config = CopilotConfig.Load(path);

            Assert.AreEqual(1, config.McpServers.Count);
            Assert.AreEqual("test-platform", config.McpServers[0].Name);
        }
    }
}
