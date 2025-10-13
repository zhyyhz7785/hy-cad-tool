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
        /// 转换为字符串（用于调试）
        /// </summary>
        public override string ToString()
        {
            return $"Vertex at ({Position.X:F2}, {Position.Y:F2}), OutgoingEdges: {OutgoingHalfedges.Count}";
        }
    }
}

