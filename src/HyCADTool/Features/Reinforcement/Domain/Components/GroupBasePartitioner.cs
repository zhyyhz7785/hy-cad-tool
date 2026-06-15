using HyCAD.Geometry;
using HyCAD.Geometry.Algorithms;
using HyCADTool.Features.Reinforcement.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>基础底板分区参数。</summary>
    public sealed class BasePartitionOptions
    {
        public double BottomSlabMaxMm { get; set; } = 1500.0;
        public double MarchStepMm { get; set; } = 10.0;
        public double WallMaxMm { get; set; } = 500.0;
        public double MassMinMm { get; set; } = 1000.0;

        public static BasePartitionOptions FromParameters(ComponentParameters p)
        {
            if (p == null)
                return new BasePartitionOptions();

            return new BasePartitionOptions
            {
                BottomSlabMaxMm = p.BottomSlabMaxThicknessMm,
                MarchStepMm = p.BottomSlabMarchStepMm,
                WallMaxMm = p.WallMaxThicknessMm,
                MassMinMm = p.MassConcreteMinSizeMm
            };
        }

        public double EffectiveMarchStepMm =>
            MarchStepMm < 1.0 ? 1.0 : MarchStepMm;
    }

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
        public bool SkippedSoilSoil { get; set; }
        public bool TriggeredNonSoilTop { get; set; }
        public string ProbeTag { get; set; }
        public bool IsTopSynthetic { get; set; }
        public double SyntheticTopLengthMm { get; set; }
        public ComponentType Kind { get; set; } = ComponentType.BottomSlab;
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
        public bool IsTopSynthetic { get; set; }
        public double SyntheticTopLengthMm { get; set; }
        public ComponentType Kind { get; set; } = ComponentType.BottomSlab;

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
    /// 阶段 B 续：沿 $\mathcal{S}_g$ 步进探测，第 2 交点非 Soil 时生成基础拼图。
    /// </summary>
    public static class GroupBasePartitioner
    {
        private const double CutXRangeToleranceMm = 1e-6;
        private const double VerticalSegmentToleranceMm = 1.0;
        private const double SoilSlopeAngleThresholdDeg = 1.0;
        private const double HitDedupeYToleranceMm = 0.01;
        private const double IntersectionEpsilon = 1e-9;

        public static List<GroupBasePartitionResult> PartitionAllGroups(
            IReadOnlyList<IReadOnlyList<ReinRegion>> groups,
            IReadOnlyList<GroupBoundaryProfile> groupProfiles,
            BasePartitionOptions options)
        {
            var results = new List<GroupBasePartitionResult>();
            if (groups == null || groupProfiles == null)
                return results;

            options = NormalizeOptions(options);

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

                var boundaryEdges = ExtractBoundaryEdges(profile, foundationIndex);

                results.Add(PartitionFoundationGraphic(
                    g,
                    groupRegions[foundationIndex],
                    profile.CutY,
                    foundationIndex,
                    options,
                    profile.CutIntersectionMinX,
                    profile.CutIntersectionMaxX,
                    boundaryEdges));
            }

            return results;
        }

        private static IReadOnlyList<ClassifiedBoundaryEdge> ExtractBoundaryEdges(
            GroupBoundaryProfile profile,
            int foundationIndex)
        {
            if (profile?.Regions == null || foundationIndex < 0 || foundationIndex >= profile.Regions.Count)
                return Array.Empty<ClassifiedBoundaryEdge>();

            var regionProfile = profile.Regions[foundationIndex];
            if (regionProfile?.Edges == null)
                return Array.Empty<ClassifiedBoundaryEdge>();

            return regionProfile.Edges
                .Where(e => e != null && e.Edge != null)
                .ToList();
        }

        public static GroupBasePartitionResult PartitionFoundationGraphic(
            int groupIndex,
            ReinRegion region,
            double cutLineY,
            int foundationRegionIndex,
            BasePartitionOptions options,
            double cutMinX,
            double cutMaxX,
            IReadOnlyList<ClassifiedBoundaryEdge> boundaryEdges)
        {
            var diagnostics = new List<BaseStripDiagnostic>();
            var bottomCells = new List<BaseSlabCell>();

            options = NormalizeOptions(options);

            if (region?.Outer == null || region.Outer.VertexCount < 3 || double.IsNaN(cutLineY))
            {
                return EmptyResult(groupIndex, foundationRegionIndex, cutLineY, diagnostics, bottomCells);
            }

            if (boundaryEdges == null || boundaryEdges.Count == 0)
            {
                return EmptyResult(groupIndex, foundationRegionIndex, cutLineY, diagnostics, bottomCells);
            }

            var soilSpans = BuildSoilSpans(boundaryEdges);
            BuildCellsBySoilMarching(
                boundaryEdges,
                soilSpans,
                cutMinX,
                cutMaxX,
                options,
                bottomCells,
                diagnostics);

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

        private static void BuildCellsBySoilMarching(
            IReadOnlyList<ClassifiedBoundaryEdge> boundaryEdges,
            IReadOnlyList<SoilSpan> soilSpans,
            double cutMinX,
            double cutMaxX,
            BasePartitionOptions options,
            List<BaseSlabCell> bottomCells,
            List<BaseStripDiagnostic> diagnostics)
        {
            if (soilSpans == null || soilSpans.Count == 0)
                return;

            double marchStep = options.EffectiveMarchStepMm;
            double bottomSlabMaxHeightMm = options.BottomSlabMaxMm;

            double lo = double.IsNaN(cutMinX) || double.IsNaN(cutMaxX)
                ? soilSpans.Min(s => s.MinX)
                : Math.Min(cutMinX, cutMaxX);
            double hi = double.IsNaN(cutMinX) || double.IsNaN(cutMaxX)
                ? soilSpans.Max(s => s.MaxX)
                : Math.Max(cutMinX, cutMaxX);

            var orderedSpans = soilSpans
                .Where(s => s.MaxX >= lo - CutXRangeToleranceMm && s.MinX <= hi + CutXRangeToleranceMm)
                .OrderBy(s => s.SegmentIndex)
                .ToList();

            if (orderedSpans.Count == 0)
                return;

            int stripIndex = 0;
            int cellId = 0;
            double cellX0 = lo;

            Line2D? lockedTopSeg = null;
            SoilSpan? lockedSoilSpan = null;
            double pendingTriggerX = double.NaN;
            double pendingSyntheticTopY = double.NaN;
            bool emitSyntheticFirst = false;

            for (int si = 0; si < orderedSpans.Count; si++)
            {
                var span = orderedSpans[si];
                double spanLo = Math.Max(span.MinX, lo);
                double spanHi = Math.Min(span.MaxX, hi);
                if (spanHi - spanLo < StripGeometry.MinStripWidthMm)
                    continue;

                double x = Math.Max(cellX0, spanLo);
                double maxTopY = double.NegativeInfinity;

                while (x <= spanHi + CutXRangeToleranceMm)
                {
                    if (lockedTopSeg != null)
                    {
                        var topSeg = lockedTopSeg.Value;
                        var soilSpan = lockedSoilSpan ?? span;
                        double xRight;
                        bool isSyntheticPiece = false;
                        double syntheticTopY = double.NaN;

                        if (!double.IsNaN(pendingTriggerX))
                        {
                            xRight = pendingTriggerX;
                            if (emitSyntheticFirst && !double.IsNaN(pendingSyntheticTopY))
                            {
                                isSyntheticPiece = true;
                                syntheticTopY = pendingSyntheticTopY;
                            }

                            pendingTriggerX = double.NaN;
                            pendingSyntheticTopY = double.NaN;
                            emitSyntheticFirst = false;
                        }
                        else
                        {
                            xRight = GetCellRightX(topSeg, soilSpan.Segment, cellX0, hi, double.NaN);
                        }

                        if (xRight <= cellX0 + CutXRangeToleranceMm)
                            break;

                        double hAtRight = (isSyntheticPiece ? syntheticTopY : StripGeometry.EvaluateYOnSegment(topSeg, xRight))
                            - StripGeometry.EvaluateYOnSegment(soilSpan.Segment, xRight);
                        bool sloped = IsSoilSloped(soilSpan.Segment);
                        double effMax = sloped ? bottomSlabMaxHeightMm * 2.0 : bottomSlabMaxHeightMm;

                        TryEmitCell(
                            cellX0, xRight, topSeg, soilSpan.Segment, soilSpans,
                            hAtRight, sloped, effMax, stripIndex, ref cellId,
                            bottomCells, diagnostics, triggered: true,
                            options: options,
                            syntheticTopY: isSyntheticPiece ? syntheticTopY : (double?)null);

                        stripIndex++;
                        cellX0 = xRight;

                        double topEndX = GetForwardEndX(topSeg, cellX0 - CutXRangeToleranceMm);
                        double soilEndX = GetForwardEndX(soilSpan.Segment, cellX0 - CutXRangeToleranceMm);
                        double segmentEndX = Math.Min(topEndX, soilEndX);

                        if (cellX0 >= segmentEndX - CutXRangeToleranceMm)
                        {
                            bool topEndedFirst = topEndX <= soilEndX + CutXRangeToleranceMm;
                            lockedTopSeg = null;
                            lockedSoilSpan = null;
                            maxTopY = double.NegativeInfinity;

                            if (topEndedFirst)
                            {
                                x = cellX0;
                                continue;
                            }

                            break;
                        }

                        continue;
                    }

                    var hits = GetVerticalBoundaryHits(boundaryEdges, x);
                    if (hits.Count < 2)
                    {
                        x += marchStep;
                        continue;
                    }

                    var hit0 = hits[0];
                    var hit1 = hits[1];

                    if (hit0.Role != BoundaryEdgeRole.Soil)
                    {
                        x += marchStep;
                        continue;
                    }

                    if (hit1.Role == BoundaryEdgeRole.Soil)
                    {
                        diagnostics.Add(new BaseStripDiagnostic
                        {
                            StripIndex = stripIndex,
                            X0 = x,
                            X1 = x,
                            Xm = x,
                            IsSoilContact = true,
                            SkippedSoilSoil = true,
                            ProbeTag = "SoilSoil跳过"
                        });
                        x += marchStep;
                        continue;
                    }

                    double candidateH = hit1.Point.Y - hit0.Point.Y;
                    bool isSloped = IsSoilSloped(hit0.Segment);
                    double effectiveMaxH = isSloped ? bottomSlabMaxHeightMm * 2.0 : bottomSlabMaxHeightMm;

                    if (candidateH > effectiveMaxH)
                    {
                        maxTopY = Math.Max(maxTopY, hit1.Point.Y);
                        diagnostics.Add(new BaseStripDiagnostic
                        {
                            StripIndex = stripIndex,
                            X0 = x,
                            X1 = x,
                            Xm = x,
                            IsSoilContact = true,
                            IsGrounded = true,
                            YBot = hit0.Point.Y,
                            YTop1 = hit1.Point.Y,
                            CandidateHeightMm = candidateH,
                            IsSloped = isSloped,
                            EffectiveMaxHeightMm = effectiveMaxH,
                            TriggeredNonSoilTop = true,
                            SkippedTooThick = true,
                            ProbeTag = "超厚跳过"
                        });
                        x += marchStep;
                        continue;
                    }

                    if (candidateH < StripGeometry.MinIntervalHeightMm)
                    {
                        x += marchStep;
                        continue;
                    }

                    diagnostics.Add(new BaseStripDiagnostic
                    {
                        StripIndex = stripIndex,
                        X0 = x,
                        X1 = x,
                        Xm = x,
                        IsSoilContact = true,
                        IsGrounded = true,
                        YBot = hit0.Point.Y,
                        YTop1 = hit1.Point.Y,
                        CandidateHeightMm = candidateH,
                        IsSloped = isSloped,
                        EffectiveMaxHeightMm = effectiveMaxH,
                        TriggeredNonSoilTop = true,
                        SkippedTooThick = false,
                        ProbeTag = "TopSeg触发"
                    });

                    lockedTopSeg = hit1.Segment;
                    lockedSoilSpan = span;
                    if (cellX0 < spanLo - CutXRangeToleranceMm)
                        cellX0 = spanLo;

                    double artificialTopY = maxTopY > double.NegativeInfinity
                        ? maxTopY
                        : hit1.Point.Y;

                    pendingTriggerX = x;
                    if (x > cellX0 + CutXRangeToleranceMm && maxTopY > double.NegativeInfinity)
                    {
                        emitSyntheticFirst = true;
                        pendingSyntheticTopY = artificialTopY;
                    }

                    maxTopY = double.NegativeInfinity;
                    continue;
                }
            }
        }

        private static double GetCellRightX(
            Line2D topSeg,
            Line2D soilSeg,
            double cellX0,
            double hi,
            double _)
        {
            double topForwardX = GetForwardEndX(topSeg, cellX0);
            double soilForwardX = GetForwardEndX(soilSeg, cellX0);
            double xRight = Math.Min(topForwardX, soilForwardX);
            return Math.Min(xRight, hi);
        }

        private static void TryEmitCell(
            double x0,
            double x1,
            Line2D topSeg,
            Line2D botSeg,
            IReadOnlyList<SoilSpan> soilSpans,
            double candidateH,
            bool isSloped,
            double effectiveMaxH,
            int stripIndex,
            ref int cellId,
            List<BaseSlabCell> bottomCells,
            List<BaseStripDiagnostic> diagnostics,
            bool triggered,
            BasePartitionOptions options = null,
            double? syntheticTopY = null)
        {
            if (x1 - x0 < StripGeometry.MinStripWidthMm)
                return;

            double topL = syntheticTopY ?? StripGeometry.EvaluateYOnSegment(topSeg, x0);
            double topR = syntheticTopY ?? StripGeometry.EvaluateYOnSegment(topSeg, x1);

            if (!TryGetSoilBottomY(x0, soilSpans, out double botL, out _))
                botL = StripGeometry.EvaluateYOnSegment(botSeg, x0);
            if (!TryGetSoilBottomY(x1, soilSpans, out double botR, out _))
                botR = StripGeometry.EvaluateYOnSegment(botSeg, x1);

            double thickness = Math.Min(topL, topR) - Math.Min(botL, botR);
            if (thickness < StripGeometry.MinIntervalHeightMm)
                return;

            if (!syntheticTopY.HasValue && (candidateH > effectiveMaxH || candidateH < StripGeometry.MinIntervalHeightMm))
                return;

            bool isSynthetic = syntheticTopY.HasValue;
            double syntheticLen = isSynthetic ? x1 - x0 : 0.0;
            ComponentType kind = isSynthetic
                ? ClassifyBySyntheticLength(syntheticLen, options)
                : ComponentType.BottomSlab;

            var diag = new BaseStripDiagnostic
            {
                StripIndex = stripIndex,
                X0 = x0,
                X1 = x1,
                Xm = (x0 + x1) / 2.0,
                IsSoilContact = true,
                IsGrounded = true,
                YBot = Math.Min(botL, botR),
                YTop1 = Math.Max(topL, topR),
                CandidateHeightMm = thickness,
                IsSloped = isSloped,
                EffectiveMaxHeightMm = effectiveMaxH,
                TriggeredNonSoilTop = triggered,
                CutY = (topL + topR) / 2.0,
                ThicknessMm = thickness,
                HasBottomSlab = true,
                IsTopSynthetic = isSynthetic,
                SyntheticTopLengthMm = syntheticLen,
                Kind = kind,
                ProbeTag = isSynthetic ? "人工顶边" : "底板"
            };

            bottomCells.Add(new BaseSlabCell
            {
                Id = cellId++,
                StripIndex = stripIndex,
                X0 = x0,
                X1 = x1,
                TopL = topL,
                TopR = topR,
                BotL = botL,
                BotR = botR,
                ThicknessMm = thickness,
                IsTopSynthetic = isSynthetic,
                SyntheticTopLengthMm = syntheticLen,
                Kind = kind
            });

            diagnostics.Add(diag);
        }

        private static BasePartitionOptions NormalizeOptions(BasePartitionOptions options)
        {
            if (options == null)
                options = new BasePartitionOptions();

            if (options.BottomSlabMaxMm < StripGeometry.MinIntervalHeightMm)
                options.BottomSlabMaxMm = 1500.0;

            if (options.MarchStepMm < 1.0)
                options.MarchStepMm = 1.0;

            if (options.WallMaxMm <= 0)
                options.WallMaxMm = 500.0;

            if (options.MassMinMm <= 0)
                options.MassMinMm = 1000.0;

            return options;
        }

        private static ComponentType ClassifyBySyntheticLength(
            double lengthMm,
            BasePartitionOptions options)
        {
            if (options == null)
                return ComponentType.BottomSlab;

            if (lengthMm < options.WallMaxMm)
                return ComponentType.Wall;

            if (lengthMm > options.MassMinMm)
                return ComponentType.MassConcrete;

            return ComponentType.BottomSlab;
        }

        private readonly struct BoundaryHit
        {
            public BoundaryHit(Point2D point, Line2D segment, BoundaryEdgeRole role, int segmentIndex)
            {
                Point = point;
                Segment = segment;
                Role = role;
                SegmentIndex = segmentIndex;
            }

            public Point2D Point { get; }
            public Line2D Segment { get; }
            public BoundaryEdgeRole Role { get; }
            public int SegmentIndex { get; }
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
                double x1 = seg.StartPoint.X, x2 = seg.EndPoint.X;
                double minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);
                if (x <= minX + IntersectionEpsilon || x >= maxX - IntersectionEpsilon)
                    continue;

                double y = StripGeometry.EvaluateYOnSegment(seg, x);
                hits.Add(new BoundaryHit(new Point2D(x, y), seg, ce.Role, ce.SegmentIndex));
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

        private static double GetForwardEndX(Line2D seg, double probe)
        {
            double startX = seg.StartPoint.X;
            double endX = seg.EndPoint.X;
            bool startForward = startX > probe + CutXRangeToleranceMm;
            bool endForward = endX > probe + CutXRangeToleranceMm;

            if (startForward && endForward)
                return Math.Max(startX, endX);
            if (endForward)
                return endX;
            if (startForward)
                return startX;

            return Math.Max(startX, endX);
        }

        private static List<SoilSpan> BuildSoilSpans(IReadOnlyList<ClassifiedBoundaryEdge> boundaryEdges)
        {
            var spans = new List<SoilSpan>();
            if (boundaryEdges == null)
                return spans;

            foreach (var edge in boundaryEdges)
            {
                if (edge?.Edge == null || edge.Role != BoundaryEdgeRole.Soil)
                    continue;

                var seg = edge.Edge;
                double dx = Math.Abs(seg.EndPoint.X - seg.StartPoint.X);
                if (dx <= VerticalSegmentToleranceMm)
                    continue;

                spans.Add(new SoilSpan(
                    Math.Min(seg.StartPoint.X, seg.EndPoint.X),
                    Math.Max(seg.StartPoint.X, seg.EndPoint.X),
                    seg,
                    edge.SegmentIndex));
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
            public SoilSpan(double minX, double maxX, Line2D segment, int segmentIndex)
            {
                MinX = minX;
                MaxX = maxX;
                Segment = segment;
                SegmentIndex = segmentIndex;
            }

            public double MinX { get; }
            public double MaxX { get; }
            public Line2D Segment { get; }
            public int SegmentIndex { get; }
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
