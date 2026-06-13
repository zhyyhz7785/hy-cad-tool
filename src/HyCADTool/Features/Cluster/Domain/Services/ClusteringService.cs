using HyCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Cluster.Domain.Services
{
    /// <summary>
    /// 聚类算法服务实现（平台无关）
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
            var bounds = CacheBounds(items, getBounds, n);
            bool[] visited = new bool[n];
            var clusters = new List<List<T>>();

            for (int i = 0; i < n; i++)
            {
                if (visited[i]) continue;

                var cluster = new List<T>();
                var queue = new Queue<int>();
                queue.Enqueue(i);
                visited[i] = true;

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    cluster.Add(items[current]);

                    for (int j = 0; j < n; j++)
                    {
                        if (visited[j]) continue;

                        if (bounds[current].IsWithinDistance(bounds[j], maxDistance))
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
        /// 空间网格（中心 cell + 自适应邻域）+ 并查集
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

            var bounds = CacheBounds(items, getBounds, n);
            var unionFind = new UnionFind(n);
            double cellSize = maxDistance;
            var grid = new Dictionary<(long Cx, long Cy), List<int>>();
            var largeIndices = new List<int>();

            for (int i = 0; i < n; i++)
            {
                if (bounds[i].Width > cellSize * 2 || bounds[i].Height > cellSize * 2)
                    largeIndices.Add(i);

                var center = bounds[i].Center;
                long cx = (long)Math.Floor(center.X / cellSize);
                long cy = (long)Math.Floor(center.Y / cellSize);
                var key = (cx, cy);
                if (!grid.TryGetValue(key, out var list))
                {
                    list = new List<int>();
                    grid[key] = list;
                }
                list.Add(i);
            }

            var candidates = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                candidates.Clear();
                var center = bounds[i].Center;
                double halfDiag = Math.Sqrt(
                    bounds[i].Width * bounds[i].Width + bounds[i].Height * bounds[i].Height) * 0.5;
                int radius = 1 + (int)Math.Ceiling((halfDiag + maxDistance) / cellSize);

                long cx0 = (long)Math.Floor(center.X / cellSize);
                long cy0 = (long)Math.Floor(center.Y / cellSize);

                for (long dx = -radius; dx <= radius; dx++)
                {
                    for (long dy = -radius; dy <= radius; dy++)
                    {
                        if (!grid.TryGetValue((cx0 + dx, cy0 + dy), out var list)) continue;
                        foreach (int j in list)
                        {
                            if (j > i)
                                candidates.Add(j);
                        }
                    }
                }

                foreach (int j in largeIndices)
                {
                    if (j > i)
                        candidates.Add(j);
                }

                foreach (int j in candidates)
                {
                    if (bounds[i].IsWithinDistance(bounds[j], maxDistance))
                        unionFind.Union(i, j);
                }
            }

            return BuildClustersFromUnionFind(items, unionFind, n);
        }

        /// <summary>
        /// X 排序 + 剪枝 + 并查集
        /// </summary>
        public List<List<T>> ClusterByBoundsDistanceSweep<T>(
            List<T> items,
            Func<T, BoundingBox> getBounds,
            double maxDistance)
        {
            if (items == null || items.Count == 0)
                return new List<List<T>>();

            int n = items.Count;
            if (n == 1)
                return new List<List<T>> { new List<T> { items[0] } };

            var bounds = CacheBounds(items, getBounds, n);
            var unionFind = new UnionFind(n);
            var order = new int[n];
            for (int i = 0; i < n; i++)
                order[i] = i;

            Array.Sort(order, (a, b) => bounds[a].MinPoint.X.CompareTo(bounds[b].MinPoint.X));

            for (int ai = 0; ai < n; ai++)
            {
                int i = order[ai];
                for (int aj = ai + 1; aj < n; aj++)
                {
                    int j = order[aj];
                    if (bounds[j].MinPoint.X - bounds[i].MaxPoint.X > maxDistance)
                        break;

                    if (bounds[i].IsWithinDistance(bounds[j], maxDistance))
                        unionFind.Union(i, j);
                }
            }

            return BuildClustersFromUnionFind(items, unionFind, n);
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
            double limitSq = maxDistance * maxDistance;

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

                    for (int j = 0; j < n; j++)
                    {
                        if (visited[j]) continue;

                        double dx = points[current].X - points[j].X;
                        double dy = points[current].Y - points[j].Y;
                        if (dx * dx + dy * dy <= limitSq)
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

        private static BoundingBox[] CacheBounds<T>(List<T> items, Func<T, BoundingBox> getBounds, int n)
        {
            var bounds = new BoundingBox[n];
            for (int i = 0; i < n; i++)
                bounds[i] = getBounds(items[i]);
            return bounds;
        }

        private static List<List<T>> BuildClustersFromUnionFind<T>(List<T> items, UnionFind unionFind, int n)
        {
            var groups = new Dictionary<int, List<T>>();
            for (int i = 0; i < n; i++)
            {
                int root = unionFind.Find(i);
                if (!groups.TryGetValue(root, out var cluster))
                {
                    cluster = new List<T>();
                    groups[root] = cluster;
                }
                cluster.Add(items[i]);
            }

            return groups.Values.ToList();
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
                while (_parent[x] != x)
                {
                    _parent[x] = _parent[_parent[x]];
                    x = _parent[x];
                }
                return x;
            }

            public void Union(int a, int b)
            {
                int ra = Find(a);
                int rb = Find(b);
                if (ra == rb) return;
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
