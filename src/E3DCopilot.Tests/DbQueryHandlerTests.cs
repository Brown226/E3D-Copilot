using System.Threading.Tasks;
using E3DCopilot.Core.Tools;
using E3DCopilot.Core.Tools.Handlers;
using E3DCopilot.Tools.Bridge;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class DbQueryHandlerTests
    {
        private DispatcherBackedHandler _handler;

        [SetUp]
        public void SetUp()
        {
            var env = new SimulatedE3DEnvironment();
            var dispatcher = new E3DToolDispatcher(env);
            _handler = new DispatcherBackedHandler(dispatcher,
                "query", "Query E3D elements", "{}", true);
        }

        [Test]
        public void Name_ReturnsQuery()
        {
            Assert.AreEqual("query", _handler.Name);
        }

        [Test]
        public void IsReadOnly_ReturnsTrue()
        {
            Assert.IsTrue(_handler.IsReadOnly);
        }

        [Test]
        public async Task ExecuteAsync_WithValidArgs_ReturnsSuccess()
        {
            var args = "{\"type\": \"PIPE\"}";
            var result = await _handler.ExecuteAsync(args);
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Text);
        }

        [Test]
        public async Task ExecuteAsync_WithEmptyArgs_ReturnsSuccess()
        {
            var args = "{}";
            var result = await _handler.ExecuteAsync(args);
            Assert.IsTrue(result.Success);
        }
    }
}
