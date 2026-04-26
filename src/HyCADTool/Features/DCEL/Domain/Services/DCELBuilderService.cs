using HyCADTool.Features.DCEL.Domain.DataStructures;
using HyCADTool.Shared.Geometry;
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
        private const int MaxIterations = 1000;

        /// <summary>
        /// 从线段列表构建 DCEL 图
        /// </summary>
        public DCELGraph BuildFromSegments(List<Line2D> segments, Tolerance tolerance)
        {
            if (segments == null || segments.Count == 0)
                return new DCELGraph();

            var graph = new DCELGraph();
            var vertexMap = new Dictionary<Point2D, Vertex>(new Point2DEqualityComparer(tolerance));

            // 1. 为每条线段创建半边对
            foreach (var segment in segments)
            {
                var startVertex = GetOrCreateVertex(graph, vertexMap, segment.StartPoint);
                var endVertex = GetOrCreateVertex(graph, vertexMap, segment.EndPoint);
                graph.AddEdgePair(startVertex, endVertex);
            }

            // 2. 删除度数小于2的孤立顶点
            RemoveIsolatedVertices(graph);

            // 3. 设置半边的 Next/Prev 关系，构建面（构建时直接设置IsOuter）
            BuildFacesFromHalfEdges(graph);

            return graph;
        }

        /// <summary>
        /// 分类面为外轮廓或内部（已弃用，现在在面构建时直接设置IsOuter）
        /// 保留以兼容接口定义
        /// </summary>
        [Obsolete("面的方向现在在构建时自动设置，无需调用此方法")]
        public void ClassifyFaces(DCELGraph graph)
        {
            // 该方法已被弃用，面的IsOuter属性在构建时直接设置
            // 为了保持接口兼容性暂时保留，但实际不执行任何操作
        }

        /// <summary>
        /// 计算面的有向面积（已弃用，使用Face.CalculateSignedArea()代替）
        /// 保留以兼容接口定义
        /// </summary>
        [Obsolete("使用Face.CalculateSignedArea()代替")]
        public double CalculateSignedArea(Face face)
        {
            return face.CalculateSignedArea();
        }

        /// <summary>
        /// 获取或创建顶点（从缓存中）
        /// </summary>
        private Vertex GetOrCreateVertex(DCELGraph graph, Dictionary<Point2D, Vertex> vertexMap, Point2D position)
        {
            if (!vertexMap.TryGetValue(position, out var vertex))
            {
                vertex = graph.AddVertex(position);
                vertexMap[position] = vertex;
            }
            return vertex;
        }

        /// <summary>
        /// 删除孤立的顶点（度数 <= 1）
        /// </summary>
        private void RemoveIsolatedVertices(DCELGraph graph)
        {
            int iteration = 0;
            bool hasRemoved = true;

            while (hasRemoved && iteration < MaxIterations)
            {
                hasRemoved = false;
                var halfEdgesToRemove = new HashSet<HalfEdge>();

                // 收集需要删除的半边
                foreach (var he in graph.HalfEdges.ToList())
                {
                    if (he.StartVertex.OutgoingHalfedges.Count <= 1)
                    {
                        halfEdgesToRemove.Add(he);
                        if (he.Twin != null)
                            halfEdgesToRemove.Add(he.Twin);
                    }
                }

                // 删除半边
                if (halfEdgesToRemove.Count > 0)
                {
                    hasRemoved = true;
                    foreach (var he in halfEdgesToRemove)
                    {
                        he.StartVertex.OutgoingHalfedges.Remove(he);
                        graph.HalfEdges.Remove(he);
                    }

                    // 删除度数为0的顶点
                    var verticesToRemove = graph.Vertices
                        .Where(v => v.OutgoingHalfedges.Count == 0)
                        .ToList();
                    foreach (var vertex in verticesToRemove)
                    {
                        graph.Vertices.Remove(vertex);
                    }
                }
                iteration++;
            }
        }

        /// <summary>
        /// 构建面：设置 Next/Prev 关系
        /// </summary>
        private void BuildFacesFromHalfEdges(DCELGraph graph)
        {
            // 按顺序排序半边（按起点坐标）
            var halfEdgeSet = graph.HalfEdges
                .OrderBy(he => he.StartVertex.Position.X)
                .ThenBy(he => he.StartVertex.Position.Y)
                .ThenBy(he => he.Twin.StartVertex.Position.Y)
                .ToList();

            while (halfEdgeSet.Any())
            {
                var startEdge = halfEdgeSet.FirstOrDefault(he => !he.IsInitialized);
                if (startEdge == null)
                    break;

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

                    if (iterationCount > MaxIterations)
                        break;

                } while (currentEdge != null && currentEdge != startEdge);

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
        /// Point2D 相等性比较器（考虑容差）
        /// </summary>
        private class Point2DEqualityComparer : IEqualityComparer<Point2D>
        {
            private readonly Tolerance _tolerance;

            public Point2DEqualityComparer(Tolerance tolerance)
            {
                _tolerance = tolerance;
            }

            public bool Equals(Point2D p1, Point2D p2)
            {
                return Math.Abs(p1.X - p2.X) <= _tolerance.Value &&
                       Math.Abs(p1.Y - p2.Y) <= _tolerance.Value;
            }

            public int GetHashCode(Point2D point)
            {
                int hashX = Math.Round(point.X / _tolerance.Value).GetHashCode();
                int hashY = Math.Round(point.Y / _tolerance.Value).GetHashCode();
                return hashX ^ hashY;
            }
        }
    }
}

