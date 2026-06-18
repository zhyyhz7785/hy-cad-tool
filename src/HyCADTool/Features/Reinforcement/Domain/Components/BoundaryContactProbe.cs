using HyCAD.Geometry;
using HyCAD.Geometry.Algorithms;
using HyCADTool.Features.Reinforcement.Domain;
using System;
using System.Collections.Generic;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>
    /// N26/N27 土/气边界接触探测：N13 外轮廓边 + ReinRegion 内部孔洞 + 本地底面外探。
    /// </summary>
    public static class BoundaryContactProbe
    {
        private const double EdgeToleranceMm = 1.0;
        private const double AnchorYToleranceMm = 80.0;
        private const double ProbeOffsetMm = 1.0;
        private const double SpanInsetMm = 1.0;
        private const double IntersectionEpsilon = 1e-6;
        private const double HitDedupeYToleranceMm = 0.5;
        private const double MinSegmentLengthMm = 200.0;
        private const double BandProbeStepMm = 5.0;

        public static bool IsHorizontalStrip(double widthMm, double heightMm)
            => widthMm >= 2.0 * heightMm && widthMm >= MinSegmentLengthMm;

        public static bool IsVerticalStrip(double widthMm, double heightMm, ComponentParameters parameters)
            => heightMm >= 2.0 * widthMm
               && widthMm <= parameters.WallMaxThicknessMm
               && heightMm >= MinSegmentLengthMm;

        public static double GetFoundationGraphicMinY(GroupBoundaryProfile profile)
        {
            if (profile == null)
                return double.NaN;

            if (profile.FoundationGraphicIndex >= 0
                && profile.Regions != null
                && profile.FoundationGraphicIndex < profile.Regions.Count)
            {
                var foundation = profile.Regions[profile.FoundationGraphicIndex];
                if (foundation != null && !double.IsNaN(foundation.GraphicMinY))
                    return foundation.GraphicMinY;
            }

            return profile.GroupMinY;
        }

        /// <summary>底边 [minX,maxX]×yBot 与土接触（仅 N13 Soil 边）。</summary>
        public static bool BottomContactsSoil(
            double minX,
            double maxX,
            double yBot,
            IReadOnlyList<ReinRegion> regions,
            GroupBoundaryProfile profile)
        {
            if (SpanOverlapsN13SoilAtY(minX, maxX, yBot, profile))
                return true;

            if (maxX - minX <= EdgeToleranceMm)
                return BottomContactsSoilAt((minX + maxX) / 2.0, yBot, regions, profile);

            foreach (double sampleX in GetSpanSampleXs(minX, maxX))
            {
                if (!BottomContactsSoilAt(sampleX, yBot, regions, profile))
                    return false;
            }

            return true;
        }

        private static bool SpanOverlapsN13SoilAtY(
            double minX,
            double maxX,
            double yBot,
            GroupBoundaryProfile profile)
        {
            var edges = GetAllRegionEdges(profile);
            if (edges == null)
                return false;

            foreach (var ce in edges)
            {
                if (ce?.Edge == null || ce.Role != BoundaryEdgeRole.Soil)
                    continue;

                if (HorizontalEdgeOverlapsSpan(minX, maxX, ce.Edge, yBot))
                    return true;
            }

            return false;
        }

        private static bool HorizontalEdgeOverlapsSpan(
            double minX,
            double maxX,
            Line2D edge,
            double yBot)
        {
            double edgeY = (edge.StartPoint.Y + edge.EndPoint.Y) / 2.0;
            if (Math.Abs(edgeY - yBot) > AnchorYToleranceMm)
                return false;

            double ex0 = Math.Min(edge.StartPoint.X, edge.EndPoint.X);
            double ex1 = Math.Max(edge.StartPoint.X, edge.EndPoint.X);
            return ex0 < maxX + EdgeToleranceMm && ex1 > minX - EdgeToleranceMm;
        }

        private static bool BottomContactsSoilAt(
            double x,
            double yBot,
            IReadOnlyList<ReinRegion> regions,
            GroupBoundaryProfile profile)
        {
            var edges = GetAllRegionEdges(profile);
            if (edges == null || edges.Count == 0)
                return false;

            foreach (var ce in edges)
            {
                if (ce?.Edge == null || ce.Role != BoundaryEdgeRole.Soil)
                    continue;

                if (HorizontalEdgeOverlapsSpanAtX(ce.Edge, x, yBot, yBot))
                    return true;
            }

            var hits = GetVerticalBoundaryHits(edges, x);
            foreach (var hit in hits)
            {
                if (hit.Role == BoundaryEdgeRole.Soil
                    && Math.Abs(hit.Point.Y - yBot) <= AnchorYToleranceMm)
                    return true;
            }

            return false;
        }

        public static bool ContactsAirAt(
            double midX,
            double y,
            bool isAbove,
            IReadOnlyList<ReinRegion> regions,
            GroupBoundaryProfile profile)
        {
            double probeY = isAbove ? y + ProbeOffsetMm : y - ProbeOffsetMm;
            var probe = new Point2D(midX, probeY);

            if (IsPointInAnyHole(probe, regions))
                return true;

            if (FaceOpensToHole(midX, midX, y, isAbove, regions))
                return true;

            if (IsConcrete(probe, regions))
                return false;

            if (!IsInsideAnyOuter(probe, regions))
                return true;

            var edges = GetAllBoundaryEdges(profile);
            if (edges == null || edges.Count == 0)
                return true;

            if (TryGetBoundaryRoleAt(edges, midX, y, isAbove, out var role))
                return role == BoundaryEdgeRole.Air;

            return true;
        }

        public static bool ContactsAirAlongSpan(
            double minX,
            double maxX,
            double y,
            bool isAbove,
            IReadOnlyList<ReinRegion> regions,
            GroupBoundaryProfile profile)
        {
            if (maxX - minX <= EdgeToleranceMm)
                return ContactsAirAt((minX + maxX) / 2.0, y, isAbove, regions, profile);

            foreach (double sampleX in GetSpanSampleXs(minX, maxX))
            {
                if (!ContactsAirAt(sampleX, y, isAbove, regions, profile))
                    return false;
            }

            return true;
        }

        /// <summary>N29：水平面沿 X 存在有效空气接触段（不要求全长每点皆气）。</summary>
        public static bool HasAirContactAlongSpan(
            double minX,
            double maxX,
            double y,
            bool isAbove,
            IReadOnlyList<ReinRegion> regions,
            GroupBoundaryProfile profile)
        {
            double span = maxX - minX;
            if (span <= EdgeToleranceMm)
                return ContactsAirAt((minX + maxX) / 2.0, y, isAbove, regions, profile);

            double minLen = Math.Min(MinSegmentLengthMm, span * 0.15);
            var intervals = CollectAirContactXIntervals(minX, maxX, y, isAbove, regions, profile);
            return HasEffectiveAirInterval(intervals, minX, maxX, minLen);
        }

        /// <summary>N29：楼板——上下面气接触段 X 重叠；底面仅用探针(不含 N13 Air 边误匹配)。</summary>
        public static bool IsSlabSandwichCandidate(
            double minX,
            double maxX,
            double minY,
            double maxY,
            IReadOnlyList<ReinRegion> regions,
            GroupBoundaryProfile profile)
        {
            double span = maxX - minX;
            if (span <= EdgeToleranceMm)
            {
                double mid = (minX + maxX) / 2.0;
                return ContactsAirAt(mid, maxY, isAbove: true, regions, profile)
                    && ContactsAirAt(mid, minY, isAbove: false, regions, profile);
            }

            double minLen = Math.Min(MinSegmentLengthMm, span * 0.15);
            var topIntervals = CollectAirContactXIntervals(minX, maxX, maxY, isAbove: true, regions, profile);
            var bottomIntervals = CollectProbeOnlyAirContactXIntervals(
                minX, maxX, minY, isAbove: false, regions, profile);
            return HasOverlappingEffectiveAirInterval(topIntervals, bottomIntervals, minX, maxX, minLen);
        }

        /// <summary>
        /// N29：单元是否处于薄气-气混凝土带内（凹角把楼板切成上下两片时，单片顶/底不全气，但整带顶底气段仍重叠）。
        /// </summary>
        public static bool IsThinAirToAirBand(
            double minX,
            double maxX,
            double cellMinY,
            double cellMaxY,
            double maxThicknessMm,
            IReadOnlyList<ReinRegion> regions,
            GroupBoundaryProfile profile)
        {
            double startY = (cellMinY + cellMaxY) / 2.0;
            var validBands = new List<(double YLo, double YHi)>();

            foreach (double sampleX in GetSpanSampleXs(minX, maxX))
            {
                if (!IsConcrete(new Point2D(sampleX, startY), regions))
                    continue;

                if (!TryMeasureThinBandAtColumn(
                        sampleX, startY, maxThicknessMm, regions, out double yLo, out double yHi))
                    continue;

                if (yHi - yLo > maxThicknessMm + EdgeToleranceMm)
                    continue;

                validBands.Add((yLo, yHi));
            }

            if (validBands.Count == 0)
                return false;

            double refYLo = MedianBandY(validBands, hi: false);
            double refYHi = MedianBandY(validBands, hi: true);
            if (refYHi - refYLo > maxThicknessMm + EdgeToleranceMm)
                return false;

            double span = maxX - minX;
            if (span <= EdgeToleranceMm)
            {
                double midX = (minX + maxX) / 2.0;
                return ContactsAirAt(midX, refYHi, isAbove: true, regions, profile)
                    && ContactsAirAt(midX, refYLo, isAbove: false, regions, profile);
            }

            double minLen = Math.Min(MinSegmentLengthMm, span * 0.15);
            var topIntervals = CollectAirContactXIntervals(
                minX, maxX, refYHi, isAbove: true, regions, profile);
            var bottomIntervals = CollectProbeOnlyAirContactXIntervals(
                minX, maxX, refYLo, isAbove: false, regions, profile);
            return HasOverlappingEffectiveAirInterval(topIntervals, bottomIntervals, minX, maxX, minLen);
        }

        /// <summary>从列内起点双向扩张薄带：遇非混凝土即停，带高不超过 maxThicknessMm。</summary>
        private static bool TryMeasureThinBandAtColumn(
            double x,
            double startY,
            double maxThicknessMm,
            IReadOnlyList<ReinRegion> regions,
            out double yLo,
            out double yHi)
        {
            yLo = startY;
            yHi = startY;

            if (!IsConcrete(new Point2D(x, startY), regions))
                return false;

            while (true)
            {
                double nextY = yHi + BandProbeStepMm;
                if (!IsConcrete(new Point2D(x, nextY), regions))
                    break;
                if (nextY - yLo > maxThicknessMm + EdgeToleranceMm)
                    break;
                yHi = nextY;
            }

            while (true)
            {
                double nextY = yLo - BandProbeStepMm;
                if (!IsConcrete(new Point2D(x, nextY), regions))
                    break;
                if (yHi - nextY > maxThicknessMm + EdgeToleranceMm)
                    break;
                yLo = nextY;
            }

            return yHi > yLo + EdgeToleranceMm;
        }

        private static double MedianBandY(IReadOnlyList<(double YLo, double YHi)> bands, bool hi)
        {
            var values = new List<double>(bands.Count);
            foreach (var band in bands)
                values.Add(hi ? band.YHi : band.YLo);

            values.Sort();
            int n = values.Count;
            if (n == 0)
                return 0;

            if (n % 2 == 1)
                return values[n / 2];

            return (values[n / 2 - 1] + values[n / 2]) / 2.0;
        }

        /// <summary>底面气接触：仅探针扫描 + 孔洞开口，不用 N13 水平 Air 边(避免蹭到邻近空腔边)。</summary>
        private static List<(double X0, double X1)> CollectProbeOnlyAirContactXIntervals(
            double minX,
            double maxX,
            double y,
            bool isAbove,
            IReadOnlyList<ReinRegion> regions,
            GroupBoundaryProfile profile)
        {
            var intervals = new List<(double X0, double X1)>();
            intervals.AddRange(GetHoleFaceAirIntervals(minX, maxX, y, isAbove, regions));
            intervals.AddRange(ScanAirContactIntervals(minX, maxX, y, isAbove, regions, profile));
            return MergeXIntervals(intervals);
        }

        private static bool HasOverlappingEffectiveAirInterval(
            List<(double X0, double X1)> topIntervals,
            List<(double X0, double X1)> bottomIntervals,
            double minX,
            double maxX,
            double minLen)
        {
            if (topIntervals == null || bottomIntervals == null)
                return false;

            foreach (var top in topIntervals)
            {
                foreach (var bottom in bottomIntervals)
                {
                    double s = Math.Max(minX, Math.Max(top.X0, bottom.X0));
                    double e = Math.Min(maxX, Math.Min(top.X1, bottom.X1));
                    if (e - s >= minLen - EdgeToleranceMm)
                        return true;
                }
            }

            return false;
        }

        private static List<(double X0, double X1)> CollectAirContactXIntervals(
            double minX,
            double maxX,
            double y,
            bool isAbove,
            IReadOnlyList<ReinRegion> regions,
            GroupBoundaryProfile profile)
        {
            var intervals = new List<(double X0, double X1)>();
            intervals.AddRange(GetHorizontalAirEdgeIntervals(minX, maxX, y, profile));
            intervals.AddRange(GetHoleFaceAirIntervals(minX, maxX, y, isAbove, regions));
            intervals.AddRange(ScanAirContactIntervals(minX, maxX, y, isAbove, regions, profile));
            return MergeXIntervals(intervals);
        }

        private static List<(double X0, double X1)> GetHorizontalAirEdgeIntervals(
            double minX,
            double maxX,
            double y,
            GroupBoundaryProfile profile)
        {
            var intervals = new List<(double X0, double X1)>();
            var edges = GetAllRegionEdges(profile);
            if (edges == null)
                return intervals;

            foreach (var ce in edges)
            {
                if (ce?.Edge == null || ce.Role != BoundaryEdgeRole.Air)
                    continue;

                var e = ce.Edge;
                if (Math.Abs(e.StartPoint.Y - e.EndPoint.Y) > EdgeToleranceMm)
                    continue;

                double edgeY = (e.StartPoint.Y + e.EndPoint.Y) / 2.0;
                if (Math.Abs(edgeY - y) > AnchorYToleranceMm)
                    continue;

                double ex0 = Math.Max(minX, Math.Min(e.StartPoint.X, e.EndPoint.X));
                double ex1 = Math.Min(maxX, Math.Max(e.StartPoint.X, e.EndPoint.X));
                if (ex1 - ex0 > EdgeToleranceMm)
                    intervals.Add((ex0, ex1));
            }

            return intervals;
        }

        private static List<(double X0, double X1)> GetHoleFaceAirIntervals(
            double minX,
            double maxX,
            double y,
            bool isAbove,
            IReadOnlyList<ReinRegion> regions)
        {
            var intervals = new List<(double X0, double X1)>();
            if (regions == null)
                return intervals;

            foreach (var region in regions)
            {
                if (region?.Holes == null)
                    continue;

                foreach (var hole in region.Holes)
                {
                    if (hole?.Vertices == null || hole.VertexCount < 3)
                        continue;

                    GetPolylineBounds(hole, out double hMinX, out double hMaxX, out double hMinY, out double hMaxY);

                    double overlapX0 = Math.Max(minX, hMinX);
                    double overlapX1 = Math.Min(maxX, hMaxX);
                    if (overlapX1 - overlapX0 < EdgeToleranceMm)
                        continue;

                    bool faceOnHoleTop = !isAbove && Math.Abs(hMaxY - y) <= EdgeToleranceMm;
                    bool faceOnHoleBottom = isAbove && Math.Abs(hMinY - y) <= EdgeToleranceMm;
                    if (!faceOnHoleTop && !faceOnHoleBottom)
                        continue;

                    intervals.Add((overlapX0, overlapX1));
                }
            }

            return intervals;
        }

        private static List<(double X0, double X1)> ScanAirContactIntervals(
            double minX,
            double maxX,
            double y,
            bool isAbove,
            IReadOnlyList<ReinRegion> regions,
            GroupBoundaryProfile profile)
        {
            var intervals = new List<(double X0, double X1)>();
            double span = maxX - minX;
            double step = Math.Max(50.0, span / 40.0);

            bool inAir = false;
            double segStart = minX;

            for (double x = minX; x <= maxX + EdgeToleranceMm; x += step)
            {
                double sampleX = Math.Min(x, maxX);
                bool air = ContactsAirAt(sampleX, y, isAbove, regions, profile);

                if (air && !inAir)
                {
                    segStart = sampleX;
                    inAir = true;
                }
                else if (!air && inAir)
                {
                    intervals.Add((segStart, sampleX));
                    inAir = false;
                }

                if (Math.Abs(sampleX - maxX) <= EdgeToleranceMm)
                    break;
            }

            if (inAir)
                intervals.Add((segStart, maxX));

            return intervals;
        }

        private static bool HasEffectiveAirInterval(
            List<(double X0, double X1)> intervals,
            double minX,
            double maxX,
            double minLen)
        {
            foreach (var iv in intervals)
            {
                double s = Math.Max(minX, iv.X0);
                double e = Math.Min(maxX, iv.X1);
                if (e - s >= minLen - EdgeToleranceMm)
                    return true;
            }

            return false;
        }

        private static List<(double X0, double X1)> MergeXIntervals(List<(double X0, double X1)> intervals)
        {
            if (intervals == null || intervals.Count == 0)
                return new List<(double X0, double X1)>();

            if (intervals.Count == 1)
                return intervals;

            intervals.Sort((a, b) => a.X0.CompareTo(b.X0));
            var merged = new List<(double X0, double X1)> { intervals[0] };
            for (int i = 1; i < intervals.Count; i++)
            {
                var cur = intervals[i];
                var last = merged[merged.Count - 1];
                if (cur.X0 <= last.X1 + EdgeToleranceMm)
                    merged[merged.Count - 1] = (last.X0, Math.Max(last.X1, cur.X1));
                else
                    merged.Add(cur);
            }

            return merged;
        }

        private static IEnumerable<double> GetSpanSampleXs(double minX, double maxX)
        {
            double lo = minX + SpanInsetMm;
            double hi = maxX - SpanInsetMm;
            if (hi <= lo)
            {
                yield return (minX + maxX) / 2.0;
                yield break;
            }

            yield return lo;
            yield return lo + (hi - lo) * 0.25;
            yield return (lo + hi) / 2.0;
            yield return lo + (hi - lo) * 0.75;
            yield return hi;
        }

        private static bool IsPointInAnyHole(Point2D probe, IReadOnlyList<ReinRegion> regions)
        {
            if (regions == null)
                return false;

            foreach (var region in regions)
            {
                if (region?.Holes == null)
                    continue;

                foreach (var hole in region.Holes)
                {
                    if (hole?.Vertices == null || hole.VertexCount < 3)
                        continue;

                    if (PolygonAlgorithms.ContainsPoint(hole.Vertices, probe))
                        return true;
                }
            }

            return false;
        }

        private static bool IsInsideAnyOuter(Point2D probe, IReadOnlyList<ReinRegion> regions)
        {
            if (regions == null)
                return false;

            foreach (var region in regions)
            {
                if (region?.Outer == null || region.Outer.VertexCount < 3)
                    continue;

                if (PolygonAlgorithms.ContainsPoint(region.Outer.Vertices, probe))
                    return true;
            }

            return false;
        }

        private static bool FaceOpensToHole(
            double minX,
            double maxX,
            double y,
            bool isAbove,
            IReadOnlyList<ReinRegion> regions)
        {
            if (regions == null)
                return false;

            foreach (var region in regions)
            {
                if (region?.Holes == null)
                    continue;

                foreach (var hole in region.Holes)
                {
                    if (hole?.Vertices == null || hole.VertexCount < 3)
                        continue;

                    GetPolylineBounds(hole, out double hMinX, out double hMaxX, out double hMinY, out double hMaxY);

                    double overlapX0 = Math.Max(minX, hMinX);
                    double overlapX1 = Math.Min(maxX, hMaxX);
                    if (overlapX1 - overlapX0 < EdgeToleranceMm)
                        continue;

                    bool faceOnHoleTop = !isAbove && Math.Abs(hMaxY - y) <= EdgeToleranceMm;
                    bool faceOnHoleBottom = isAbove && Math.Abs(hMinY - y) <= EdgeToleranceMm;
                    if (!faceOnHoleTop && !faceOnHoleBottom)
                        continue;

                    double sampleX = (overlapX0 + overlapX1) / 2.0;
                    double probeY = isAbove ? y + ProbeOffsetMm : y - ProbeOffsetMm;
                    var probe = new Point2D(sampleX, probeY);

                    if (IsPointInAnyHole(probe, regions))
                        return true;
                }
            }

            return false;
        }

        private static void GetPolylineBounds(
            Polyline2D poly,
            out double minX,
            out double maxX,
            out double minY,
            out double maxY)
        {
            minX = double.MaxValue;
            maxX = double.MinValue;
            minY = double.MaxValue;
            maxY = double.MinValue;

            for (int i = 0; i < poly.VertexCount; i++)
            {
                var p = poly.GetPointAt(i);
                minX = Math.Min(minX, p.X);
                maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y);
                maxY = Math.Max(maxY, p.Y);
            }
        }

        private static IReadOnlyList<ClassifiedBoundaryEdge> GetAllRegionEdges(GroupBoundaryProfile profile)
        {
            if (profile?.Regions == null)
                return null;

            var all = new List<ClassifiedBoundaryEdge>();
            foreach (var rp in profile.Regions)
            {
                if (rp?.Edges == null)
                    continue;
                all.AddRange(rp.Edges);
            }

            return all;
        }

        private static IReadOnlyList<ClassifiedBoundaryEdge> GetAllBoundaryEdges(GroupBoundaryProfile profile)
            => GetAllRegionEdges(profile);

        private static bool HorizontalEdgeOverlapsSpanAtX(
            Line2D edge,
            double x,
            double yBot,
            double foundationMinY)
        {
            double edgeY = (edge.StartPoint.Y + edge.EndPoint.Y) / 2.0;
            if (Math.Abs(edgeY - yBot) > AnchorYToleranceMm
                && Math.Abs(edgeY - foundationMinY) > AnchorYToleranceMm)
                return false;

            double ex0 = Math.Min(edge.StartPoint.X, edge.EndPoint.X);
            double ex1 = Math.Max(edge.StartPoint.X, edge.EndPoint.X);
            return x >= ex0 - EdgeToleranceMm && x <= ex1 + EdgeToleranceMm;
        }

        private static bool TryGetBoundaryRoleAt(
            IReadOnlyList<ClassifiedBoundaryEdge> edges,
            double midX,
            double y,
            bool isAbove,
            out BoundaryEdgeRole role)
        {
            role = BoundaryEdgeRole.Air;
            double bestDist = double.MaxValue;
            bool found = false;

            foreach (var ce in edges)
            {
                if (ce?.Edge == null)
                    continue;

                var seg = ce.Edge;
                double sx0 = Math.Min(seg.StartPoint.X, seg.EndPoint.X);
                double sx1 = Math.Max(seg.StartPoint.X, seg.EndPoint.X);
                if (midX < sx0 - EdgeToleranceMm || midX > sx1 + EdgeToleranceMm)
                    continue;

                double edgeY = StripGeometry.EvaluateYOnSegment(seg, midX);
                double dist = isAbove ? y - edgeY : edgeY - y;
                if (dist < -EdgeToleranceMm || dist > EdgeToleranceMm)
                    continue;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    role = ce.Role;
                    found = true;
                }
            }

            return found;
        }

        private static bool IsConcrete(Point2D point, IReadOnlyList<ReinRegion> regions)
        {
            if (regions == null || regions.Count == 0)
                return false;

            foreach (var region in regions)
            {
                if (region?.IsValidRebarPoint(point) == true)
                    return true;
            }

            return false;
        }

        private readonly struct BoundaryHit
        {
            public BoundaryHit(Point2D point, BoundaryEdgeRole role)
            {
                Point = point;
                Role = role;
            }

            public Point2D Point { get; }
            public BoundaryEdgeRole Role { get; }
        }

        private static List<BoundaryHit> GetVerticalBoundaryHits(
            IReadOnlyList<ClassifiedBoundaryEdge> boundaryEdges,
            double x)
        {
            var hits = new List<BoundaryHit>();
            if (boundaryEdges == null)
                return hits;

            foreach (var ce in boundaryEdges)
            {
                if (ce?.Edge == null)
                    continue;

                var seg = ce.Edge;
                double x1 = seg.StartPoint.X;
                double x2 = seg.EndPoint.X;
                double minX = Math.Min(x1, x2);
                double maxX = Math.Max(x1, x2);
                if (x <= minX + IntersectionEpsilon || x >= maxX - IntersectionEpsilon)
                    continue;

                double y = StripGeometry.EvaluateYOnSegment(seg, x);
                hits.Add(new BoundaryHit(new Point2D(x, y), ce.Role));
            }

            if (hits.Count == 0)
                return hits;

            hits.Sort((a, b) => a.Point.Y.CompareTo(b.Point.Y));

            var deduped = new List<BoundaryHit> { hits[0] };
            for (int i = 1; i < hits.Count; i++)
            {
                if (hits[i].Point.Y - deduped[deduped.Count - 1].Point.Y > HitDedupeYToleranceMm)
                    deduped.Add(hits[i]);
            }

            return deduped;
        }
    }
}
