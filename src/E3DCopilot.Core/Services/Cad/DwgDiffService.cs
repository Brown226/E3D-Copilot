using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using E3DCopilot.Core.Models.Building;

namespace E3DCopilot.Core.Services.Cad
{
    /// <summary>
    /// 两张图纸的几何差异比对。
    ///
    /// 设计口径（对齐 review-compare 的「先定基准、按方向分类」）：
    /// - A 为基准（旧版），B 为目标（新版）；
    /// - 只做几何/图层的确定性匹配，不做"这根线是不是那堵墙改造的"这类语义判断；
    /// - 匹配不上就如实归入「新增 / 删除」，绝不猜。
    ///
    /// 匹配顺序（由严到宽）：
    /// 1. 精确键：类型 + 图层 + 归一化端点集合（端点排序以消除起止方向反转）
    /// 2. 邻近键：同类型同图层，中点距离 ≤ tol 且长度差 ≤ tol → 判为「修改」
    /// 3. 剩余：A 侧归「删除」，B 侧归「新增」
    ///
    /// 注意：A/B 两版图纸的原点和比例必须一致。若 B 版重新出图时原点平移，
    /// 结果会变成"全删全增"，这是数据问题，不是算法问题。
    /// </summary>
    public class DwgDiffService
    {
        /// <summary>几何匹配容差（图纸单位 mm）。同版本重出图建议 5，跨软件导出建议 10~50。</summary>
        public double Tolerance { get; set; } = 5;

        public DiffResult Compare(List<LineSegment> segmentsA, List<LineSegment> segmentsB)
        {
            if (segmentsA == null) segmentsA = new List<LineSegment>();
            if (segmentsB == null) segmentsB = new List<LineSegment>();

            // ── 1. 精确键匹配 ──
            var mapB = new Dictionary<string, List<LineSegment>>();
            foreach (var seg in segmentsB)
            {
                string key = SegmentKey(seg);
                if (!mapB.TryGetValue(key, out var bucket))
                {
                    bucket = new List<LineSegment>();
                    mapB[key] = bucket;
                }
                bucket.Add(seg);
            }

            var takenB = new HashSet<LineSegment>();
            var unmatchedA = new List<LineSegment>();
            int unchanged = 0;

            foreach (var seg in segmentsA)
            {
                string key = SegmentKey(seg);
                if (mapB.TryGetValue(key, out var bucket) && bucket.Count > 0)
                {
                    var hit = bucket[0];
                    bucket.RemoveAt(0);
                    takenB.Add(hit);
                    unchanged++;
                }
                else
                {
                    unmatchedA.Add(seg);
                }
            }

            var unmatchedB = segmentsB.Where(s => !takenB.Contains(s)).ToList();

            // ── 2. 邻近键匹配 → 「修改」 ──
            var modified = new List<ModifiedSegment>();
            var stillUnmatchedA = new List<LineSegment>();

            foreach (var a in unmatchedA)
            {
                var midA = Mid(a);
                double lenA = a.Length;
                LineSegment best = null;
                double bestDist = double.MaxValue;

                foreach (var b in unmatchedB)
                {
                    if (takenB.Contains(b)) continue;
                    if (!string.Equals(b.Layer, a.Layer, StringComparison.OrdinalIgnoreCase)) continue;

                    var midB = Mid(b);
                    double dist = Math.Sqrt((midB.Item1 - midA.Item1) * (midB.Item1 - midA.Item1)
                                          + (midB.Item2 - midA.Item2) * (midB.Item2 - midA.Item2));
                    if (dist > Tolerance) continue;
                    if (Math.Abs(b.Length - lenA) > Tolerance) continue;
                    if (dist < bestDist) { bestDist = dist; best = b; }
                }

                if (best != null)
                {
                    takenB.Add(best);
                    modified.Add(new ModifiedSegment
                    {
                        Layer = a.Layer,
                        Before = Describe(a),
                        After = Describe(best),
                        Shift = Math.Round(bestDist, 1),
                        LengthDelta = Math.Round(best.Length - lenA, 1)
                    });
                }
                else
                {
                    stillUnmatchedA.Add(a);
                }
            }

            // ── 3. 剩余归入新增 / 删除 ──
            var result = new DiffResult
            {
                UnchangedCount = unchanged,
                Modified = modified,
                Removed = stillUnmatchedA.Select(Describe).ToList(),
                Added = unmatchedB.Where(b => !takenB.Contains(b)).Select(Describe).ToList(),
                Tolerance = Tolerance
            };
            return result;
        }

