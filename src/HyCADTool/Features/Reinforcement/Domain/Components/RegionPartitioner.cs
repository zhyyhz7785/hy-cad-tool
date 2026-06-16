using HyCAD.Geometry;
using HyCAD.Geometry.Algorithms;
using HyCADTool.Features.Reinforcement.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>边方向分类（梁判定预备）。</summary>
    public enum PartitionEdgeClass
    {
        HorizontalA,
        VerticalB,
        SlabCa,
        WallCb
    }

    /// <summary>割线扫描分区结果。</summary>
    public sealed class PartitionRegion
    {
        public ComponentType Type { get; set; }
        public Polygon2D Polygon { get; set; }
        public double ThicknessMm { get; set; }
    }

    /// <summary>
    /// 竖直割线扫描分区 — 四阶段流水线（基础 → 墙 → 板/梁 → 大体积）。
    ///
    /// 阶段0：X 断点中点割线 + gap 内外测试 → 梯形 cell。
    /// 阶段1：最下区间统一 cut 剖基础；[cut,顶] → protrusionPool / floatingCells（非落地板系路由）。
    /// 阶段2：protrusionPool 高度带 + 轮廓量宽 → 墙 / 大体积池。
    /// 阶段3：板组薄厚拆分；厚列下凸 → 梁(窄) / 大体积池(宽)。
    /// 阶段4：大体积池收尾 + 局部贴大体积升级。
    /// </summary>
    public static class RegionPartitioner
    {
        private const double MinStripWidthMm = 0.5;
        private const double DedupeYToleranceMm = 0.01;
        private const double MinIntervalHeightMm = 1.0;
        private const double MergeOverlapMm = 1.0;
        private const double IntersectionEpsilon = 1e-9;

        private const double HorizontalAngleDeg = 5.0;
        private const double VerticalAngleDeg = 85.0;
        private const double SlabWallSplitDeg = 45.0;

        private const double DefaultBottomSlabMaxHeightMm = 1500.0;
        private const double DefaultWallMaxWidthMm = 500.0;
        private const double DefaultAnchorageMm = 500.0;
        private const double DefaultSlabMaxThicknessMm = 300.0;
        private const double DefaultBeamMaxWidthMm = 800.0;
        private const double DefaultLocalConcreteMaxHeightMm = 1000.0;
        /// <summary>墙带最小高度：低于该值不作墙，归大体积混凝土。</summary>
        private const double WallMinHeightMm = 500.0;

        private sealed class StripBottomInfo
        {
            public int StripIndex;
            public double X0, X1;
            public InsideInterval Iv;
        }

        private enum CellKind
        {
            Slab,
            BottomSlab,
            Raised
        }

        private sealed class TrapezoidCell
        {
            public int Id;
            public int StripIndex;
            public CellKind Kind;
            public double TopL, TopR, BotL, BotR;
            public double X0, X1;
            public double ThicknessMm;

            // 单区间列的原始列底（墙取全高时恢复用）
            public double OrigBotL, OrigBotR;

            public double MaxTop => Math.Max(TopL, TopR);
            public double MinBot => Math.Min(BotL, BotR);

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

        /// <summary>与 X 轴夹角归一化到 [0°, 90°] 后边分类。</summary>
        public static PartitionEdgeClass ClassifyEdge(Line2D seg)
        {
            if (!seg.Direction.TryNormalize(out Vector2D dir))
                return PartitionEdgeClass.SlabCa;

            double angle = Math.Abs(Math.Atan2(dir.Y, dir.X) * 180.0 / Math.PI);
            if (angle > 90.0)
                angle = 180.0 - angle;

            if (angle <= HorizontalAngleDeg)
                return PartitionEdgeClass.HorizontalA;
            if (angle >= VerticalAngleDeg)
                return PartitionEdgeClass.VerticalB;
            if (angle < SlabWallSplitDeg)
                return PartitionEdgeClass.SlabCa;
            return PartitionEdgeClass.WallCb;
        }

        /// <summary>对 ReinRegion 做四阶段构件分区。</summary>
        public static List<PartitionRegion> Partition(
            ReinRegion region,
            double bottomSlabMaxHeightMm = DefaultBottomSlabMaxHeightMm,
            double wallMaxWidthMm = DefaultWallMaxWidthMm,
            double anchorageMm = DefaultAnchorageMm,
            double slabMaxThicknessMm = DefaultSlabMaxThicknessMm,
            double beamMaxWidthMm = DefaultBeamMaxWidthMm,
            double localConcreteMaxHeightMm = DefaultLocalConcreteMaxHeightMm,
            double? groundY = null,
            bool mergeBumpsIntoBottomSlab = true,
            double? soilCutY = null,
            bool useVerticalEdgeWalls = false,
            double wallMinHeightForEdgeMm = 200.0,
            double parallelAngleThresholdDeg = 15.0,
            double parallelLineRatioMin = 0.6,
            List<WallColumnRect> detectedWallColumns = null)
        {
            var result = new List<PartitionRegion>();
            if (region?.Outer == null || region.Outer.VertexCount < 3)
                return result;

            if (bottomSlabMaxHeightMm < MinIntervalHeightMm)
                bottomSlabMaxHeightMm = DefaultBottomSlabMaxHeightMm;
            if (localConcreteMaxHeightMm < MinIntervalHeightMm)
                localConcreteMaxHeightMm = DefaultLocalConcreteMaxHeightMm;

            double regionMinY = GetRegionMinY(region);
            bool grounded = soilCutY.HasValue
                ? regionMinY <= soilCutY.Value
                : !groundY.HasValue || regionMinY <= groundY.Value + bottomSlabMaxHeightMm;

            var segments = CollectSegments(region);
            if (segments.Count == 0)
                return result;

            var breakpoints = CollectBreakpoints(region);
            if (breakpoints.Count < 2)
                return result;

            int stripCount = breakpoints.Count - 1;
            var intervalsAtStrip = new List<InsideInterval>[stripCount];
            var stripBottoms = new List<StripBottomInfo>();
            var upperSlabCells = new List<TrapezoidCell>();
            var bottomTopAtStrip = new double?[stripCount];
            int cellId = 0;

            // ---------------- 阶段0：条带采样 ----------------
            for (int i = 0; i < stripCount; i++)
            {
                double x0 = breakpoints[i];
                double x1 = breakpoints[i + 1];
                if (x1 - x0 < MinStripWidthMm)
                    continue;

                double xm = (x0 + x1) / 2.0;
                var intervals = GetInsideIntervalsAt(region, segments, xm);
                intervalsAtStrip[i] = intervals;
                if (intervals.Count == 0)
                    continue;

                for (int k = 0; k < intervals.Count - 1; k++)
                {
                    var cell = BuildCell(ref cellId, i, CellKind.Slab, x0, x1, intervals[k]);
                    if (cell != null)
                        upperSlabCells.Add(cell);
                }

                stripBottoms.Add(new StripBottomInfo
                {
                    StripIndex = i,
                    X0 = x0,
                    X1 = x1,
                    Iv = intervals[intervals.Count - 1]
                });
            }

            if (stripBottoms.Count == 0)
                return result;

            // ---------------- 阶段1：统一基础切割 ----------------
            var bottomSlabCells = new List<TrapezoidCell>();
            var protrusionPool = new List<TrapezoidCell>();
            var floatingCells = new List<TrapezoidCell>();
            var shortBumps = new List<TrapezoidCell>();
            var slabCellAtStrip = new Dictionary<int, TrapezoidCell>();
            var bareStripTop = new Dictionary<int, double>();

            // 预填各条带底板顶（供邻近桥接 cut 使用）
            foreach (var sb in stripBottoms)
            {
                double topL = EvaluateYOnSegment(sb.Iv.TopSeg, sb.X0);
                double topR = EvaluateYOnSegment(sb.Iv.TopSeg, sb.X1);
                double botL = EvaluateYOnSegment(sb.Iv.BotSeg, sb.X0);
                double botR = EvaluateYOnSegment(sb.Iv.BotSeg, sb.X1);
                double maxBot = Math.Max(botL, botR);
                double maxTop = Math.Max(topL, topR);
                double bottomCeiling = soilCutY ?? (maxBot + bottomSlabMaxHeightMm);
                bottomTopAtStrip[sb.StripIndex] = Math.Min(maxTop, bottomCeiling);
            }

            foreach (var sb in stripBottoms)
            {
                int i = sb.StripIndex;
                double x0 = sb.X0, x1 = sb.X1;
                var iv = sb.Iv;

                double topL = EvaluateYOnSegment(iv.TopSeg, x0);
                double topR = EvaluateYOnSegment(iv.TopSeg, x1);
                double botL = EvaluateYOnSegment(iv.BotSeg, x0);
                double botR = EvaluateYOnSegment(iv.BotSeg, x1);
                double maxBot = Math.Max(botL, botR);
                double maxTop = Math.Max(topL, topR);

                double cut;
                if (grounded)
                {
                    FindNeighborBottomTops(bottomTopAtStrip, i, out double? lt, out double? rt);
                    double bottomCeiling = soilCutY ?? (maxBot + bottomSlabMaxHeightMm);
                    double refTop = lt.HasValue && rt.HasValue
                        ? Math.Min(lt.Value, rt.Value)
                        : (lt ?? rt ?? bottomCeiling);

                    cut = Math.Min(refTop, bottomCeiling);
                    if (cut > maxTop)
                        cut = maxTop;
                    // 下限夹紧：cut 不得低于本条带自身底面，否则深坑邻列把 cut 拖到
                    // 真实底以下 → 不生成底板 cell + bumpHeight 虚高跳过矮凸起合并（单侧洋红/孤洞）
                    if (cut < maxBot)
                        cut = maxBot;
                }
                else
                {
                    cut = maxBot;
                }

                if (grounded && cut - maxBot >= MinIntervalHeightMm)
                {
                    var slabCell = new TrapezoidCell
                    {
                        Id = cellId++,
                        StripIndex = i,
                        Kind = CellKind.BottomSlab,
                        X0 = x0,
                        X1 = x1,
                        TopL = cut,
                        TopR = cut,
                        BotL = botL,
                        BotR = botR,
                        OrigBotL = botL,
                        OrigBotR = botR,
                        ThicknessMm = cut - (botL + botR) / 2.0
                    };
                    bottomSlabCells.Add(slabCell);
                    slabCellAtStrip[i] = slabCell;
                    bareStripTop[i] = cut;
                    bottomTopAtStrip[i] = cut;
                }

                if (topL - cut >= MinIntervalHeightMm || topR - cut >= MinIntervalHeightMm)
                {
                    var raisedCell = new TrapezoidCell
                    {
                        Id = cellId++,
                        StripIndex = i,
                        Kind = CellKind.Raised,
                        X0 = x0,
                        X1 = x1,
                        TopL = topL,
                        TopR = topR,
                        BotL = cut,
                        BotR = cut,
                        OrigBotL = botL,
                        OrigBotR = botR,
                        ThicknessMm = ((topL - cut) + (topR - cut)) / 2.0
                    };

                    if (!grounded)
                    {
                        floatingCells.Add(raisedCell);
                    }
                    else
                    {
                        double bumpHeight = Math.Max(topL, topR) - cut;
                        bool shortFeature = bumpHeight < localConcreteMaxHeightMm;
                        bool toShort = shortFeature
                            && (slabCellAtStrip.ContainsKey(i) || mergeBumpsIntoBottomSlab);
                        if (toShort)
                            shortBumps.Add(raisedCell);
                        else
                            protrusionPool.Add(raisedCell);
                    }
                }
            }

            // 非落地列预分组：含薄列证据 → 板系（阶段3）；全厚列 → 墙/大体积
            foreach (var group in MergeCells(floatingCells))
            {
                if (group.Any(c => c.ThicknessMm <= slabMaxThicknessMm))
                {
                    foreach (var c in group)
                        c.Kind = CellKind.Slab;
                    upperSlabCells.AddRange(group);
                }
                else
                {
                    protrusionPool.AddRange(group);
                }
            }

            ProcessShortBottomBumps(
                mergeBumpsIntoBottomSlab,
                shortBumps,
                bottomSlabCells,
                slabCellAtStrip,
                bareStripTop,
                protrusionPool,
                result,
                ref cellId);

            // Y 断点（高度带剖分）
            var yBreakSet = new SortedSet<double>();
            for (int i = 0; i < stripCount; i++)
            {
                var ivs = intervalsAtStrip[i];
                if (ivs == null)
                    continue;

                foreach (var iv in ivs)
                {
                    yBreakSet.Add(iv.Top);
                    yBreakSet.Add(iv.Bottom);
                }
            }

            var yBreaks = yBreakSet.ToList();

            var cellsByStrip = upperSlabCells
                .Concat(bottomSlabCells)
                .GroupBy(c => c.StripIndex)
                .ToDictionary(g => g.Key, g => g.ToList());

            var wallCandidates = new List<TrapezoidCell>();
            var wallCells = new List<TrapezoidCell>();
            var massPool = new List<TrapezoidCell>();
            var slabPieces = new List<TrapezoidCell>();

            // 阶段1 收尾：矮凸起已在 ProcessShortBottomBumps 处理；其余 → 墙/大体积候选
            foreach (var group in MergeCells(protrusionPool))
            {
                double height = group.Max(c => c.MaxTop) - group.Min(c => c.MinBot);
                if (height < localConcreteMaxHeightMm)
                {
                    EmitGroup(result, group, ComponentType.LocalConcrete,
                        group.Average(c => c.ThicknessMm));
                }
                else
                {
                    wallCandidates.AddRange(group);
                }
            }

            // ---------------- 阶段2：墙体 ----------------
            if (useVerticalEdgeWalls)
            {
                var wallColumns = DetectWallColumnsByVerticalEdges(
                    segments, region, wallCandidates, wallMaxWidthMm, wallMinHeightForEdgeMm,
                    parallelAngleThresholdDeg, parallelLineRatioMin);
                if (detectedWallColumns != null)
                    detectedWallColumns.AddRange(wallColumns);

                ApplyVerticalEdgeWallsToCandidates(
                    wallCandidates, wallColumns, ref cellId, wallCells, massPool);
            }
            else
            {
                foreach (var cell in wallCandidates)
                {
                    SplitRaisedCellByWidth(
                        cell, yBreaks, region, segments, breakpoints, cellsByStrip,
                        wallMaxWidthMm, anchorageMm, localConcreteMaxHeightMm, ref cellId,
                        wallCells, massPool, slabPieces, allowSlabBands: true);
                }
            }

            foreach (var group in MergeCells(wallCells))
            {
                double width = group.Max(c => c.X1) - group.Min(c => c.X0);
                EmitGroup(result, group, ComponentType.Wall, width);
            }

            // ---------------- 阶段3：板 + 梁 ----------------
            var allSlabCells = upperSlabCells.Concat(slabPieces).ToList();
            var beamCells = new List<TrapezoidCell>();
            ProcessSlabGroups(
                allSlabCells, slabMaxThicknessMm, beamMaxWidthMm, anchorageMm,
                ref cellId, result, massPool, beamCells);

            foreach (var group in MergeCells(beamCells))
            {
                double width = group.Max(c => c.X1) - group.Min(c => c.X0);
                EmitGroup(result, group, ComponentType.Beam, width);
            }

            // ---------------- 阶段4：大体积 / 局部收尾 ----------------
            EmitMassAndLocal(result, massPool, localConcreteMaxHeightMm);

            foreach (var group in MergeCells(bottomSlabCells))
            {
                EmitGroup(result, group, ComponentType.BottomSlab,
                    group.Average(c => c.ThicknessMm));
            }

            return result;
        }

        // ===================================================================
        //  cell 构建与合并
        // ===================================================================

        private static TrapezoidCell BuildCell(
            ref int cellId,
            int stripIndex,
            CellKind kind,
            double x0,
            double x1,
            InsideInterval iv)
        {
            double topL = EvaluateYOnSegment(iv.TopSeg, x0);
            double topR = EvaluateYOnSegment(iv.TopSeg, x1);
            double botL = EvaluateYOnSegment(iv.BotSeg, x0);
            double botR = EvaluateYOnSegment(iv.BotSeg, x1);

            if (topL - botL < MinIntervalHeightMm && topR - botR < MinIntervalHeightMm)
                return null;

            return new TrapezoidCell
            {
                Id = cellId++,
                StripIndex = stripIndex,
                Kind = kind,
                X0 = x0,
                X1 = x1,
                TopL = topL,
                TopR = topR,
                BotL = botL,
                BotR = botR,
                OrigBotL = botL,
                OrigBotR = botR,
                ThicknessMm = ((topL - botL) + (topR - botR)) / 2.0
            };
        }

        private static void ProcessShortBottomBumps(
            bool mergeBumpsIntoBottomSlab,
            List<TrapezoidCell> shortBumps,
            List<TrapezoidCell> bottomSlabCells,
            Dictionary<int, TrapezoidCell> slabCellAtStrip,
            Dictionary<int, double> bareStripTop,
            List<TrapezoidCell> protrusionPool,
            List<PartitionRegion> result,
            ref int cellId)
        {
            if (shortBumps.Count == 0)
                return;

            var highStripIndices = new HashSet<int>(protrusionPool.Select(p => p.StripIndex));

            if (mergeBumpsIntoBottomSlab)
            {
                foreach (var bump in shortBumps)
                {
                    if (slabCellAtStrip.TryGetValue(bump.StripIndex, out var slab))
                    {
                        slab.TopL = bump.TopL;
                        slab.TopR = bump.TopR;
                        slab.ThicknessMm = ((slab.TopL - slab.BotL) + (slab.TopR - slab.BotR)) / 2.0;
                        continue;
                    }

                    // 孤立矮凸起：底面取真实轮廓 OrigBot，新建底板 cell 补洞
                    var newSlab = new TrapezoidCell
                    {
                        Id = cellId++,
                        StripIndex = bump.StripIndex,
                        Kind = CellKind.BottomSlab,
                        X0 = bump.X0,
                        X1 = bump.X1,
                        TopL = bump.TopL,
                        TopR = bump.TopR,
                        BotL = bump.OrigBotL,
                        BotR = bump.OrigBotR,
                        OrigBotL = bump.OrigBotL,
                        OrigBotR = bump.OrigBotR,
                        ThicknessMm = ((bump.TopL - bump.OrigBotL) + (bump.TopR - bump.OrigBotR)) / 2.0
                    };
                    bottomSlabCells.Add(newSlab);
                    slabCellAtStrip[bump.StripIndex] = newSlab;
                    bareStripTop[bump.StripIndex] = newSlab.MaxTop;
                }

                return;
            }

            foreach (var run in MergeCells(bottomSlabCells))
            {
                var runStrips = new HashSet<int>(run.Select(c => c.StripIndex));
                double runMinTop = double.MaxValue;

                foreach (var cell in run)
                {
                    if (highStripIndices.Contains(cell.StripIndex))
                        continue;

                    if (bareStripTop.TryGetValue(cell.StripIndex, out double bare))
                        runMinTop = Math.Min(runMinTop, bare);
                }

                foreach (var bump in shortBumps)
                {
                    if (!runStrips.Contains(bump.StripIndex))
                        continue;

                    if (highStripIndices.Contains(bump.StripIndex))
                        continue;

                    runMinTop = Math.Min(runMinTop, bump.MaxTop);
                }

                if (runMinTop == double.MaxValue)
                    continue;

                var bumpStripsHandled = new HashSet<int>();

                foreach (var bump in shortBumps)
                {
                    if (!runStrips.Contains(bump.StripIndex))
                        continue;

                    if (highStripIndices.Contains(bump.StripIndex))
                        continue;

                    if (!slabCellAtStrip.TryGetValue(bump.StripIndex, out var slab))
                        continue;

                    // 逐侧夹紧：底板顶不得低于底面（斜坡过渡段防自交）
                    double newTopL = Math.Max(runMinTop, slab.BotL);
                    double newTopR = Math.Max(runMinTop, slab.BotR);
                    slab.TopL = newTopL;
                    slab.TopR = newTopR;
                    slab.ThicknessMm = ((newTopL - slab.BotL) + (newTopR - slab.BotR)) / 2.0;
                    bumpStripsHandled.Add(bump.StripIndex);

                    double avgH = ((bump.TopL - newTopL) + (bump.TopR - newTopR)) / 2.0;
                    if (avgH >= MinIntervalHeightMm)
                    {
                        EmitGroup(result, new List<TrapezoidCell>
                        {
                            new TrapezoidCell
                            {
                                StripIndex = bump.StripIndex,
                                Kind = CellKind.Raised,
                                X0 = bump.X0,
                                X1 = bump.X1,
                                TopL = bump.TopL,
                                TopR = bump.TopR,
                                BotL = newTopL,
                                BotR = newTopR,
                                ThicknessMm = avgH
                            }
                        }, ComponentType.LocalConcrete, avgH);
                    }
                }

                foreach (var cell in run)
                {
                    if (highStripIndices.Contains(cell.StripIndex))
                        continue;

                    if (bumpStripsHandled.Contains(cell.StripIndex))
                        continue;

                    if (!bareStripTop.TryGetValue(cell.StripIndex, out double bare))
                        continue;

                    double origTopL = cell.TopL;
                    double origTopR = cell.TopR;
                    double newTopL = Math.Max(runMinTop, cell.BotL);
                    double newTopR = Math.Max(runMinTop, cell.BotR);
                    cell.TopL = newTopL;
                    cell.TopR = newTopR;
                    cell.ThicknessMm = ((newTopL - cell.BotL) + (newTopR - cell.BotR)) / 2.0;

                    double avgH = ((origTopL - newTopL) + (origTopR - newTopR)) / 2.0;
                    if (avgH >= MinIntervalHeightMm)
                    {
                        EmitGroup(result, new List<TrapezoidCell>
                        {
                            new TrapezoidCell
                            {
                                StripIndex = cell.StripIndex,
                                Kind = CellKind.Raised,
                                X0 = cell.X0,
                                X1 = cell.X1,
                                TopL = origTopL,
                                TopR = origTopR,
                                BotL = newTopL,
                                BotR = newTopR,
                                ThicknessMm = avgH
                            }
                        }, ComponentType.LocalConcrete, avgH);
                    }
                }
            }
        }

        private static void ProcessSlabGroups(
            List<TrapezoidCell> slabCells,
            double slabMaxThicknessMm,
            double beamMaxWidthMm,
            double anchorageMm,
            ref int cellId,
            List<PartitionRegion> result,
            List<TrapezoidCell> massPool,
            List<TrapezoidCell> beamCells)
        {
            foreach (var group in MergeCells(slabCells))
            {
                var thin = group.Where(c => c.ThicknessMm <= slabMaxThicknessMm).ToList();
                var thick = group.Where(c => c.ThicknessMm > slabMaxThicknessMm).ToList();

                if (thick.Count > 0 && thin.Count == 0)
                {
                    massPool.AddRange(group);
                    continue;
                }

                if (thick.Count == 0)
                {
                    EmitGroup(result, group, ComponentType.Slab,
                        group.Average(c => c.ThicknessMm));
                    continue;
                }

                // 薄列下边为板基线（板底标高）
                double baseline = thin.Max(c => c.MinBot);
                var stillSlab = new List<TrapezoidCell>(thin);
                var lowerPieces = new List<TrapezoidCell>();
                var topByLowerId = new Dictionary<int, TrapezoidCell>();

                foreach (var cell in thick)
                {
                    double topL = cell.TopL;
                    double topR = cell.TopR;
                    double botL = cell.BotL;
                    double botR = cell.BotR;
                    bool hasTop = Math.Max(topL, topR) - baseline >= MinIntervalHeightMm;
                    bool hasLower = baseline - Math.Min(botL, botR) >= MinIntervalHeightMm;

                    if (hasLower)
                    {
                        var lower = new TrapezoidCell
                        {
                            Id = cellId++,
                            StripIndex = cell.StripIndex,
                            Kind = CellKind.Raised,
                            X0 = cell.X0,
                            X1 = cell.X1,
                            TopL = baseline,
                            TopR = baseline,
                            BotL = botL,
                            BotR = botR,
                            OrigBotL = cell.OrigBotL,
                            OrigBotR = cell.OrigBotR,
                            ThicknessMm = baseline - (botL + botR) / 2.0
                        };
                        lowerPieces.Add(lower);

                        if (hasTop)
                        {
                            topByLowerId[lower.Id] = new TrapezoidCell
                            {
                                StripIndex = cell.StripIndex,
                                Kind = CellKind.Slab,
                                X0 = cell.X0,
                                X1 = cell.X1,
                                TopL = topL,
                                TopR = topR,
                                BotL = baseline,
                                BotR = baseline,
                                ThicknessMm = ((topL - baseline) + (topR - baseline)) / 2.0
                            };
                        }
                    }
                    else if (hasTop)
                    {
                        massPool.Add(CopyCell(ref cellId, new TrapezoidCell
                        {
                            StripIndex = cell.StripIndex,
                            Kind = CellKind.Raised,
                            X0 = cell.X0,
                            X1 = cell.X1,
                            TopL = topL,
                            TopR = topR,
                            BotL = baseline,
                            BotR = baseline,
                            ThicknessMm = ((topL - baseline) + (topR - baseline)) / 2.0
                        }, CellKind.Raised));
                    }
                }

                foreach (var sub in MergeCells(lowerPieces))
                {
                    bool isMass = TouchesAnyCell(sub, massPool)
                               || (sub.Max(c => c.X1) - sub.Min(c => c.X0)) > beamMaxWidthMm;

                    if (isMass)
                    {
                        massPool.AddRange(sub);
                        double mx0 = sub.Min(c => c.X0);
                        double mx1 = sub.Max(c => c.X1);
                        bool thinLeft = thin.Any(t => Math.Abs(t.X1 - mx0) < MinStripWidthMm);
                        bool thinRight = thin.Any(t => Math.Abs(t.X0 - mx1) < MinStripWidthMm);

                        foreach (var lc in sub)
                        {
                            if (!topByLowerId.TryGetValue(lc.Id, out var tp))
                                continue;

                            massPool.Add(CopyCell(ref cellId, tp, CellKind.Raised));

                            if (thinLeft)
                            {
                                var clipped = ClipCellX(tp, tp.X0, Math.Min(tp.X1, mx0 + anchorageMm));
                                if (clipped != null)
                                    stillSlab.Add(CopyCell(ref cellId, clipped, CellKind.Slab));
                            }

                            if (thinRight)
                            {
                                var clipped = ClipCellX(tp, Math.Max(tp.X0, mx1 - anchorageMm), tp.X1);
                                if (clipped != null)
                                    stillSlab.Add(CopyCell(ref cellId, clipped, CellKind.Slab));
                            }
                        }
                    }
                    else
                    {
                        beamCells.AddRange(sub);
                        foreach (var lc in sub)
                        {
                            if (topByLowerId.TryGetValue(lc.Id, out var tp))
                                stillSlab.Add(CopyCell(ref cellId, tp, CellKind.Slab));
                        }
                    }
                }

                foreach (var sub in MergeCells(stillSlab))
                {
                    EmitGroup(result, sub, ComponentType.Slab,
                        sub.Average(c => c.ThicknessMm));
                }
            }
        }

        private static TrapezoidCell CopyCell(ref int cellId, TrapezoidCell src, CellKind kind)
        {
            return new TrapezoidCell
            {
                Id = cellId++,
                StripIndex = src.StripIndex,
                Kind = kind,
                X0 = src.X0,
                X1 = src.X1,
                TopL = src.TopL,
                TopR = src.TopR,
                BotL = src.BotL,
                BotR = src.BotR,
                OrigBotL = src.OrigBotL,
                OrigBotR = src.OrigBotR,
                ThicknessMm = ((src.TopL - src.BotL) + (src.TopR - src.BotR)) / 2.0
            };
        }

        /// <summary>按新 X 界线裁剪梯形 cell（Top/Bot 线性插值）。</summary>
        private static TrapezoidCell ClipCellX(TrapezoidCell src, double newX0, double newX1)
        {
            if (newX1 - newX0 < MinStripWidthMm)
                return null;

            double topL = InterpolateYAtX(src.X0, src.TopL, src.X1, src.TopR, newX0);
            double topR = InterpolateYAtX(src.X0, src.TopL, src.X1, src.TopR, newX1);
            double botL = InterpolateYAtX(src.X0, src.BotL, src.X1, src.BotR, newX0);
            double botR = InterpolateYAtX(src.X0, src.BotL, src.X1, src.BotR, newX1);

            return new TrapezoidCell
            {
                StripIndex = src.StripIndex,
                Kind = src.Kind,
                X0 = newX0,
                X1 = newX1,
                TopL = topL,
                TopR = topR,
                BotL = botL,
                BotR = botR,
                OrigBotL = src.OrigBotL,
                OrigBotR = src.OrigBotR,
                ThicknessMm = ((topL - botL) + (topR - botR)) / 2.0
            };
        }

        private static double InterpolateYAtX(
            double x0, double y0, double x1, double y1, double x)
        {
            if (Math.Abs(x1 - x0) < IntersectionEpsilon)
                return (y0 + y1) / 2.0;

            double t = (x - x0) / (x1 - x0);
            if (t < 0) t = 0;
            else if (t > 1) t = 1;
            return y0 + t * (y1 - y0);
        }

        private static void EmitMassAndLocal(
            List<PartitionRegion> result,
            List<TrapezoidCell> massPool,
            double localConcreteMaxHeightMm)
        {
            var massGroups = new List<List<TrapezoidCell>>();
            var localGroups = new List<List<TrapezoidCell>>();

            foreach (var group in MergeCells(massPool))
            {
                double height = group.Max(c => c.MaxTop) - group.Min(c => c.MinBot);
                if (height >= localConcreteMaxHeightMm)
                    massGroups.Add(group);
                else
                    localGroups.Add(group);
            }

            bool upgraded = true;
            while (upgraded)
            {
                upgraded = false;
                for (int i = localGroups.Count - 1; i >= 0; i--)
                {
                    foreach (var mg in massGroups)
                    {
                        if (!TouchesAnyCell(localGroups[i], mg))
                            continue;

                        massGroups.Add(localGroups[i]);
                        localGroups.RemoveAt(i);
                        upgraded = true;
                        break;
                    }
                }
            }

            foreach (var group in massGroups)
            {
                EmitGroup(result, group, ComponentType.MassConcrete,
                    group.Average(c => c.ThicknessMm));
            }

            foreach (var group in localGroups)
            {
                EmitGroup(result, group, ComponentType.LocalConcrete,
                    group.Average(c => c.ThicknessMm));
            }
        }

        /// <summary>相邻条带 + 同 Kind + 共享边界 Y 重叠 → 并查集分量。</summary>
        private static List<List<TrapezoidCell>> MergeCells(List<TrapezoidCell> cells)
        {
            var groups = new List<List<TrapezoidCell>>();
            if (cells.Count == 0)
                return groups;

            var idMap = new Dictionary<int, int>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
                idMap[cells[i].Id] = i;

            var byStrip = cells
                .GroupBy(c => c.StripIndex)
                .ToDictionary(g => g.Key, g => g.ToList());

            var uf = new UnionFind(cells.Count);
            foreach (var cell in cells)
            {
                // 跳过的窄条带可能造成 StripIndex 不连续，向后探查到下一个有 cell 的条带
                for (int next = cell.StripIndex + 1; next < cell.StripIndex + 8; next++)
                {
                    if (!byStrip.TryGetValue(next, out var nextStrip))
                        continue;

                    foreach (var other in nextStrip)
                    {
                        if (other.Kind != cell.Kind)
                            continue;
                        if (Math.Abs(other.X0 - cell.X1) > MinStripWidthMm)
                            continue;

                        double overlap = Math.Min(cell.TopR, other.TopL)
                                       - Math.Max(cell.BotR, other.BotL);
                        if (overlap > MergeOverlapMm)
                            uf.Union(idMap[cell.Id], idMap[other.Id]);
                    }

                    break;
                }
            }

            var byRoot = new Dictionary<int, List<TrapezoidCell>>();
            for (int i = 0; i < cells.Count; i++)
            {
                int root = uf.Find(i);
                if (!byRoot.TryGetValue(root, out var list))
                {
                    list = new List<TrapezoidCell>();
                    byRoot[root] = list;
                }

                list.Add(cells[i]);
            }

            groups.AddRange(byRoot.Values);
            return groups;
        }

        private static void EmitGroup(
            List<PartitionRegion> result,
            List<TrapezoidCell> group,
            ComponentType type,
            double thickness)
        {
            var polys = group.Select(c => c.ToPolygon()).ToList();
            foreach (var poly in PolygonBoolean.UnionAll(polys))
            {
                if (poly == null || poly.VertexCount < 3)
                    continue;

                result.Add(new PartitionRegion
                {
                    Type = type,
                    Polygon = poly,
                    ThicknessMm = thickness
                });
            }
        }

        // ===================================================================
        //  竖边成对识别墙（N12）
        // ===================================================================

        private const double VerticalFaceInsideOffsetMm = 1.0;

        private sealed class VerticalFace
        {
            public double X;
            public double YBot, YTop;
            public bool IsLeftFace;
            /// <summary>与 X 轴夹角归一化到 [0°,90°]。</summary>
            public double AngleDeg;
        }

        /// <summary>线段与 X 轴夹角归一化到 [0°,90°]。</summary>
        private static double NormalizeSegmentAngleDeg(Line2D seg)
        {
            if (!seg.Direction.TryNormalize(out Vector2D dir))
                return 0.0;

            double angle = Math.Abs(Math.Atan2(dir.Y, dir.X) * 180.0 / Math.PI);
            if (angle > 90.0)
                angle = 180.0 - angle;
            return angle;
        }

        /// <summary>墙立面：与竖直夹角 ≤ threshold（即归一化角 ≥ 90°−threshold）。</summary>
        private static bool IsVerticalWallFaceAngle(double normalizedAngleDeg, double thresholdDeg)
        {
            if (thresholdDeg <= 0)
                thresholdDeg = 15.0;
            return normalizedAngleDeg >= 90.0 - thresholdDeg - IntersectionEpsilon;
        }

        /// <summary>
        /// 从轮廓边提取左/右竖直面，成对（间距≤墙厚、平行、Y重叠≥minH、平行占比≥ratio）→ 墙柱矩形。
        /// </summary>
        private static List<WallColumnRect> DetectWallColumnsByVerticalEdges(
            List<Line2D> segments,
            ReinRegion region,
            List<TrapezoidCell> wallCandidates,
            double wallMaxWidthMm,
            double wallMinHeightMm,
            double parallelAngleThresholdDeg,
            double parallelLineRatioMin)
        {
            var result = new List<WallColumnRect>();
            if (segments == null || segments.Count == 0 || wallCandidates == null || wallCandidates.Count == 0)
                return result;

            if (wallMinHeightMm < MinIntervalHeightMm)
                wallMinHeightMm = MinIntervalHeightMm;
            if (parallelAngleThresholdDeg <= 0)
                parallelAngleThresholdDeg = 15.0;
            if (parallelLineRatioMin <= 0 || parallelLineRatioMin > 1.0)
                parallelLineRatioMin = 0.6;

            var faces = CollectVerticalFaces(
                segments, region, wallMinHeightMm, parallelAngleThresholdDeg);
            var leftFaces = faces.Where(f => f.IsLeftFace).OrderBy(f => f.X).ToList();
            var rightFaces = faces.Where(f => !f.IsLeftFace).OrderBy(f => f.X).ToList();

            foreach (var left in leftFaces)
            {
                VerticalFace bestRight = null;
                double bestWidth = double.MaxValue;
                double bestOverlapRatio = 0;

                foreach (var right in rightFaces)
                {
                    if (right.X <= left.X + IntersectionEpsilon)
                        continue;

                    if (Math.Abs(left.AngleDeg - right.AngleDeg) > parallelAngleThresholdDeg + IntersectionEpsilon)
                        continue;

                    double width = right.X - left.X;
                    if (width > wallMaxWidthMm + IntersectionEpsilon)
                        continue;

                    double yBot = Math.Max(left.YBot, right.YBot);
                    double yTop = Math.Min(left.YTop, right.YTop);
                    if (yTop - yBot < wallMinHeightMm)
                        continue;

                    if (!IsConcreteBetween(region, left.X, right.X, yBot, yTop))
                        continue;

                    double overlapBot = yBot;
                    double overlapTop = yTop;
                    ClampWallColumnYToCandidates(
                        left.X, right.X, ref overlapBot, ref overlapTop, wallCandidates);

                    if (overlapTop - overlapBot < wallMinHeightMm)
                        continue;

                    if (!PassesParallelLineRatioGate(
                            left.X, right.X, overlapBot, overlapTop, wallCandidates,
                            parallelLineRatioMin, out double overlapRatio))
                        continue;

                    if (width < bestWidth)
                    {
                        bestWidth = width;
                        bestRight = right;
                        bestOverlapRatio = overlapRatio;
                    }
                }

                if (bestRight == null)
                    continue;

                double finalBot = Math.Max(left.YBot, bestRight.YBot);
                double finalTop = Math.Min(left.YTop, bestRight.YTop);
                ClampWallColumnYToCandidates(
                    left.X, bestRight.X, ref finalBot, ref finalTop, wallCandidates);

                if (finalTop - finalBot < wallMinHeightMm)
                    continue;

                result.Add(new WallColumnRect
                {
                    X0 = left.X,
                    X1 = bestRight.X,
                    YBot = finalBot,
                    YTop = finalTop,
                    OverlapRatio = bestOverlapRatio
                });
            }

            return MergeOverlappingWallColumns(result);
        }

        private static List<VerticalFace> CollectVerticalFaces(
            List<Line2D> segments,
            ReinRegion region,
            double wallMinHeightMm,
            double parallelAngleThresholdDeg)
        {
            var faces = new List<VerticalFace>();
            double delta = VerticalFaceInsideOffsetMm;

            foreach (var seg in segments)
            {
                double angleDeg = NormalizeSegmentAngleDeg(seg);
                if (!IsVerticalWallFaceAngle(angleDeg, parallelAngleThresholdDeg))
                    continue;

                double yBot = Math.Min(seg.StartPoint.Y, seg.EndPoint.Y);
                double yTop = Math.Max(seg.StartPoint.Y, seg.EndPoint.Y);
                if (yTop - yBot < wallMinHeightMm)
                    continue;

                double yMid = (yBot + yTop) / 2.0;
                double x = EvaluateXOnSegment(seg, yMid);
                bool rightInside = region.IsValidRebarPoint(new Point2D(x + delta, yMid));
                bool leftInside = region.IsValidRebarPoint(new Point2D(x - delta, yMid));

                if (rightInside && !leftInside)
                {
                    faces.Add(new VerticalFace
                    {
                        X = x,
                        YBot = yBot,
                        YTop = yTop,
                        IsLeftFace = true,
                        AngleDeg = angleDeg
                    });
                }
                else if (leftInside && !rightInside)
                {
                    faces.Add(new VerticalFace
                    {
                        X = x,
                        YBot = yBot,
                        YTop = yTop,
                        IsLeftFace = false,
                        AngleDeg = angleDeg
                    });
                }
            }

            return faces;
        }

        private static double EvaluateXOnSegment(Line2D seg, double y)
        {
            double y1 = seg.StartPoint.Y, y2 = seg.EndPoint.Y;
            if (Math.Abs(y2 - y1) < IntersectionEpsilon)
                return (seg.StartPoint.X + seg.EndPoint.X) / 2.0;

            double t = (y - y1) / (y2 - y1);
            if (t < 0) t = 0;
            else if (t > 1) t = 1;
            return seg.StartPoint.X + t * (seg.EndPoint.X - seg.StartPoint.X);
        }

        private static bool PassesParallelLineRatioGate(
            double xL,
            double xR,
            double overlapBot,
            double overlapTop,
            List<TrapezoidCell> wallCandidates,
            double parallelLineRatioMin,
            out double overlapRatio)
        {
            overlapRatio = 0;

            var covering = wallCandidates
                .Where(c => c.X1 > xL + IntersectionEpsilon
                    && c.X0 < xR - IntersectionEpsilon
                    && c.X0 >= xL - IntersectionEpsilon
                    && c.X1 <= xR + IntersectionEpsilon)
                .ToList();

            if (covering.Count == 0)
                return false;

            double columnFullHeight = covering.Max(c => c.MaxTop) - covering.Min(c => c.MinBot);
            if (columnFullHeight < MinIntervalHeightMm)
                return false;

            double overlapHeight = overlapTop - overlapBot;
            overlapRatio = overlapHeight / columnFullHeight;
            return overlapRatio >= parallelLineRatioMin - IntersectionEpsilon;
        }

        private static bool IsConcreteBetween(
            ReinRegion region,
            double xL,
            double xR,
            double yBot,
            double yTop)
        {
            if (yTop - yBot < MinIntervalHeightMm)
                return false;

            double xMid = (xL + xR) / 2.0;
            double span = yTop - yBot;
            double[] sampleY =
            {
                yBot + span * 0.25,
                yBot + span * 0.5,
                yBot + span * 0.75
            };

            foreach (double y in sampleY)
            {
                if (!region.IsValidRebarPoint(new Point2D(xMid, y)))
                    return false;
            }

            return true;
        }

        private static void ClampWallColumnYToCandidates(
            double x0,
            double x1,
            ref double yBot,
            ref double yTop,
            List<TrapezoidCell> wallCandidates)
        {
            var overlapping = wallCandidates
                .Where(c => c.X1 > x0 + IntersectionEpsilon
                    && c.X0 < x1 - IntersectionEpsilon
                    && c.X0 >= x0 - IntersectionEpsilon
                    && c.X1 <= x1 + IntersectionEpsilon)
                .ToList();

            if (overlapping.Count == 0)
                return;

            double clampBot = overlapping.Max(c => c.MinBot);
            double clampTop = overlapping.Min(c => c.MaxTop);
            yBot = Math.Max(yBot, clampBot);
            yTop = Math.Min(yTop, clampTop);
        }

        private static List<WallColumnRect> MergeOverlappingWallColumns(List<WallColumnRect> columns)
        {
            if (columns.Count <= 1)
                return columns;

            var merged = new List<WallColumnRect>();
            foreach (var col in columns.OrderBy(c => c.X0).ThenBy(c => c.YBot))
            {
                var existing = merged.FirstOrDefault(m =>
                    Math.Abs(m.X0 - col.X0) < DedupeYToleranceMm
                    && Math.Abs(m.X1 - col.X1) < DedupeYToleranceMm
                    && !(col.YTop < m.YBot - DedupeYToleranceMm || col.YBot > m.YTop + DedupeYToleranceMm));

                if (existing == null)
                {
                    merged.Add(new WallColumnRect
                    {
                        X0 = col.X0,
                        X1 = col.X1,
                        YBot = col.YBot,
                        YTop = col.YTop,
                        OverlapRatio = col.OverlapRatio
                    });
                }
                else
                {
                    existing.YBot = Math.Min(existing.YBot, col.YBot);
                    existing.YTop = Math.Max(existing.YTop, col.YTop);
                    existing.OverlapRatio = Math.Max(existing.OverlapRatio, col.OverlapRatio);
                }
            }

            return merged;
        }

        /// <summary>按墙柱矩形 Y 区间切分 wallCandidate → 墙带 / 大体积带。</summary>
        private static void ApplyVerticalEdgeWallsToCandidates(
            List<TrapezoidCell> wallCandidates,
            List<WallColumnRect> wallColumns,
            ref int cellId,
            List<TrapezoidCell> wallCells,
            List<TrapezoidCell> massPool)
        {
            foreach (var cell in wallCandidates)
            {
                var wallIntervals = CollectWallYIntervalsForCell(cell, wallColumns);
                if (wallIntervals.Count == 0)
                {
                    massPool.Add(CopyCell(ref cellId, cell, CellKind.Raised));
                    continue;
                }

                wallIntervals = MergeYIntervals(wallIntervals);
                double cursor = cell.MinBot;

                foreach (var interval in wallIntervals.OrderBy(i => i.Bot))
                {
                    if (interval.Bot - cursor >= MinIntervalHeightMm)
                    {
                        var massPiece = CreateFlatBandCell(
                            cell, ref cellId, cell.StripIndex, CellKind.Raised,
                            cell.X0, cell.X1, cursor, interval.Bot);
                        if (massPiece != null)
                            massPool.Add(massPiece);
                    }

                    var wallPiece = CreateFlatBandCell(
                        cell, ref cellId, cell.StripIndex, CellKind.Raised,
                        cell.X0, cell.X1, interval.Bot, interval.Top);
                    if (wallPiece != null)
                        wallCells.Add(wallPiece);

                    cursor = interval.Top;
                }

                if (cell.MaxTop - cursor >= MinIntervalHeightMm)
                {
                    var massPiece = CreateFlatBandCell(
                        cell, ref cellId, cell.StripIndex, CellKind.Raised,
                        cell.X0, cell.X1, cursor, cell.MaxTop);
                    if (massPiece != null)
                        massPool.Add(massPiece);
                }
            }
        }

        private static List<(double Bot, double Top)> CollectWallYIntervalsForCell(
            TrapezoidCell cell,
            List<WallColumnRect> wallColumns)
        {
            var intervals = new List<(double Bot, double Top)>();

            foreach (var wall in wallColumns)
            {
                if (cell.X0 < wall.X0 - IntersectionEpsilon || cell.X1 > wall.X1 + IntersectionEpsilon)
                    continue;

                double yBot = Math.Max(wall.YBot, cell.MinBot);
                double yTop = Math.Min(wall.YTop, cell.MaxTop);
                if (yTop - yBot >= MinIntervalHeightMm)
                    intervals.Add((yBot, yTop));
            }

            return intervals;
        }

        private static List<(double Bot, double Top)> MergeYIntervals(List<(double Bot, double Top)> intervals)
        {
            if (intervals.Count <= 1)
                return intervals;

            var sorted = intervals.OrderBy(i => i.Bot).ToList();
            var merged = new List<(double Bot, double Top)> { sorted[0] };

            for (int i = 1; i < sorted.Count; i++)
            {
                var last = merged[merged.Count - 1];
                var cur = sorted[i];

                if (cur.Bot <= last.Top + DedupeYToleranceMm)
                {
                    merged[merged.Count - 1] = (last.Bot, Math.Max(last.Top, cur.Top));
                }
                else
                {
                    merged.Add(cur);
                }
            }

            return merged;
        }

        /// <summary>
        /// 上凸 cell 按高度带剖分：
        /// 逐高度带用原始轮廓水平割线量测该高度处混凝土实际宽度（轮廓线到轮廓线），
        /// 宽 ≤ 墙厚上限 → 墙带；否则同高板距当前列 ≤ 锚固长度 → 板带，否则 → 大体积带。
        /// 墙带向下/向上深入相邻混凝土锚固长度，但不越出本列区域边界。
        /// </summary>
        private static void SplitRaisedCellByWidth(
            TrapezoidCell cell,
            List<double> yBreaks,
            ReinRegion region,
            List<Line2D> segments,
            List<double> breakpoints,
            Dictionary<int, List<TrapezoidCell>> cellsByStrip,
            double wallMaxWidthMm,
            double anchorageMm,
            double localConcreteMaxHeightMm,
            ref int cellId,
            List<TrapezoidCell> wallCells,
            List<TrapezoidCell> massPieces,
            List<TrapezoidCell> slabPieces,
            bool allowSlabBands)
        {
            double yLo = cell.MinBot;
            double yHi = cell.MaxTop;
            double xmCol = (cell.X0 + cell.X1) / 2.0;

            var ys = new List<double> { yLo };
            foreach (var y in yBreaks)
            {
                if (y > yLo + MinIntervalHeightMm && y < yHi - MinIntervalHeightMm)
                    ys.Add(y);
            }

            ys.Add(yHi);

            // 0 = 墙带，1 = 大体积带，2 = 板带候选
            int n = ys.Count - 1;
            var kinds = new int[n];
            var slabLeftEdge = new double[n];
            var slabRightEdge = new double[n];
            var needsAnchorTongue = new bool[n];

            for (int b = 0; b < n; b++)
            {
                slabLeftEdge[b] = double.NaN;
                slabRightEdge[b] = double.NaN;

                double ym = (ys[b] + ys[b + 1]) / 2.0;
                double width = MeasureContourWidthAt(
                    region, segments, breakpoints, cellsByStrip,
                    cell.X0, cell.X1, xmCol, ym,
                    out double leftEdgeX, out double rightEdgeX);
                slabLeftEdge[b] = leftEdgeX;
                slabRightEdge[b] = rightEdgeX;

                bool leftAnchorage = !double.IsNaN(leftEdgeX)
                    && cell.X0 - leftEdgeX <= anchorageMm;
                bool rightAnchorage = !double.IsNaN(rightEdgeX)
                    && rightEdgeX - cell.X1 <= anchorageMm;
                bool slabWithinAnchorage = leftAnchorage || rightAnchorage;

                kinds[b] = width <= wallMaxWidthMm
                    ? 0
                    : (allowSlabBands && slabWithinAnchorage ? 2 : 1);
            }

            // 2-run 下邻判型：下邻为大体积 → 转大体积并延伸到列顶，标记锚固舌
            int r0 = 0;
            while (r0 < n)
            {
                if (kinds[r0] != 2)
                {
                    r0++;
                    continue;
                }

                int r1 = r0;
                while (r1 + 1 < n && kinds[r1 + 1] == 2)
                    r1++;

                int belowKind = -1;
                for (int b = r0 - 1; b >= 0; b--)
                {
                    if (kinds[b] != 2)
                    {
                        belowKind = kinds[b];
                        break;
                    }
                }

                if (belowKind == 1)
                {
                    for (int b = r0; b <= r1; b++)
                    {
                        kinds[b] = 1;
                        needsAnchorTongue[b] = true;
                    }
                }

                r0 = r1 + 1;
            }

            // 连续同类带合并成 run 后出 cell
            int b0 = 0;
            while (b0 < n)
            {
                int b1 = b0;
                while (b1 + 1 < n && kinds[b1 + 1] == kinds[b0])
                    b1++;

                double runBot = ys[b0];
                double runTop = ys[b1 + 1];

                // 矮墙带（高 < 500）不作墙，归大体积
                int runKind = kinds[b0];
                if (runKind == 0 && runTop - runBot < WallMinHeightMm)
                    runKind = 1;

                if (runKind == 0)
                {
                    // 墙带：上方延伸锚固长度（不出列顶）；
                    // 下方与底板相连（墙带底到列底切割线之间只有 < 1000 的薄夹层，
                    // 如局部混凝土/底板超限薄层）→ 延伸到底板尽头（列底）；
                    // 下方为真大体积块（夹层 ≥ 1000）→ 只深入锚固长度
                    double topL = Math.Min(cell.TopL, runTop + anchorageMm);
                    double topR = Math.Min(cell.TopR, runTop + anchorageMm);
                    double botL, botR;
                    double gapBelow = runBot - cell.MinBot;
                    if (gapBelow < localConcreteMaxHeightMm)
                    {
                        botL = cell.OrigBotL;
                        botR = cell.OrigBotR;
                    }
                    else
                    {
                        botL = Math.Max(cell.OrigBotL, runBot - anchorageMm);
                        botR = Math.Max(cell.OrigBotR, runBot - anchorageMm);
                    }

                    wallCells.Add(new TrapezoidCell
                    {
                        Id = cellId++,
                        StripIndex = cell.StripIndex,
                        Kind = CellKind.Raised,
                        X0 = cell.X0,
                        X1 = cell.X1,
                        TopL = topL,
                        TopR = topR,
                        BotL = botL,
                        BotR = botR,
                        ThicknessMm = cell.X1 - cell.X0
                    });
                }
                else
                {
                    double topL = Math.Min(cell.TopL, runTop);
                    double topR = Math.Min(cell.TopR, runTop);
                    double botL = Math.Max(cell.BotL, runBot);
                    double botR = Math.Max(cell.BotR, runBot);

                    var piece = new TrapezoidCell
                    {
                        Id = cellId++,
                        StripIndex = cell.StripIndex,
                        Kind = runKind == 2 ? CellKind.Slab : CellKind.Raised,
                        X0 = cell.X0,
                        X1 = cell.X1,
                        TopL = topL,
                        TopR = topR,
                        BotL = botL,
                        BotR = botR,
                        ThicknessMm = ((topL - botL) + (topR - botR)) / 2.0
                    };

                    if (runKind == 2)
                        slabPieces.Add(piece);
                    else
                        massPieces.Add(piece);
                }

                for (int tb = b0; tb <= b1; tb++)
                {
                    if (!needsAnchorTongue[tb])
                        continue;

                    double yBot = ys[tb];
                    double yTop = ys[tb + 1];
                    TryAddAnchorTongue(
                        cell, ref cellId, cell.StripIndex, yBot, yTop,
                        slabLeftEdge[tb], slabRightEdge[tb], anchorageMm, slabPieces);
                }

                b0 = b1 + 1;
            }
        }

        private static void TryAddAnchorTongue(
            TrapezoidCell cell,
            ref int cellId,
            int stripIndex,
            double yBot,
            double yTop,
            double slabLeftEdgeX,
            double slabRightEdgeX,
            double anchorageMm,
            List<TrapezoidCell> slabPieces)
        {
            if (yTop - yBot < MinIntervalHeightMm)
                return;

            if (!double.IsNaN(slabLeftEdgeX))
            {
                double x1 = Math.Min(cell.X1, slabLeftEdgeX + anchorageMm);
                var tongue = CreateFlatBandCell(
                    cell, ref cellId, stripIndex, CellKind.Slab,
                    cell.X0, x1, yBot, yTop);
                if (tongue != null)
                    slabPieces.Add(tongue);
            }

            if (!double.IsNaN(slabRightEdgeX))
            {
                double x0 = Math.Max(cell.X0, slabRightEdgeX - anchorageMm);
                var tongue = CreateFlatBandCell(
                    cell, ref cellId, stripIndex, CellKind.Slab,
                    x0, cell.X1, yBot, yTop);
                if (tongue != null)
                    slabPieces.Add(tongue);
            }
        }

        private static TrapezoidCell CreateFlatBandCell(
            TrapezoidCell cell,
            ref int cellId,
            int stripIndex,
            CellKind kind,
            double x0,
            double x1,
            double yBot,
            double yTop)
        {
            if (x1 - x0 < MinStripWidthMm || yTop - yBot < MinIntervalHeightMm)
                return null;

            return new TrapezoidCell
            {
                Id = cellId++,
                StripIndex = stripIndex,
                Kind = kind,
                X0 = x0,
                X1 = x1,
                TopL = yTop,
                TopR = yTop,
                BotL = yBot,
                BotR = yBot,
                OrigBotL = cell.OrigBotL,
                OrigBotR = cell.OrigBotR,
                ThicknessMm = yTop - yBot
            };
        }

        /// <summary>
        /// 用原始轮廓水平割线 y = ym 量测包含 xm 的连续混凝土实际宽度，
        /// 并返回同高度左右板缘 X（NaN = 该侧无板）。
        /// </summary>
        private static double MeasureContourWidthAt(
            ReinRegion region,
            List<Line2D> segments,
            List<double> breakpoints,
            Dictionary<int, List<TrapezoidCell>> cellsByStrip,
            double colX0,
            double colX1,
            double xm,
            double ym,
            out double slabLeftEdgeX,
            out double slabRightEdgeX)
        {
            slabLeftEdgeX = double.NaN;
            slabRightEdgeX = double.NaN;

            var hits = new List<double>();
            foreach (var seg in segments)
            {
                double y1 = seg.StartPoint.Y, y2 = seg.EndPoint.Y;
                double minY = Math.Min(y1, y2), maxY = Math.Max(y1, y2);

                // 水平边自动跳过；ym 取高度带中点，基本不落在顶点上
                if (ym <= minY + IntersectionEpsilon || ym >= maxY - IntersectionEpsilon)
                    continue;

                double t = (ym - y1) / (y2 - y1);
                hits.Add(seg.StartPoint.X + t * (seg.EndPoint.X - seg.StartPoint.X));
            }

            if (hits.Count < 2)
                return double.MaxValue;

            hits.Sort();
            var pts = new List<double> { hits[0] };
            for (int i = 1; i < hits.Count; i++)
            {
                if (hits[i] - pts[pts.Count - 1] > DedupeYToleranceMm)
                    pts.Add(hits[i]);
            }

            // gap 内外测试，找包含 xm 的连续 inside 区段
            double wx0 = double.NaN, wx1 = double.NaN;
            double runStart = double.NaN;
            bool active = false;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                double gx = (pts[i] + pts[i + 1]) / 2.0;
                bool inside = pts[i + 1] - pts[i] >= MinIntervalHeightMm
                    && region.IsValidRebarPoint(new Point2D(gx, ym));

                if (inside)
                {
                    if (!active)
                    {
                        runStart = pts[i];
                        active = true;
                    }
                }
                else if (active)
                {
                    if (xm >= runStart && xm <= pts[i])
                    {
                        wx0 = runStart;
                        wx1 = pts[i];
                        break;
                    }

                    active = false;
                }
            }

            if (double.IsNaN(wx0) && active
                && xm >= runStart && xm <= pts[pts.Count - 1])
            {
                wx0 = runStart;
                wx1 = pts[pts.Count - 1];
            }

            if (double.IsNaN(wx0))
                return double.MaxValue;

            // 同高板 cell → 左右板缘 X
            foreach (var kv in cellsByStrip)
            {
                double sx0 = breakpoints[kv.Key];
                double sx1 = breakpoints[kv.Key + 1];

                foreach (var c in kv.Value)
                {
                    if (c.Kind != CellKind.Slab || ym < c.MinBot || ym > c.MaxTop)
                        continue;

                    if (sx1 <= colX0 + MinStripWidthMm)
                    {
                        if (double.IsNaN(slabLeftEdgeX) || sx1 > slabLeftEdgeX)
                            slabLeftEdgeX = sx1;
                    }

                    if (sx0 >= colX1 - MinStripWidthMm)
                    {
                        if (double.IsNaN(slabRightEdgeX) || sx0 < slabRightEdgeX)
                            slabRightEdgeX = sx0;
                    }
                }
            }

            return wx1 - wx0;
        }

        /// <summary>组内任一 cell 与大体积 cell 水平相邻（Y 重叠）或垂直贴合（X 重叠）→ 相连。</summary>
        private static bool TouchesAnyCell(
            List<TrapezoidCell> group,
            List<TrapezoidCell> others)
        {
            foreach (var a in group)
            {
                foreach (var b in others)
                {
                    // 水平相邻：条带相接 + Y 重叠
                    bool sideBySide = Math.Abs(a.X1 - b.X0) < MinStripWidthMm
                                   || Math.Abs(b.X1 - a.X0) < MinStripWidthMm;
                    if (sideBySide)
                    {
                        double yOverlap = Math.Min(a.MaxTop, b.MaxTop) - Math.Max(a.MinBot, b.MinBot);
                        if (yOverlap > MergeOverlapMm)
                            return true;
                    }

                    // 垂直贴合：X 重叠 + 上下边相接
                    double xOverlap = Math.Min(a.X1, b.X1) - Math.Max(a.X0, b.X0);
                    if (xOverlap > MinStripWidthMm)
                    {
                        if (Math.Abs(a.MinBot - b.MaxTop) <= MergeOverlapMm
                            || Math.Abs(b.MinBot - a.MaxTop) <= MergeOverlapMm)
                            return true;
                    }
                }
            }

            return false;
        }

        private static void FindNeighborBottomTops(
            double?[] bottomTopAtStrip,
            int stripIndex,
            out double? leftTop,
            out double? rightTop)
        {
            leftTop = null;
            rightTop = null;

            for (int j = stripIndex - 1; j >= 0; j--)
            {
                if (bottomTopAtStrip[j].HasValue)
                {
                    leftTop = bottomTopAtStrip[j];
                    break;
                }
            }

            for (int j = stripIndex + 1; j < bottomTopAtStrip.Length; j++)
            {
                if (bottomTopAtStrip[j].HasValue)
                {
                    rightTop = bottomTopAtStrip[j];
                    break;
                }
            }
        }

        // ===================================================================
        //  采样
        // ===================================================================

        private static List<Line2D> CollectSegments(ReinRegion region)
        {
            var segments = new List<Line2D>();
            foreach (var ring in region.AllRings)
            {
                if (ring == null)
                    continue;

                for (int i = 0; i < ring.SegmentCount; i++)
                    segments.Add(ring.GetSegmentAt(i));
            }

            return segments;
        }

        private static List<double> CollectBreakpoints(ReinRegion region)
        {
            var xs = new SortedSet<double>();

            foreach (var ring in region.AllRings)
            {
                if (ring == null)
                    continue;

                for (int i = 0; i < ring.VertexCount; i++)
                    xs.Add(ring.GetPointAt(i).X);
            }

            return xs.ToList();
        }

        private sealed class InsideInterval
        {
            public double Top;
            public double Bottom;
            public Line2D TopSeg;
            public Line2D BotSeg;
        }

        /// <summary>
        /// 中点割线 x = xm（严格在两顶点断点之间）求交；
        /// gap 中点 inside 测试合并出混凝土区间（自上而下，j1 最高）。
        /// </summary>
        private static List<InsideInterval> GetInsideIntervalsAt(
            ReinRegion region,
            List<Line2D> segments,
            double xm)
        {
            var hits = new List<(double Y, Line2D Seg)>();

            foreach (var seg in segments)
            {
                double x1 = seg.StartPoint.X, x2 = seg.EndPoint.X;
                double minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);

                // xm 永不等于顶点 X → 严格内部判定即可；竖直边自动跳过
                if (xm <= minX + IntersectionEpsilon || xm >= maxX - IntersectionEpsilon)
                    continue;

                double t = (xm - x1) / (x2 - x1);
                double y = seg.StartPoint.Y + t * (seg.EndPoint.Y - seg.StartPoint.Y);
                hits.Add((y, seg));
            }

            var intervals = new List<InsideInterval>();
            if (hits.Count < 2)
                return intervals;

            hits.Sort((a, b) => b.Y.CompareTo(a.Y));

            // 数值重合点去重（相切环），保留首个
            var pts = new List<(double Y, Line2D Seg)> { hits[0] };
            for (int i = 1; i < hits.Count; i++)
            {
                if (pts[pts.Count - 1].Y - hits[i].Y > DedupeYToleranceMm)
                    pts.Add(hits[i]);
            }

            // gap 内外测试（不依赖奇偶配对），连续 inside gap 合并
            InsideInterval current = null;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                double top = pts[i].Y;
                double bottom = pts[i + 1].Y;
                double midY = (top + bottom) / 2.0;

                bool inside = top - bottom >= MinIntervalHeightMm
                    && region.IsValidRebarPoint(new Point2D(xm, midY));

                if (inside)
                {
                    if (current == null)
                    {
                        current = new InsideInterval
                        {
                            Top = top,
                            TopSeg = pts[i].Seg,
                            Bottom = bottom,
                            BotSeg = pts[i + 1].Seg
                        };
                    }
                    else
                    {
                        current.Bottom = bottom;
                        current.BotSeg = pts[i + 1].Seg;
                    }
                }
                else if (current != null)
                {
                    intervals.Add(current);
                    current = null;
                }
            }

            if (current != null)
                intervals.Add(current);

            return intervals;
        }

        /// <summary>线段在 x 处的 Y（t 截断到 [0,1]，防数值越界）。</summary>
        private static double EvaluateYOnSegment(Line2D seg, double x)
        {
            double x1 = seg.StartPoint.X, x2 = seg.EndPoint.X;
            if (Math.Abs(x2 - x1) < IntersectionEpsilon)
                return (seg.StartPoint.Y + seg.EndPoint.Y) / 2.0;

            double t = (x - x1) / (x2 - x1);
            if (t < 0) t = 0;
            else if (t > 1) t = 1;

            return seg.StartPoint.Y + t * (seg.EndPoint.Y - seg.StartPoint.Y);
        }

        private static double GetRegionMinY(ReinRegion region)
        {
            double minY = double.MaxValue;
            if (region?.Outer == null)
                return 0;

            for (int i = 0; i < region.Outer.VertexCount; i++)
                minY = Math.Min(minY, region.Outer.GetPointAt(i).Y);

            return minY == double.MaxValue ? 0 : minY;
        }
    }
}
