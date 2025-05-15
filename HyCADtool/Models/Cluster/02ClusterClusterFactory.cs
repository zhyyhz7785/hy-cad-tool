using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Models.Cluster
{
    /// <summary>
    /// 提供用于聚类的工厂方法，封装 DBSCAN 聚类逻辑，并构建完整 ClusterResult 结构。
    /// </summary>
    public static class ClusterFactory
    {
        /// <summary>
        /// 基于配置参数执行 DBSCAN 聚类，输出结构化 ClusterResult 集合
        /// </summary>
        /// <param name="points">原始点集</param>
        /// <param name="config">聚类配置</param>
        /// <returns>聚类结果列表，每项包含 MBR 与中心点</returns>
        public static List<ClusterResult> Create(List<Point3d> points, ClusterConfig config)
        {
            if (points == null || points.Count == 0)
                throw new ArgumentException("点集不能为空", nameof(points));

            int clusterId = 0;
            var visited = new HashSet<Point3d>();
            var pointClusterMap = new Dictionary<Point3d, int>();
            var results = new List<ClusterResult>();

            foreach (var pt in points)
            {
                if (visited.Contains(pt)) continue;
                visited.Add(pt);

                var neighbors = GetNeighbors(pt, points, config.EpsilonX, config.EpsilonY);
                if (neighbors.Count < config.MinPoints)
                {
                    pointClusterMap[pt] = -1; // 噪声点
                    continue;
                }

                // 创建新的聚类对象，赋予唯一编号
                var cluster = new ClusterResult { ClusterId = clusterId };

                // 递归扩展该聚类
                ExpandCluster(pt, neighbors, cluster, points, visited, pointClusterMap, config);

                // 设置基础包络（最小外包矩形）
                SetEnvelope(cluster);

                // 创建标准矩形轮廓
                SetEnvelopePolyline(cluster);

                // 基于配置扩展包络边界并创建多段线
                SetExpandedEnvelope(cluster, config);

                // 根据配置是否生成凸包
                if (config.GenerateConvexHull)
                {
                    SetConvexHull(cluster);
                }

                // 添加到聚类结果列表
                results.Add(cluster);
                clusterId++;
            }

            return results;
        }

        /// <summary>
        /// 执行 DBSCAN 聚类的核心扩展逻辑，递归构建邻近点集
        /// </summary>
        private static void ExpandCluster(Point3d pt, List<Point3d> neighbors, ClusterResult cluster,
            List<Point3d> allPoints, HashSet<Point3d> visited,
            Dictionary<Point3d, int> clusterMap, ClusterConfig config)
        {
            cluster.Points.Add(pt);
            clusterMap[pt] = cluster.ClusterId;

            for (int i = 0; i < neighbors.Count; i++)
            {
                var np = neighbors[i];
                if (!visited.Contains(np))
                {
                    visited.Add(np);
                    var newNeighbors = GetNeighbors(np, allPoints, config.EpsilonX, config.EpsilonY);
                    if (newNeighbors.Count >= config.MinPoints)
                        neighbors.AddRange(newNeighbors.Except(neighbors));
                }

                if (!clusterMap.ContainsKey(np))
                {
                    cluster.Points.Add(np);
                    clusterMap[np] = cluster.ClusterId;
                }
            }
        }

        /// <summary>
        /// 在给定容差范围内寻找点集中的邻居点
        /// </summary>
        private static List<Point3d> GetNeighbors(Point3d center, List<Point3d> all, double epsX, double epsY)
        {
            return all.Where(p =>
                Math.Abs(p.X - center.X) <= epsX / 2 &&
                Math.Abs(p.Y - center.Y) <= epsY / 2).ToList();
        }

        /// <summary>
        /// 设置 ClusterResult 的 EnvelopeExtents 属性（最小外包矩形）
        /// </summary>
        private static void SetEnvelope(ClusterResult cluster)
        {
            if (cluster.Points == null || cluster.Points.Count == 0) return;

            double minX = cluster.Points.Min(p => p.X);
            double maxX = cluster.Points.Max(p => p.X);
            double minY = cluster.Points.Min(p => p.Y);
            double maxY = cluster.Points.Max(p => p.Y);

            cluster.EnvelopeExtents = new Extents3d(
                new Point3d(minX, minY, 0),
                new Point3d(maxX, maxY, 0));
        }

        /// <summary>
        /// 设置 EnvelopePolyline 为标准 MBR 的矩形框
        /// </summary>
        private static void SetEnvelopePolyline(ClusterResult cluster)
        {
            var min = cluster.EnvelopeExtents.MinPoint;
            var max = cluster.EnvelopeExtents.MaxPoint;

            var pline = new Polyline();
            pline.AddVertexAt(0, new Point2d(min.X, min.Y), 0, 0, 0);
            pline.AddVertexAt(1, new Point2d(max.X, min.Y), 0, 0, 0);
            pline.AddVertexAt(2, new Point2d(max.X, max.Y), 0, 0, 0);
            pline.AddVertexAt(3, new Point2d(min.X, max.Y), 0, 0, 0);
            pline.Closed = true;

            cluster.EnvelopePolyline = pline;
        }

        /// <summary>
        /// 基于配置的扩展边距设置 ClusterResult 的 EnvelopeExpandedPolyline 属性
        /// </summary>
        private static void SetExpandedEnvelope(ClusterResult cluster, ClusterConfig config)
        {
            var min = cluster.EnvelopeExtents.MinPoint;
            var max = cluster.EnvelopeExtents.MaxPoint;

            var expandedMin = new Point3d(
                min.X - config.ExpandMargins.Left,
                min.Y - config.ExpandMargins.Bottom,
                0);

            var expandedMax = new Point3d(
                max.X + config.ExpandMargins.Right,
                max.Y + config.ExpandMargins.Top,
                0);

            var pline = new Polyline();
            pline.AddVertexAt(0, new Point2d(expandedMin.X, expandedMin.Y), 0, 0, 0);
            pline.AddVertexAt(1, new Point2d(expandedMax.X, expandedMin.Y), 0, 0, 0);
            pline.AddVertexAt(2, new Point2d(expandedMax.X, expandedMax.Y), 0, 0, 0);
            pline.AddVertexAt(3, new Point2d(expandedMin.X, expandedMax.Y), 0, 0, 0);
            pline.Closed = true;

            cluster.EnvelopeExpandedPolyline = pline;
        }

        /// <summary>
        /// 若启用，生成聚类点集的凸包轮廓线
        /// </summary>
        private static void SetConvexHull(ClusterResult cluster)
        {
            var sorted = cluster.Points.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
            List<Point3d> lower = new List<Point3d>();
            foreach (var p in sorted)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], p) <= 0)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(p);
            }
            List<Point3d> upper = new List<Point3d>();
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

            var hull = new Polyline();
            for (int i = 0; i < lower.Count; i++)
            {
                hull.AddVertexAt(i, new Point2d(lower[i].X, lower[i].Y), 0, 0, 0);
            }
            hull.Closed = true;
            cluster.ConvexHullPolyline = hull;
        }

        /// <summary>
        /// 计算叉积（用于凸包判断）
        /// </summary>
        private static double Cross(Point3d o, Point3d a, Point3d b)
        {
            return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        }
    }
}