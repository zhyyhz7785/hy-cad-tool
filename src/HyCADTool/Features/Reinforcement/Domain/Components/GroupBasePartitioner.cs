using HyCAD.Geometry;
using HyCAD.Geometry.Algorithms;
using HyCADTool.Features.Reinforcement.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>单条带基础分区诊断。</summary>
    public sealed class BaseStripDiagnostic
    {
        public int StripIndex { get; set; }
        public double X0 { get; set; }
        public double X1 { get; set; }
        public double Xm { get; set; }
        public bool IsGrounded { get; set; }
        public bool IsSoilContact { get; set; }
        public double YBot { get; set; }
        public double YTop1 { get; set; }
        public double CandidateHeightMm { get; set; }
        public double CutY { get; set; }
        public double ThicknessMm { get; set; }
        public bool SkippedTooThick { get; set; }
        public bool HasBottomSlab { get; set; }
        public bool IsSloped { get; set; }
        public double EffectiveMaxHeightMm { get; set; }
    }

    /// <summary>单组基础底板分区结果。</summary>
    public sealed class GroupBasePartitionResult
    {
        public int GroupIndex { get; set; }
        public int FoundationRegionIndex { get; set; } = -1;
        public double CutLineY { get; set; } = double.NaN;
        public IReadOnlyList<BaseStripDiagnostic> StripDiagnostics { get; set; }
        public IReadOnlyList<Polygon2D> BasePolygons { get; set; }
        public IReadOnlyList<BaseSlabCell> BottomCells { get; set; }
    }

    /// <summary>基础底板梯形 cell（条带内 [y_bot, y_top1]）。</summary>
    public sealed class BaseSlabCell
    {
        public int StripIndex { get; set; }
        public int Id { get; set; }
        public double X0 { get; set; }
        public double X1 { get; set; }
        public double TopL { get; set; }
        public double TopR { get; set; }
        public double BotL { get; set; }
        public double BotR { get; set; }
        public double ThicknessMm { get; set; }

        public Polygon2D ToPolygon()
        {
            return new Polygon2D(new[]
            {
                new Point2D(X0, TopL),
                new Point2D(X1, TopR),
                new Point2D(X1, BotR),
                new Point2D(X0, BotL)
            }, isClosed: true);
        }
    }

    /// <summary>
    /// 阶段 B 续：对每个设计分组的基础图形 $O^*_g$ 识别基础/底板。
    /// 底边 = 土壤 CCW 弧（左土气点→右土气点）；上轮廓 = 最下混凝土带第 2 交点 TopSeg；H≤h_b 才出底板。
    /// </summary>
    public static class GroupBasePartitioner
    {
        private const double CutXRangeToleranceMm = 1e-6;
        private const double VerticalSegmentToleranceMm = 1.0;
        private const double SoilSlopeAngleThresholdDeg = 1.0;
        private const int MarchBreakpointMaxIterations = 100000;

        public static List<GroupBasePartitionResult> PartitionAllGroups(
            IReadOnlyList<IReadOnlyList<ReinRegion>> groups,
            IReadOnlyList<GroupBoundaryProfile> groupProfiles,
            double bottomSlabMaxHeightMm)
        {
            var results = new List<GroupBasePartitionResult>();
            if (groups == null || groupProfiles == null)
                return results;

            if (bottomSlabMaxHeightMm < StripGeometry.MinIntervalHeightMm)
                bottomSlabMaxHeightMm = 1500.0;

            for (int g = 0; g < groups.Count && g < groupProfiles.Count; g++)
            {
                var profile = groupProfiles[g];
                var groupRegions = groups[g];
                if (profile == null || groupRegions == null)
                    continue;

                int foundationIndex = profile.FoundationGraphicIndex;
                if (foundationIndex < 0 || foundationIndex >= groupRegions.Count)
                {
                    results.Add(new GroupBasePartitionResult
                    {
                        GroupIndex = g,
                        FoundationRegionIndex = foundationIndex,
                        CutLineY = profile.CutY,
                        StripDiagnostics = Array.Empty<BaseStripDiagnostic>(),
                        BasePolygons = Array.Empty<Polygon2D>(),
                        BottomCells = Array.Empty<BaseSlabCell>()
                    });
                    continue;
                }

                var soilEdges = ExtractSoilEdges(profile, foundationIndex);

                results.Add(PartitionFoundationGraphic(
                    g,
                    groupRegions[foundationIndex],
                    profile.CutY,
                    foundationIndex,
                    bottomSlabMaxHeightMm,
                    profile.CutIntersectionMinX,
                    profile.CutIntersectionMaxX,
                    soilEdges));
            }

            return results;
        }

        private static IReadOnlyList<ClassifiedBoundaryEdge> ExtractSoilEdges(
            GroupBoundaryProfile profile,
            int foundationIndex)
        {
            if (profile?.Regions == null || foundationIndex < 0 || foundationIndex >= profile.Regions.Count)
                return Array.Empty<ClassifiedBoundaryEdge>();

            var regionProfile = profile.Regions[foundationIndex];
            if (regionProfile?.Edges == null)
                return Array.Empty<ClassifiedBoundaryEdge>();

            return regionProfile.Edges
                .Where(e => e != null && e.Role == BoundaryEdgeRole.Soil && e.Edge != null)
                .ToList();
        }

        public static GroupBasePartitionResult PartitionFoundationGraphic(
            int groupIndex,
            ReinRegion region,
            double cutLineY,
            int foundationRegionIndex,
            double bottomSlabMaxHeightMm,
            double cutMinX,
            double cutMaxX,
            IReadOnlyList<ClassifiedBoundaryEdge> soilEdges)
        {
            var diagnostics = new List<BaseStripDiagnostic>();
            var bottomCells = new List<BaseSlabCell>();

            if (region?.Outer == null || region.Outer.VertexCount < 3 || double.IsNaN(cutLineY))
            {
                return EmptyResult(groupIndex, foundationRegionIndex, cutLineY, diagnostics, bottomCells);
            }

            var soilSpans = BuildSoilSpans(soilEdges);
            var segments = StripGeometry.CollectSegments(region);
            var breakpoints = CollectBreakpointsInCutRange(soilSpans, region, segments, cutMinX, cutMaxX);
            if (breakpoints.Count < 2 || segments.Count == 0)
            {
                return EmptyResult(groupIndex, foundationRegionIndex, cutLineY, diagnostics, bottomCells);
            }

            int stripCount = breakpoints.Count - 1;
            int cellId = 0;

            for (int i = 0; i < stripCount; i++)
            {
                double x0 = breakpoints[i];
                double x1 = breakpoints[i + 1];
                if (x1 - x0 < StripGeometry.MinStripWidthMm)
                    continue;

                double xm = (x0 + x1) / 2.0;

                if (!TryGetSoilBottomY(xm, soilSpans, out double soilYMid, out Line2D soilSegMid))
                {
                    diagnostics.Add(new BaseStripDiagnostic
                    {
                        StripIndex = i,
                        X0 = x0,
                        X1 = x1,
                        Xm = xm,
                        IsSoilContact = false,
                        IsGrounded = false
                    });
                    continue;
                }

                var intervals = StripGeometry.GetInsideIntervalsAt(region, segments, xm);
                if (intervals.Count == 0)
                    continue;

                var iv = intervals[intervals.Count - 1];
                double topYMid = StripGeometry.EvaluateYOnSegment(iv.TopSeg, xm);
                double topL = StripGeometry.EvaluateYOnSegment(iv.TopSeg, x0);
                double topR = StripGeometry.EvaluateYOnSegment(iv.TopSeg, x1);

                if (!TryGetSoilBottomY(x0, soilSpans, out double botL, out _))
                    botL = soilYMid;
                if (!TryGetSoilBottomY(x1, soilSpans, out double botR, out _))
                    botR = soilYMid;

                double yBot = Math.Min(botL, botR);
                double yTop1 = Math.Max(Math.Max(topL, topR), topYMid);
                double candidateH = topYMid - soilYMid;
                bool isSloped = IsSoilSloped(soilSegMid);
                double effectiveMaxH = isSloped ? bottomSlabMaxHeightMm * 2.0 : bottomSlabMaxHeightMm;

                var diag = new BaseStripDiagnostic
                {
                    StripIndex = i,
                    X0 = x0,
                    X1 = x1,
                    Xm = xm,
                    IsSoilContact = true,
                    IsGrounded = true,
                    YBot = yBot,
                    YTop1 = yTop1,
                    CandidateHeightMm = candidateH,
                    IsSloped = isSloped,
                    EffectiveMaxHeightMm = effectiveMaxH
                };

                if (candidateH > effectiveMaxH)
                {
                    diag.SkippedTooThick = true;
                    diagnostics.Add(diag);
                    continue;
                }

                if (candidateH < StripGeometry.MinIntervalHeightMm)
                {
                    diagnostics.Add(diag);
                    continue;
                }

                diag.CutY = (topL + topR) / 2.0;
                diag.ThicknessMm = candidateH;
                diag.HasBottomSlab = true;

                bottomCells.Add(new BaseSlabCell
                {
                    Id = cellId++,
                    StripIndex = i,
                    X0 = x0,
                    X1 = x1,
                    TopL = topL,
                    TopR = topR,
                    BotL = botL,
                    BotR = botR,
                    ThicknessMm = candidateH
                });

                diagnostics.Add(diag);
            }

            var mergedGroups = MergeBottomCells(bottomCells);
            var basePolygons = new List<Polygon2D>();
            foreach (var group in mergedGroups)
            {
                foreach (var poly in PolygonBoolean.UnionAll(group.Select(c => c.ToPolygon()).ToList()))
                {
                    if (poly != null && poly.VertexCount >= 3)
                        basePolygons.Add(poly);
                }
            }

            return new GroupBasePartitionResult
            {
                GroupIndex = groupIndex,
                FoundationRegionIndex = foundationRegionIndex,
                CutLineY = cutLineY,
                StripDiagnostics = diagnostics,
                BasePolygons = basePolygons,
                BottomCells = bottomCells
            };
        }

        private static List<SoilSpan> BuildSoilSpans(IReadOnlyList<ClassifiedBoundaryEdge> soilEdges)
        {
            var spans = new List<SoilSpan>();
            if (soilEdges == null)
                return spans;

            foreach (var edge in soilEdges)
            {
                if (edge?.Edge == null)
                    continue;

                var seg = edge.Edge;
                double dx = Math.Abs(seg.EndPoint.X - seg.StartPoint.X);
                if (dx <= VerticalSegmentToleranceMm)
                    continue;

                spans.Add(new SoilSpan(
                    Math.Min(seg.StartPoint.X, seg.EndPoint.X),
                    Math.Max(seg.StartPoint.X, seg.EndPoint.X),
                    seg));
            }

            return spans;
        }

        private static bool TryGetSoilBottomY(
            double x,
            IReadOnlyList<SoilSpan> spans,
            out double yBot,
            out Line2D soilSeg)
        {
            yBot = 0;
            soilSeg = default;
            if (spans == null || spans.Count == 0)
                return false;

            bool found = false;
            double bestY = double.MaxValue;
            Line2D bestSeg = default;

            foreach (var span in spans)
            {
                if (x < span.MinX - CutXRangeToleranceMm || x > span.MaxX + CutXRangeToleranceMm)
                    continue;

                double y = StripGeometry.EvaluateYOnSegment(span.Segment, x);
                if (!found || y < bestY)
                {
                    bestY = y;
                    bestSeg = span.Segment;
                    found = true;
                }
            }

            if (!found)
                return false;

            yBot = bestY;
            soilSeg = bestSeg;
            return true;
        }

        private static bool IsSoilSloped(Line2D seg)
        {
            double dx = Math.Abs(seg.EndPoint.X - seg.StartPoint.X);
            double dy = Math.Abs(seg.EndPoint.Y - seg.StartPoint.Y);
            if (dx <= VerticalSegmentToleranceMm)
                return false;

            double angleDeg = Math.Atan2(dy, dx) * (180.0 / Math.PI);
            return angleDeg >= SoilSlopeAngleThresholdDeg;
        }

        private static bool TryGetTopSegAt(
            ReinRegion region,
            IReadOnlyList<Line2D> segments,
            double x,
            out Line2D topSeg)
        {
            topSeg = default;
            if (region == null || segments == null || segments.Count == 0)
                return false;

            var intervals = StripGeometry.GetInsideIntervalsAt(region, segments, x);
            if (intervals == null || intervals.Count == 0)
                return false;

            topSeg = intervals[intervals.Count - 1].TopSeg;
            double dx = Math.Abs(topSeg.EndPoint.X - topSeg.StartPoint.X);
            double dy = Math.Abs(topSeg.EndPoint.Y - topSeg.StartPoint.Y);
            return dx + dy > CutXRangeToleranceMm;
        }

        /// <summary>
        /// 沿 +X 行进：取边段上 probe 处交点 P 到前方端点（X&gt;probe）的真实斜边长度。
        /// </summary>
        private static bool TryGetForwardEndpoint(
            Line2D seg,
            double probe,
            out double endX,
            out double trueLen,
            out double yAtProbe)
        {
            endX = probe;
            trueLen = 0;
            yAtProbe = StripGeometry.EvaluateYOnSegment(seg, probe);

            bool startForward = seg.StartPoint.X > probe + CutXRangeToleranceMm;
            bool endForward = seg.EndPoint.X > probe + CutXRangeToleranceMm;

            Point2D forward;
            if (startForward && endForward)
            {
                forward = seg.StartPoint.X <= seg.EndPoint.X ? seg.EndPoint : seg.StartPoint;
            }
            else if (endForward)
            {
                forward = seg.EndPoint;
            }
            else if (startForward)
            {
                forward = seg.StartPoint;
            }
            else
            {
                return false;
            }

            endX = forward.X;
            var probePt = new Point2D(probe, yAtProbe);
            trueLen = probePt.DistanceTo(forward);
            return trueLen > CutXRangeToleranceMm;
        }

        private static double FindNextSoilMinX(IReadOnlyList<SoilSpan> soilSpans, double probe, double hi)
        {
            double best = hi;
            if (soilSpans == null)
                return best;

            foreach (var span in soilSpans)
            {
                if (span.MinX > probe + CutXRangeToleranceMm && span.MinX < best)
                    best = span.MinX;
            }

            return best;
        }

        private static List<double> CollectBreakpointsFromSoilSpansOnly(IReadOnlyList<SoilSpan> soilSpans)
        {
            var xs = new SortedSet<double>();
            if (soilSpans != null)
            {
                foreach (var span in soilSpans)
                {
                    xs.Add(span.MinX);
                    xs.Add(span.MaxX);
                }
            }

            return xs.Count > 0 ? xs.ToList() : new List<double>();
        }

        private static List<double> CollectBreakpointsInCutRange(
            IReadOnlyList<SoilSpan> soilSpans,
            ReinRegion region,
            IReadOnlyList<Line2D> segments,
            double cutMinX,
            double cutMaxX)
        {
            if (soilSpans == null || soilSpans.Count == 0)
                return new List<double>();

            if (double.IsNaN(cutMinX) || double.IsNaN(cutMaxX))
                return CollectBreakpointsFromSoilSpansOnly(soilSpans);

            double lo = Math.Min(cutMinX, cutMaxX);
            double hi = Math.Max(cutMinX, cutMaxX);
            if (hi - lo < StripGeometry.MinStripWidthMm)
                return new List<double> { lo, hi };

            double epsX = Math.Max(0.01, (hi - lo) * 1e-6);
            var breakpoints = new List<double> { lo };
            double x = lo;
            int guard = 0;

            while (x < hi - CutXRangeToleranceMm && guard < MarchBreakpointMaxIterations)
            {
                guard++;
                double probe = Math.Min(x + epsX, hi);

                if (!TryGetSoilBottomY(probe, soilSpans, out _, out Line2D soilSeg))
                {
                    double jump = FindNextSoilMinX(soilSpans, probe, hi);
                    if (jump <= x + CutXRangeToleranceMm)
                        break;

                    x = jump;
                    AddBreakpoint(breakpoints, x);
                    continue;
                }

                if (!TryGetForwardEndpoint(soilSeg, probe, out double downEndX, out double lenDown, out _))
                {
                    x = Math.Min(probe + StripGeometry.MinStripWidthMm, hi);
                    AddBreakpoint(breakpoints, x);
                    continue;
                }

                double xNext = downEndX;
                if (TryGetTopSegAt(region, segments, probe, out Line2D topSeg)
                    && TryGetForwardEndpoint(topSeg, probe, out double upEndX, out double lenUp, out _)
                    && lenUp < lenDown)
                {
                    xNext = upEndX;
                }

                xNext = Math.Min(xNext, hi);
                if (xNext <= x + CutXRangeToleranceMm)
                    xNext = Math.Min(x + StripGeometry.MinStripWidthMm, hi);

                if (xNext <= x + CutXRangeToleranceMm)
                    break;

                AddBreakpoint(breakpoints, xNext);
                x = xNext;
            }

            AddBreakpoint(breakpoints, hi);
            return breakpoints;
        }

        private static void AddBreakpoint(List<double> breakpoints, double x)
        {
            if (breakpoints.Count == 0)
            {
                breakpoints.Add(x);
                return;
            }

            if (Math.Abs(breakpoints[breakpoints.Count - 1] - x) > CutXRangeToleranceMm)
                breakpoints.Add(x);
        }

        private static GroupBasePartitionResult EmptyResult(
            int groupIndex,
            int foundationRegionIndex,
            double cutLineY,
            List<BaseStripDiagnostic> diagnostics,
            List<BaseSlabCell> bottomCells)
        {
            return new GroupBasePartitionResult
            {
                GroupIndex = groupIndex,
                FoundationRegionIndex = foundationRegionIndex,
                CutLineY = cutLineY,
                StripDiagnostics = diagnostics,
                BasePolygons = Array.Empty<Polygon2D>(),
                BottomCells = bottomCells
            };
        }

        private static List<List<BaseSlabCell>> MergeBottomCells(List<BaseSlabCell> cells)
        {
            var groups = new List<List<BaseSlabCell>>();
            if (cells.Count == 0)
                return groups;

            var idMap = new Dictionary<int, int>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
                idMap[cells[i].Id] = i;

            var byStrip = cells.GroupBy(c => c.StripIndex).ToDictionary(g => g.Key, g => g.ToList());
            var uf = new UnionFind(cells.Count);

            foreach (var cell in cells)
            {
                for (int next = cell.StripIndex + 1; next < cell.StripIndex + 8; next++)
                {
                    if (!byStrip.TryGetValue(next, out var nextStrip))
                        continue;

                    foreach (var other in nextStrip)
                    {
                        if (Math.Abs(other.X0 - cell.X1) > StripGeometry.MinStripWidthMm)
                            continue;

                        double overlap = Math.Min(cell.TopR, other.TopL) - Math.Max(cell.BotR, other.BotL);
                        if (overlap > StripGeometry.MergeOverlapMm)
                            uf.Union(idMap[cell.Id], idMap[other.Id]);
                    }

                    break;
                }
            }

            var byRoot = new Dictionary<int, List<BaseSlabCell>>();
            for (int i = 0; i < cells.Count; i++)
            {
                int root = uf.Find(i);
                if (!byRoot.TryGetValue(root, out var list))
                {
                    list = new List<BaseSlabCell>();
                    byRoot[root] = list;
                }

                list.Add(cells[i]);
            }

            groups.AddRange(byRoot.Values);
            return groups;
        }

        private readonly struct SoilSpan
        {
            public SoilSpan(double minX, double maxX, Line2D segment)
            {
                MinX = minX;
                MaxX = maxX;
                Segment = segment;
            }

            public double MinX { get; }
            public double MaxX { get; }
            public Line2D Segment { get; }
        }

        private sealed class UnionFind
        {
            private readonly int[] _parent;
            private readonly int[] _rank;

            public UnionFind(int n)
            {
                _parent = new int[n];
                _rank = new int[n];
                for (int i = 0; i < n; i++)
                    _parent[i] = i;
            }

            public int Find(int x)
            {
                if (_parent[x] != x)
                    _parent[x] = Find(_parent[x]);
                return _parent[x];
            }

            public void Union(int a, int b)
            {
                int ra = Find(a), rb = Find(b);
                if (ra == rb)
                    return;

                if (_rank[ra] < _rank[rb])
                    _parent[ra] = rb;
                else if (_rank[ra] > _rank[rb])
                    _parent[rb] = ra;
                else
                {
                    _parent[rb] = ra;
                    _rank[ra]++;
                }
            }
        }
    }
}
