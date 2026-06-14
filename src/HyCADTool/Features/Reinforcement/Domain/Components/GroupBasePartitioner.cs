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
        public bool ClampedByNeighbor { get; set; }
        public bool HasBottomSlab { get; set; }
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

    /// <summary>基础底板梯形 cell（条带内 [y_bot, cut]）。</summary>
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
    /// 落地门控：最下区间底边须在土壤接触侧（BotSeg 中点 y ≤ cutY）。
    /// </summary>
    public static class GroupBasePartitioner
    {
        private const double SoilContactToleranceMm = 1.0;

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

                results.Add(PartitionFoundationGraphic(
                    g,
                    groupRegions[foundationIndex],
                    profile.CutY,
                    foundationIndex,
                    bottomSlabMaxHeightMm));
            }

            return results;
        }

        public static GroupBasePartitionResult PartitionFoundationGraphic(
            int groupIndex,
            ReinRegion region,
            double cutLineY,
            int foundationRegionIndex,
            double bottomSlabMaxHeightMm)
        {
            var diagnostics = new List<BaseStripDiagnostic>();
            var bottomCells = new List<BaseSlabCell>();

            if (region?.Outer == null || region.Outer.VertexCount < 3 || double.IsNaN(cutLineY))
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

            var segments = StripGeometry.CollectSegments(region);
            var breakpoints = StripGeometry.CollectBreakpoints(region);
            if (breakpoints.Count < 2 || segments.Count == 0)
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

            int stripCount = breakpoints.Count - 1;
            var cutAtStrip = new double?[stripCount];
            var stripSamples = new List<(int Index, double X0, double X1, double Xm, StripInsideInterval Iv, double MaxBot, double MaxTop)>();
            int cellId = 0;

            for (int i = 0; i < stripCount; i++)
            {
                double x0 = breakpoints[i];
                double x1 = breakpoints[i + 1];
                if (x1 - x0 < StripGeometry.MinStripWidthMm)
                    continue;

                double xm = (x0 + x1) / 2.0;
                var intervals = StripGeometry.GetInsideIntervalsAt(region, segments, xm);
                if (intervals.Count == 0)
                    continue;

                var iv = intervals[intervals.Count - 1];
                double topL = StripGeometry.EvaluateYOnSegment(iv.TopSeg, x0);
                double topR = StripGeometry.EvaluateYOnSegment(iv.TopSeg, x1);
                double botL = StripGeometry.EvaluateYOnSegment(iv.BotSeg, x0);
                double botR = StripGeometry.EvaluateYOnSegment(iv.BotSeg, x1);
                double maxBot = Math.Max(botL, botR);
                double maxTop = Math.Max(topL, topR);
                double botMidY = StripGeometry.EvaluateYOnSegment(iv.BotSeg, xm);

                bool isSoil = botMidY <= cutLineY + SoilContactToleranceMm;
                double candidateH = maxTop - maxBot;

                var diag = new BaseStripDiagnostic
                {
                    StripIndex = i,
                    X0 = x0,
                    X1 = x1,
                    Xm = xm,
                    IsSoilContact = isSoil,
                    IsGrounded = isSoil,
                    YBot = maxBot,
                    YTop1 = maxTop,
                    CandidateHeightMm = candidateH
                };

                if (isSoil)
                {
                    if (candidateH <= bottomSlabMaxHeightMm)
                    {
                        cutAtStrip[i] = maxTop;
                        diag.ClampedByNeighbor = false;
                    }
                    else
                    {
                        cutAtStrip[i] = null;
                        diag.ClampedByNeighbor = true;
                    }
                }

                stripSamples.Add((i, x0, x1, xm, iv, maxBot, maxTop));
                diagnostics.Add(diag);
            }

            // 真实底板快照：仅含第一遍 H≤h_b 落地的条带，供高条带邻接拉通参照，
            // 避免被第二遍写回的"已夹高条带 cut"污染传播。
            var genuineCut = (double?[])cutAtStrip.Clone();

            foreach (var sample in stripSamples)
            {
                var diag = diagnostics.First(d => d.StripIndex == sample.Index);
                if (!diag.IsGrounded)
                    continue;

                double maxBot = sample.MaxBot;
                double maxTop = sample.MaxTop;
                double cut;

                if (cutAtStrip[sample.Index].HasValue)
                {
                    cut = cutAtStrip[sample.Index].Value;
                }
                else
                {
                    StripGeometry.FindNeighborBottomTops(genuineCut, sample.Index, out double? lt, out double? rt);

                    // 拉通厚度 = 邻接真实底板 cut - 本高列 maxBot。
                    // 超过 h_b 的侧是大体积/墙方向（需被拉通），放弃比较；仅保留真实基础高度侧。
                    double? leftCut = lt.HasValue && lt.Value - maxBot <= bottomSlabMaxHeightMm ? lt : null;
                    double? rightCut = rt.HasValue && rt.Value - maxBot <= bottomSlabMaxHeightMm ? rt : null;

                    if (leftCut.HasValue && rightCut.HasValue)
                        cut = Math.Max(leftCut.Value, rightCut.Value);
                    else if (leftCut.HasValue)
                        cut = leftCut.Value;
                    else if (rightCut.HasValue)
                        cut = rightCut.Value;
                    else
                        cut = maxBot + bottomSlabMaxHeightMm;

                    if (cut > maxTop)
                        cut = maxTop;
                    if (cut < maxBot)
                        cut = maxBot;

                    cutAtStrip[sample.Index] = cut;
                    diag.ClampedByNeighbor = true;
                }

                diag.CutY = cut;
                diag.ThicknessMm = cut - maxBot;

                if (cut - maxBot >= StripGeometry.MinIntervalHeightMm)
                {
                    double topL = cut;
                    double topR = cut;
                    double botL = StripGeometry.EvaluateYOnSegment(sample.Iv.BotSeg, sample.X0);
                    double botR = StripGeometry.EvaluateYOnSegment(sample.Iv.BotSeg, sample.X1);

                    bottomCells.Add(new BaseSlabCell
                    {
                        Id = cellId++,
                        StripIndex = sample.Index,
                        X0 = sample.X0,
                        X1 = sample.X1,
                        TopL = topL,
                        TopR = topR,
                        BotL = botL,
                        BotR = botR,
                        ThicknessMm = diag.ThicknessMm
                    });

                    diag.HasBottomSlab = true;
                }
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
