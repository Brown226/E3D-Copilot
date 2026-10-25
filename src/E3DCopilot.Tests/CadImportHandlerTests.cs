using System.Collections.Generic;
using System.Threading.Tasks;
using E3DCopilot.Core.Models.Building;
using E3DCopilot.Core.Models.Geometry;
using E3DCopilot.Core.Services.Cad;
using E3DCopilot.Core.Tools.Handlers;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    /// <summary>
    /// CAD 导入链路的两个建模质量缺陷修复（交接文档 §6 缺陷 1/2）：
    /// - 缺陷 2：DWG 文件路径此前不做共线合并——sidecar 把 POLYLINE 逐段拆开，
    ///   墙轮廓变成碎段、每条独立建墙，拐角缺口。修复后与坐标串路径同规则合并。
    /// - 缺陷 1：柱截面此前恒 300×300（Width/Depth 从不赋值）。
    ///   修复后 Width=线段长度、Depth=墙厚、定位取线段中点。
    /// </summary>
    [TestFixture]
    public class CadImportHandlerTests
    {
        private class FakeCadParser : ICadParser
        {
            private readonly List<LineSegment> _segments;
            public FakeCadParser(List<LineSegment> segments) { _segments = segments; }
            public CadParseResult ParseFile(string filePath)
            {
                return new CadParseResult { Success = true, WallSegments = _segments };
            }
        }

        private static LineSegment Seg(double x1, double y1, double x2, double y2, string layer = "土建")
        {
            return new LineSegment(new Point3D(x1, y1, 0), new Point3D(x2, y2, 0), layer);
        }

        // ====== 缺陷 2：DWG 文件路径必须做共线合并 ======

        [Test]
        public async Task Parse_FilePath_MergesCollinearFragments()
        {
            // 一面长墙被 POLYLINE 拆成 3 段共线连续短线，另有 1 段独立垂直墙
            var parser = new FakeCadParser(new List<LineSegment>
            {
                Seg(0, 0, 1000, 0),
                Seg(1000, 0, 2000, 0),
                Seg(2000, 0, 3000, 0),
                Seg(0, 0, 0, 800),
            });
            var handler = new CadImportHandler(parser: parser);

            var result = await handler.ExecuteAsync(
                "{\"action\":\"parse\",\"file_path\":\"D:\\\\fake.dwg\"}");

            Assert.IsTrue(result.Success, result.Error);
            StringAssert.Contains("原始线段: 2 条", result.Text,
                "3 段共线碎段应合并为 1 条 + 1 条垂直墙 = 2 条（修复前返回 4 条碎段）");
        }

        [Test]
        public async Task Parse_FilePath_KeepsNonCollinearSegments()
        {
            // 共线但不连续（中间断开 500mm）的线段不能被错误合并
            var parser = new FakeCadParser(new List<LineSegment>
            {
                Seg(0, 0, 1000, 0),
                Seg(1500, 0, 3000, 0),
                Seg(0, 0, 0, 600),
            });
            var handler = new CadImportHandler(parser: parser);

            var result = await handler.ExecuteAsync(
                "{\"action\":\"parse\",\"file_path\":\"D:\\\\fake.dwg\"}");

            Assert.IsTrue(result.Success, result.Error);
            StringAssert.Contains("原始线段: 3 条", result.Text,
                "不共线/不连续的线段必须保留原样");
        }

        [Test]
        public async Task Parse_FilePath_ParallelOffsetWalls_NotMerged()
        {
            // 旧合并函数只查方向平行不查同一直线——平行的两堵墙（建筑里遍地都是）
            // 会被熔成一条跨越两者的斜线。修复后必须保持 2 段。
            var parser = new FakeCadParser(new List<LineSegment>
            {
                Seg(0, 0, 5000, 0),
                Seg(0, 3000, 5000, 3000),
            });
            var handler = new CadImportHandler(parser: parser);

            var result = await handler.ExecuteAsync(
                "{\"action\":\"parse\",\"file_path\":\"D:\\\\fake.dwg\"}");

            Assert.IsTrue(result.Success, result.Error);
            StringAssert.Contains("原始线段: 2 条", result.Text,
                "平行但不同直线的两堵墙绝不能合并");
        }

        [Test]
        public async Task Parse_PathsString_ContiguousPiecesStillMerge()
        {
            // 回归：坐标串路径的既有行为——同一面墙的连续碎段仍要合并
            var handler = new CadImportHandler(parser: new FakeCadParser(new List<LineSegment>()));

            var result = await handler.ExecuteAsync(
                "{\"action\":\"parse\",\"paths_string\":\"[(0,0,0),(1000,0,0)],[(1000,0,0),(2000,0,0)]\"}");

            Assert.IsTrue(result.Success, result.Error);
            StringAssert.Contains("原始线段: 1 条", result.Text,
                "坐标串中首尾相接的共线碎段仍应合并为 1 段");
        }

        // ====== 缺陷 1：柱截面从图纸线段推导 ======

        [Test]
        public async Task Import_ColumnLayer_DerivesSectionFromSegmentLength()
        {
            // 「C1 柱墙剖线」含"柱"→判定 Column；1500mm 水平单线
            var parser = new FakeCadParser(new List<LineSegment>
            {
                Seg(10000, 20000, 11500, 20000, "C1 柱墙剖线"),
            });
            var handler = new CadImportHandler(parser: parser);

            var result = await handler.ExecuteAsync(
                "{\"action\":\"import\",\"file_path\":\"D:\\\\fake.dwg\",\"auto_execute\":false," +
                "\"wall_height\":3000,\"wall_thickness\":200}");

            Assert.IsTrue(result.Success, result.Error);
            // DESP 宽×深×高 = 线段长度 × 墙厚 × 墙高（修复前恒 300.00 300.00 3000.00）
            StringAssert.Contains("DESP 1500.00 200.00 3000.00", result.Text,
                "柱截面应从线段长度推导：1500(线长)×200(墙厚)×3000(墙高)");
            // 定位取线段中点 (10750, 20000)，不再取起点 (10000, 20000)
            StringAssert.Contains("POS E 10750.00 N 20000.00 U 0.00", result.Text,
                "柱定位应取线段中点，而非线段起点");
        }

        [Test]
        public async Task Import_ColumnLayer_VariousLengths_ProduceVariousSections()
        {
            // 演示图纸柱线段长度 996~2800——不同长度必须得到不同截面，不再千篇一律
            var parser = new FakeCadParser(new List<LineSegment>
            {
                Seg(0, 0, 1000, 0, "柱"),
                Seg(5000, 0, 6600, 0, "柱"),
                Seg(10000, 0, 12800, 0, "柱"),
            });
            var handler = new CadImportHandler(parser: parser);

            var result = await handler.ExecuteAsync(
                "{\"action\":\"import\",\"file_path\":\"D:\\\\fake.dwg\",\"auto_execute\":false," +
                "\"wall_height\":3000,\"wall_thickness\":200}");

            Assert.IsTrue(result.Success, result.Error);
            StringAssert.Contains("DESP 1000.00 200.00 3000.00", result.Text);
            StringAssert.Contains("DESP 1600.00 200.00 3000.00", result.Text);
            StringAssert.Contains("DESP 2800.00 200.00 3000.00", result.Text);
        }

        [Test]
        public async Task Import_WallLayer_UnaffectedByColumnDerivation()
        {
            // 墙路径不受柱推导影响：仍按 POSS/POSE 建墙，DESP 厚×高
            var parser = new FakeCadParser(new List<LineSegment>
            {
                Seg(0, 0, 5000, 0, "土建"),
            });
            var handler = new CadImportHandler(parser: parser);

            var result = await handler.ExecuteAsync(
                "{\"action\":\"import\",\"file_path\":\"D:\\\\fake.dwg\",\"auto_execute\":false," +
                "\"wall_height\":3000,\"wall_thickness\":200}");

            Assert.IsTrue(result.Success, result.Error);
            StringAssert.Contains("NEW STWALL", result.Text);
            StringAssert.Contains("DESP 200.00 3000.00", result.Text);
            StringAssert.Contains("POSS E 0.00 N 0.00 U 0.00", result.Text);
            StringAssert.Contains("POSE E 5000.00 N 0.00 U 0.00", result.Text);
        }
    }
}
