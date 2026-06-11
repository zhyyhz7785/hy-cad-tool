using HyCAD.Geometry;
using HyCAD.Geometry.Algorithms;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Reinforcement.Domain
{
    /// <summary>
    /// 配筋有效区域：外轮廓 + 孔洞；用于嵌套多边形分组与出界裁剪。
    /// </summary>
    public sealed class ReinRegion
    {
        public Polyline2D Outer { get; }
        public IReadOnlyList<Polyline2D> Holes { get; }
        public Polyline2D[] AllRings { get; }

        public ReinRegion(Polyline2D outer, IEnumerable<Polyline2D> holes)
        {
            Outer = outer;
            Holes = holes?.ToList() ?? new List<Polyline2D>();
            AllRings = BuildAllRings(Outer, Holes);
        }

        /// <summary>
        /// 点是否在有效混凝土区域内（外轮廓内且不在任何孔洞内）。
        /// </summary>
        public bool IsValidRebarPoint(Point2D point)
        {
            if (!PolygonAlgorithms.ContainsPoint(Outer.Vertices, point))
                return false;

            foreach (var hole in Holes)
            {
                if (PolygonAlgorithms.ContainsPoint(hole.Vertices, point))
                    return false;
            }

            return true;
        }

        private static Polyline2D[] BuildAllRings(Polyline2D outer, IReadOnlyList<Polyline2D> holes)
        {
            if (holes == null || holes.Count == 0)
                return new[] { outer };

            var rings = new Polyline2D[1 + holes.Count];
            rings[0] = outer;
            for (int i = 0; i < holes.Count; i++)
                rings[i + 1] = holes[i];
            return rings;
        }
    }
}
