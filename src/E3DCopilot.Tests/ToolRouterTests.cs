using System.Threading.Tasks;
using E3DCopilot.Tools.Registry;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class ToolRouterTests
    {
        private ToolRouter _router;

        [SetUp]
        public void SetUp()
        {
            _router = new ToolRouter();
        }

        // ====== Query routing ======

        [Test]
        public async Task RouteAsync_QueryWithoutAttributes_StaysAsQuery()
        {
            var (toolName, args) = await _router.RouteAsync("query", "{\"type\": \"PIPE\"}");
            Assert.AreEqual("query", toolName);
        }

        [Test]
        public async Task RouteAsync_QueryWithAttributes_RoutesToGetAttributes()
        {
            var (toolName, _) = await _router.RouteAsync("query", "{\"type\": \"PIPE\", \"attributes\": [\"DIA\"]}");
            Assert.AreEqual("get_attributes", toolName);
        }

        // ====== Modify routing ======

        [Test]
        public async Task RouteAsync_Modify_StaysAsModify()
        {
            var (toolName, _) = await _router.RouteAsync("modify", "{\"element\": \"PIPE-001\"}");
            Assert.AreEqual("modify", toolName);
        }

        // ====== Check routing ======

        [Test]
        public async Task RouteAsync_Check_StaysAsCheck()
        {
            var (toolName, _) = await _router.RouteAsync("check", "{\"type\": \"exists\", \"element\": \"PIPE-001\"}");
            Assert.AreEqual("check", toolName);
        }

        // ====== Calculate routing ======

        [Test]
        public async Task RouteAsync_CalculateDistance_StaysAsCalculate()
        {
            var (toolName, _) = await _router.RouteAsync("calculate", "{\"type\": \"distance\", \"x1\": 0, \"y1\": 0}");
            Assert.AreEqual("calculate", toolName);
        }

        [Test]
        public async Task RouteAsync_CalculateWithElement_RoutesToExecutePml()
        {
            var (toolName, _) = await _router.RouteAsync("calculate", "{\"type\": \"distance\", \"element1\": \"PIPE-001\"}");
            Assert.AreEqual("execute_pml", toolName);
        }

        [Test]
        public async Task RouteAsync_CalculateWithElementKey_RoutesToExecutePml()
        {
            var (toolName, _) = await _router.RouteAsync("calculate", "{\"element\": \"EQUI-001\"}");
            Assert.AreEqual("execute_pml", toolName);
        }

        [Test]
        public async Task RouteAsync_CalculateAngle_StaysAsCalculate()
        {
            var (toolName, _) = await _router.RouteAsync("calculate", "{\"type\": \"angle\"}");
            Assert.AreEqual("calculate", toolName);
        }

        [Test]
        public async Task RouteAsync_CalculateUnknownType_RoutesToExecutePml()
        {
            var (toolName, _) = await _router.RouteAsync("calculate", "{\"type\": \"unknown_calc\"}");
            Assert.AreEqual("execute_pml", toolName);
        }

        // ====== Unknown tools ======

        [Test]
        public async Task RouteAsync_UnknownTool_PassesThrough()
        {
            var (toolName, args) = await _router.RouteAsync("grep", "{\"pattern\": \"test\"}");
            Assert.AreEqual("grep", toolName);
        }

        // ====== Edge cases ======

        [Test]
        public async Task RouteAsync_NullArgs_PassesThrough()
        {
            var (toolName, _) = await _router.RouteAsync("query", null);
            Assert.AreEqual("query", toolName);
        }

        [Test]
        public async Task RouteAsync_EmptyArgs_PassesThrough()
        {
            var (toolName, _) = await _router.RouteAsync("query", "");
            Assert.AreEqual("query", toolName);
        }

        [Test]
        public async Task RouteAsync_InvalidJson_FallsBackToOriginalTool()
        {
            var (toolName, _) = await _router.RouteAsync("query", "not json");
            Assert.AreEqual("query", toolName);
        }

        [Test]
        public async Task RouteAsync_PreservesArgs()
        {
            var originalArgs = "{\"type\": \"PIPE\", \"limit\": 10}";
            var (_, routedArgs) = await _router.RouteAsync("query", originalArgs);
            Assert.AreEqual(originalArgs, routedArgs);
        }
    }
}
