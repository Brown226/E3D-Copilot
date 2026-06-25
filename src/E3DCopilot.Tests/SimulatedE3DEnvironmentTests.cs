using System.Collections.Generic;
using E3DCopilot.Tools.Bridge;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class SimulatedE3DEnvironmentTests
    {
        private SimulatedE3DEnvironment _env;

        [SetUp]
        public void SetUp()
        {
            _env = new SimulatedE3DEnvironment();
        }

        // ====== QueryElements ======

        [Test]
        public void QueryElements_ByType_PIPE_ReturnsPipes()
        {
            var results = _env.QueryElements("PIPE", null, null, 50);
            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.TrueForAll(r => r.Type.Contains("PIPE")));
        }

        [Test]
        public void QueryElements_ByType_EQUI_ReturnsEquipment()
        {
            var results = _env.QueryElements("EQUI", null, null, 50);
            Assert.AreEqual(1, results.Count);
        }

        [Test]
        public void QueryElements_ByName_ReturnsMatchingElement()
        {
            var results = _env.QueryElements("PIPE", "001", null, 50);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("PIPE-001", results[0].Name);
        }

        [Test]
        public void QueryElements_WithLimit_RespectsLimit()
        {
            var results = _env.QueryElements("PIPE", null, null, 1);
            Assert.LessOrEqual(results.Count, 1);
        }

        [Test]
        public void QueryElements_NoMatch_ReturnsEmpty()
        {
            var results = _env.QueryElements("NONEXISTENT", null, null, 50);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void QueryElements_NullType_ReturnsAll()
        {
            var results = _env.QueryElements(null, null, null, 50);
            Assert.Greater(results.Count, 0);
        }

        // ====== GetAttribute ======

        [Test]
        public void GetAttribute_ExistingElement_ReturnsValue()
        {
            var value = _env.GetAttribute("PIPE-001", "DIA");
            Assert.AreEqual("DN100", value);
        }

        [Test]
        public void GetAttribute_NonExistentElement_ReturnsNull()
        {
            var value = _env.GetAttribute("PIPE-999", "DIA");
            Assert.IsNull(value);
        }

        [Test]
        public void GetAttribute_NonExistentAttribute_ReturnsNull()
        {
            var value = _env.GetAttribute("PIPE-001", "NONEXISTENT");
            Assert.IsNull(value);
        }

        [Test]
        public void GetAttribute_CaseInsensitive()
        {
            var value = _env.GetAttribute("pipe-001", "dia");
            Assert.AreEqual("DN100", value);
        }

        // ====== SetAttribute ======

        [Test]
        public void SetAttribute_ExistingElement_UpdatesValue()
        {
            _env.SetAttribute("PIPE-001", "DIA", "DN200");
            var value = _env.GetAttribute("PIPE-001", "DIA");
            Assert.AreEqual("DN200", value);
        }

        [Test]
        public void SetAttribute_NewAttribute_CreatesIt()
        {
            _env.SetAttribute("PIPE-001", "NEW_ATTR", "value123");
            var value = _env.GetAttribute("PIPE-001", "NEW_ATTR");
            Assert.AreEqual("value123", value);
        }

        [Test]
        public void SetAttribute_NonExistentElement_NoOp()
        {
            // Should not throw
            _env.SetAttribute("PIPE-999", "DIA", "DN200");
        }

        // ====== CheckExists ======

        [Test]
        public void CheckExists_ExistingElement_ReturnsTrue()
        {
            Assert.IsTrue(_env.CheckExists("PIPE-001"));
        }

        [Test]
        public void CheckExists_NonExistentElement_ReturnsFalse()
        {
            Assert.IsFalse(_env.CheckExists("PIPE-999"));
        }

        [Test]
        public void CheckExists_CaseInsensitive()
        {
            Assert.IsTrue(_env.CheckExists("pipe-001"));
        }

        // ====== ExecutePml ======

        [Test]
        public void ExecutePml_ReturnsSimulatedResult()
        {
            var result = _env.ExecutePml("$p hello");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("模拟"));
        }

        // ====== GetCurrentElementName ======

        [Test]
        public void GetCurrentElementName_ReturnsDefault()
        {
            Assert.AreEqual("PIPE-001", _env.GetCurrentElementName());
        }

        // ====== GetSelectedElementNames ======

        [Test]
        public void GetSelectedElementNames_ReturnsPreset()
        {
            var selected = _env.GetSelectedElementNames();
            Assert.IsNotNull(selected);
            Assert.Greater(selected.Count, 0);
        }

        // ====== CreateElement ======

        [Test]
        public void CreateElement_NewElement_Succeeds()
        {
            var result = _env.CreateElement("ZONE-01", "NEW-PIPE", "PIPE", "{\"DIA\": \"DN100\"}");
            Assert.IsTrue(result.Contains("\"success\": true"));
            Assert.IsTrue(_env.CheckExists("NEW-PIPE"));
        }

        [Test]
        public void CreateElement_DuplicateName_Fails()
        {
            var result = _env.CreateElement("ZONE-01", "PIPE-001", "PIPE", null);
            Assert.IsTrue(result.Contains("\"success\": false"));
        }

        [Test]
        public void CreateElement_WithAttributes_SetsAttributes()
        {
            _env.CreateElement("ZONE-01", "NEW-EQUI", "EQUIPMENT", "{\"DESCRIPTION\": \"Pump A\"}");
            var desc = _env.GetAttribute("NEW-EQUI", "DESCRIPTION");
            Assert.AreEqual("Pump A", desc);
        }

        // ====== DeleteElement ======

        [Test]
        public void DeleteElement_Existing_ReturnsTrue()
        {
            Assert.IsTrue(_env.DeleteElement("PIPE-001"));
            Assert.IsFalse(_env.CheckExists("PIPE-001"));
        }

        [Test]
        public void DeleteElement_NonExistent_ReturnsFalse()
        {
            Assert.IsFalse(_env.DeleteElement("PIPE-999"));
        }

        // ====== QueryElements after mutations ======

        [Test]
        public void QueryElements_AfterCreate_IncludesNewElement()
        {
            _env.CreateElement("ZONE-01", "PIPE-NEW", "PIPE", null);
            var results = _env.QueryElements("PIPE", null, null, 50);
            Assert.AreEqual(3, results.Count);
        }

        [Test]
        public void QueryElements_AfterDelete_ExcludesDeletedElement()
        {
            _env.DeleteElement("PIPE-001");
            var results = _env.QueryElements("PIPE", null, null, 50);
            Assert.AreEqual(1, results.Count);
        }
    }
}
