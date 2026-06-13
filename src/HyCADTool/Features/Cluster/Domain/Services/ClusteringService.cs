using HyCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Cluster.Domain.Services
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
        /// 空间网格 + 并查集：候选对由 expanded bbox 覆盖的网格单元筛出，再精确 DistanceTo 判定。
        /// </summary>
        public List<List<T>> ClusterByBoundsDistanceGrid<T>(
            List<T> items,
            Func<T, BoundingBox> getBounds,
            double maxDistance)
        {
            if (items == null || items.Count == 0)
                return new List<List<T>>();

            int n = items.Count;
            if (n == 1)
                return new List<List<T>> { new List<T> { items[0] } };

            var bounds = new BoundingBox[n];
            for (int i = 0; i < n; i++)
                bounds[i] = getBounds(items[i]);

            var parent = new int[n];
            var rank = new int[n];
            for (int i = 0; i < n; i++)
                parent[i] = i;

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }
                return x;
            }

            void Union(int a, int b)
            {
                int ra = Find(a);
                int rb = Find(b);
                if (ra == rb) return;
                if (rank[ra] < rank[rb])
                    parent[ra] = rb;
                else if (rank[ra] > rank[rb])
                    parent[rb] = ra;
                else
                {
                    parent[rb] = ra;
                    rank[ra]++;
                }
            }

            double cellSize = maxDistance;
            var grid = new Dictionary<(long Cx, long Cy), List<int>>();

            for (int i = 0; i < n; i++)
            {
                ForEachGridCell(bounds[i], cellSize, (cx, cy) =>
                {
                    var key = (cx, cy);
                    if (!grid.TryGetValue(key, out var list))
                    {
                        list = new List<int>();
                        grid[key] = list;
                    }
                    list.Add(i);
                });
            }

            var candidates = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                candidates.Clear();
                var query = bounds[i].Expand(maxDistance, maxDistance);
                ForEachGridCell(query, cellSize, (cx, cy) =>
                {
                    if (!grid.TryGetValue((cx, cy), out var list)) return;
                    foreach (int j in list)
                    {
                        if (j > i)
                            candidates.Add(j);
                    }
                });

                foreach (int j in candidates)
                {
                    if (bounds[i].DistanceTo(bounds[j]) <= maxDistance)
                        Union(i, j);
                }
            }

            var groups = new Dictionary<int, List<T>>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(i);
                if (!groups.TryGetValue(root, out var cluster))
                {
                    cluster = new List<T>();
                    groups[root] = cluster;
                }
                cluster.Add(items[i]);
            }

            return groups.Values.ToList();
        }

        private static void ForEachGridCell(BoundingBox region, double cellSize, Action<long, long> action)
        {
            long minCx = (long)Math.Floor(region.MinPoint.X / cellSize);
            long maxCx = (long)Math.Floor(region.MaxPoint.X / cellSize);
            long minCy = (long)Math.Floor(region.MinPoint.Y / cellSize);
            long maxCy = (long)Math.Floor(region.MaxPoint.Y / cellSize);

            for (long cx = minCx; cx <= maxCx; cx++)
            {
                for (long cy = minCy; cy <= maxCy; cy++)
                    action(cx, cy);
            }
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

