using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.DCEL.Domain.DataStructures
{
    /// <summary>
    /// DCEL 面（Doubly Connected Edge List - Face）
    /// 表示拓扑结构中的一个面（多边形区域）
    /// </summary>
    public class Face
    {
        /// <summary>
        /// 面的组成半边列表（Components）
        /// 包含外轮廓和内部孔洞的所有半边
        /// </summary>
        public List<HalfEdge> Components { get; set; }

        /// <summary>
        /// 是否为外轮廓面（Is Outer Contour）
        /// true = 逆时针方向（外轮廓），false = 顺时针方向（内部）
        /// </summary>
        public bool IsOuter { get; internal set; }

        // 缓存面积值
        private double? _cachedSignedArea;

        /// <summary>
        /// 构造函数
        /// </summary>
        public Face()
        {
            Components = new List<HalfEdge>();
            IsOuter = false;
        }

        /// <summary>
        /// 获取面的顶点数量
        /// </summary>
        public int GetVertexCount()
        {
            return Components.Count;
        }

        /// <summary>
        /// 获取面的所有顶点
        /// </summary>
        public IEnumerable<Vertex> GetVertices()
        {
            return Components.Select(he => he.StartVertex);
        }

        /// <summary>
        /// 计算有向面积（Signed Area）
        /// 正值 = 逆时针遍历 = 外轮廓
        /// 负值 = 顺时针遍历 = 内部
        /// </summary>
        /// <returns>有向面积</returns>
        public double CalculateSignedArea()
        {
            if (_cachedSignedArea.HasValue)
                return _cachedSignedArea.Value;

            if (Components == null || Components.Count < 3)
                return 0.0;

            double area = 0.0;
            foreach (var he in Components)
            {
                if (he?.StartVertex?.Position == null || he?.Next?.StartVertex?.Position == null)
                    continue;

                var current = he.StartVertex.Position;
                var next = he.Next.StartVertex.Position;
                area += (current.X * next.Y) - (next.X * current.Y);
            }

            _cachedSignedArea = area * 0.5;
            return _cachedSignedArea.Value;
        }

        /// <summary>
        /// 获取绝对面积
        /// </summary>
        public double GetArea()
        {
            return Math.Abs(CalculateSignedArea());
        }

        /// <summary>
        /// 根据有向面积设置面的方向（内部方法）
        /// 在构建DCEL时由BuilderService调用
        /// </summary>
        internal void SetOrientation()
        {
            double signedArea = CalculateSignedArea();
            IsOuter = signedArea > 0;
        }

        /// <summary>
        /// 清除缓存（当面被修改时调用）
        /// </summary>
        internal void InvalidateCache()
        {
            _cachedSignedArea = null;
        }

        /// <summary>
        /// 转换为字符串（用于调试）
        /// </summary>
        public override string ToString()
        {
            return $"Face with {Components.Count} edges, IsOuter: {IsOuter}, Area: {GetArea():F2}";
        }
    }
}

