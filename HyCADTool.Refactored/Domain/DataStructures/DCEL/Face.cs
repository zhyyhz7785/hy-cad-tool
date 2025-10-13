using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Domain.DataStructures.DCEL
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
        /// 构造函数
        /// </summary>
        public Face()
        {
            Components = new List<HalfEdge>();
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
        /// 转换为字符串（用于调试）
        /// </summary>
        public override string ToString()
        {
            return $"Face with {Components.Count} edges";
        }
    }
}

