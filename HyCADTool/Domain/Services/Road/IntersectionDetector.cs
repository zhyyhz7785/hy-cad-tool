using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.Geometry;
using Polyline3D = HyCADTool.Shared.Geometry.Polyline3D;

namespace HyCADTool.Domain.Services.Road
{
    /// <summary>
    /// 在平面 (XY) 上扫描多条 <see cref="Alignment"/> 中心线，找两两线段的真交点，并按距离聚成交叉口候选点。
    /// </summary>
    public sealed class IntersectionDetector
    {
        /// <summary>单条两线相交的原始命中。</summary>
        public readonly struct LineHit
        {
            public Guid IdA { get; }
            public Guid IdB { get; }
            public Point2D Point { get; }
            public double StationA { get; }
            public double StationB { get; }

            public LineHit(Guid idA, Guid idB, Point2D point, double staA, double staB)
            {
                IdA = idA; IdB = idB; Point = point; StationA = staA; StationB = staB;
            }
        }

        /// <summary>聚类后的一条交叉口：中心 + 汇交的路线 Id（≥2 条）。</summary>
        public sealed class IntersectionCluster
        {
            public Point2D Center { get; }
            public IReadOnlyList<Guid> AlignmentIds { get; }
            public IReadOnlyList<LineHit> SourceHits { get; }

            public IntersectionCluster(Point2D center, IReadOnlyList<Guid> alignmentIds, IReadOnlyList<LineHit> sourceHits)
            {
                Center = center;
                AlignmentIds = alignmentIds;
                SourceHits = sourceHits;
            }
        }

        /// <param name="alignments">全部路线（至少 2 条才可能有平交）。</param>
        /// <param name="clusterRadiusM">将彼此距离小于该值的真交点合并为一处交叉口。</param>
        public IReadOnlyList<IntersectionCluster> Detect(
            IReadOnlyList<Alignment> alignments,
            double clusterRadiusM = 0.5)
        {
            if (alignments == null || alignments.Count < 2) return Array.Empty<IntersectionCluster>();

            var hits = new List<LineHit>();
            for (int i = 0; i < alignments.Count; i++)
            {
                for (int j = i + 1; j < alignments.Count; j++)
                {
                    var a = alignments[i];
                    var b = alignments[j];
                    if (a == null || b == null) continue;
                    if (a.Id == b.Id) continue;
                    if (a.Centerline == null || b.Centerline == null) continue;
                    if (a.Centerline.VertexCount < 2 || b.Centerline.VertexCount < 2) continue;
                    CollectSegSegHits(a, b, hits);
                }
            }

            if (hits.Count == 0) return Array.Empty<IntersectionCluster>();
            return ClusterHits(hits, Math.Max(1e-3, clusterRadiusM));
        }

        private static void CollectSegSegHits(Alignment a, Alignment b, List<LineHit> hits)
        {
            var preA = PrefixLengths(a.Centerline);
            var preB = PrefixLengths(b.Centerline);
            for (int ia = 0; ia < a.Centerline.VertexCount - 1; ia++)
            {
                var a0 = a.Centerline.GetPointAt(ia);
                var a1 = a.Centerline.GetPointAt(ia + 1);
                double lenA = SegmentLen2D(a0, a1);
                if (lenA < 1e-9) continue;

                for (int ib = 0; ib < b.Centerline.VertexCount - 1; ib++)
                {
                    var b0 = b.Centerline.GetPointAt(ib);
                    var b1 = b.Centerline.GetPointAt(ib + 1);
                    double lenB = SegmentLen2D(b0, b1);
                    if (lenB < 1e-9) continue;

                    if (TryIntersectSegments2D(
                        new Point2D(a0.X, a0.Y), new Point2D(a1.X, a1.Y),
                        new Point2D(b0.X, b0.Y), new Point2D(b1.X, b1.Y),
                        out var p, out double t, out double u))
                    {
                        double staA = preA[ia] + t * lenA;
                        double staB = preB[ib] + u * lenB;
                        hits.Add(new LineHit(a.Id, b.Id, p, staA, staB));
                    }
                }
            }
        }

        private static double[] PrefixLengths(Polyline3D pl)
        {
            int n = pl.VertexCount;
            var pre = new double[n > 0 ? n : 1];
            for (int i = 0; i < n - 1; i++)
            {
                pre[i + 1] = pre[i] + SegmentLen2D(pl.GetPointAt(i), pl.GetPointAt(i + 1));
            }
            return pre;
        }

        private static double SegmentLen2D(Point3D p0, Point3D p1)
        {
            double dx = p1.X - p0.X, dy = p1.Y - p0.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool TryIntersectSegments2D(
            Point2D a0, Point2D a1, Point2D b0, Point2D b1,
            out Point2D p, out double t, out double u)
        {
            p = default; t = u = 0;
            // a0 + t*(a1-a0) = b0 + u*(b1-b0)
            double rX = a1.X - a0.X, rY = a1.Y - a0.Y;
            double sX = b1.X - b0.X, sY = b1.Y - b0.Y;
            double denom = rX * sY - rY * sX;
            if (Math.Abs(denom) < 1e-12) return false;
            t = ((b0.X - a0.X) * sY - (b0.Y - a0.Y) * sX) / denom;
            u = ((b0.X - a0.X) * rY - (b0.Y - a0.Y) * rX) / denom;
            if (t < -1e-8 || t > 1 + 1e-8 || u < -1e-8 || u > 1 + 1e-8) return false;
            p = new Point2D(a0.X + t * rX, a0.Y + t * rY);
            return true;
        }

        private static IReadOnlyList<IntersectionCluster> ClusterHits(IReadOnlyList<LineHit> hits, double R)
        {
            var R2 = R * R;
            var remaining = hits.ToList();
            var clusters = new List<IntersectionCluster>();

            while (remaining.Count > 0)
            {
                var seed = remaining[0];
                remaining.RemoveAt(0);
                var group = new List<LineHit> { seed };
                bool changed;
                do
                {
                    changed = false;
                    for (int k = remaining.Count - 1; k >= 0; k--)
                    {
                        var h = remaining[k];
                        for (int g = 0; g < group.Count; g++)
                        {
                            if (Dist2(h.Point, group[g].Point) <= R2)
                            {
                                group.Add(h);
                                remaining.RemoveAt(k);
                                changed = true;
                                break;
                            }
                        }
                    }
                } while (changed);

                var idSet = new HashSet<Guid>();
                double sx = 0, sy = 0, n = 0;
                foreach (var h in group)
                {
                    idSet.Add(h.IdA);
                    idSet.Add(h.IdB);
                    sx += h.Point.X; sy += h.Point.Y; n++;
                }
                var center = n > 0
                    ? new Point2D(sx / n, sy / n)
                    : seed.Point;
                var ids = idSet.ToList();
                if (ids.Count < 2) continue;
                clusters.Add(new IntersectionCluster(center, ids, group));
            }

            return clusters;
        }

        private static double Dist2(Point2D a, Point2D b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }
    }
}
