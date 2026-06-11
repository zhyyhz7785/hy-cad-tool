using HyCAD.Geometry;
using HyCAD.Geometry.Algorithms;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Reinforcement.Domain
{
    /// <summary>
    /// 从选中的多条闭合多段线构建配筋区域（外轮廓 + 直接子孔洞）。
    /// </summary>
    public static class ReinRegionBuilder
    {
        public sealed class ClassifiedBoundary
        {
            public Polyline2D Boundary { get; set; }
            public int NestingDepth { get; set; }
            public bool IsHole => (NestingDepth & 1) == 1;
        }

        public static List<ClassifiedBoundary> ClassifyBoundaries(IReadOnlyList<Polyline2D> boundaries)
        {
            var result = new List<ClassifiedBoundary>(boundaries.Count);

            for (int i = 0; i < boundaries.Count; i++)
            {
                var boundary = boundaries[i].Clone();
                boundary.RemoveDuplicateVertices();
                boundary.IsClosed = true;

                int depth = ComputeNestingDepth(boundary, boundaries, i);
                if (depth % 2 == 0)
                    boundary.SetCounterClockwise();
                else
                    boundary.SetClockwise();

                result.Add(new ClassifiedBoundary
                {
                    Boundary = boundary,
                    NestingDepth = depth
                });
            }

            return result;
        }

        /// <summary>
        /// 为每条偶数深度（外轮廓）边界构建区域，并返回需配筋的边界列表（外轮廓 + 直接子孔洞）。
        /// </summary>
        public static List<(ReinRegion Region, List<Polyline2D> ReinBoundaries)> BuildRegions(
            IReadOnlyList<ClassifiedBoundary> classified)
        {
            var regions = new List<(ReinRegion, List<Polyline2D>)>();
            var outers = classified.Where(c => !c.IsHole).ToList();

            foreach (var outer in outers)
            {
                var directHoles = classified
                    .Where(c => c.IsHole && IsDirectlyNestedIn(c.Boundary, outer.Boundary, classified))
                    .Select(c => c.Boundary)
                    .ToList();

                var region = new ReinRegion(outer.Boundary, directHoles);
                var reinBoundaries = new List<Polyline2D> { outer.Boundary };
                reinBoundaries.AddRange(directHoles);
                regions.Add((region, reinBoundaries));
            }

            return regions;
        }

        private static int ComputeNestingDepth(Polyline2D boundary, IReadOnlyList<Polyline2D> all, int selfIndex)
        {
            if (boundary.VertexCount < 3)
                return 0;

            Point2D testPoint = boundary.GetPointAt(0);
            int depth = 0;

            for (int i = 0; i < all.Count; i++)
            {
                if (i == selfIndex)
                    continue;

                var other = all[i];
                if (other.VertexCount < 3 || !other.IsClosed)
                    continue;

                if (PolygonAlgorithms.ContainsPoint(other.Vertices, testPoint))
                    depth++;
            }

            return depth;
        }

        private static bool IsDirectlyNestedIn(
            Polyline2D inner,
            Polyline2D outer,
            IReadOnlyList<ClassifiedBoundary> classified)
        {
            if (inner.VertexCount < 3 || outer.VertexCount < 3)
                return false;

            Point2D testPoint = inner.GetPointAt(0);
            if (!PolygonAlgorithms.ContainsPoint(outer.Vertices, testPoint))
                return false;

            foreach (var other in classified)
            {
                if (!other.IsHole || ReferenceEquals(other.Boundary, inner))
                    continue;

                if (PolygonAlgorithms.ContainsPoint(other.Boundary.Vertices, testPoint)
                    && PolygonAlgorithms.ContainsPoint(outer.Vertices, other.Boundary.GetPointAt(0)))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
