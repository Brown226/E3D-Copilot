using System;
using System.Collections.Generic;
using E3DCopilot.Core.Models.Building;
using E3DCopilot.Core.Models.Geometry;
using E3DCopilot.Core.Services.Cad;
using NUnit.Framework;

namespace E3DCopilot.Tests
{
    /// <summary>
    /// 图纸 A/B 版差异比对算法。
    ///
    /// 这些用例锁住四条最容易回归的规则：
    /// 1. 端点方向反转不算改动（图纸重出时线段起止经常翻转）
    /// 2. 容差内的位移必须判为「修改」，不能因为坐标量化被吞成「未变」
    /// 3. 超容差的位移判为「新增 + 删除」
    /// 4. 图层不同即不匹配（避免不同专业构件互相顶替）
    /// </summary>
    [TestFixture]
    public class DwgDiffServiceTests
    {
        private DwgDiffService _svc;

        [SetUp]
        public void SetUp()
        {
            _svc = new DwgDiffService { Tolerance = 5 };
        }

        private static LineSegment Seg(double x1, double y1, double x2, double y2, string layer = "土建")
        {
            return new LineSegment(new Point3D(x1, y1, 0), new Point3D(x2, y2, 0), layer);
        }

        private static List<LineSegment> BaseDrawing()
        {
            return new List<LineSegment> { Seg(0, 0, 1000, 0), Seg(0, 0, 0, 1000) };
        }

        [Test]
        public void Compare_Identical_AllUnchanged()
        {
            var r = _svc.Compare(BaseDrawing(), BaseDrawing());

            Assert.AreEqual(0, r.Added.Count);
            Assert.AreEqual(0, r.Removed.Count);
            Assert.AreEqual(0, r.Modified.Count);
            Assert.AreEqual(2, r.UnchangedCount);
        }

        [Test]
        public void Compare_ReversedEndpoints_StillUnchanged()
        {
            var b = new List<LineSegment> { Seg(1000, 0, 0, 0), Seg(0, 1000, 0, 0) };

            var r = _svc.Compare(BaseDrawing(), b);

            Assert.AreEqual(0, r.Added.Count, "端点反转不应算新增");
            Assert.AreEqual(0, r.Removed.Count, "端点反转不应算删除");
            Assert.AreEqual(2, r.UnchangedCount);
        }

        [Test]
        public void Compare_ExtraSegmentInB_ReportedAsAdded()
        {
            var b = BaseDrawing();
            b.Add(Seg(5000, 5000, 6000, 5000));

            var r = _svc.Compare(BaseDrawing(), b);

            Assert.AreEqual(1, r.Added.Count);
            Assert.AreEqual(0, r.Removed.Count);
            Assert.AreEqual(0, r.Modified.Count);
        }

        [Test]
        public void Compare_MissingSegmentInB_ReportedAsRemoved()
        {
            var b = BaseDrawing();
            b.Add(Seg(5000, 5000, 6000, 5000));

            var r = _svc.Compare(b, BaseDrawing());

            Assert.AreEqual(1, r.Removed.Count);
            Assert.AreEqual(0, r.Added.Count);
            Assert.AreEqual(0, r.Modified.Count);
        }

        /// <summary>
        /// 这条是回归测试：早期实现按容差量化坐标做精确键，
        /// 2mm 位移被量化到同一格，结果判成「未变」，整条「修改」分支形同虚设。
        /// </summary>
        [Test]
        public void Compare_ShiftWithinTolerance_ReportedAsModified()
        {
            var b = new List<LineSegment> { Seg(2, 2, 1002, 2), Seg(0, 0, 0, 1000) };

            var r = _svc.Compare(BaseDrawing(), b);

            Assert.AreEqual(1, r.Modified.Count, "容差内的位移应判为修改，不能被量化吞掉");
            Assert.AreEqual(0, r.Added.Count);
            Assert.AreEqual(0, r.Removed.Count);
            // 位移 (2,2) → √(2²+2²) ≈ 2.8mm
            Assert.AreEqual(2.8, r.Modified[0].Shift, 0.05);
        }

        [Test]
        public void Compare_ShiftBeyondTolerance_ReportedAsAddAndRemove()
        {
            var b = new List<LineSegment> { Seg(50, 50, 1050, 50), Seg(0, 0, 0, 1000) };

            var r = _svc.Compare(BaseDrawing(), b);

            Assert.AreEqual(1, r.Added.Count);
            Assert.AreEqual(1, r.Removed.Count);
            Assert.AreEqual(0, r.Modified.Count);
        }

        [Test]
        public void Compare_SamePositionDifferentLayer_NotMatched()
        {
            var b = new List<LineSegment> { Seg(0, 0, 1000, 0, "C1 柱墙剖线"), Seg(0, 0, 0, 1000) };

            var r = _svc.Compare(BaseDrawing(), b);

            Assert.AreEqual(1, r.Added.Count);
            Assert.AreEqual(1, r.Removed.Count);
        }

        [Test]
        public void Compare_NullInput_DoesNotThrow()
        {
            var r = _svc.Compare(null, null);

            Assert.AreEqual(0, r.Added.Count);
            Assert.AreEqual(0, r.Removed.Count);
            Assert.AreEqual(0, r.Modified.Count);
        }

        [Test]
        public void FormatReport_ContainsAllSections()
        {
            var b = BaseDrawing();
            b.Add(Seg(5000, 5000, 6000, 5000));

            string report = DwgDiffService.FormatReport(_svc.Compare(BaseDrawing(), b));

            Assert.IsTrue(report.Contains("比对结果"));
            Assert.IsTrue(report.Contains("【新增】"));
        }
    }
}
