using HyCAD.Geometry;
using HyCAD.Geometry.Algorithms;
using HyCADTool.Features.Reinforcement.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>边方向分类（阶段 2 墙/梁判定预备）。</summary>
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
    /// 竖直割线扫描精确分区。
    ///
    /// 关键设计（修复顶点穿越导致的交点退化）：
    /// - X 断点 = 全部环顶点 X；割线取 **相邻断点的中点**，永不穿过顶点 → 交点恒偶数；
    /// - 区间判定不依赖配对奇偶性：相邻交点构成 gap，用 gap 中点 IsValidRebarPoint
    ///   测内外（与 gj 切割线同思路），连续 inside gap 合并成区间；
    /// - 每条带区间记录上/下边所属线段，在条带左右边界 x0/x1 处插值出精确梯形
    ///   （线段必跨满整条带，因为顶点只在断点上）；
    /// - 标签：每条带最下区间 = 底板，其余 = 板（j1 最高，最下配对 = 底板）；
    /// - 相邻条带、同类型、共享边界 Y 重叠 → 并查集合并，UnionAll 出守恒多边形。
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

        private sealed class TrapezoidCell
        {
            public int Id;
            public int StripIndex;
            public ComponentType Type;
            public double TopL, TopR, BotL, BotR;
            public double X0, X1;
            public double ThicknessMm;

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

        /// <summary>对 ReinRegion 做板/底板精确分区。</summary>
        public static List<PartitionRegion> Partition(ReinRegion region)
        {
            var result = new List<PartitionRegion>();
            if (region?.Outer == null || region.Outer.VertexCount < 3)
                return result;

            var segments = CollectSegments(region);
            if (segments.Count == 0)
                return result;

            var breakpoints = CollectBreakpoints(region);
            if (breakpoints.Count < 2)
                return result;

            var cells = new List<TrapezoidCell>();
            int cellId = 0;

            for (int i = 0; i < breakpoints.Count - 1; i++)
            {
                double x0 = breakpoints[i];
                double x1 = breakpoints[i + 1];
                if (x1 - x0 < MinStripWidthMm)
                    continue;

                double xm = (x0 + x1) / 2.0;
                var intervals = GetInsideIntervalsAt(region, segments, xm);

                for (int k = 0; k < intervals.Count; k++)
                {
                    var iv = intervals[k];
                    var type = k == intervals.Count - 1
                        ? ComponentType.BottomSlab
                        : ComponentType.Slab;

                    double topL = EvaluateYOnSegment(iv.TopSeg, x0);
                    double topR = EvaluateYOnSegment(iv.TopSeg, x1);
                    double botL = EvaluateYOnSegment(iv.BotSeg, x0);
                    double botR = EvaluateYOnSegment(iv.BotSeg, x1);

                    if (topL - botL < MinIntervalHeightMm && topR - botR < MinIntervalHeightMm)
                        continue;

                    cells.Add(new TrapezoidCell
                    {
                        Id = cellId++,
                        StripIndex = i,
                        Type = type,
                        X0 = x0,
                        X1 = x1,
                        TopL = topL,
                        TopR = topR,
                        BotL = botL,
                        BotR = botR,
                        ThicknessMm = ((topL - botL) + (topR - botR)) / 2.0
                    });
                }
            }

            if (cells.Count == 0)
                return result;

            // 相邻条带、同类型、共享边界 Y 重叠 → 同一分量
            var byStrip = cells
                .GroupBy(c => c.StripIndex)
                .ToDictionary(g => g.Key, g => g.ToList());

            var uf = new UnionFind(cells.Count);
            foreach (var cell in cells)
            {
                if (!byStrip.TryGetValue(cell.StripIndex + 1, out var nextStrip))
                    continue;

                foreach (var other in nextStrip)
                {
                    if (other.Type != cell.Type)
                        continue;

                    // 仅真正相接的条带才合并（中间被窄条带跳过时不相接）
                    if (Math.Abs(other.X0 - cell.X1) > MinStripWidthMm)
                        continue;

                    double overlap = Math.Min(cell.TopR, other.TopL)
                                   - Math.Max(cell.BotR, other.BotL);
                    if (overlap > MergeOverlapMm)
                        uf.Union(cell.Id, other.Id);
                }
            }

            var groups = new Dictionary<int, List<TrapezoidCell>>();
            foreach (var cell in cells)
            {
                int root = uf.Find(cell.Id);
                if (!groups.TryGetValue(root, out var list))
                {
                    list = new List<TrapezoidCell>();
                    groups[root] = list;
                }

                list.Add(cell);
            }

            foreach (var group in groups.Values)
            {
                var polys = group.Select(c => c.ToPolygon()).ToList();
                var united = PolygonBoolean.UnionAll(polys);
                double avgThickness = group.Average(c => c.ThicknessMm);
                var type = group[0].Type;

                foreach (var poly in united)
                {
                    if (poly == null || poly.VertexCount < 3)
                        continue;

                    result.Add(new PartitionRegion
                    {
                        Type = type,
                        Polygon = poly,
                        ThicknessMm = avgThickness
                    });
                }
            }

            return result;
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
    }
}
