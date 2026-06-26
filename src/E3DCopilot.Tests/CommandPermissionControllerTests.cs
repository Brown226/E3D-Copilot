using E3DCopilot.Core.Security;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class CommandPermissionControllerTests
    {
        private CommandPermissionController _ctrl;

        [SetUp]
        public void SetUp()
        {
            _ctrl = CommandPermissionController.CreateDefault();
        }

        // ====== CheckTool ======

        [Test]
        public void CheckTool_Query_ReturnsAllow()
        {
            Assert.AreEqual(CommandPermissionController.AccessMode.Allow,
                _ctrl.CheckTool("query", "{}"));
        }

        [Test]
        public void CheckTool_GetAttributes_ReturnsAllow()
        {
            Assert.AreEqual(CommandPermissionController.AccessMode.Allow,
                _ctrl.CheckTool("get_attributes", "{}"));
        }

        [Test]
        public void CheckTool_Check_ReturnsAllow()
        {
            Assert.AreEqual(CommandPermissionController.AccessMode.Allow,
                _ctrl.CheckTool("check", "{}"));
        }

        [Test]
        public void CheckTool_Calculate_ReturnsAllow()
        {
            Assert.AreEqual(CommandPermissionController.AccessMode.Allow,
                _ctrl.CheckTool("calculate", "{}"));
        }

        [Test]
        public void CheckTool_Modify_ReturnsAsk()
        {
            Assert.AreEqual(CommandPermissionController.AccessMode.Ask,
                _ctrl.CheckTool("modify", "{}"));
        }

        [Test]
        public void CheckTool_ExecutePml_ReturnsAsk()
        {
            Assert.AreEqual(CommandPermissionController.AccessMode.Ask,
                _ctrl.CheckTool("execute_pml", "{}"));
        }

        [Test]
        public void CheckTool_Export_ReturnsAsk()
        {
            Assert.AreEqual(CommandPermissionController.AccessMode.Ask,
                _ctrl.CheckTool("export", "{}"));
        }

        [Test]
        public void CheckTool_UnknownReadOnlyTool_ReturnsAllow()
        {
            // 不在 _writeTools 中的工具默认 Allow
            Assert.AreEqual(CommandPermissionController.AccessMode.Allow,
                _ctrl.CheckTool("some_readonly_tool", "{}"));
        }

        [Test]
        public void CheckTool_WriteToolNotInRules_ReturnsAsk()
        {
            // "delete" is in _writeTools but not in rules
            var ctrl = new CommandPermissionController();
            Assert.AreEqual(CommandPermissionController.AccessMode.Ask,
                ctrl.CheckTool("delete", "{}"));
        }

        // ====== IsBatchOperation ======

        [Test]
        public void IsBatchOperation_NullArgs_ReturnsFalse()
        {
            Assert.IsFalse(_ctrl.IsBatchOperation(null));
        }

        [Test]
        public void IsBatchOperation_EmptyArgs_ReturnsFalse()
        {
            Assert.IsFalse(_ctrl.IsBatchOperation(""));
        }

        [Test]
        public void IsBatchOperation_SmallArray_ReturnsFalse()
        {
            Assert.IsFalse(_ctrl.IsBatchOperation("{\"items\": [1, 2, 3]}"));
        }

        [Test]
        public void IsBatchOperation_LargeArray_ReturnsTrue()
        {
            Assert.IsTrue(_ctrl.IsBatchOperation("{\"items\": [1, 2, 3, 4, 5, 6]}"));
        }

        [Test]
        public void IsBatchOperation_NestedLargeArray_ReturnsTrue()
        {
            Assert.IsTrue(_ctrl.IsBatchOperation("{\"data\": {\"items\": [1, 2, 3, 4, 5, 6]}}"));
        }

        [Test]
        public void IsBatchOperation_InvalidJson_ReturnsFalse()
        {
            Assert.IsFalse(_ctrl.IsBatchOperation("not json at all"));
        }

        // ====== AddRule (custom rules) ======

        [Test]
        public void AddRule_GlobPattern_OverridesDefault()
        {
            var ctrl = new CommandPermissionController();
            ctrl.AddRule("modify", CommandPermissionController.AccessMode.Allow, "auto approve modify");

            Assert.AreEqual(CommandPermissionController.AccessMode.Allow,
                ctrl.CheckTool("modify", "{}"));
        }

        [Test]
        public void AddRule_RegexPattern_MatchesCorrectly()
        {
            var ctrl = new CommandPermissionController();
            ctrl.AddRule("^query.*$", CommandPermissionController.AccessMode.Block, "block all query variants");

            Assert.AreEqual(CommandPermissionController.AccessMode.Block,
                ctrl.CheckTool("query_extended", "{}"));
        }

        [Test]
        public void AddRule_WildcardGlob_MatchesAll()
        {
            var ctrl = new CommandPermissionController();
            ctrl.AddRule("*", CommandPermissionController.AccessMode.Block, "block all");

            Assert.AreEqual(CommandPermissionController.AccessMode.Block,
                ctrl.CheckTool("anything", "{}"));
        }

        // ====== CreateDefault ======

        [Test]
        public void CreateDefault_HasExpectedRules()
        {
            var ctrl = CommandPermissionController.CreateDefault();

            // 只读工具 → Allow
            Assert.AreEqual(CommandPermissionController.AccessMode.Allow, ctrl.CheckTool("query", "{}"));
            Assert.AreEqual(CommandPermissionController.AccessMode.Allow, ctrl.CheckTool("get_attributes", "{}"));
            Assert.AreEqual(CommandPermissionController.AccessMode.Allow, ctrl.CheckTool("check", "{}"));
            Assert.AreEqual(CommandPermissionController.AccessMode.Allow, ctrl.CheckTool("calculate", "{}"));
            Assert.AreEqual(CommandPermissionController.AccessMode.Allow, ctrl.CheckTool("grep", "{}"));
            Assert.AreEqual(CommandPermissionController.AccessMode.Allow, ctrl.CheckTool("glob", "{}"));
            Assert.AreEqual(CommandPermissionController.AccessMode.Allow, ctrl.CheckTool("todo_write", "{}"));
            Assert.AreEqual(CommandPermissionController.AccessMode.Allow, ctrl.CheckTool("memory", "{}"));

            // 写工具 → Ask
            Assert.AreEqual(CommandPermissionController.AccessMode.Ask, ctrl.CheckTool("export", "{}"));
            Assert.AreEqual(CommandPermissionController.AccessMode.Ask, ctrl.CheckTool("modify", "{}"));
            Assert.AreEqual(CommandPermissionController.AccessMode.Ask, ctrl.CheckTool("execute_pml", "{}"));
        }
    }
}
