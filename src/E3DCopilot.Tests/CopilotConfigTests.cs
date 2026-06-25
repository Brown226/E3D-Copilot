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

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "e3dcopilot_test_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
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

            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Test]
        public void Load_NoExistingFile_CreatesDefaultConfig()
        {
            var path = Path.Combine(_tempDir, "config.json");
            var config = CopilotConfig.Load(path);

            Assert.IsNotNull(config);
            Assert.IsTrue(File.Exists(path));
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

            Assert.AreEqual("test", loaded.DefaultProvider);
            Assert.AreEqual("test/model-1", loaded.DefaultModel);
        }

        [Test]
        public void Save_CreatesFile()
        {
            var path = Path.Combine(_tempDir, "save_test", "config.json");
            var config = new CopilotConfig();
            config.Save(path);

            Assert.IsTrue(File.Exists(path));
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
            Assert.AreEqual(12, config.Ui.FontSize);
            Assert.AreEqual("act", config.Ui.DefaultMode);
            Assert.IsTrue(config.Safety.AutoApproveReadonly);
            Assert.IsTrue(config.Safety.ConfirmDelete);
            Assert.AreEqual("info", config.Logging.Level);
        }

        // ====== Migration ======

        [Test]
        public void Load_LegacyConfig_MigratesProviders()
        {
            var path = Path.Combine(_tempDir, "legacy.json");
            // Simulate old config: empty Providers, but Llm section present
            var json = @"{
                ""DefaultProvider"": """",
                ""DefaultModel"": """",
                ""Providers"": [],
                ""Ui"": { ""Language"": ""zh-CN"" },
                ""Safety"": {},
                ""Memory"": {},
                ""Logging"": {}
            }";
            File.WriteAllText(path, json);

            var config = CopilotConfig.Load(path);

            // After migration, Providers should be populated
            Assert.Greater(config.Providers.Count, 0);
        }
    }
}
