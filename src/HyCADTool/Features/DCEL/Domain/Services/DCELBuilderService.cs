using HyCADTool.Features.DCEL.Domain.DataStructures;
using HyCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.DCEL.Domain.Services
{
    /// <summary>
    /// DCEL 构建服务实现（DCEL Builder Service Implementation）
    /// 负责从几何数据构建双连接边表数据结构
    /// </summary>
    public class DCELBuilderService : IDCELBuilderService
    {
        /// <summary>
        /// 从线段列表构建 DCEL 图
        /// </summary>
        public DCELGraph BuildFromSegments(List<Line2D> segments, Tolerance tolerance)
        {
            if (segments == null || segments.Count == 0)
                return new DCELGraph();

            var graph = new DCELGraph();
            var comparer = new Point2DEqualityComparer(tolerance);
            var vertexMap = new Dictionary<Point2D, Vertex>(comparer);
            var vertexIndex = new Dictionary<Vertex, int>();
            var edgeKeys = new HashSet<(int, int)>();

            // 1. 为每条线段创建半边对（过滤零长线段与重复线段）
            foreach (var segment in segments)
            {
                // 零长线段（量化后起终点同格）会产生自环半边，跳过
                if (comparer.Equals(segment.StartPoint, segment.EndPoint))
                    continue;

                var startVertex = GetOrCreateVertex(graph, vertexMap, vertexIndex, segment.StartPoint);
                var endVertex = GetOrCreateVertex(graph, vertexMap, vertexIndex, segment.EndPoint);

                // 重复线段去重（无向：A→B 与 B→A 视为同一条边）
                int i1 = vertexIndex[startVertex];
                int i2 = vertexIndex[endVertex];
                var key = i1 < i2 ? (i1, i2) : (i2, i1);
                if (!edgeKeys.Add(key))
                    continue;

                graph.AddEdgePair(startVertex, endVertex);
            }

            // 2. 删除悬挂链（度数<=1 的顶点及其边，级联剥离）
            RemoveIsolatedVertices(graph);

            // 3. 设置半边的 Next/Prev 关系，构建面（构建时直接设置IsOuter）
            BuildFacesFromHalfEdges(graph);

            return graph;
        }

        /// <summary>
        /// 获取或创建顶点（从缓存中）
        /// </summary>
        private Vertex GetOrCreateVertex(
            DCELGraph graph,
            Dictionary<Point2D, Vertex> vertexMap,
            Dictionary<Vertex, int> vertexIndex,
            Point2D position)
        {
            if (!vertexMap.TryGetValue(position, out var vertex))
            {
                vertex = graph.AddVertex(position);
                vertexMap[position] = vertex;
                vertexIndex[vertex] = vertexIndex.Count;
            }
            return vertex;
        }

        /// <summary>
        /// 删除悬挂的顶点与边（度数 &lt;= 1）。
        /// 队列式级联剥离：删除一条悬挂边后邻居顶点降度，若也变为悬挂则继续剥离。
        /// 总复杂度 O(V + E)，且无迭代次数上限（任意长的悬挂链都能剥干净）。
        /// </summary>
        private void RemoveIsolatedVertices(DCELGraph graph)
        {
            var removedEdges = new HashSet<HalfEdge>();
            var queue = new Queue<Vertex>();

            foreach (var vertex in graph.Vertices)
            {
                if (vertex.OutgoingHalfedges.Count <= 1)
                    queue.Enqueue(vertex);
            }

            while (queue.Count > 0)
            {
                var vertex = queue.Dequeue();

                // 度数0：无边可删；度数1：删除唯一出射边及其孪生边
                if (vertex.OutgoingHalfedges.Count != 1)
                    continue;

                var halfEdge = vertex.OutgoingHalfedges[0];
                var twin = halfEdge.Twin;

                vertex.OutgoingHalfedges.Clear();
                removedEdges.Add(halfEdge);

                if (twin != null)
                {
                    removedEdges.Add(twin);
                    var neighbor = twin.StartVertex;
                    if (neighbor != null)
                    {
                        neighbor.OutgoingHalfedges.Remove(twin);
                        if (neighbor.OutgoingHalfedges.Count <= 1)
                            queue.Enqueue(neighbor);
                    }
                }
            }

            if (removedEdges.Count > 0)
                graph.HalfEdges.RemoveAll(removedEdges.Contains);

            graph.Vertices.RemoveAll(v => v.OutgoingHalfedges.Count == 0);
        }

        /// <summary>
        /// 构建面：设置 Next/Prev 关系
        /// </summary>
        private void BuildFacesFromHalfEdges(DCELGraph graph)
        {
            // 按顺序排序半边（按起点坐标），保证起始边选取的确定性
            var sortedEdges = graph.HalfEdges
                .OrderBy(he => he.StartVertex.Position.X)
                .ThenBy(he => he.StartVertex.Position.Y)
                .ThenBy(he => he.Twin.StartVertex.Position.Y)
                .ToList();

            // 单个面的边数不可能超过全部半边数，以此为环路保护上限
            // （旧实现固定 1000 会把超长边界面静默丢弃）
            int maxFaceEdges = graph.HalfEdges.Count + 1;

            // 索引推进替代每轮 FirstOrDefault 全表扫描（O(E²) → O(E)）
            for (int scanIndex = 0; scanIndex < sortedEdges.Count; scanIndex++)
            {
                var startEdge = sortedEdges[scanIndex];
                if (startEdge.IsInitialized)
                    continue;

                startEdge.IsInitialized = true;
                var currentEdge = startEdge;
                var faceEdges = new List<HalfEdge>();
                int iterationCount = 0;

                do
                {
                    faceEdges.Add(currentEdge);
                    currentEdge.IsInitialized = true;

                    // 使用左侧法则找到下一条边
                    var nextEdge = FindNextEdgeCounterClockwise(currentEdge);
                    if (nextEdge == null)
                        break;

                    currentEdge = nextEdge;
                    iterationCount++;

                    if (iterationCount > maxFaceEdges)
                        break;

                } while (currentEdge != startEdge);

                // 如果形成闭合面，创建面并直接设置IsOuter属性
                if (currentEdge == startEdge && faceEdges.Count >= 3)
                {
                    var face = graph.CreateFace(faceEdges);
                    face.SetOrientation(); // 根据有向面积设置IsOuter属性
                }
            }
        }

        /// <summary>
        /// 左侧法则：找到逆时针方向的下一条半边
        /// 注意：与原代码保持一致，使用反向向量计算角度
        /// </summary>
        private HalfEdge FindNextEdgeCounterClockwise(HalfEdge currentEdge)
        {
            var vertex1 = currentEdge.StartVertex;
            var vertex2 = currentEdge.Twin.StartVertex;

            // 重要：与原代码保持一致，使用反向向量（从vertex2指向vertex1）
            var vector = new Vector2D(
                vertex1.Position.X - vertex2.Position.X,
                vertex1.Position.Y - vertex2.Position.Y
            );

            HalfEdge bestEdge = null;
            double minAngle = double.MaxValue;

            foreach (var candidate in vertex2.OutgoingHalfedges)
            {
                if (candidate == currentEdge.Twin)
                    continue; // 跳过孪生边

                // 计算候选边的向量（从vertex2指向目标顶点）
                var currentVector = new Vector2D(
                    candidate.Twin.StartVertex.Position.X - vertex2.Position.X,
                    candidate.Twin.StartVertex.Position.Y - vertex2.Position.Y
                );

                double angle = CalculateCounterClockwiseAngle(vector, currentVector);

                if (angle < minAngle)
                {
                    minAngle = angle;
                    bestEdge = candidate;
                }
            }

            return bestEdge;
        }

        /// <summary>
        /// 计算逆时针夹角（范围 [0, 2π]）
        /// </summary>
        private double CalculateCounterClockwiseAngle(Vector2D from, Vector2D to)
        {
            double dot = from.X * to.X + from.Y * to.Y;
            double crossZ = from.X * to.Y - from.Y * to.X;
            double angle = Math.Atan2(crossZ, dot);
            return angle >= 0 ? angle : (2 * Math.PI + angle);
        }

        /// <summary>
        /// Point2D 相等性比较器（网格量化语义）。
        /// Equals 与 GetHashCode 使用同一套量化函数（坐标/容差四舍五入到整数格），
        /// 保证「相等的点哈希必然相同」——旧实现 Equals 用容差比较而哈希用网格取整，
        /// 容差内的两点可能落入不同哈希桶，导致顶点随机分裂、面无法闭合。
        /// </summary>
        private class Point2DEqualityComparer : IEqualityComparer<Point2D>
        {
            private readonly double _cellSize;

            public Point2DEqualityComparer(Tolerance tolerance)
            {
                _cellSize = tolerance != null && tolerance.Value > 0 ? tolerance.Value : 1e-9;
            }

            private long QuantizeX(Point2D p) => (long)Math.Round(p.X / _cellSize);
            private long QuantizeY(Point2D p) => (long)Math.Round(p.Y / _cellSize);

            public bool Equals(Point2D p1, Point2D p2)
            {
                return QuantizeX(p1) == QuantizeX(p2) && QuantizeY(p1) == QuantizeY(p2);
            }

            public int GetHashCode(Point2D point)
            {
                unchecked
                {
                    return (QuantizeX(point).GetHashCode() * 397) ^ QuantizeY(point).GetHashCode();
                }
            }
        }
    }
}
