using System.Collections.Generic;
using E3DCopilot.Core.Models.Building;
using E3DCopilot.Core.Models.Geometry;
using E3DCopilot.Core.Services.Cad;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    /// <summary>
    /// PmlScriptGenerator（CAD→E3D 建筑脚本生成器）单元测试。
    /// 重点验证 P3 修复：规格名为空时不应生成悬空的 SPRE ... SPECIFICATION 行。
    /// </summary>
    [TestFixture]
    public class PmlScriptGeneratorTests
    {
        private PmlScriptGenerator _gen;

        [SetUp]
        public void SetUp()
        {
            _gen = new PmlScriptGenerator();
        }

        private static BuildingElement Wall(double thickness = 200, double height = 3000)
        {
            return new BuildingElement
            {
                Type = BuildingElementType.Wall,
                Points = new List<Point3D> { new Point3D(0, 0, 0), new Point3D(5000, 0, 0) },
                Properties = new Dictionary<string, object>
                {
                    { "Thickness", thickness },
                    { "Height", height }
                }
            };
        }

        [Test]
        public void BuildingScript_DefaultSpec_EmitsSpre()
        {
            var pml = _gen.GenerateBuildingScript(new List<BuildingElement> { Wall() });
            // 默认规格存在，应发 SPRE
            Assert.IsTrue(pml.Contains("NEW STWALL"));
            Assert.IsTrue(pml.Contains("SPRE SPCOMPONENT 3 of SELEC 1 of SPECIFICATION"));
        }

        [Test]
        public void BuildingScript_EmptySpecOverride_SkipsSpre()
        {
            // 显式把墙规格覆盖为空 → 不应生成悬空 SPECIFICATION 行（否则整段 import 失败）
            var overrides = new Dictionary<BuildingElementType, string>
            {
                { BuildingElementType.Wall, "" }
            };
            var pml = _gen.GenerateBuildingScript(
                new List<BuildingElement> { Wall() }, specifications: overrides);

            Assert.IsTrue(pml.Contains("NEW STWALL"), "几何仍应生成");
            Assert.IsFalse(pml.Contains("SPRE"), "空规格时不应出现 SPRE 行");
            Assert.IsFalse(pml.Contains("SPECIFICATION"), "空规格时不应出现悬空 SPECIFICATION");
        }

        [Test]
        public void BuildingScript_OwnerProvided_UsesCeNotNewSite()
        {
            var pml = _gen.GenerateBuildingScript(
                new List<BuildingElement> { Wall() }, owner: "/Copy-of-CIVIL");
            Assert.IsTrue(pml.Contains("CE /Copy-of-CIVIL"));
            Assert.IsFalse(pml.Contains("NEW SITE"));
        }
    }
}
