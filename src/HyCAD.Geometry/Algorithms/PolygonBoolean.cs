using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;

namespace HyCAD.Geometry.Algorithms
{
    /// <summary>
    /// 多边形布尔运算（Clipper2）。
    /// </summary>
    public static class PolygonBoolean
    {
        /// <summary>
        /// 求 subject ∩ clip，减去 holes 后返回面积最大的多边形。
        /// </summary>
        public static Polygon2D IntersectAndSubtractHoles(
            Polygon2D subject,
            Polygon2D clip,
            IReadOnlyList<Polygon2D> holes = null)
        {
            if (subject == null || clip == null)
                return null;

            var solids = IntersectAllAndSubtractHoles(subject, clip, holes);
            return solids.Count == 0 ? null : solids[0];
        }

        /// <summary>
        /// 将轴对齐矩形（subject）与 clip 多边形求交，并减去孔洞。
        /// </summary>
        public static Polygon2D IntersectRectWithRegion(
            double minX,
            double minY,
            double maxX,
            double maxY,
            Polyline2D outer,
            IReadOnlyList<Polyline2D> holes)
        {
            var rectVerts = RectClipper.ClipPolygonToRect(
                new[]
                {
                    new Point2D(minX, minY),
                    new Point2D(maxX, minY),
                    new Point2D(maxX, maxY),
                    new Point2D(minX, maxY)
                },
                minX, minY, maxX, maxY);

            if (rectVerts.Count < 3)
                return null;

            var subject = new Polygon2D(rectVerts, isClosed: true);
            var clip = PolylineToPolygon(outer);
            if (clip == null)
                return null;

            IReadOnlyList<Polygon2D> holePolys = null;
            if (holes != null && holes.Count > 0)
            {
                holePolys = holes
                    .Select(PolylineToPolygon)
                    .Where(h => h != null)
                    .ToList();
            }

            return IntersectAndSubtractHoles(subject, clip, holePolys);
        }


        /// <summary>
        /// 将轴对齐矩形与 clip 多边形求交并减去孔洞，返回全部片段（按面积降序）。
        /// </summary>
        public static IReadOnlyList<Polygon2D> IntersectRectWithRegionAll(
            double minX,
            double minY,
            double maxX,
            double maxY,
            Polyline2D outer,
            IReadOnlyList<Polyline2D> holes)
        {
            var rectVerts = RectClipper.ClipPolygonToRect(
                new[]
                {
                    new Point2D(minX, minY),
                    new Point2D(maxX, minY),
                    new Point2D(maxX, maxY),
                    new Point2D(minX, maxY)
                },
                minX, minY, maxX, maxY);

            if (rectVerts.Count < 3)
                return Array.Empty<Polygon2D>();

            var subject = new Polygon2D(rectVerts, isClosed: true);
            var clip = PolylineToPolygon(outer);
            if (clip == null)
                return Array.Empty<Polygon2D>();

            IReadOnlyList<Polygon2D> holePolys = null;
            if (holes != null && holes.Count > 0)
            {
                holePolys = holes
                    .Select(PolylineToPolygon)
                    .Where(h => h != null)
                    .ToList();
            }

            return IntersectAllAndSubtractHoles(subject, clip, holePolys);
        }

        /// <summary>
        /// subject ∩ outer，减去 holes 后返回全部片段（按面积降序）。
        /// </summary>
        public static IReadOnlyList<Polygon2D> IntersectPolygonWithRegionAll(
            Polygon2D subject,
            Polyline2D outer,
            IReadOnlyList<Polyline2D> holes)
        {
            if (subject == null || subject.VertexCount < 3)
                return Array.Empty<Polygon2D>();

            var clip = PolylineToPolygon(outer);
            if (clip == null)
                return Array.Empty<Polygon2D>();

            IReadOnlyList<Polygon2D> holePolys = null;
            if (holes != null && holes.Count > 0)
            {
                holePolys = holes
                    .Select(PolylineToPolygon)
                    .Where(h => h != null)
                    .ToList();
            }

            return IntersectAllAndSubtractHoles(subject, clip, holePolys);
        }

        private static IReadOnlyList<Polygon2D> IntersectAllAndSubtractHoles(
            Polygon2D subject,
            Polygon2D clip,
            IReadOnlyList<Polygon2D> holes = null)
        {
            if (subject == null || clip == null)
                return Array.Empty<Polygon2D>();

            var tree = Boolean(
                ClipType.Intersection,
                new PathsD { ToPath(subject) },
                new PathsD { ToPath(clip) });
            if (tree.Count == 0)
                return Array.Empty<Polygon2D>();

            if (holes != null && holes.Count > 0)
            {
                foreach (var hole in holes)
                {
                    if (hole == null || hole.VertexCount < 3)
                        continue;

                    var current = Flatten(tree);
                    if (current.Count == 0)
                        return Array.Empty<Polygon2D>();

                    tree = Boolean(ClipType.Difference, current, new PathsD { ToPath(hole) });
                    if (tree.Count == 0)
                        return Array.Empty<Polygon2D>();
                }
            }

            return Solids(tree);
        }

