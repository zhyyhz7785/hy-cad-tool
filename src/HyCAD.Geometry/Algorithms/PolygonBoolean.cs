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

            var subjectPath = ToPath(subject);
            var clipPath = ToPath(clip);

            var intersection = Clipper.Intersect(
                new PathsD { subjectPath },
                new PathsD { clipPath },
                FillRule.NonZero);

            if (intersection == null || intersection.Count == 0)
                return null;

            if (holes != null && holes.Count > 0)
            {
                foreach (var hole in holes)
                {
                    if (hole == null || hole.VertexCount < 3)
                        continue;

                    intersection = Clipper.Difference(
                        intersection,
                        new PathsD { ToPath(hole) },
                        FillRule.NonZero);

                    if (intersection == null || intersection.Count == 0)
                        return null;
                }
            }

            return LargestPolygon(intersection);
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

        private static IReadOnlyList<Polygon2D> IntersectAllAndSubtractHoles(
            Polygon2D subject,
            Polygon2D clip,
            IReadOnlyList<Polygon2D> holes = null)
        {
            if (subject == null || clip == null)
                return Array.Empty<Polygon2D>();

            var subjectPath = ToPath(subject);
            var clipPath = ToPath(clip);

            var intersection = Clipper.Intersect(
                new PathsD { subjectPath },
                new PathsD { clipPath },
                FillRule.NonZero);

            if (intersection == null || intersection.Count == 0)
                return Array.Empty<Polygon2D>();

            if (holes != null && holes.Count > 0)
            {
                foreach (var hole in holes)
                {
                    if (hole == null || hole.VertexCount < 3)
                        continue;

                    intersection = Clipper.Difference(
                        intersection,
                        new PathsD { ToPath(hole) },
                        FillRule.NonZero);

                    if (intersection == null || intersection.Count == 0)
                        return Array.Empty<Polygon2D>();
                }
            }

            return AllPolygons(intersection);
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
                return AllPolygons(new PathsD { paths[0] });

            var united = Clipper.Union(paths, FillRule.NonZero);
            return united == null || united.Count == 0
                ? Array.Empty<Polygon2D>()
                : AllPolygons(united);
        }

        private static IReadOnlyList<Polygon2D> AllPolygons(PathsD paths)
        {
            var result = new List<Polygon2D>();
            foreach (var path in paths)
            {
                if (path == null || path.Count < 3)
                    continue;

                var verts = path.Select(p => new Point2D(p.x, p.y)).ToList();
                result.Add(new Polygon2D(verts, isClosed: true));
            }

            return result
                .OrderByDescending(poly => System.Math.Abs(Clipper.Area(ToPath(poly))))
                .ToList();
        }
        private static Polygon2D LargestPolygon(PathsD paths)
        {
            PathD best = null;
            double bestArea = 0;

            foreach (var path in paths)
            {
                if (path == null || path.Count < 3)
                    continue;

                double area = System.Math.Abs(Clipper.Area(path));
                if (area > bestArea)
                {
                    bestArea = area;
                    best = path;
                }
            }

            if (best == null)
                return null;

            var verts = best.Select(p => new Point2D(p.x, p.y)).ToList();
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
