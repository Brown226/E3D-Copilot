using System.Collections.Generic;
using E3DCopilot.Core.Messaging;
using Newtonsoft.Json;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class MessageContractsTests
    {
        // ====== MessageTypes constants ======

        [Test]
        public void MessageTypes_UserMessage_HasCorrectValue()
        {
            Assert.AreEqual("user:message", MessageTypes.UserMessage);
        }

        [Test]
        public void MessageTypes_LlmStreamDelta_HasCorrectValue()
        {
            Assert.AreEqual("llm:stream:delta", MessageTypes.LlmStreamDelta);
        }

        [Test]
        public void MessageTypes_ToolDispatch_HasCorrectValue()
        {
            Assert.AreEqual("tool:dispatch", MessageTypes.ToolDispatch);
        }

        [Test]
        public void MessageTypes_HostReady_HasCorrectValue()
        {
            Assert.AreEqual("host:ready", MessageTypes.HostReady);
        }

        [Test]
        public void MessageTypes_TurnDone_HasCorrectValue()
        {
            Assert.AreEqual("turn:done", MessageTypes.TurnDone);
        }

        // ====== CopilotMessage serialization ======

        [Test]
        public void CopilotMessage_SerializesCorrectly()
        {
            var msg = new CopilotMessage<UserMessagePayload>
            {
                Type = MessageTypes.UserMessage,
                Payload = new UserMessagePayload { Text = "hello" }
            };

            var json = JsonConvert.SerializeObject(msg);
            Assert.IsTrue(json.Contains("\"type\":\"user:message\""));
            Assert.IsTrue(json.Contains("\"text\":\"hello\""));
        }

        [Test]
        public void CopilotMessage_DeserializesCorrectly()
        {
            var json = "{\"type\":\"user:message\",\"payload\":{\"text\":\"hello\",\"images\":null,\"files\":null,\"tabId\":null}}";
            var msg = JsonConvert.DeserializeObject<CopilotMessage<UserMessagePayload>>(json);

            Assert.AreEqual("user:message", msg.Type);
            Assert.AreEqual("hello", msg.Payload.Text);
        }

        // ====== UserMessagePayload ======

        [Test]
        public void UserMessagePayload_DefaultValues()
        {
            var p = new UserMessagePayload();
            Assert.AreEqual("", p.Text);
            Assert.IsNull(p.Images);
            Assert.IsNull(p.Files);
        }

        [Test]
        public void UserMessagePayload_WithTabId()
        {
            var p = new UserMessagePayload { Text = "test", TabId = "tab_1" };
            Assert.AreEqual("tab_1", p.TabId);
        }

        // ====== ApprovalPayload ======

        [Test]
        public void ApprovalPayload_RoundTrip()
        {
            var original = new ApprovalPayload { Id = "approval_1", Allow = true };
            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<ApprovalPayload>(json);

            Assert.AreEqual("approval_1", deserialized.Id);
            Assert.IsTrue(deserialized.Allow);
        }

        // ====== ToolDispatchPayload ======

        [Test]
        public void ToolDispatchPayload_Serialization()
        {
            var p = new ToolDispatchPayload
            {
                Id = "tool_1",
                Name = "query",
                Args = new { type = "PIPE" },
                TabId = "tab_1"
            };

            var json = JsonConvert.SerializeObject(p);
            Assert.IsTrue(json.Contains("\"id\":\"tool_1\""));
            Assert.IsTrue(json.Contains("\"name\":\"query\""));
        }

        // ====== ToolResultPayload ======

        [Test]
        public void ToolResultPayload_WithResult()
        {
            var p = new ToolResultPayload
            {
                Id = "tool_1",
                Result = "{\"found\": 3}"
            };

            var json = JsonConvert.SerializeObject(p);
            Assert.IsTrue(json.Contains("\"result\""));
            Assert.IsNull(p.Error);
        }

        [Test]
        public void ToolResultPayload_WithError()
        {
            var p = new ToolResultPayload
            {
                Id = "tool_1",
                Error = "element not found"
            };

            var json = JsonConvert.SerializeObject(p);
            Assert.IsTrue(json.Contains("\"error\":\"element not found\""));
        }

        // ====== ConfigSyncPayload ======

        [Test]
        public void ConfigSyncPayload_WithProviders()
        {
            var p = new ConfigSyncPayload
            {
                CurrentProvider = "local",
                CurrentModel = "Qwen3.5",
                Providers = new[]
                {
                    new ProviderInfo
                    {
                        Name = "local",
                        Kind = "openai",
                        BaseUrl = "http://localhost:8000",
                        Models = new[] { "Qwen3.5" },
                        Enabled = true
                    }
                }
            };

            var json = JsonConvert.SerializeObject(p);
            var deserialized = JsonConvert.DeserializeObject<ConfigSyncPayload>(json);

            Assert.AreEqual("local", deserialized.CurrentProvider);
            Assert.AreEqual(1, deserialized.Providers.Length);
            Assert.AreEqual("local", deserialized.Providers[0].Name);
        }

        // ====== HostReadyPayload ======

        [Test]
        public void HostReadyPayload_RoundTrip()
        {
            var original = new HostReadyPayload
            {
                Version = "1.0.0.6",
                Platform = "E3D",
                Timestamp = 1234567890
            };

            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<HostReadyPayload>(json);

            Assert.AreEqual("1.0.0.6", deserialized.Version);
            Assert.AreEqual("E3D", deserialized.Platform);
            Assert.AreEqual(1234567890, deserialized.Timestamp);
        }

        // ====== ProviderInfo ======

        [Test]
        public void ProviderInfo_DefaultValues()
        {
            var p = new ProviderInfo();
            Assert.AreEqual("openai", p.Kind);
            Assert.AreEqual("", p.ApiKey);
            Assert.IsTrue(p.Enabled);
            Assert.IsFalse(p.BuiltIn);
        }

        // ====== ModelInfo ======

        [Test]
        public void ModelInfo_Properties()
        {
            var m = new ModelInfo
            {
                Ref = "local/Qwen3.5",
                Provider = "local",
                Model = "Qwen3.5",
                Current = true
            };

            Assert.AreEqual("local/Qwen3.5", m.Ref);
            Assert.IsTrue(m.Current);
        }

        // ====== AskUserPayload ======

        [Test]
        public void AskResponsePayload_RoundTrip()
        {
            var original = new AskResponsePayload
            {
                Id = "1",
                Answers = new List<AskAnswerItem>
                {
                    new AskAnswerItem { QuestionId = "q1", Selected = new List<string> { "React" } }
                }
            };

            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<AskResponsePayload>(json);

            Assert.AreEqual("1", deserialized.Id);
            Assert.AreEqual(1, deserialized.Answers.Count);
            Assert.AreEqual("q1", deserialized.Answers[0].QuestionId);
            Assert.AreEqual("React", deserialized.Answers[0].Selected[0]);
        }

        // ====== SetPlanModePayload ======

        [Test]
        public void SetPlanModePayload_DefaultIsAct()
        {
            var p = new SetPlanModePayload();
            Assert.AreEqual("act", p.Mode);
        }

        [Test]
        public void SetPlanModePayload_PlanMode()
        {
            var p = new SetPlanModePayload { Mode = "plan" };
            var json = JsonConvert.SerializeObject(p);
            Assert.IsTrue(json.Contains("\"plan\""));
        }
    }
}
