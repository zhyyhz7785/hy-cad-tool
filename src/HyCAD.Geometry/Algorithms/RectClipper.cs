using System;
using System.Collections.Generic;

namespace HyCAD.Geometry.Algorithms
{
    /// <summary>
    /// Sutherland–Hodgman：将任意多边形裁剪到轴对齐矩形内。
    /// </summary>
    public static class RectClipper
    {
        private const double Epsilon = 1e-9;

        /// <summary>
        /// 裁剪多边形到 [minX,maxX]×[minY,maxY]。无交集时返回空列表。
        /// </summary>
        public static List<Point2D> ClipPolygonToRect(
            IReadOnlyList<Point2D> subject,
            double minX,
            double minY,
            double maxX,
            double maxY)
        {
            if (subject == null || subject.Count < 3)
                return new List<Point2D>();

            var output = new List<Point2D>(subject);
            output = ClipEdge(output, p => p.X >= minX - Epsilon, (s, e) => IntersectVertical(s, e, minX));
            output = ClipEdge(output, p => p.X <= maxX + Epsilon, (s, e) => IntersectVertical(s, e, maxX));
            output = ClipEdge(output, p => p.Y >= minY - Epsilon, (s, e) => IntersectHorizontal(s, e, minY));
            output = ClipEdge(output, p => p.Y <= maxY + Epsilon, (s, e) => IntersectHorizontal(s, e, maxY));

            if (output.Count >= 3 && output.Count > 0)
            {
                var first = output[0];
                var last = output[output.Count - 1];
                if (first.DistanceTo(last) > Epsilon)
                    output.Add(first);
            }

            return output.Count >= 3 ? output : new List<Point2D>();
        }

        /// <summary>
        /// 裁剪 <see cref="Polyline2D"/> 到轴对齐矩形。
        /// </summary>
        public static Polyline2D ClipPolylineToRect(
            Polyline2D subject,
            double minX,
            double minY,
            double maxX,
            double maxY)
        {
            if (subject == null)
                return null;

            var verts = new List<Point2D>();
            for (int i = 0; i < subject.VertexCount; i++)
                verts.Add(subject.GetPointAt(i));

            var clipped = ClipPolygonToRect(verts, minX, minY, maxX, maxY);
            if (clipped.Count < 3)
                return null;

            return new Polyline2D(clipped, isClosed: true);
        }

        private static List<Point2D> ClipEdge(
            List<Point2D> input,
            Func<Point2D, bool> inside,
            Func<Point2D, Point2D, Point2D> intersect)
        {
            if (input.Count == 0)
                return input;

            var output = new List<Point2D>();
            var previous = input[input.Count - 1];
            bool prevInside = inside(previous);

            foreach (var current in input)
            {
                bool currInside = inside(current);
                if (currInside)
                {
                    if (!prevInside)
                        output.Add(intersect(previous, current));
                    output.Add(current);
                }
                else if (prevInside)
                {
                    output.Add(intersect(previous, current));
                }

                previous = current;
                prevInside = currInside;
            }

            return output;
        }

        private static Point2D IntersectVertical(Point2D s, Point2D e, double x)
        {
            double dx = e.X - s.X;
            if (System.Math.Abs(dx) < Epsilon)
                return new Point2D(x, s.Y);
            double t = (x - s.X) / dx;
            return new Point2D(x, s.Y + t * (e.Y - s.Y));
        }

        private static Point2D IntersectHorizontal(Point2D s, Point2D e, double y)
        {
            double dy = e.Y - s.Y;
            if (System.Math.Abs(dy) < Epsilon)
                return new Point2D(s.X, y);
            double t = (y - s.Y) / dy;
            return new Point2D(s.X + t * (e.X - s.X), y);
        }
    }
}
