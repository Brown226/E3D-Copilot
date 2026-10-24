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

            // 只校验关键工具存在 + 数量下限，不锁定具体总数：
            // 此前硬编码 AreEqual(28) 导致每新增一个工具就要改测试，
            // 且断言早在工具增至 30 个时就已失效。
            Assert.GreaterOrEqual(handlers.Count, 28, "注册的工具数不应少于 28 个");
            Assert.IsTrue(handlers.Any(h => h.Name == "design"));
            Assert.IsTrue(handlers.Any(h => h.Name == "piping"));
            Assert.IsTrue(handlers.Any(h => h.Name == "geometry"));
            Assert.IsTrue(handlers.Any(h => h.Name == "cad_import"));
            Assert.IsTrue(handlers.Any(h => h.Name == "autocad"));

            // 演示链路依赖的两个工具，缺任一则整条流程不可用
            Assert.IsTrue(handlers.Any(h => h.Name == "dwg_diff"), "缺少 dwg_diff（A/B 版差异比对）");
            Assert.IsTrue(handlers.Any(h => h.Name == "design_workflow"), "缺少 design_workflow（全流程编排）");
        }
    }
}
