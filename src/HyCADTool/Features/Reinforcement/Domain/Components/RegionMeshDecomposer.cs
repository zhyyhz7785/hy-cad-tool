using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>区域网格化分步阶段（N17–N21 调试用；N23–N24 为合并顺序对比）。</summary>
    public enum RegionMeshDecomposeStage
    {
        Trapezoids,
        RawParts,
        YSplit,
        HorizontalMerge,
        Complete,
        /// <summary>C.2 竖向合并（对比 N20 横先）。</summary>
        VerticalMerge,
        /// <summary>C.3 先竖后横合并+方向（对比 N21 先横后竖）。</summary>
        CompleteVerticalFirst,
        /// <summary>N30：夹平三角 + 全顶点延长线裁至第一交点有限弦分割。</summary>
        ReflexRectPartition
    }

    /// <summary>
    /// 混凝土区域网格化：先沿每条斜边切出 1 个三角形（斜线为斜边）把区域夹平成正交多边形，
    /// 再对正交区域做竖直条带矩形分解 + 横/竖向合并。每条斜边只产 1 个三角形。
    /// </summary>
    public static class RegionMeshDecomposer
    {
        private const double YToleranceMm = 0.01;
        private const double MergeToleranceMm = 1.0;
        private const double AspectRatioThreshold = 1.5;
        private const double AxisAlignToleranceMm = 0.5;
        private const double InteriorProbeEpsMm = 0.5;
        private const double IncidentSegmentDotTolerance = 0.999;
        private const double MinRayParameterMm = 1e-6;
        private const double RayParallelTolerance = 1e-10;

        private static readonly Vector2D NegUnitX = new Vector2D(-1, 0);
        private static readonly Vector2D NegUnitY = new Vector2D(0, -1);

        private sealed class TrapezoidPiece
        {
            public double X0, X1, TopL, TopR, BotL, BotR;
            public Line2D TopSeg, BotSeg;
        }

        private sealed class RectPiece
        {
            public double X0, X1, YBot, YTop;
        }

        private sealed class AxisSegment
        {
            public bool IsVertical;
            public double FixedCoord;
            public double Span0;
            public double Span1;
        }

        /// <summary>分解 ReinRegion 为矩形 + 三角形网格单元（纯几何，不按 cutY 切分）。</summary>
        public static List<MeshCell> Decompose(ReinRegion region)
            => DecomposeToStage(region, RegionMeshDecomposeStage.Complete);

        /// <summary>分解到指定阶段（N17–N21 分步预览）。</summary>
        public static List<MeshCell> DecomposeToStage(ReinRegion region, RegionMeshDecomposeStage stage)
        {
            if (region?.Outer == null || region.Outer.VertexCount < 3)
                return new List<MeshCell>();

            var clampTriangles = new List<MeshCell>();
            var clampedRegion = BuildClampedRegion(region, clampTriangles);

            if (stage == RegionMeshDecomposeStage.Trapezoids)
            {
                var trapPieces = CollectTrapezoidPieces(clampedRegion);
                var trapResult = new List<MeshCell>(trapPieces.Count + clampTriangles.Count);
                trapResult.AddRange(trapPieces.Select(CreateTrapezoidCell));
                trapResult.AddRange(clampTriangles);
                return trapResult;
            }

            if (stage == RegionMeshDecomposeStage.ReflexRectPartition)
            {
                var gridRects = PartitionByClippedExtension(clampedRegion);
                return BuildRectAndTriangleCells(gridRects, clampTriangles);
            }

            var trapezoids = CollectTrapezoidPieces(clampedRegion);
            var rectPieces = new List<RectPiece>();
            var triangles = new List<MeshCell>(clampTriangles);
            foreach (var trap in trapezoids)
                SplitTrapezoidIntoParts(trap, rectPieces, triangles);

            if (stage == RegionMeshDecomposeStage.RawParts)
                return BuildRectAndTriangleCells(rectPieces, triangles);

            rectPieces = SplitRectPiecesByGlobalYs(rectPieces);

            if (stage == RegionMeshDecomposeStage.YSplit)
                return BuildRectAndTriangleCells(rectPieces, triangles);

            if (stage == RegionMeshDecomposeStage.HorizontalMerge
                || stage == RegionMeshDecomposeStage.Complete)
            {
                var mergedH = MergeRectanglesHorizontally(rectPieces);
                if (stage == RegionMeshDecomposeStage.HorizontalMerge)
                    return BuildRectAndTriangleCells(mergedH, triangles);

                var mergedV = MergeRectanglesVertically(mergedH);
                return BuildRectAndTriangleCells(mergedV, triangles);
            }

            if (stage == RegionMeshDecomposeStage.VerticalMerge
                || stage == RegionMeshDecomposeStage.CompleteVerticalFirst)
            {
                var mergedV = MergeRectanglesVertically(rectPieces);
                if (stage == RegionMeshDecomposeStage.VerticalMerge)
                    return BuildRectAndTriangleCells(mergedV, triangles);

                var mergedH = MergeRectanglesHorizontally(mergedV);
                return BuildRectAndTriangleCells(mergedH, triangles);
            }

            return new List<MeshCell>();
        }

        private static List<MeshCell> BuildRectAndTriangleCells(
            IReadOnlyList<RectPiece> rectPieces,
            IReadOnlyList<MeshCell> triangles)
        {
            var result = new List<MeshCell>(rectPieces.Count + triangles.Count);
            result.AddRange(rectPieces.Select(r => CreateRectangleCell(r.X0, r.X1, r.YBot, r.YTop)));
            result.AddRange(triangles);
            return result;
        }

        private static MeshCell CreateTrapezoidCell(TrapezoidPiece t)
        {
            var poly = new Polygon2D(new[]
            {
                new Point2D(t.X0, t.BotL),
                new Point2D(t.X1, t.BotR),
                new Point2D(t.X1, t.TopR),
                new Point2D(t.X0, t.TopL)
            }, isClosed: true);

            return new MeshCell
            {
                Kind = MeshCellKind.Rectangle,
                Orientation = MeshCellOrientation.None,
                Polygon = poly,
                AreaMm2 = poly.GetArea()
            };
        }

        /// <summary>把区域所有环的斜边夹平为正交折线，每条斜边切出 1 个三角形单元。</summary>
        private static ReinRegion BuildClampedRegion(ReinRegion region, List<MeshCell> triangles)
        {
            var clampedOuter = BuildClampedRing(region, region.Outer, triangles);
            var clampedHoles = new List<Polyline2D>();
            foreach (var hole in region.Holes)
                clampedHoles.Add(BuildClampedRing(region, hole, triangles));

            return new ReinRegion(clampedOuter, clampedHoles);
        }

        /// <summary>逐边夹平一个环：斜边替换为正交台阶并切出三角形，轴对齐边原样保留。</summary>
        private static Polyline2D BuildClampedRing(ReinRegion region, Polyline2D ring, List<MeshCell> triangles)
        {
            int n = ring.VertexCount;
            if (n < 3)
                return ring.Clone();

            int segCount = ring.IsClosed ? n : n - 1;
            var verts = new List<Point2D>(n * 2);

            for (int i = 0; i < n; i++)
            {
                Point2D p0 = ring.GetPointAt(i);
                verts.Add(p0);

                if (i >= segCount)
                    continue;

                Point2D p1 = ring.GetPointAt((i + 1) % n);
                double dx = p1.X - p0.X;
                double dy = p1.Y - p0.Y;

                // 轴对齐边：原样保留。
                if (Math.Abs(dx) <= AxisAlignToleranceMm || Math.Abs(dy) <= AxisAlignToleranceMm)
                    continue;

                if (TryClampSlope(region, p0, p1, out Point2D corner, out MeshCell triangle))
                {
                    verts.Add(corner);
                    triangles.Add(triangle);
                }
            }

            var clamped = new Polyline2D(verts, ring.IsClosed);
            clamped.RemoveDuplicateVertices(YToleranceMm);
            return clamped;
        }

        /// <summary>
        /// 对斜边 p0->p1 求夹平台阶角点与三角形：斜线为斜边，直角点在台阶处。
        /// 通过法向探测确定混凝土在斜边的上/下方，决定夹平到高端还是低端。
        /// </summary>
        private static bool TryClampSlope(ReinRegion region, Point2D p0, Point2D p1, out Point2D corner, out MeshCell triangle)
        {
            corner = default;
            triangle = null;

            double dx = p1.X - p0.X;
            double dy = p1.Y - p0.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < AxisAlignToleranceMm)
                return false;

            double nx = -dy / len;
            double ny = dx / len;
            var mid = new Point2D((p0.X + p1.X) / 2.0, (p0.Y + p1.Y) / 2.0);

            bool posInside = region.IsValidRebarPoint(
                new Point2D(mid.X + nx * InteriorProbeEpsMm, mid.Y + ny * InteriorProbeEpsMm));
            bool negInside = region.IsValidRebarPoint(
                new Point2D(mid.X - nx * InteriorProbeEpsMm, mid.Y - ny * InteriorProbeEpsMm));
            if (posInside == negInside)
                return false;

            double interiorNy = posInside ? ny : -ny;
            if (Math.Abs(interiorNy) < 1e-9)
                return false;

            // 混凝土在斜边上方 → 底边，夹平到高端；否则夹平到低端。
            bool concreteAbove = interiorNy > 0;
            double clampY = concreteAbove ? Math.Max(p0.Y, p1.Y) : Math.Min(p0.Y, p1.Y);

            Point2D moved = Math.Abs(p0.Y - clampY) > Math.Abs(p1.Y - clampY) ? p0 : p1;
            var cornerPt = new Point2D(moved.X, clampY);

            var cell = CreateTriangleCell(p0, p1, cornerPt);
            if (cell == null)
                return false;

            corner = cornerPt;
            triangle = cell;
            return true;
        }

        private static MeshCell CreateTriangleCell(Point2D a, Point2D b, Point2D c)
        {
            double area2 = Math.Abs((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y));
            if (area2 < YToleranceMm)
                return null;

            var poly = new Polygon2D(new[] { a, b, c }, isClosed: true);
            if (poly.GetArea() < StripGeometry.MinIntervalHeightMm)
                return null;

            return new MeshCell
            {
                Kind = MeshCellKind.Triangle,
                Orientation = MeshCellOrientation.None,
                Polygon = poly,
                AreaMm2 = poly.GetArea()
            };
        }

        private static List<TrapezoidPiece> CollectTrapezoidPieces(ReinRegion region)
        {
            var pieces = new List<TrapezoidPiece>();
            var segments = StripGeometry.CollectSegments(region);
            var breakpoints = StripGeometry.CollectBreakpoints(region);
            if (breakpoints.Count < 2)
                return pieces;

            var active = new List<TrapezoidPiece>();

            for (int i = 0; i < breakpoints.Count - 1; i++)
            {
                double x0 = breakpoints[i];
                double x1 = breakpoints[i + 1];
                if (x1 - x0 < StripGeometry.MinStripWidthMm)
                    continue;

                double xm = (x0 + x1) / 2.0;
                var intervals = StripGeometry.GetInsideIntervalsAt(region, segments, xm);
                var usedActive = new HashSet<TrapezoidPiece>();
                var nextActive = new List<TrapezoidPiece>();

                foreach (var iv in intervals)
                {
                    if (iv == null)
                        continue;

                    TrapezoidPiece match = FindExtendableActive(active, usedActive, iv, x0);
                    if (match != null)
                    {
                        usedActive.Add(match);
                        match.X1 = x1;
                        match.TopR = StripGeometry.EvaluateYOnSegment(iv.TopSeg, x1);
                        match.BotR = StripGeometry.EvaluateYOnSegment(iv.BotSeg, x1);
                        nextActive.Add(match);
                    }
                    else
                    {
                        nextActive.Add(new TrapezoidPiece
                        {
                            X0 = x0,
                            X1 = x1,
                            TopSeg = iv.TopSeg,
                            BotSeg = iv.BotSeg,
                            TopL = StripGeometry.EvaluateYOnSegment(iv.TopSeg, x0),
                            TopR = StripGeometry.EvaluateYOnSegment(iv.TopSeg, x1),
                            BotL = StripGeometry.EvaluateYOnSegment(iv.BotSeg, x0),
                            BotR = StripGeometry.EvaluateYOnSegment(iv.BotSeg, x1)
                        });
                    }
                }

                foreach (var openPiece in active)
                {
                    if (!usedActive.Contains(openPiece))
                        pieces.Add(openPiece);
                }

                active = nextActive;
            }

            pieces.AddRange(active);
            return pieces;
        }

        private static TrapezoidPiece FindExtendableActive(
            IReadOnlyList<TrapezoidPiece> active,
            HashSet<TrapezoidPiece> usedActive,
            StripInsideInterval iv,
            double x0)
        {
            foreach (var candidate in active)
            {
                if (usedActive.Contains(candidate))
                    continue;
                if (!ReferenceEquals(candidate.TopSeg, iv.TopSeg))
                    continue;
                if (!ReferenceEquals(candidate.BotSeg, iv.BotSeg))
                    continue;
                if (Math.Abs(candidate.X1 - x0) > MergeToleranceMm)
                    continue;

                return candidate;
            }

            return null;
        }

        private static void SplitTrapezoidIntoParts(
            TrapezoidPiece t,
            List<RectPiece> rectPieces,
            List<MeshCell> triangles)
        {
            double yt = Math.Min(t.TopL, t.TopR);
            double yb = Math.Max(t.BotL, t.BotR);
            bool topSloped = Math.Abs(t.TopL - t.TopR) >= YToleranceMm;
            bool botSloped = Math.Abs(t.BotL - t.BotR) >= YToleranceMm;

            // 有安全矩形带：矩形 + 顶/底楔形三角（互不重叠）。
            if (yt - yb >= StripGeometry.MinIntervalHeightMm)
            {
                rectPieces.Add(new RectPiece
                {
                    X0 = t.X0,
                    X1 = t.X1,
                    YBot = yb,
                    YTop = yt
                });

                if (topSloped)
                    AddTopTriangles(t, yt, triangles);
                if (botSloped)
                    AddBottomTriangles(t, yb, triangles);
                return;
            }

            // 无安全带：顶底都平的薄片直接丢弃。
            if (!topSloped && !botSloped)
                return;

            // 否则整个梯形按对角分成 1~2 个三角形（退化三角形由面积阈值自动滤除），
            // 不再叠加楔形三角，避免与对角三角重叠。
            AddTrapezoidDiagonalTriangles(t, triangles);
        }

        private static void AddTopTriangles(TrapezoidPiece t, double yt, List<MeshCell> triangles)
        {
            if (Math.Abs(t.TopL - t.TopR) < YToleranceMm)
                return;

            if (t.TopL > t.TopR)
                AddTriangle(triangles, t.X0, t.TopL, t.X0, yt, t.X1, yt);
            else
                AddTriangle(triangles, t.X1, t.TopR, t.X1, yt, t.X0, yt);
        }

        private static void AddBottomTriangles(TrapezoidPiece t, double yb, List<MeshCell> triangles)
        {
            if (Math.Abs(t.BotL - t.BotR) < YToleranceMm)
                return;

            if (t.BotL > t.BotR)
                AddTriangle(triangles, t.X1, t.BotR, t.X1, yb, t.X0, yb);
            else
                AddTriangle(triangles, t.X0, t.BotL, t.X0, yb, t.X1, yb);
        }

        private static void AddTrapezoidDiagonalTriangles(TrapezoidPiece t, List<MeshCell> triangles)
        {
            AddTriangle(triangles, t.X0, t.TopL, t.X1, t.TopR, t.X1, t.BotR);
            AddTriangle(triangles, t.X0, t.TopL, t.X1, t.BotR, t.X0, t.BotL);
        }

        private static List<RectPiece> SplitRectPiecesByGlobalYs(List<RectPiece> rectPieces)
        {
            if (rectPieces.Count == 0)
                return new List<RectPiece>();

            var ys = BuildSortedAxis(rectPieces.SelectMany(r => new[] { r.YBot, r.YTop }));
            if (ys.Count < 2)
                return new List<RectPiece>(rectPieces);

            var cells = new List<RectPiece>();
            foreach (var rect in rectPieces)
            {
                int iy0 = FindAxisIndex(ys, rect.YBot);
                int iy1 = FindAxisIndex(ys, rect.YTop);
                if (iy0 < 0 || iy1 < 0 || iy1 <= iy0)
                {
                    cells.Add(rect);
                    continue;
                }

                for (int iy = iy0; iy < iy1; iy++)
                {
                    cells.Add(new RectPiece
                    {
                        X0 = rect.X0,
                        X1 = rect.X1,
                        YBot = ys[iy],
                        YTop = ys[iy + 1]
                    });
                }
            }

            return cells;
        }

        private static List<double> BuildSortedAxis(IEnumerable<double> values)
        {
            var merged = new List<double>();
            foreach (double value in values.OrderBy(v => v))
            {
                if (merged.Count == 0 || value - merged[merged.Count - 1] > YToleranceMm)
                    merged.Add(value);
            }

            return merged;
        }

        private static int FindAxisIndex(IReadOnlyList<double> axis, double value)
        {
            int lo = 0;
            int hi = axis.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                double diff = axis[mid] - value;
                if (Math.Abs(diff) <= YToleranceMm)
                    return mid;
                if (diff < 0)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }

            return -1;
        }

        private static List<RectPiece> MergeRectanglesHorizontally(List<RectPiece> rectPieces)
        {
            if (rectPieces.Count == 0)
                return new List<RectPiece>();

            var groups = rectPieces
                .GroupBy(r => (
                    YBot: Math.Round(r.YBot / YToleranceMm) * YToleranceMm,
                    YTop: Math.Round(r.YTop / YToleranceMm) * YToleranceMm))
                .ToList();

            var merged = new List<RectPiece>();
            foreach (var group in groups)
            {
                var sorted = group.OrderBy(r => r.X0).ToList();
                double runX0 = sorted[0].X0;
                double runX1 = sorted[0].X1;
                double yBot = sorted[0].YBot;
                double yTop = sorted[0].YTop;

                for (int i = 1; i < sorted.Count; i++)
                {
                    var cur = sorted[i];
                    if (cur.X0 - runX1 <= MergeToleranceMm
                        && Math.Abs(cur.YBot - yBot) < YToleranceMm
                        && Math.Abs(cur.YTop - yTop) < YToleranceMm)
                    {
                        runX1 = Math.Max(runX1, cur.X1);
                    }
                    else
                    {
                        merged.Add(new RectPiece { X0 = runX0, X1 = runX1, YBot = yBot, YTop = yTop });
                        runX0 = cur.X0;
                        runX1 = cur.X1;
                        yBot = cur.YBot;
                        yTop = cur.YTop;
                    }
                }

                merged.Add(new RectPiece { X0 = runX0, X1 = runX1, YBot = yBot, YTop = yTop });
            }

            return merged;
        }

        private static List<RectPiece> MergeRectanglesVertically(List<RectPiece> rectPieces)
        {
            if (rectPieces.Count == 0)
                return new List<RectPiece>();

            var groups = rectPieces
                .GroupBy(r => (
                    X0: Math.Round(r.X0 / YToleranceMm) * YToleranceMm,
                    X1: Math.Round(r.X1 / YToleranceMm) * YToleranceMm))
                .ToList();

            var merged = new List<RectPiece>();
            foreach (var group in groups)
            {
                var sorted = group.OrderBy(r => r.YBot).ToList();
                double runX0 = sorted[0].X0;
                double runX1 = sorted[0].X1;
                double yBot = sorted[0].YBot;
                double yTop = sorted[0].YTop;

                for (int i = 1; i < sorted.Count; i++)
                {
                    var cur = sorted[i];
                    if (cur.YBot - yTop <= MergeToleranceMm
                        && Math.Abs(cur.X0 - runX0) < YToleranceMm
                        && Math.Abs(cur.X1 - runX1) < YToleranceMm)
                    {
                        yTop = Math.Max(yTop, cur.YTop);
                    }
                    else
                    {
                        merged.Add(new RectPiece { X0 = runX0, X1 = runX1, YBot = yBot, YTop = yTop });
                        runX0 = cur.X0;
                        runX1 = cur.X1;
                        yBot = cur.YBot;
                        yTop = cur.YTop;
                    }
                }

                merged.Add(new RectPiece { X0 = runX0, X1 = runX1, YBot = yBot, YTop = yTop });
            }

            return merged;
        }

        private static MeshCell CreateRectangleCell(double x0, double x1, double yBot, double yTop)
        {
            var poly = new Polygon2D(new[]
            {
                new Point2D(x0, yBot),
                new Point2D(x1, yBot),
                new Point2D(x1, yTop),
                new Point2D(x0, yTop)
            }, isClosed: true);

            return new MeshCell
            {
                Kind = MeshCellKind.Rectangle,
                Orientation = ClassifyRectangleOrientation(x1 - x0, yTop - yBot),
                Polygon = poly,
                AreaMm2 = poly.GetArea()
            };
        }

        private static MeshCellOrientation ClassifyRectangleOrientation(double widthMm, double heightMm)
        {
            double shortSide = Math.Max(Math.Min(widthMm, heightMm), 1e-6);
            double ratio = Math.Max(widthMm, heightMm) / shortSide;

            if (ratio < AspectRatioThreshold)
                return MeshCellOrientation.Square;

            return heightMm > widthMm
                ? MeshCellOrientation.Vertical
                : MeshCellOrientation.Horizontal;
        }

        /// <summary>N30：全顶点延长线裁至第一交点，有限弦分割（原子矩形，不合并）。</summary>
        private static List<RectPiece> PartitionByClippedExtension(ReinRegion region)
        {
            if (region == null)
                return new List<RectPiece>();

            var chords = BuildExtensionChords(region);
            var boundary = BuildBoundarySegments(region);
            var segments = new List<AxisSegment>(boundary.Count + chords.Count);
            segments.AddRange(boundary);
            segments.AddRange(chords);
            return PartitionBySegmentGrid(region, segments);
        }

        private static List<AxisSegment> BuildBoundarySegments(ReinRegion region)
        {
            var segments = new List<AxisSegment>();
            if (region?.AllRings == null)
                return segments;

            foreach (var ring in region.AllRings)
            {
                if (ring == null)
                    continue;

                for (int i = 0; i < ring.SegmentCount; i++)
                {
                    var seg = ring.GetSegmentAt(i);
                    double dx = Math.Abs(seg.StartPoint.X - seg.EndPoint.X);
                    double dy = Math.Abs(seg.StartPoint.Y - seg.EndPoint.Y);

                    if (dy <= AxisAlignToleranceMm && dx > AxisAlignToleranceMm)
                    {
                        segments.Add(new AxisSegment
                        {
                            IsVertical = false,
                            FixedCoord = (seg.StartPoint.Y + seg.EndPoint.Y) / 2.0,
                            Span0 = Math.Min(seg.StartPoint.X, seg.EndPoint.X),
                            Span1 = Math.Max(seg.StartPoint.X, seg.EndPoint.X)
                        });
                    }
                    else if (dx <= AxisAlignToleranceMm && dy > AxisAlignToleranceMm)
                    {
                        segments.Add(new AxisSegment
                        {
                            IsVertical = true,
                            FixedCoord = (seg.StartPoint.X + seg.EndPoint.X) / 2.0,
                            Span0 = Math.Min(seg.StartPoint.Y, seg.EndPoint.Y),
                            Span1 = Math.Max(seg.StartPoint.Y, seg.EndPoint.Y)
                        });
                    }
                }
            }

            return segments;
        }

        private static List<AxisSegment> BuildExtensionChords(ReinRegion region)
        {
            var chords = new List<AxisSegment>();
            if (region == null)
                return chords;

            double eps = InteriorProbeEpsMm;
            foreach (var v in CollectAllVertices(region))
            {
                if (region.IsValidRebarPoint(new Point2D(v.X + eps, v.Y)))
                {
                    if (TryClipInteriorAxisRay(region, v, Vector2D.UnitX, out Point2D hit))
                    {
                        chords.Add(new AxisSegment
                        {
                            IsVertical = false,
                            FixedCoord = v.Y,
                            Span0 = Math.Min(v.X, hit.X),
                            Span1 = Math.Max(v.X, hit.X)
                        });
                    }
                }

                if (region.IsValidRebarPoint(new Point2D(v.X - eps, v.Y)))
                {
                    if (TryClipInteriorAxisRay(region, v, NegUnitX, out Point2D hit))
                    {
                        chords.Add(new AxisSegment
                        {
                            IsVertical = false,
                            FixedCoord = v.Y,
                            Span0 = Math.Min(v.X, hit.X),
                            Span1 = Math.Max(v.X, hit.X)
                        });
                    }
                }

                if (region.IsValidRebarPoint(new Point2D(v.X, v.Y + eps)))
                {
                    if (TryClipInteriorAxisRay(region, v, Vector2D.UnitY, out Point2D hit))
                    {
                        chords.Add(new AxisSegment
                        {
                            IsVertical = true,
                            FixedCoord = v.X,
                            Span0 = Math.Min(v.Y, hit.Y),
                            Span1 = Math.Max(v.Y, hit.Y)
                        });
                    }
                }

                if (region.IsValidRebarPoint(new Point2D(v.X, v.Y - eps)))
                {
                    if (TryClipInteriorAxisRay(region, v, NegUnitY, out Point2D hit))
                    {
                        chords.Add(new AxisSegment
                        {
                            IsVertical = true,
                            FixedCoord = v.X,
                            Span0 = Math.Min(v.Y, hit.Y),
                            Span1 = Math.Max(v.Y, hit.Y)
                        });
                    }
                }
            }

            return chords;
        }

        private static List<RectPiece> PartitionBySegmentGrid(ReinRegion region, IReadOnlyList<AxisSegment> segments)
        {
            var ySeeds = BuildSortedAxis(CollectHorizontalChordYs(segments));
            if (ySeeds.Count < 2)
                return new List<RectPiece>();

            var rects = new List<RectPiece>();
            for (int iy = 0; iy < ySeeds.Count - 1; iy++)
            {
                double y0 = ySeeds[iy];
                double y1 = ySeeds[iy + 1];
                if (y1 - y0 < YToleranceMm)
                    continue;

                double yMid = (y0 + y1) / 2.0;
                var localXs = BuildSortedAxis(
                    CollectVerticalChordXs(segments, y0, y1)
                        .Concat(CollectPolygonXsAtY(region?.Outer, yMid)));
                if (localXs.Count < 2)
                    continue;

                for (int ix = 0; ix < localXs.Count - 1; ix++)
                {
                    double x0 = localXs[ix];
                    double x1 = localXs[ix + 1];
                    if (x1 - x0 < YToleranceMm)
                        continue;

                    var localYs = BuildSortedAxis(
                        CollectHorizontalChordYs(segments, x0, x1)
                            .Concat(new[] { y0, y1 }));
                    if (localYs.Count < 2)
                        continue;

                    for (int jy = 0; jy < localYs.Count - 1; jy++)
                    {
                        double yc0 = localYs[jy];
                        double yc1 = localYs[jy + 1];
                        if (yc1 - yc0 < YToleranceMm)
                            continue;

                        if (!RangesOverlap(yc0, yc1, y0, y1))
                            continue;

                        double cy0 = Math.Max(yc0, y0);
                        double cy1 = Math.Min(yc1, y1);
                        if (cy1 - cy0 < YToleranceMm)
                            continue;

                        double cx = (x0 + x1) / 2.0;
                        double cy = (cy0 + cy1) / 2.0;
                        if (!region.IsValidRebarPoint(new Point2D(cx, cy)))
                            continue;

                        rects.Add(new RectPiece { X0 = x0, X1 = x1, YBot = cy0, YTop = cy1 });
                    }
                }
            }

            return rects;
        }

        /// <summary>水平线 y 与外环边的交点/端点 X，用于补全条带内竖弦不足时的 X 轴。</summary>
        private static IEnumerable<double> CollectPolygonXsAtY(Polyline2D ring, double y)
        {
            if (ring == null)
                yield break;

            for (int i = 0; i < ring.SegmentCount; i++)
            {
                var seg = ring.GetSegmentAt(i);
                double sy = seg.StartPoint.Y;
                double ey = seg.EndPoint.Y;
                double sx = seg.StartPoint.X;
                double ex = seg.EndPoint.X;

                if (Math.Abs(sy - ey) <= AxisAlignToleranceMm)
                {
                    if (Math.Abs(y - sy) <= AxisAlignToleranceMm)
                    {
                        yield return sx;
                        yield return ex;
                    }

                    continue;
                }

                double ymin = Math.Min(sy, ey);
                double ymax = Math.Max(sy, ey);
                if (y < ymin - AxisAlignToleranceMm || y > ymax + AxisAlignToleranceMm)
                    continue;

                double t = (y - sy) / (ey - sy);
                if (t < -1e-6 || t > 1.0 + 1e-6)
                    continue;

                yield return sx + t * (ex - sx);
            }
        }

        private static IEnumerable<double> CollectHorizontalChordYs(IReadOnlyList<AxisSegment> segments)
        {
            if (segments == null)
                yield break;

            foreach (var seg in segments)
            {
                if (!seg.IsVertical)
                    yield return seg.FixedCoord;
            }
        }

        private static IEnumerable<double> CollectHorizontalChordYs(IReadOnlyList<AxisSegment> segments, double x0, double x1)
        {
            if (segments == null)
                yield break;

            foreach (var seg in segments)
            {
                if (seg.IsVertical)
                    continue;

                if (RangesOverlap(x0, x1, seg.Span0, seg.Span1))
                    yield return seg.FixedCoord;
            }
        }

        private static IEnumerable<double> CollectVerticalChordXs(IReadOnlyList<AxisSegment> segments, double y0, double y1)
        {
            if (segments == null)
                yield break;

            foreach (var seg in segments)
            {
                if (!seg.IsVertical)
                    continue;

                if (RangesOverlap(y0, y1, seg.Span0, seg.Span1))
                    yield return seg.FixedCoord;
            }
        }

        private static bool RangesOverlap(double a0, double a1, double b0, double b1)
        {
            if (a0 > a1)
                (a0, a1) = (a1, a0);
            if (b0 > b1)
                (b0, b1) = (b1, b0);

            return a0 < b1 - YToleranceMm && b0 < a1 - YToleranceMm;
        }

        private static IEnumerable<Point2D> CollectAllVertices(ReinRegion region)
        {
            if (region?.Outer != null)
            {
                for (int i = 0; i < region.Outer.VertexCount; i++)
                    yield return region.Outer.GetPointAt(i);
            }

            if (region?.Holes == null)
                yield break;

            foreach (var hole in region.Holes)
            {
                if (hole == null)
                    continue;
                for (int i = 0; i < hole.VertexCount; i++)
                    yield return hole.GetPointAt(i);
            }
        }

        private static bool TryClipInteriorAxisRay(
            ReinRegion region,
            Point2D origin,
            Vector2D direction,
            out Point2D hit)
        {
            hit = default;
            if (region?.AllRings == null || !direction.TryNormalize(out Vector2D dir))
                return false;

            double best = double.MaxValue;
            bool found = false;

            foreach (var ring in region.AllRings)
            {
                if (ring == null)
                    continue;

                if (TryNearestHitOnRing(origin, dir, ring, out Point2D ringHit, out double t) && t < best)
                {
                    best = t;
                    hit = ringHit;
                    found = true;
                }
            }

            return found;
        }

        private static bool TryNearestHitOnRing(
            Point2D origin,
            Vector2D dir,
            Polyline2D ring,
            out Point2D hit,
            out double rayParameter)
        {
            hit = origin;
            rayParameter = 0;

            Point2D nearest = origin;
            double nearestDist = double.MaxValue;
            bool found = false;

            for (int i = 0; i < ring.SegmentCount; i++)
            {
                var seg = ring.GetSegmentAt(i);
                if (ShouldSkipIncidentSegment(origin, dir, seg))
                    continue;

                if (TryRayHitSegment(origin, dir, seg.StartPoint, seg.EndPoint, out Point2D segHit, out double t)
                    && t < nearestDist)
                {
                    nearestDist = t;
                    nearest = segHit;
                    found = true;
                }
            }

            if (found)
            {
                hit = nearest;
                rayParameter = nearestDist;
                return true;
            }

            return false;
        }

        private static bool ShouldSkipIncidentSegment(Point2D origin, Vector2D dir, Line2D seg)
        {
            Vector2D segDir = seg.StartPoint.VectorTo(seg.EndPoint);
            if (!segDir.TryNormalize(out Vector2D nSegDir))
                return true;

            if (origin.DistanceTo(seg.StartPoint) <= AxisAlignToleranceMm
                && nSegDir.Dot(dir) > IncidentSegmentDotTolerance)
                return true;

            if (origin.DistanceTo(seg.EndPoint) <= AxisAlignToleranceMm
                && (-nSegDir).Dot(dir) > IncidentSegmentDotTolerance)
                return true;

            return false;
        }

        private static bool TryRayHitSegment(
            Point2D origin,
            Vector2D dir,
            Point2D segStart,
            Point2D segEnd,
            out Point2D hit,
            out double rayParameter)
        {
            hit = Point2D.Origin;
            rayParameter = 0;

            double sx = segEnd.X - segStart.X;
            double sy = segEnd.Y - segStart.Y;
            double cross = dir.X * sy - dir.Y * sx;

            if (Math.Abs(cross) < RayParallelTolerance)
                return TryRayHitCollinearSegment(origin, dir, segStart, segEnd, out hit, out rayParameter);

            double ox = segStart.X - origin.X;
            double oy = segStart.Y - origin.Y;
            rayParameter = (ox * sy - oy * sx) / cross;
            double u = (ox * dir.Y - oy * dir.X) / cross;

            if (rayParameter < MinRayParameterMm || u < -1e-6 || u > 1.0 + 1e-6)
                return false;

            hit = new Point2D(origin.X + rayParameter * dir.X, origin.Y + rayParameter * dir.Y);
            return true;
        }

        private static bool TryRayHitCollinearSegment(
            Point2D origin,
            Vector2D dir,
            Point2D segStart,
            Point2D segEnd,
            out Point2D hit,
            out double rayParameter)
        {
            hit = Point2D.Origin;
            rayParameter = 0;

            bool hasStart = IsPointOnRayForward(origin, dir, segStart, out double tStart);
            bool hasEnd = IsPointOnRayForward(origin, dir, segEnd, out double tEnd);

            if (!hasStart && !hasEnd)
                return false;

            if (hasStart && hasEnd)
            {
                if (tStart <= tEnd)
                {
                    rayParameter = tStart;
                    hit = segStart;
                }
                else
                {
                    rayParameter = tEnd;
                    hit = segEnd;
                }
            }
            else if (hasStart)
            {
                rayParameter = tStart;
                hit = segStart;
            }
            else
            {
                rayParameter = tEnd;
                hit = segEnd;
            }

            return rayParameter >= MinRayParameterMm;
        }

        private static bool IsPointOnRayForward(
            Point2D origin,
            Vector2D dir,
            Point2D point,
            out double rayParameter)
        {
            rayParameter = 0;
            Vector2D offset = origin.VectorTo(point);
            if (offset.IsZero())
                return false;

            if (!offset.TryNormalize(out Vector2D offsetDir))
                return false;

            if (offsetDir.Dot(dir) < IncidentSegmentDotTolerance)
                return false;

            rayParameter = origin.DistanceTo(point);
            return rayParameter >= MinRayParameterMm;
        }

        private static void AddTriangle(
            List<MeshCell> triangles,
            double x1, double y1,
            double x2, double y2,
            double x3, double y3)
        {
            if (Math.Abs((x2 - x1) * (y3 - y1) - (x3 - x1) * (y2 - y1)) < YToleranceMm * YToleranceMm)
                return;

            var poly = new Polygon2D(new[]
            {
                new Point2D(x1, y1),
                new Point2D(x2, y2),
                new Point2D(x3, y3)
            }, isClosed: true);

            if (poly.GetArea() < StripGeometry.MinIntervalHeightMm)
                return;

            triangles.Add(new MeshCell
            {
                Kind = MeshCellKind.Triangle,
                Polygon = poly,
                AreaMm2 = poly.GetArea()
            });
        }
    }
}
