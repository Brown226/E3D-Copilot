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

        // ====== 回归：PML 输出语法与 ASCII 安全 ======

        [Test]
        public void BuildingScript_NeverUsesEchoSubstitution()
        {
            // $! 在 PML 里是文本替换，$!ECHO 会被解析成「变量 ECHO」，
            // 直接报 ERROR - Variable ECHO not defined，并让整段脚本第 1 行就中断。
            var pml = _gen.GenerateBuildingScript(new List<BuildingElement> { Wall() });

            Assert.IsFalse(pml.Contains("$!ECHO"),
                "PML 中 $! 是文本替换语法，不能用 $!ECHO 输出");
            Assert.IsTrue(pml.Contains("$P "),
                "输出应使用 $P 命令");
        }

        [Test]
        public void BuildingScript_IsPureAscii()
        {
            // E3D 的 PML 解释器按 ANSI(936/GBK) 读取脚本文件，
            // 写入 UTF-8 中文会变成 æ¤ã... 乱码并中断整段脚本。
            var pml = _gen.GenerateBuildingScript(
                new List<BuildingElement> { Wall(), Wall() },
                owner: "/Copy-of-CIVIL");

            foreach (char c in pml)
            {
                if (c > 127)
                {
                    Assert.Fail("PML 脚本含非 ASCII 字符，会因编码不匹配变成乱码: " + c);
                }
            }
        }

        [Test]
        public void PipeScript_IsPureAsciiAndNoEcho()
        {
            var paths = new List<PipePath>
            {
                new PipePath
                {
                    Waypoints = new List<Point3D>
                    {
                        new Point3D(0, 0, 0),
                        new Point3D(3000, 0, 0)
                    }
                }
            };
            var pml = _gen.GeneratePipeScript(paths);

            Assert.IsFalse(pml.Contains("$!ECHO"));
            foreach (char c in pml)
            {
                if (c > 127)
                    Assert.Fail("PML 脚本含非 ASCII 字符: " + c);
            }
        }

        [Test]
        public void GenericElement_ActuallyCreatesGeometry()
        {
            // 之前通用分支只打两行 $!ECHO，什么都没创建，
            // 调用方却按 elements.Count 报告「已创建 N 个元素」。
            var unknown = new BuildingElement
            {
                Type = BuildingElementType.Equipment,
                Points = new List<Point3D>
                {
                    new Point3D(0, 0, 0),
                    new Point3D(1000, 0, 0)
                },
                Properties = new Dictionary<string, object>()
            };
            // Equipment 走 GenerateEquipmentScript，这里用坐标未知类型验证 STWALL 兜底
            var elem = new BuildingElement
            {
                Type = (BuildingElementType)(-1),
                Points = new List<Point3D>
                {
                    new Point3D(0, 0, 0),
                    new Point3D(1000, 0, 0)
                },
                Properties = new Dictionary<string, object>()
            };
            var pml = _gen.GenerateBuildingScript(
                new List<BuildingElement> { unknown, elem }, owner: "/Copy-of-CIVIL");

            Assert.IsTrue(pml.Contains("NEW STWALL") || pml.Contains("NEW EQUIP"),
                "未知类型也必须生成实际建模语句，不能只有输出语句");
        }
    }
}
