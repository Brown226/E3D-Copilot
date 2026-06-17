using System.Threading.Tasks;
using E3DCopilot.Tools.Bridge;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class E3DToolDispatcherTests
    {
        private E3DToolDispatcher _dispatcher;
        private SimulatedE3DEnvironment _env;

        [SetUp]
        public void SetUp()
        {
            _env = new SimulatedE3DEnvironment();
            _dispatcher = new E3DToolDispatcher(_env);
        }

        [Test]
        public async Task Query_WithType_ReturnsMatchingElements()
        {
            // Arrange
            var args = "{\"type\": \"PIPE\"}";

            // Act
            var resultJson = await _dispatcher.ExecuteAsync("query", args);
            var result = JObject.Parse(resultJson);

            // Assert
            Assert.IsTrue((bool)result["success"]);
            Assert.Greater((int)result["count"], 0);
        }

        [Test]
        public async Task Query_WithName_ReturnsSpecificElement()
        {
            // Arrange
            var args = "{\"type\": \"PIPE\", \"name\": \"001\"}";

            // Act
            var resultJson = await _dispatcher.ExecuteAsync("query", args);
            var result = JObject.Parse(resultJson);

            // Assert
            Assert.IsTrue((bool)result["success"]);
            var elements = (JArray)result["elements"];
            Assert.AreEqual(1, elements.Count);
            Assert.AreEqual("PIPE-001", elements[0]["name"].ToString());
        }

        [Test]
        public async Task Query_WithLimit_RespectsLimit()
        {
            // Arrange
            var args = "{\"type\": \"PIPE\", \"limit\": 1}";

            // Act
            var resultJson = await _dispatcher.ExecuteAsync("query", args);
            var result = JObject.Parse(resultJson);

            // Assert
            Assert.IsTrue((bool)result["success"]);
            var elements = (JArray)result["elements"];
            Assert.LessOrEqual(elements.Count, 1);
        }

        [Test]
        public async Task Check_ExistingElement_ReturnsTrue()
        {
            // Arrange
            var args = "{\"element\": \"PIPE-001\"}";

            // Act
            var resultJson = await _dispatcher.ExecuteAsync("check", args);
            var result = JObject.Parse(resultJson);

            // Assert
            Assert.IsTrue((bool)result["success"]);
            Assert.IsTrue((bool)result["exists"]);
        }

        [Test]
        public async Task Check_NonExistingElement_ReturnsFalse()
        {
            // Arrange
            var args = "{\"element\": \"PIPE-999\"}";

            // Act
            var resultJson = await _dispatcher.ExecuteAsync("check", args);
            var result = JObject.Parse(resultJson);

            // Assert
            Assert.IsTrue((bool)result["success"]);
            Assert.IsFalse((bool)result["exists"]);
        }

        [Test]
        public async Task Modify_ExistingElement_UpdatesAttribute()
        {
            // Arrange
            var args = "{\"element\": \"PIPE-001\", \"attribute\": \"DIA\", \"value\": \"DN150\"}";

            // Act
            var resultJson = await _dispatcher.ExecuteAsync("modify", args);
            var result = JObject.Parse(resultJson);

            // Assert
            Assert.IsTrue((bool)result["success"]);

            // 验证属性已更新
            var newValue = _env.GetAttribute("PIPE-001", "DIA");
            Assert.AreEqual("DN150", newValue);
        }

        [Test]
        public async Task ExecutePml_ReturnsResult()
        {
            // Arrange
            var args = "{\"command\": \"$p hello\"}";

            // Act
            var resultJson = await _dispatcher.ExecuteAsync("execute_pml", args);
            var result = JObject.Parse(resultJson);

            // Assert
            Assert.IsTrue((bool)result["success"]);
            Assert.IsNotNull(result["result"]);
        }

        [Test]
        public async Task UnknownTool_ReturnsError()
        {
            // Arrange
            var args = "{}";

            // Act
            var resultJson = await _dispatcher.ExecuteAsync("unknown_tool", args);
            var result = JObject.Parse(resultJson);

            // Assert
            Assert.IsNotNull(result["error"]);
        }
    }
}
