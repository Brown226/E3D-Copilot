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
        private DbQueryHandler _handler;
        private E3DToolDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            var env = new SimulatedE3DEnvironment();
            _dispatcher = new E3DToolDispatcher(env);
            _handler = new DbQueryHandler(_dispatcher);
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
            // Arrange
            var args = "{\"type\": \"PIPE\"}";

            // Act
            var result = await _handler.ExecuteAsync(args);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Text);
        }

        [Test]
        public async Task ExecuteAsync_WithEmptyArgs_ReturnsSuccess()
        {
            // Arrange
            var args = "{}";

            // Act
            var result = await _handler.ExecuteAsync(args);

            // Assert
            Assert.IsTrue(result.Success);
        }
    }
}
