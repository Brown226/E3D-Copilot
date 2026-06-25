using E3DCopilot.Tools.Bridge;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    [TestFixture]
    public class PmlGeneratorTests
    {
        private PmlGenerator _gen;

        [SetUp]
        public void SetUp()
        {
            _gen = new PmlGenerator();
        }

        // ====== GenerateQuery ======

        [Test]
        public void GenerateQuery_WithType_ContainsType()
        {
            var pml = _gen.GenerateQuery("PIPE", null);
            Assert.IsTrue(pml.Contains("all PIPE"));
            Assert.IsTrue(pml.Contains("DO !item values !items"));
        }

        [Test]
        public void GenerateQuery_WithPattern_ContainsMatchwild()
        {
            var pml = _gen.GenerateQuery("PIPE", "001-*");
            Assert.IsTrue(pml.Contains("Matchwild(name,'001-*')"));
        }

        [Test]
        public void GenerateQuery_WithScope_ContainsForClause()
        {
            var pml = _gen.GenerateQuery("PIPE", null, scope: "ZONE-01");
            Assert.IsTrue(pml.Contains("for $!ZONE-01"));
        }

        [Test]
        public void GenerateQuery_WithoutPattern_NoMatchwild()
        {
            var pml = _gen.GenerateQuery("EQUI", null);
            Assert.IsFalse(pml.Contains("Matchwild"));
        }

        [Test]
        public void GenerateQuery_ContainsCountOutput()
        {
            var pml = _gen.GenerateQuery("PIPE", null);
            Assert.IsTrue(pml.Contains("!items.size()"));
        }

        // ====== GenerateBatchSet ======

        [Test]
        public void GenerateBatchSet_Basic_ContainsAttributeAssignment()
        {
            var pml = _gen.GenerateBatchSet("PIPE", "DIA", "DN150");
            Assert.IsTrue(pml.Contains("all PIPE"));
            Assert.IsTrue(pml.Contains("!item.:DIA = 'DN150'"));
        }

        [Test]
        public void GenerateBatchSet_WithFilter_ContainsMatchwild()
        {
            var pml = _gen.GenerateBatchSet("PIPE", "DIA", "DN150", filter: "LINE-*");
            Assert.IsTrue(pml.Contains("Matchwild(name,'LINE-*')"));
        }

        [Test]
        public void GenerateBatchSet_WithScope_ContainsForClause()
        {
            var pml = _gen.GenerateBatchSet("PIPE", "SPEC", "SCH80", scope: "ZONE-02");
            Assert.IsTrue(pml.Contains("for $!ZONE-02"));
        }

        [Test]
        public void GenerateBatchSet_ContainsCounter()
        {
            var pml = _gen.GenerateBatchSet("PIPE", "DIA", "DN150");
            Assert.IsTrue(pml.Contains("!count = 0"));
            Assert.IsTrue(pml.Contains("!count = !count + 1"));
        }

        // ====== GenerateCheck ======

        [Test]
        public void GenerateCheck_ContainsExistCheck()
        {
            var pml = _gen.GenerateCheck("PIPE-001");
            Assert.IsTrue(pml.Contains("var !flag exist $!PIPE-001"));
            Assert.IsTrue(pml.Contains("if !flag eq 'TRUEA'"));
            Assert.IsTrue(pml.Contains("PIPE-001 存在"));
            Assert.IsTrue(pml.Contains("PIPE-001 不存在"));
        }

        // ====== GenerateNavigate ======

        [Test]
        public void GenerateNavigate_ContainsElementAndCeInfo()
        {
            var pml = _gen.GenerateNavigate("PIPE-001");
            Assert.IsTrue(pml.Contains("$!PIPE-001"));
            Assert.IsTrue(pml.Contains("!!ce.name"));
            Assert.IsTrue(pml.Contains("!!ce.type"));
        }

        // ====== GenerateGetChildren ======

        [Test]
        public void GenerateGetChildren_WithoutScope_IteratesCurrentElement()
        {
            var pml = _gen.GenerateGetChildren();
            Assert.IsTrue(pml.Contains("DO !child values !!ce.mem"));
            Assert.IsFalse(pml.Contains("$!"));
        }

        [Test]
        public void GenerateGetChildren_WithScope_SetsScopeFirst()
        {
            var pml = _gen.GenerateGetChildren(scope: "ZONE-01");
            Assert.IsTrue(pml.Contains("$!ZONE-01"));
            Assert.IsTrue(pml.Contains("DO !child values !!ce.mem"));
        }

        // ====== GenerateDistance ======

        [Test]
        public void GenerateDistance_ContainsBothElementsAndCalculation()
        {
            var pml = _gen.GenerateDistance("PIPE-001", "EQUI-001");
            Assert.IsTrue(pml.Contains("$!PIPE-001"));
            Assert.IsTrue(pml.Contains("$!EQUI-001"));
            Assert.IsTrue(pml.Contains("!pos1.Distance(!pos2)"));
            Assert.IsTrue(pml.Contains("距离"));
        }
    }
}
