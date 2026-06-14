using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain;
using System;
using System.Collections.Generic;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>竖线 x=xm 处的混凝土区间（自上而下排序，末项为最下区间）。</summary>
    public sealed class StripInsideInterval
    {
        public double Top { get; set; }
        public double Bottom { get; set; }
        public Line2D TopSeg { get; set; }
        public Line2D BotSeg { get; set; }
    }

    /// <summary>
    /// 条带竖切几何原语（自 N8 RegionPartitioner 移植，供基础底板分区独立使用）。
    /// </summary>
    public static class StripGeometry
    {
        public const double MinStripWidthMm = 0.5;
        public const double MinIntervalHeightMm = 1.0;
        public const double MergeOverlapMm = 1.0;

        private const double DedupeYToleranceMm = 0.01;
        private const double IntersectionEpsilon = 1e-9;

        public static List<Line2D> CollectSegments(ReinRegion region)
        {
            var segments = new List<Line2D>();
            if (region?.AllRings == null)
                return segments;

            foreach (var ring in region.AllRings)
            {
                if (ring == null)
                    continue;

                for (int i = 0; i < ring.SegmentCount; i++)
                    segments.Add(ring.GetSegmentAt(i));
            }

            return segments;
        }

        public static List<double> CollectBreakpoints(ReinRegion region)
        {
            var xs = new SortedSet<double>();
            if (region?.AllRings == null)
                return new List<double>();

            foreach (var ring in region.AllRings)
            {
                if (ring == null)
                    continue;

                for (int i = 0; i < ring.VertexCount; i++)
                    xs.Add(ring.GetPointAt(i).X);
            }

            return new List<double>(xs);
        }

        /// <summary>中点割线 x=xm 求混凝土区间（自上而下）。</summary>
        public static List<StripInsideInterval> GetInsideIntervalsAt(
            ReinRegion region,
            IReadOnlyList<Line2D> segments,
            double xm)
        {
            var hits = new List<(double Y, Line2D Seg)>();

            foreach (var seg in segments)
            {
                double x1 = seg.StartPoint.X, x2 = seg.EndPoint.X;
                double minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);

                if (xm <= minX + IntersectionEpsilon || xm >= maxX - IntersectionEpsilon)
                    continue;

                double t = (xm - x1) / (x2 - x1);
                double y = seg.StartPoint.Y + t * (seg.EndPoint.Y - seg.StartPoint.Y);
                hits.Add((y, seg));
            }

            var intervals = new List<StripInsideInterval>();
            if (hits.Count < 2)
                return intervals;

            hits.Sort((a, b) => b.Y.CompareTo(a.Y));

            var pts = new List<(double Y, Line2D Seg)> { hits[0] };
            for (int i = 1; i < hits.Count; i++)
            {
                if (pts[pts.Count - 1].Y - hits[i].Y > DedupeYToleranceMm)
                    pts.Add(hits[i]);
            }

            StripInsideInterval current = null;
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
                        current = new StripInsideInterval
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

        public static double EvaluateYOnSegment(Line2D seg, double x)
        {
            double x1 = seg.StartPoint.X, x2 = seg.EndPoint.X;
            if (Math.Abs(x2 - x1) < IntersectionEpsilon)
                return (seg.StartPoint.Y + seg.EndPoint.Y) / 2.0;

            double t = (x - x1) / (x2 - x1);
            if (t < 0) t = 0;
            else if (t > 1) t = 1;

            return seg.StartPoint.Y + t * (seg.EndPoint.Y - seg.StartPoint.Y);
        }

        public static void FindNeighborBottomTops(
            IReadOnlyList<double?> bottomTopAtStrip,
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

            for (int j = stripIndex + 1; j < bottomTopAtStrip.Count; j++)
            {
                if (bottomTopAtStrip[j].HasValue)
                {
                    rightTop = bottomTopAtStrip[j];
                    break;
                }
            }
        }
    }
}
