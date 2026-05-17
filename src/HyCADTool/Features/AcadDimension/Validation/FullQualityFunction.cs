using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Features.AcadDimension.Domain.Boundary;
using HyCADTool.Features.AcadDimension.Domain.Results;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Features.AcadDimension.Validation
{
    /// <summary>
    /// Phase 6 完整版 Quality 函数：在 Coverage / DirectionalCoverage 之上补齐 4 条规则。
    /// 仍守 Q 仅校核不选择（06 §5.2 / 07 §11 第 5 条）；Issues 暴露具体诊断给命令行。
    ///
    /// 5 条规则（每条独立 0~1 评分；Overall = 等权均值）：
    ///   1. <b>Coverage</b>          —— 顶点是否被某条标注端点"涉及"（漏标点诊断）
    ///   2. <b>NoOverlap</b>         —— 同方向相邻 dimLine + 测量区间是否冲突
    ///   3. <b>WithinBounds</b>      —— dimLinePoint 是否在 polyline bbox 拓展范围内
    ///   4. <b>Consistency</b>       —— 同 Source 的偏移距离 stddev 是否在 1mm 内
    ///   5. <b>DirectionalCoverage</b>—— 外四方向是否都有标注（沿用 M1 版）
    /// </summary>
    public sealed class FullQualityFunction : IQualityFunction
    {
        private const double VertexAxisTolerance = 1.0;
        private const double OverlapDimLineGap = 100.0;
        private const double OutOfBoundsExtraSlack = 200.0;
        private const double ConsistencyStdDevThreshold = 1.0;

        public QualityScoreCard Evaluate(NewDdsResult result, BoundaryFeatures features)
        {
            var card = new QualityScoreCard();
            var dims = (result?.Dimensions ?? new List<DerivedDimension>()).ToList();

            if (dims.Count == 0)
            {
                card.Issues.Add("无标注产出（派生链路返回空）");
                return card;
            }

            EvaluateCoverage(dims, features, card);
            EvaluateNoOverlap(dims, card);
            EvaluateWithinBounds(dims, features, card);
            EvaluateConsistency(dims, features, card);
            EvaluateDirectionalCoverage(dims, card);

            card.Overall = (card.Coverage
                + card.NoOverlap
                + card.WithinBounds
                + card.Consistency
                + card.DirectionalCoverage) / 5.0;

            return card;
        }

        // === 1. Coverage =============================================================
        // 工程语义："顶点是否被某条分段尺寸标注到位"——
        // 把 dim 看作沿测量轴的投影：水平 dim 投影到 X 轴上，竖直 dim 投影到 Y 轴上。
        // 顶点 V 覆盖 iff 存在至少一条分段 dim（**排除 Total 类**——总尺寸覆盖整个 bbox 会掩盖漏标）：
        //   水平 dim：|V.X − ExtensionLine 任一端点的 X| < 1mm
        //   竖直 dim：|V.Y − ExtensionLine 任一端点的 Y| < 1mm
        // 即顶点的主轴坐标恰好是某条分段尺寸的端点 → 工程上认为该顶点位置已被标注链记录。
        // L/T/不对称形等台阶顶点的 X 或 Y 都会落在外四方向链的某段端点上，故 Cov 应接近 1。
        private static void EvaluateCoverage(List<DerivedDimension> dims, BoundaryFeatures features, QualityScoreCard card)
        {
            var verts = features?.Vertices;
            if (verts == null || verts.Count == 0)
            {
                card.Coverage = 1.0;
                return;
            }

            var sectionDims = dims.Where(d => !IsTotal(d.Source)).ToList();
            if (sectionDims.Count == 0)
            {
                card.Coverage = 0.0;
                card.Issues.Add("无任何分段尺寸");
                return;
            }

            // 预收集水平/竖直分段 dim 端点轴坐标，避免内层 O(N*D) 重复访问端点
            var horizontalAxisCoords = new List<double>();
            var verticalAxisCoords = new List<double>();
            foreach (var d in sectionDims)
            {
                bool isHorizontal = Math.Abs(d.Rotation) < 1e-3;
                if (isHorizontal)
                {
                    horizontalAxisCoords.Add(d.ExtensionLine1Point.X);
                    horizontalAxisCoords.Add(d.ExtensionLine2Point.X);
                }
                else
                {
                    verticalAxisCoords.Add(d.ExtensionLine1Point.Y);
                    verticalAxisCoords.Add(d.ExtensionLine2Point.Y);
                }
            }

            int total = verts.Count;
            int covered = 0;
            int leakLogged = 0;
            for (int i = 0; i < total; i++)
            {
                var v = verts[i];
                bool xCovered = AnyWithinTolerance(horizontalAxisCoords, v.X, VertexAxisTolerance);
                bool yCovered = AnyWithinTolerance(verticalAxisCoords, v.Y, VertexAxisTolerance);
                if (xCovered || yCovered) covered++;
                else if (leakLogged < 3)
                {
                    card.Issues.Add($"漏标点 #{i} ({v.X:F0},{v.Y:F0})");
                    leakLogged++;
                }
            }
            if (leakLogged > 0 && total - covered > leakLogged)
                card.Issues.Add($"…另有 {total - covered - leakLogged} 个漏标点未列出");
            card.Coverage = (double)covered / total;
        }

        private static bool IsTotal(DimensionSource s)
            => s == DimensionSource.OutsideTotalLeft
            || s == DimensionSource.OutsideTotalRight
            || s == DimensionSource.OutsideTotalUp
            || s == DimensionSource.OutsideTotalDown;

        private static bool AnyWithinTolerance(List<double> sortedOrAny, double target, double tol)
        {
            for (int i = 0; i < sortedOrAny.Count; i++)
            {
                if (Math.Abs(sortedOrAny[i] - target) < tol) return true;
            }
            return false;
        }

        // === 2. NoOverlap ============================================================
        // 同 Rotation 的 dim 按 dimLine key 排序，相邻两条若 dimLine 距离 < OverlapDimLineGap
        // 且测量区间相交 → 视为视觉冲突。Score = 1 - overlap_count / dim_count。
        private static void EvaluateNoOverlap(List<DerivedDimension> dims, QualityScoreCard card)
        {
            int overlap = 0;
            foreach (var rotGroup in dims.GroupBy(d => Math.Round(d.Rotation, 3)))
            {
                bool isHorizontal = Math.Abs(rotGroup.Key) < 1e-3;
                var sorted = isHorizontal
                    ? rotGroup.OrderBy(d => d.DimensionLinePoint.Y).ToList()
                    : rotGroup.OrderBy(d => d.DimensionLinePoint.X).ToList();
                for (int i = 0; i < sorted.Count - 1; i++)
                {
                    var a = sorted[i];
                    var b = sorted[i + 1];
                    double aKey = isHorizontal ? a.DimensionLinePoint.Y : a.DimensionLinePoint.X;
                    double bKey = isHorizontal ? b.DimensionLinePoint.Y : b.DimensionLinePoint.X;
                    if (Math.Abs(aKey - bKey) >= OverlapDimLineGap) continue;
                    if (SegmentsIntersect(a, b, isHorizontal))
                    {
                        overlap++;
                        if (card.Issues.Count(s => s.StartsWith("标注重叠")) < 3)
                            card.Issues.Add($"标注重叠 ({(isHorizontal ? "水平" : "竖直")}) Rotation={rotGroup.Key:F2}");
                    }
                }
            }
            card.NoOverlap = dims.Count == 0 ? 1.0 : Math.Max(0.0, 1.0 - (double)overlap / dims.Count);
        }

        private static bool SegmentsIntersect(DerivedDimension a, DerivedDimension b, bool isHorizontal)
        {
            var aSeg = MeasureRange(a, isHorizontal);
            var bSeg = MeasureRange(b, isHorizontal);
            return aSeg.Lo < bSeg.Hi && bSeg.Lo < aSeg.Hi;
        }

        // === 3. WithinBounds =========================================================
        // 所有 dimLinePoint 应在 (bbox 外扩 max(配置距离) + slack) 范围内；超界 → 异常。
        private static void EvaluateWithinBounds(List<DerivedDimension> dims, BoundaryFeatures features, QualityScoreCard card)
        {
            if (features == null) { card.WithinBounds = 1.0; return; }
            var bb = features.Bounds;
            double slack = OutOfBoundsExtraSlack;
            double xMin = bb.MinPoint.X - 5_000_000.0;
            double xMax = bb.MaxPoint.X + 5_000_000.0;
            double yMin = bb.MinPoint.Y - 5_000_000.0;
            double yMax = bb.MaxPoint.Y + 5_000_000.0;
            double bboxDiag = Math.Sqrt(
                Math.Pow(bb.MaxPoint.X - bb.MinPoint.X, 2) +
                Math.Pow(bb.MaxPoint.Y - bb.MinPoint.Y, 2));
            double allowed = bboxDiag + slack;

            int outCount = 0;
            foreach (var d in dims)
            {
                if (d.DimensionLinePoint.X < xMin || d.DimensionLinePoint.X > xMax ||
                    d.DimensionLinePoint.Y < yMin || d.DimensionLinePoint.Y > yMax)
                {
                    outCount++;
                    continue;
                }
                double dx = Math.Max(0, Math.Max(bb.MinPoint.X - d.DimensionLinePoint.X, d.DimensionLinePoint.X - bb.MaxPoint.X));
                double dy = Math.Max(0, Math.Max(bb.MinPoint.Y - d.DimensionLinePoint.Y, d.DimensionLinePoint.Y - bb.MaxPoint.Y));
                if (Math.Sqrt(dx * dx + dy * dy) > allowed)
                    outCount++;
            }
            if (outCount > 0)
                card.Issues.Add($"标注超界 {outCount} 条（dimLine 离 bbox > {allowed:F0}mm）");
            card.WithinBounds = dims.Count == 0 ? 1.0 : 1.0 - (double)outCount / dims.Count;
        }

        // === 4. Consistency ==========================================================
        // 同 Source 的 dim「偏移距离」（dimLine 离 polyline bbox 边）应一致；stddev > 1mm → 不一致。
        private static void EvaluateConsistency(List<DerivedDimension> dims, BoundaryFeatures features, QualityScoreCard card)
        {
            if (features == null) { card.Consistency = 1.0; return; }
            var bb = features.Bounds;

            int totalGroups = 0;
            int consistentGroups = 0;
            foreach (var grp in dims.GroupBy(d => d.Source))
            {
                var offsets = grp.Select(d => OffsetFromBoundary(d, bb)).ToList();
                if (offsets.Count <= 1) { totalGroups++; consistentGroups++; continue; }
                double mean = offsets.Average();
                double sum2 = offsets.Sum(v => (v - mean) * (v - mean));
                double sd = Math.Sqrt(sum2 / offsets.Count);
                totalGroups++;
                if (sd <= ConsistencyStdDevThreshold) consistentGroups++;
                else card.Issues.Add($"Source={grp.Key} 偏移不一致 stddev={sd:F1}mm");
            }
            card.Consistency = totalGroups == 0 ? 1.0 : (double)consistentGroups / totalGroups;
        }

        private static double OffsetFromBoundary(DerivedDimension d, BoundingBox bb)
        {
            switch (d.Source)
            {
                case DimensionSource.OutsideLeft:
                case DimensionSource.OutsideTotalLeft:
                    return Math.Abs(bb.MinPoint.X - d.DimensionLinePoint.X);
                case DimensionSource.OutsideRight:
                case DimensionSource.OutsideTotalRight:
                    return Math.Abs(d.DimensionLinePoint.X - bb.MaxPoint.X);
                case DimensionSource.OutsideDown:
                case DimensionSource.OutsideTotalDown:
                    return Math.Abs(bb.MinPoint.Y - d.DimensionLinePoint.Y);
                case DimensionSource.OutsideUp:
                case DimensionSource.OutsideTotalUp:
                    return Math.Abs(d.DimensionLinePoint.Y - bb.MaxPoint.Y);
            }
            return 0.0;
        }

        // === 5. DirectionalCoverage ==================================================
        private static void EvaluateDirectionalCoverage(List<DerivedDimension> dims, QualityScoreCard card)
        {
            int hasLeft = dims.Any(d => d.Source == DimensionSource.OutsideLeft) ? 1 : 0;
            int hasRight = dims.Any(d => d.Source == DimensionSource.OutsideRight) ? 1 : 0;
            int hasUp = dims.Any(d => d.Source == DimensionSource.OutsideUp) ? 1 : 0;
            int hasDown = dims.Any(d => d.Source == DimensionSource.OutsideDown) ? 1 : 0;
            card.DirectionalCoverage = (hasLeft + hasRight + hasUp + hasDown) / 4.0;
            if (hasLeft == 0) card.Issues.Add("外左方向无标注");
            if (hasRight == 0) card.Issues.Add("外右方向无标注");
            if (hasUp == 0) card.Issues.Add("外上方向无标注");
            if (hasDown == 0) card.Issues.Add("外下方向无标注");
        }

        // === 工具 =====================================================================
        private static (double Lo, double Hi) MeasureRange(DerivedDimension d, bool isHorizontal)
        {
            double v1 = isHorizontal ? d.ExtensionLine1Point.X : d.ExtensionLine1Point.Y;
            double v2 = isHorizontal ? d.ExtensionLine2Point.X : d.ExtensionLine2Point.Y;
            return v1 <= v2 ? (v1, v2) : (v2, v1);
        }
    }
}
