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

        // ════════════════════════════════════════
        //  undo_redo 接线：modify 成功后记录撤销条目
        // ════════════════════════════════════════

        [Test]
        public async Task Modify_Success_RecordsUndoEntry()
        {
            var manager = E3DCopilot.Core.Tools.UndoRedoManager.Instance;
            manager.Clear();

            // modify 前读取旧值（模拟环境 PIPE-001 存在）
            var oldValue = _env.GetAttribute("PIPE-001", "DESC") ?? "";

            var resultJson = await _dispatcher.ExecuteAsync("modify",
                "{\"dburi\": \"PIPE-001\", \"attributes\": {\"DESC\": \"modified-by-test\"}}");
            var result = JObject.Parse(resultJson);

            // 成功的 modify 必须留下撤销记录（此前 Record 零调用者，undo 永远空转）
            Assert.IsTrue((bool)result["success"], "modify 应成功: " + resultJson);
            Assert.AreEqual(1, manager.UndoCount, "modify 成功后应记录 1 条撤销条目");

            var peek = manager.PeekUndo();
            Assert.AreEqual("PIPE-001", peek.Element);
            Assert.AreEqual("DESC", peek.Attribute);
            Assert.AreEqual(oldValue, peek.OldValue);
            Assert.AreEqual("modified-by-test", peek.NewValue);

            manager.Clear();
        }
        [Test]
        public async Task UndoCreate_DeletesBatchElements()
        {
            var manager = E3DCopilot.Core.Tools.UndoRedoManager.Instance;
            manager.Clear();
            // Simulated 环境内置 PIPE-001；记录一个创建批次
            manager.RecordCreation("test", "测试批次", new[] { "PIPE-001", "NOT-EXIST-XYZ" });
            Assert.AreEqual(1, manager.CreationCount);

            // undo_create 走真实 handler（dispatcher design delete_element）
            var executor = E3DCopilot.Core.Tools.ToolExecutor.CreateDefault(_dispatcher, null);
            var tr = await executor.ExecuteAsync("undo_redo", "{\"action\": \"undo_create\"}");

            // PIPE-001 删除成功；NOT-EXIST 失败被如实汇报
            StringAssert.Contains("删除", tr.Text ?? "");
            manager.Clear();
        }

    }
}
