using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Domain.DataStructures.DCEL
{
    /// <summary>
    /// DCEL 顶点（Doubly Connected Edge List - Vertex）
    /// 表示拓扑结构中的一个顶点
    /// </summary>
    public class Vertex
    {
        /// <summary>
        /// 顶点位置（Vertex Position）
        /// </summary>
        public Point2D Position { get; set; }

        /// <summary>
        /// 从该顶点出发的半边列表（Outgoing Half-Edges）
        /// </summary>
        public List<HalfEdge> OutgoingHalfedges { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="position">顶点位置</param>
        public Vertex(Point2D position)
        {
            Position = position;
            OutgoingHalfedges = new List<HalfEdge>();
        }

        /// <summary>
        /// 获取顶点度数（Degree）
        /// </summary>
        /// <returns>从该顶点出发的半边数量</returns>
        public int GetDegree()
        {
            return OutgoingHalfedges?.Count ?? 0;
        }

        /// <summary>
        /// 获取所有邻接顶点（Adjacent Vertices）
        /// </summary>
        /// <returns>所有通过边连接的顶点</returns>
        public IEnumerable<Vertex> GetAdjacentVertices()
        {
            if (OutgoingHalfedges == null)
                yield break;

            foreach (var he in OutgoingHalfedges)
            {
                var endVertex = he.GetEndVertex();
                if (endVertex != null)
                    yield return endVertex;
            }
        }

        /// <summary>
        /// 查找连接到指定顶点的半边
        /// </summary>
        /// <param name="target">目标顶点</param>
        /// <returns>连接的半边，如果不存在返回 null</returns>
        public HalfEdge FindEdgeTo(Vertex target)
        {
            if (OutgoingHalfedges == null || target == null)
                return null;

            foreach (var he in OutgoingHalfedges)
            {
                if (he.GetEndVertex() == target)
                    return he;
            }

            return null;
        }

        /// <summary>
        /// 转换为字符串（用于调试）
        /// </summary>
        public override string ToString()
        {
            return $"Vertex at ({Position.X:F2}, {Position.Y:F2}), Degree: {GetDegree()}";
        }
    }
}

