using System.Linq;
using System.Threading.Tasks;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Tools;
using E3DCopilot.Core.Tools.Handlers;
using E3DCopilot.Tools.Bridge;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class ToolExecutorTests
    {
        private ToolExecutor _executor;
        private E3DToolDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            var env = new SimulatedE3DEnvironment();
            _dispatcher = new E3DToolDispatcher(env);
            _executor = ToolExecutor.CreateDefault(_dispatcher, null);
        }

        [Test]
        public void HasHandler_Query_ReturnsTrue()
        {
            Assert.IsTrue(_executor.HasHandler("query"));
        }

        [Test]
        public void HasHandler_Modify_ReturnsTrue()
        {
            Assert.IsTrue(_executor.HasHandler("modify"));
        }

        [Test]
        public void HasHandler_Unknown_ReturnsFalse()
        {
            Assert.IsFalse(_executor.HasHandler("unknown_tool"));
        }

        [Test]
        public async Task ExecuteAsync_Query_ReturnsSuccess()
        {
            // Arrange
            var args = "{\"type\": \"PIPE\"}";

            // Act
            var result = await _executor.ExecuteAsync("query", args);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Text);
        }

        [Test]
        public async Task ExecuteAsync_UnknownTool_ReturnsFail()
        {
            // Act
            var result = await _executor.ExecuteAsync("nonexistent", "{}");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("未知工具"));
        }

        [Test]
        public void GetAllHandlers_ReturnsExpectedHandlers()
        {
            var handlers = _executor.GetAllHandlers();
            Assert.AreEqual(28, handlers.Count);
            Assert.IsTrue(handlers.Any(h => h.Name == "design"));
            Assert.IsTrue(handlers.Any(h => h.Name == "piping"));
            Assert.IsTrue(handlers.Any(h => h.Name == "geometry"));
            Assert.IsTrue(handlers.Any(h => h.Name == "cad_import"));
            Assert.IsTrue(handlers.Any(h => h.Name == "autocad"));
        }
    }
}