        /// <summary>
        /// subject ∩ clip，返回全部片段（按面积降序）。
        /// </summary>
        public static IReadOnlyList<Polygon2D> IntersectAll(Polygon2D subject, Polygon2D clip)
        {
            return IntersectAllAndSubtractHoles(subject, clip, holes: null);
        }

        /// <summary>
        /// 合并多个多边形，返回全部结果（NonZero）。
        /// </summary>
        public static IReadOnlyList<Polygon2D> UnionAll(IReadOnlyList<Polygon2D> polygons)
        {
            if (polygons == null || polygons.Count == 0)
                return Array.Empty<Polygon2D>();

            PathsD paths = new PathsD();
            foreach (var poly in polygons)
            {
                if (poly == null || poly.VertexCount < 3)
                    continue;
                paths.Add(ToPath(poly));
            }

            if (paths.Count == 0)
                return Array.Empty<Polygon2D>();

            if (paths.Count == 1)
            {
                var only = ToPolygon(paths[0]);
                return only == null
                    ? Array.Empty<Polygon2D>()
                    : new[] { only };
            }

            var tree = Boolean(ClipType.Union, paths, null);
            return tree.Count == 0
                ? Array.Empty<Polygon2D>()
                : Solids(tree);
        }

        /// <summary>
        /// Clipper2 默认精度。与 <see cref="Clipper.Intersect(PathsD, PathsD, FillRule, int)"/> 的默认参数一致。
        /// </summary>
        private const int ClipperPrecision = 2;

        private static PolyTreeD Boolean(ClipType op, PathsD subject, PathsD clip)
        {
            var tree = new PolyTreeD();
            Clipper.BooleanOp(op, subject, clip, tree, FillRule.NonZero, ClipperPrecision);
            return tree;
        }

        /// <summary>
        /// 只收集非孔洞环。孔洞里的岛（孔的非孔子节点）仍作为独立实体返回。
        /// </summary>
        private static IReadOnlyList<Polygon2D> Solids(PolyTreeD tree)
        {
            var result = new List<Polygon2D>();
            CollectSolids(tree, result);
            return result
                .OrderByDescending(poly => System.Math.Abs(Clipper.Area(ToPath(poly))))
                .ToList();
        }

        private static void CollectSolids(PolyPathD node, List<Polygon2D> result)
        {
            for (int i = 0; i < node.Count; i++)
            {
                var child = node[i];
                if (!child.IsHole)
                {
                    var poly = ToPolygon(child.Polygon);
                    if (poly != null)
                        result.Add(poly);
                }

                if (child.Count > 0)
                    CollectSolids(child, result);
            }
        }

        /// <summary>
        /// 下一轮差集的主体要带上孔洞环，否则只传外环会把已挖掉的孔重新填上。
        /// </summary>
        private static PathsD Flatten(PolyTreeD tree)
        {
            var paths = new PathsD();
            CollectPaths(tree, paths);
            return paths;
        }

        private static void CollectPaths(PolyPathD node, PathsD paths)
        {
            for (int i = 0; i < node.Count; i++)
            {
                var child = node[i];
                if (child.Polygon != null && child.Polygon.Count >= 3)
                    paths.Add(child.Polygon);
                if (child.Count > 0)
                    CollectPaths(child, paths);
            }
        }

        private static Polygon2D ToPolygon(PathD path)
        {
            if (path == null || path.Count < 3)
                return null;

            var verts = new List<Point2D>(path.Count);
            foreach (var p in path)
                verts.Add(new Point2D(p.x, p.y));
            return new Polygon2D(verts, isClosed: true);
        }

        private static PathD ToPath(Polygon2D polygon)
        {
            var path = new PathD();
            foreach (var v in polygon.Vertices)
                path.Add(new PointD(v.X, v.Y));
            return path;
        }

        private static Polygon2D PolylineToPolygon(Polyline2D poly)
        {
            if (poly == null || poly.VertexCount < 3)
                return null;

            var verts = new List<Point2D>();
            for (int i = 0; i < poly.VertexCount; i++)
                verts.Add(poly.GetPointAt(i));
            return new Polygon2D(verts, poly.IsClosed);
        }
    }
}
