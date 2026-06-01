using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.Cluster.Domain.Models;
using HyCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Cluster.Services
{
    /// <summary>
    /// DBSCAN 聚类工厂服务
    /// 替代旧 ClusterFactory
    /// </summary>
    public class ClusterFactoryService
    {
        /// <summary>
        /// 基于配置参数执行 DBSCAN 聚类（输入 AutoCAD Point3d，内部转换为 Domain Point2D）
        /// </summary>
        public List<ClusterResult> CreateClusters(List<Point3d> points, ClusterConfig config)
        {
            if (points == null || points.Count == 0)
                return new List<ClusterResult>();

            // 转换为 Domain 类型
            var domainPoints = points.Select(p => new Point2D(p.X, p.Y)).ToList();
            return CreateClustersFromDomain(domainPoints, config);
        }

        /// <summary>
        /// 基于 Domain 类型的聚类（纯逻辑）
        /// </summary>
        public List<ClusterResult> CreateClustersFromDomain(List<Point2D> points, ClusterConfig config)
        {
            if (points == null || points.Count == 0)
                return new List<ClusterResult>();

            int clusterId = 0;
            var visited = new HashSet<int>();
            var pointClusterMap = new Dictionary<int, int>();
            var results = new List<ClusterResult>();

            for (int i = 0; i < points.Count; i++)
            {
                if (visited.Contains(i)) continue;
                visited.Add(i);

                var neighbors = GetNeighborIndices(i, points, config.EpsilonX, config.EpsilonY);
                if (neighbors.Count < config.MinPoints)
                {
                    pointClusterMap[i] = -1; // 噪声点
                    continue;
                }

                var cluster = new ClusterResult { ClusterId = clusterId };
                ExpandCluster(i, neighbors, cluster, points, visited, pointClusterMap, config);

                // 构建包络
                SetEnvelope(cluster);
                SetExpandedEnvelope(cluster, config);

                if (config.GenerateConvexHull)
                    SetConvexHull(cluster);

                results.Add(cluster);
                clusterId++;
            }

            return results;
        }

        #region AutoCAD 辅助：从 ClusterResult 创建 Polyline

        /// <summary>
        /// 从聚类结果的 Envelope 创建 AutoCAD Polyline
        /// </summary>
        public static Polyline CreateEnvelopePolyline(ClusterResult cluster)
        {
            var bb = cluster.Envelope;
            var pline = new Polyline();
            pline.AddVertexAt(0, new Point2d(bb.MinPoint.X, bb.MinPoint.Y), 0, 0, 0);
            pline.AddVertexAt(1, new Point2d(bb.MaxPoint.X, bb.MinPoint.Y), 0, 0, 0);
            pline.AddVertexAt(2, new Point2d(bb.MaxPoint.X, bb.MaxPoint.Y), 0, 0, 0);
            pline.AddVertexAt(3, new Point2d(bb.MinPoint.X, bb.MaxPoint.Y), 0, 0, 0);
            pline.Closed = true;
            return pline;
        }

        /// <summary>
        /// 从聚类结果的 ExpandedEnvelope 创建 AutoCAD Polyline
        /// </summary>
        public static Polyline CreateExpandedEnvelopePolyline(ClusterResult cluster)
        {
            var bb = cluster.ExpandedEnvelope;
            var pline = new Polyline();
            pline.AddVertexAt(0, new Point2d(bb.MinPoint.X, bb.MinPoint.Y), 0, 0, 0);
            pline.AddVertexAt(1, new Point2d(bb.MaxPoint.X, bb.MinPoint.Y), 0, 0, 0);
            pline.AddVertexAt(2, new Point2d(bb.MaxPoint.X, bb.MaxPoint.Y), 0, 0, 0);
            pline.AddVertexAt(3, new Point2d(bb.MinPoint.X, bb.MaxPoint.Y), 0, 0, 0);
            pline.Closed = true;
            return pline;
        }

        /// <summary>
        /// 从凸包顶点创建 AutoCAD Polyline
        /// </summary>
        public static Polyline CreateConvexHullPolyline(ClusterResult cluster)
        {
            if (cluster.ConvexHullVertices == null || cluster.ConvexHullVertices.Count < 3)
                return null;

            var hull = new Polyline();
            for (int i = 0; i < cluster.ConvexHullVertices.Count; i++)
            {
                var pt = cluster.ConvexHullVertices[i];
                hull.AddVertexAt(i, new Point2d(pt.X, pt.Y), 0, 0, 0);
            }
            hull.Closed = true;
            return hull;
        }

        #endregion

        #region DBSCAN 核心

        private void ExpandCluster(int ptIdx, List<int> neighbors, ClusterResult cluster,
            List<Point2D> allPoints, HashSet<int> visited,
            Dictionary<int, int> clusterMap, ClusterConfig config)
        {
            cluster.Points.Add(allPoints[ptIdx]);
            clusterMap[ptIdx] = cluster.ClusterId;

            for (int i = 0; i < neighbors.Count; i++)
            {
                int np = neighbors[i];
                if (!visited.Contains(np))
                {
                    visited.Add(np);
                    var newNeighbors = GetNeighborIndices(np, allPoints, config.EpsilonX, config.EpsilonY);
                    if (newNeighbors.Count >= config.MinPoints)
                    {
                        foreach (var nn in newNeighbors)
                            if (!neighbors.Contains(nn))
                                neighbors.Add(nn);
                    }
                }

                if (!clusterMap.ContainsKey(np))
                {
                    cluster.Points.Add(allPoints[np]);
                    clusterMap[np] = cluster.ClusterId;
                }
            }
        }

        private List<int> GetNeighborIndices(int centerIdx, List<Point2D> all, double epsX, double epsY)
        {
            var center = all[centerIdx];
            var result = new List<int>();
            for (int i = 0; i < all.Count; i++)
            {
                if (Math.Abs(all[i].X - center.X) <= epsX / 2 &&
                    Math.Abs(all[i].Y - center.Y) <= epsY / 2)
                    result.Add(i);
            }
            return result;
        }

        #endregion

        #region 包络构建

        private static void SetEnvelope(ClusterResult cluster)
        {
            if (cluster.Points == null || cluster.Points.Count == 0) return;

            double minX = cluster.Points.Min(p => p.X);
            double maxX = cluster.Points.Max(p => p.X);
            double minY = cluster.Points.Min(p => p.Y);
            double maxY = cluster.Points.Max(p => p.Y);

            cluster.Envelope = new BoundingBox(
                new Point2D(minX, minY),
                new Point2D(maxX, maxY));
        }

        private static void SetExpandedEnvelope(ClusterResult cluster, ClusterConfig config)
        {
            var bb = cluster.Envelope;
            cluster.ExpandedEnvelope = new BoundingBox(
                new Point2D(bb.MinPoint.X - config.ExpandMargins.Left, bb.MinPoint.Y - config.ExpandMargins.Bottom),
                new Point2D(bb.MaxPoint.X + config.ExpandMargins.Right, bb.MaxPoint.Y + config.ExpandMargins.Top));
        }

        private static void SetConvexHull(ClusterResult cluster)
        {
            var sorted = cluster.Points.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
            var lower = new List<Point2D>();
            foreach (var p in sorted)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], p) <= 0)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(p);
            }
            var upper = new List<Point2D>();
            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                var p = sorted[i];
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], p) <= 0)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(p);
            }
            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);

            cluster.ConvexHullVertices = lower;
        }

        private static double Cross(Point2D o, Point2D a, Point2D b)
        {
            return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        }

        #endregion
    }
}
