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

        /// <summary>底边 [minX,maxX]×yBot 与土接触（N13 边 + 各区域本地底面外探）。</summary>
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
            if (IsLocalBottomExteriorSoil(x, yBot, regions))
                return true;

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

        private static bool IsLocalBottomExteriorSoil(
            double x,
            double yBot,
            IReadOnlyList<ReinRegion> regions)
        {
            if (regions == null)
                return false;

            foreach (var region in regions)
            {
                if (region?.Outer == null || region.Outer.VertexCount < 3)
                    continue;

                if (!TryGetExteriorBottomYAtX(region, x, out double localBottomY))
                    continue;

                if (Math.Abs(yBot - localBottomY) > AnchorYToleranceMm)
                    continue;

                var probe = new Point2D(x, yBot - ProbeOffsetMm);
                if (!region.IsValidRebarPoint(probe))
                    return true;
            }

            return false;
        }

        private static bool TryGetExteriorBottomYAtX(
            ReinRegion region,
            double x,
            out double bottomY)
        {
            bottomY = double.NaN;
            var segments = StripGeometry.CollectSegments(region);
            var intervals = StripGeometry.GetInsideIntervalsAt(region, segments, x);
            if (intervals == null || intervals.Count == 0)
                return false;

            bottomY = intervals[intervals.Count - 1].Bottom;
            return !double.IsNaN(bottomY);
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