        /// <summary>
        /// 线段精确键：图层 + 排序后的端点。
        ///
        /// 这里必须用「精确坐标」而不是按容差量化后的坐标。
        /// 原因：量化会把容差内的微小位移压成同一个键，导致一根平移了 2mm 的线
        /// 被判成「未变」，于是它永远进不了「修改」分支——而 2mm 位移恰恰是
        /// 图纸改版里最常见的改动。容差只用于第 2 步的邻近匹配。
        ///
        /// 浮点用 R（往返格式）而非 F1，避免 0.05 这类值被格式化误差吃掉。
        /// 端点排序保证 (a→b) 与 (b→a) 得到同一个键。
        /// </summary>
        private string SegmentKey(LineSegment seg)
        {
            var p1 = Tuple.Create(seg.Start.X, seg.Start.Y);
            var p2 = Tuple.Create(seg.End.X, seg.End.Y);
            // 字典序排序，消除起止方向差异
            if (p1.Item1 > p2.Item1 || (p1.Item1 == p2.Item1 && p1.Item2 > p2.Item2))
            {
                var tmp = p1; p1 = p2; p2 = tmp;
            }
            return string.Format("{0}|{1:R},{2:R}|{3:R},{4:R}",
                seg.Layer ?? "", p1.Item1, p1.Item2, p2.Item1, p2.Item2);
        }

        private static Tuple<double, double> Mid(LineSegment seg)
        {
            return Tuple.Create((seg.Start.X + seg.End.X) / 2, (seg.Start.Y + seg.End.Y) / 2);
        }

        private static SegmentInfo Describe(LineSegment seg)
        {
            return new SegmentInfo
            {
                Layer = seg.Layer,
                StartX = Math.Round(seg.Start.X, 1),
                StartY = Math.Round(seg.Start.Y, 1),
                EndX = Math.Round(seg.End.X, 1),
                EndY = Math.Round(seg.End.Y, 1),
                Length = Math.Round(seg.Length, 1)
            };
        }

        /// <summary>生成面向人类阅读的比对报告</summary>
        public static string FormatReport(DiffResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("比对结果：新增 {0} 处、删除 {1} 处、修改 {2} 处（未变 {3} 处）",
                r.Added.Count, r.Removed.Count, r.Modified.Count, r.UnchangedCount));
            sb.AppendLine(string.Format("几何匹配容差：{0}mm", r.Tolerance));

            if (r.Added.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("【新增】");
                foreach (var s in r.Added.Take(20))
                    sb.AppendLine("  " + s.ToString());
                if (r.Added.Count > 20) sb.AppendLine("  ... 另有 " + (r.Added.Count - 20) + " 处");
            }

            if (r.Removed.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("【删除】");
                foreach (var s in r.Removed.Take(20))
                    sb.AppendLine("  " + s.ToString());
                if (r.Removed.Count > 20) sb.AppendLine("  ... 另有 " + (r.Removed.Count - 20) + " 处");
            }

            if (r.Modified.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("【修改】");
                foreach (var m in r.Modified.Take(20))
                    sb.AppendLine(string.Format("  {0} 位移={1}mm 长度变化={2}mm",
                        m.Layer, m.Shift, m.LengthDelta));
                if (r.Modified.Count > 20) sb.AppendLine("  ... 另有 " + (r.Modified.Count - 20) + " 处");
            }

            return sb.ToString();
        }
    }

    public class DiffResult
    {
        public int UnchangedCount { get; set; }
        public double Tolerance { get; set; }
        public List<SegmentInfo> Added { get; set; } = new List<SegmentInfo>();
        public List<SegmentInfo> Removed { get; set; } = new List<SegmentInfo>();
        public List<ModifiedSegment> Modified { get; set; } = new List<ModifiedSegment>();
    }

    public class SegmentInfo
    {
        public string Layer { get; set; }
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public double Length { get; set; }

        public override string ToString()
        {
            return string.Format("{0} len={1} ({2},{3}) -> ({4},{5})",
                Layer, Length, StartX, StartY, EndX, EndY);
        }
    }

    public class ModifiedSegment
    {
        public string Layer { get; set; }
        public SegmentInfo Before { get; set; }
        public SegmentInfo After { get; set; }
        public double Shift { get; set; }
        public double LengthDelta { get; set; }
    }
}
