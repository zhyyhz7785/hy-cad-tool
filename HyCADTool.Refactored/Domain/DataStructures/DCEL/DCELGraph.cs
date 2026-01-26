using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Domain.DataStructures.DCEL
{
    /// <summary>
    /// DCEL 图（Doubly Connected Edge List Graph）
    /// 双连接边表数据结构，用于表示平面图的拓扑关系
    /// </summary>
    public class DCELGraph
    {
        /// <summary>
        /// 顶点列表（Vertices）
        /// </summary>
        public List<Vertex> Vertices { get; private set; }

        /// <summary>
        /// 半边列表（Half-Edges）
        /// </summary>
        public List<HalfEdge> HalfEdges { get; private set; }

        /// <summary>
        /// 所有面列表（All Faces）
        /// </summary>
        public List<Face> Faces { get; private set; }

        /// <summary>
        /// 外部面列表（Outer Faces）
        /// 动态计算，基于Face.IsOuter属性
        /// </summary>
        public IEnumerable<Face> OuterFaces => Faces.Where(f => f.IsOuter);

        /// <summary>
        /// 内部面列表（Inner Faces）
        /// 动态计算，基于Face.IsOuter属性
        /// </summary>
        public IEnumerable<Face> InnerFaces => Faces.Where(f => !f.IsOuter);

        /// <summary>
        /// 构造函数
        /// </summary>
        public DCELGraph()
        {
            Vertices = new List<Vertex>();
            HalfEdges = new List<HalfEdge>();
            Faces = new List<Face>();
        }

        /// <summary>
        /// 添加一个顶点（Add Vertex）
        /// </summary>
        /// <param name="position">顶点位置</param>
        /// <returns>创建的顶点</returns>
        public Vertex AddVertex(Point2D position)
        {
            var vertex = new Vertex(position);
            Vertices.Add(vertex);
            return vertex;
        }

        /// <summary>
        /// 添加一对孪生半边（Add Edge Pair）
        /// </summary>
        /// <param name="origin">起点顶点</param>
        /// <param name="destination">终点顶点</param>
        /// <returns>孪生半边对（he1: origin → destination, he2: destination → origin）</returns>
        public (HalfEdge, HalfEdge) AddEdgePair(Vertex origin, Vertex destination)
        {
            // 创建孪生半边
            var he1 = new HalfEdge { StartVertex = origin };
            var he2 = new HalfEdge { StartVertex = destination };

            // 设置孪生关系
            he1.Twin = he2;
            he2.Twin = he1;

            // 更新顶点的出射半边列表
            origin.OutgoingHalfedges.Add(he1);
            destination.OutgoingHalfedges.Add(he2);

            // 添加到半边列表
            HalfEdges.Add(he1);
            HalfEdges.Add(he2);

            return (he1, he2);
        }

        /// <summary>
        /// 创建一个面（Create Face）
        /// </summary>
        /// <param name="halfEdges">组成面的半边列表（逆时针顺序）</param>
        /// <returns>创建的面</returns>
        public Face CreateFace(List<HalfEdge> halfEdges)
        {
            var face = new Face();
            int edgeCount = halfEdges.Count;

            // 设置半边的拓扑关系
            for (int i = 0; i < edgeCount; i++)
            {
                halfEdges[i].IncidentFace = face;
                halfEdges[i].IsInitialized = true;
                halfEdges[i].Next = halfEdges[(i + 1) % edgeCount];
                halfEdges[i].Prev = halfEdges[(i - 1 + edgeCount) % edgeCount];
                face.Components.Add(halfEdges[i]);
            }

            Faces.Add(face);
            return face;
        }

        /// <summary>
        /// 清空图（Clear Graph）
        /// </summary>
        public void Clear()
        {
            Vertices.Clear();
            HalfEdges.Clear();
            Faces.Clear();
        }

        /// <summary>
        /// 获取图的统计信息（Get Statistics）
        /// </summary>
        public (int VertexCount, int EdgeCount, int FaceCount) GetStatistics()
        {
            return (Vertices.Count, HalfEdges.Count / 2, Faces.Count);
        }

        /// <summary>
        /// 验证 DCEL 拓扑一致性（Validate Topology）
        /// </summary>
        /// <param name="errors">输出错误列表</param>
        /// <returns>如果验证通过返回 true，否则返回 false</returns>
        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();

            // 1. 验证半边的孪生关系
            foreach (var he in HalfEdges)
            {
                if (he.Twin == null)
                {
                    errors.Add($"半边 {he} 缺少孪生边");
                    continue;
                }

                if (he.Twin.Twin != he)
                {
                    errors.Add($"半边 {he} 的孪生关系不对称");
                }
            }

            // 2. 验证面的闭合性
            foreach (var face in Faces)
            {
                if (face.Components == null || face.Components.Count == 0)
                {
                    errors.Add($"面 {face} 没有组成半边");
                    continue;
                }

                var startEdge = face.Components.FirstOrDefault();
                if (startEdge == null) continue;

                var currentEdge = startEdge;
                int count = 0;
                int maxCount = face.Components.Count * 2; // 防止无限循环

                do
                {
                    if (currentEdge.Next == null)
                    {
                        errors.Add($"面 {face} 的半边链不完整");
                        break;
                    }
                    currentEdge = currentEdge.Next;
                    count++;
                } while (currentEdge != startEdge && count < maxCount);

                if (count >= maxCount)
                {
                    errors.Add($"面 {face} 的半边链存在循环");
                }
            }

            // 3. 验证顶点的出射边
            foreach (var vertex in Vertices)
            {
                if (vertex.OutgoingHalfedges == null)
                {
                    errors.Add($"顶点 {vertex} 的出射边列表为空");
                    continue;
                }

                foreach (var he in vertex.OutgoingHalfedges)
                {
                    if (he.StartVertex != vertex)
                    {
                        errors.Add($"顶点 {vertex} 的出射边 {he} 起点不一致");
                    }
                }
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// 转换为字符串（用于调试）
        /// </summary>
        public override string ToString()
        {
            var stats = GetStatistics();
            var outerCount = OuterFaces.Count();
            var innerCount = InnerFaces.Count();
            return $"DCEL Graph: {stats.VertexCount} vertices, {stats.EdgeCount} edges, {stats.FaceCount} faces ({outerCount} outer, {innerCount} inner)";
        }
    }
}

