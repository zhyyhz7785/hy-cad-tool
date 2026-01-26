using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System;

namespace HyCADTool.Refactored.Domain.DataStructures.DCEL
{
    /// <summary>
    /// DCEL 半边（Doubly Connected Edge List - Half-Edge）
    /// 表示一条边的一半，包含拓扑连接信息
    /// </summary>
    public class HalfEdge
    {
        /// <summary>
        /// 半边的起始顶点（Start Vertex）
        /// </summary>
        public Vertex StartVertex { get; set; }

        /// <summary>
        /// 孪生边（Twin）- 与之相反的边
        /// </summary>
        public HalfEdge Twin { get; set; }

        /// <summary>
        /// 下一条半边（Next Half-Edge）
        /// 沿面边界逆时针方向的下一条边
        /// </summary>
        public HalfEdge Next { get; set; }

        /// <summary>
        /// 前一条半边（Previous Half-Edge）
        /// 沿面边界逆时针方向的前一条边
        /// </summary>
        public HalfEdge Prev { get; set; }

        /// <summary>
        /// 半边所属的面（Incident Face）
        /// </summary>
        public Face IncidentFace { get; set; }

        /// <summary>
        /// 是否已初始化（Is Initialized）
        /// 表示半边是否已被分配到某个面
        /// </summary>
        public bool IsInitialized { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public HalfEdge()
        {
            StartVertex = null;
            Twin = null;
            Next = null;
            Prev = null;
            IncidentFace = null;
            IsInitialized = false;
        }

        /// <summary>
        /// 获取半边的方向向量（Direction Vector）
        /// </summary>
        /// <returns>从起点到终点的向量</returns>
        /// <exception cref="InvalidOperationException">半边未正确初始化时抛出</exception>
        public Vector2D GetVector()
        {
            if (Twin?.StartVertex == null || StartVertex == null)
                throw new InvalidOperationException("半边未正确初始化（Twin 或 StartVertex 为空）");

            return new Vector2D(
                Twin.StartVertex.Position.X - StartVertex.Position.X,
                Twin.StartVertex.Position.Y - StartVertex.Position.Y
            );
        }

        /// <summary>
        /// 获取半边的终点（End Vertex）
        /// </summary>
        public Vertex GetEndVertex()
        {
            return Twin?.StartVertex;
        }

        /// <summary>
        /// 获取半边的长度（Length）
        /// </summary>
        /// <returns>边的欧几里得长度</returns>
        public double GetLength()
        {
            var endVertex = GetEndVertex();
            if (endVertex == null || StartVertex == null)
                return 0.0;

            return StartVertex.Position.DistanceTo(endVertex.Position);
        }

        /// <summary>
        /// 获取半边的方向角（弧度）
        /// </summary>
        /// <returns>从起点到终点的角度，范围 [-π, π]</returns>
        public double GetAngle()
        {
            try
            {
                var vector = GetVector();
                return Math.Atan2(vector.Y, vector.X);
            }
            catch
            {
                return 0.0;
            }
        }

        /// <summary>
        /// 判断是否为边界边（Boundary Edge）
        /// 边界边的孪生边没有关联的面
        /// </summary>
        public bool IsBoundaryEdge()
        {
            return Twin?.IncidentFace == null;
        }

        /// <summary>
        /// 判断是否已完全初始化
        /// </summary>
        public bool IsFullyInitialized()
        {
            return StartVertex != null &&
                   Twin != null &&
                   Next != null &&
                   Prev != null &&
                   IncidentFace != null;
        }

        /// <summary>
        /// 转换为字符串（用于调试）
        /// </summary>
        public override string ToString()
        {
            var start = StartVertex != null ? $"({StartVertex.Position.X:F2}, {StartVertex.Position.Y:F2})" : "null";
            var end = GetEndVertex() != null ? $"({GetEndVertex().Position.X:F2}, {GetEndVertex().Position.Y:F2})" : "null";
            return $"HalfEdge: {start} -> {end}, Initialized: {IsInitialized}";
        }
    }
}

