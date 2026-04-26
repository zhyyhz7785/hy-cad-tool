using HyCADTool.Shared.Geometry;
using System;
using System.Collections.Generic;

namespace HyCADTool.Domain.Services.GeometryAlgorithms
{
    /// <summary>
    /// 聚类算法服务实现（平台无关）
    /// 使用 BFS（广度优先搜索）进行基于距离的聚类
    /// </summary>
    public class ClusteringService : IClusteringService
    {
        /// <summary>
        /// 基于边界框距离的聚类算法（BFS）
        /// </summary>
        public List<List<T>> ClusterByBoundsDistance<T>(
            List<T> items,
            Func<T, BoundingBox> getBounds,
            double maxDistance)
        {
            if (items == null || items.Count == 0)
                return new List<List<T>>();

            int n = items.Count;
            bool[] visited = new bool[n];
            List<List<T>> clusters = new List<List<T>>();

            // BFS 聚类
            for (int i = 0; i < n; i++)
            {
                if (visited[i]) continue;

                var cluster = new List<T>();
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(i);
                visited[i] = true;

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    cluster.Add(items[current]);

                    BoundingBox currentBounds = getBounds(items[current]);

                    // 查找相邻项目
                    for (int j = 0; j < n; j++)
                    {
                        if (visited[j]) continue;

                        BoundingBox otherBounds = getBounds(items[j]);
                        double distance = currentBounds.DistanceTo(otherBounds);

                        if (distance <= maxDistance)
                        {
                            queue.Enqueue(j);
                            visited[j] = true;
                        }
                    }
                }

                if (cluster.Count > 0)
                    clusters.Add(cluster);
            }

            return clusters;
        }

        /// <summary>
        /// 基于点距离的聚类算法（BFS）
        /// </summary>
        public List<List<Point2D>> ClusterByPointDistance(
            List<Point2D> points,
            double maxDistance)
        {
            if (points == null || points.Count == 0)
                return new List<List<Point2D>>();

            int n = points.Count;
            bool[] visited = new bool[n];
            List<List<Point2D>> clusters = new List<List<Point2D>>();

            // BFS 聚类
            for (int i = 0; i < n; i++)
            {
                if (visited[i]) continue;

                var cluster = new List<Point2D>();
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(i);
                visited[i] = true;

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    cluster.Add(points[current]);

                    // 查找相邻点
                    for (int j = 0; j < n; j++)
                    {
                        if (visited[j]) continue;

                        double distance = points[current].DistanceTo(points[j]);

                        if (distance <= maxDistance)
                        {
                            queue.Enqueue(j);
                            visited[j] = true;
                        }
                    }
                }

                if (cluster.Count > 0)
                    clusters.Add(cluster);
            }

            return clusters;
        }
    }
}

