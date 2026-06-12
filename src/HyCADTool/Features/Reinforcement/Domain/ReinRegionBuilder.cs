using HyCAD.Geometry;
using HyCAD.Geometry.Algorithms;
using HyCADTool.Features.Cluster.Domain.Services;
using System;
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

        /// <summary>
        /// 构件识别用：相交/搭接的外轮廓 Union 合并为同一区域，孔洞按包含关系归属。
        /// 完全分离的外轮廓仍各自独立，各自拥有最小标高。
        /// </summary>
        public static List<(ReinRegion Region, List<Polyline2D> ReinBoundaries)> BuildMergedRegions(
            IReadOnlyList<ClassifiedBoundary> classified)
        {
            var regions = new List<(ReinRegion, List<Polyline2D>)>();
            var outers = classified.Where(c => !c.IsHole).Select(c => c.Boundary).ToList();
            var holes = classified.Where(c => c.IsHole).Select(c => c.Boundary).ToList();

            if (outers.Count == 0)
                return regions;

            var outerPolys = outers
                .Where(o => o.VertexCount >= 3)
                .Select(o => new Polygon2D(o.Vertices, isClosed: true))
                .ToList();

            if (outerPolys.Count == 0)
                return regions;

            var mergedOuters = PolygonBoolean.UnionAll(outerPolys);
            if (mergedOuters.Count == 0)
                return BuildRegions(classified);

            foreach (var mergedPoly in mergedOuters)
            {
                if (mergedPoly == null || mergedPoly.VertexCount < 3)
                    continue;

                var outerPl = new Polyline2D(mergedPoly.Vertices, isClosed: true);
                outerPl.RemoveDuplicateVertices();
                outerPl.SetCounterClockwise();

                var assignedHoles = new List<Polyline2D>();
                foreach (var hole in holes)
                {
                    if (hole.VertexCount < 3)
                        continue;

                    var testPoint = hole.GetPointAt(0);
                    if (PolygonAlgorithms.ContainsPoint(outerPl.Vertices, testPoint))
                        assignedHoles.Add(hole);
                }

                var region = new ReinRegion(outerPl, assignedHoles);
                var reinBoundaries = new List<Polyline2D> { outerPl };
                reinBoundaries.AddRange(assignedHoles);
                regions.Add((region, reinBoundaries));
            }

            return regions;
        }

        /// <summary>
        /// 构件识别用：按外轮廓 bbox 间隙聚类（同 hymbr ClusterByBoundsDistance），
        /// 每组内 Union 合并相交外轮廓，孔洞按包含关系归属；返回 N 个独立计算组。
        /// </summary>
        public static List<List<ReinRegion>> BuildGroupedRegions(
            IReadOnlyList<ClassifiedBoundary> classified,
            double groupDistanceMm)
        {
            var groups = new List<List<ReinRegion>>();
            var outers = classified.Where(c => !c.IsHole).ToList();
            if (outers.Count == 0)
                return groups;

            if (groupDistanceMm <= 0)
                groupDistanceMm = 1500.0;

            var outerItems = outers
                .Where(o => o.Boundary.VertexCount >= 3)
                .Select(o => new OuterClusterItem(o, GetBoundaryBox(o.Boundary)))
                .ToList();

            if (outerItems.Count == 0)
                return groups;

            var clusteringService = new ClusteringService();
            var clusters = clusteringService.ClusterByBoundsDistance(
                outerItems,
                o => o.Bbox,
                groupDistanceMm);

            foreach (var cluster in clusters)
            {
                if (cluster.Count == 0)
                    continue;

                var clusterSet = new HashSet<ClassifiedBoundary>(cluster.Select(c => c.Classified));
                var filtered = classified.Where(c =>
                {
                    if (!c.IsHole)
                        return clusterSet.Contains(c);

                    if (c.Boundary.VertexCount < 3)
                        return false;

                    var testPoint = c.Boundary.GetPointAt(0);
                    return cluster.Any(o =>
                        PolygonAlgorithms.ContainsPoint(o.Classified.Boundary.Vertices, testPoint));
                }).ToList();

                var merged = BuildMergedRegions(filtered);
                var groupRegions = merged.Select(m => m.Region).ToList();
                if (groupRegions.Count > 0)
                    groups.Add(groupRegions);
            }

            return groups;
        }

        private sealed class OuterClusterItem
        {
            public OuterClusterItem(ClassifiedBoundary classified, BoundingBox bbox)
            {
                Classified = classified;
                Bbox = bbox;
            }

            public ClassifiedBoundary Classified { get; }
            public BoundingBox Bbox { get; }
        }

        private static BoundingBox GetBoundaryBox(Polyline2D poly)
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            for (int i = 0; i < poly.VertexCount; i++)
            {
                var p = poly.GetPointAt(i);
                minX = Math.Min(minX, p.X);
                maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y);
                maxY = Math.Max(maxY, p.Y);
            }

            if (minX == double.MaxValue)
                return new BoundingBox(Point2D.Origin, Point2D.Origin);

            return new BoundingBox(new Point2D(minX, minY), new Point2D(maxX, maxY));
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
