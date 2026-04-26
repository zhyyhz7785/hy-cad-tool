using HyCADTool.Shared.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Domain.Services.GeometryAlgorithms
{
    /// <summary>
    /// 交点算法服务（平台无关）
    /// 提供几何图形相交计算的算法
    /// </summary>
    public static class IntersectionAlgorithms
    {
        /// <summary>
        /// 计算两条线段的交点
        /// </summary>
        public static Point2D GetLineIntersection(Line2D line1, Line2D line2, double tolerance = 1e-6)
        {
            return line1.GetIntersection(line2, tolerance);
        }

        /// <summary>
        /// 计算多边形与线段的所有交点
        /// </summary>
        public static IEnumerable<Point2D> GetPolygonLineIntersections(Polygon2D polygon, Line2D line, double tolerance = 1e-6)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));

            var intersections = new List<Point2D>();

            // 检查每条边与线段的交点
            foreach (var edge in polygon.GetEdges())
            {
                var intersection = edge.GetIntersection(line, tolerance);
                if (intersection != default)
                {
                    // 检查是否已经存在相同的交点（避免重复）
                    bool isDuplicate = intersections.Any(p => p.DistanceTo(intersection) < tolerance);
                    if (!isDuplicate)
                    {
                        intersections.Add(intersection);
                    }
                }
            }

            return intersections;
        }

        /// <summary>
        /// 计算两个多边形的交点
        /// </summary>
        public static IEnumerable<Point2D> GetPolygonIntersections(Polygon2D polygon1, Polygon2D polygon2, double tolerance = 1e-6)
        {
            if (polygon1 == null)
                throw new ArgumentNullException(nameof(polygon1));
            if (polygon2 == null)
                throw new ArgumentNullException(nameof(polygon2));

            var intersections = new List<Point2D>();

            // 检查 polygon1 的每条边与 polygon2 的每条边
            foreach (var edge1 in polygon1.GetEdges())
            {
                foreach (var edge2 in polygon2.GetEdges())
                {
                    var intersection = edge1.GetIntersection(edge2, tolerance);
                    if (intersection != default)
                    {
                        // 检查是否重复
                        bool isDuplicate = intersections.Any(p => p.DistanceTo(intersection) < tolerance);
                        if (!isDuplicate)
                        {
                            intersections.Add(intersection);
                        }
                    }
                }
            }

            return intersections;
        }

        /// <summary>
        /// 判断圆与线段是否相交
        /// </summary>
        public static bool CircleIntersectsLine(Circle2D circle, Line2D line, double tolerance = 1e-6)
        {
            // 计算线段到圆心的最短距离
            double distance = line.DistanceToPoint(circle.Center);
            return distance <= circle.Radius + tolerance;
        }

        /// <summary>
        /// 计算圆与线段的交点
        /// </summary>
        public static IEnumerable<Point2D> GetCircleLineIntersections(Circle2D circle, Line2D line, double tolerance = 1e-6)
        {
            var intersections = new List<Point2D>();

            // 线段方向向量
            Vector2D d = line.Direction;
            double length = d.Length;
            if (length < tolerance)
                return intersections;

            // 单位方向向量
            Vector2D dUnit = d / length;

            // 从起点到圆心的向量
            Vector2D f = line.StartPoint.VectorTo(circle.Center);

            // 求解二次方程 t^2 + 2bt + c = 0
            double b = f.Dot(dUnit);
            double c = f.Dot(f) - circle.Radius * circle.Radius;

            double discriminant = b * b - c;

            if (discriminant < -tolerance)
            {
                // 无交点
                return intersections;
            }

            if (Math.Abs(discriminant) < tolerance)
            {
                // 一个交点（切线）
                double t = -b;
                if (t >= -tolerance && t <= length + tolerance)
                {
                    Point2D point = line.StartPoint.Add(dUnit * t);
                    intersections.Add(point);
                }
            }
            else
            {
                // 两个交点
                double sqrtDisc = Math.Sqrt(discriminant);
                double t1 = -b - sqrtDisc;
                double t2 = -b + sqrtDisc;

                if (t1 >= -tolerance && t1 <= length + tolerance)
                {
                    Point2D point1 = line.StartPoint.Add(dUnit * t1);
                    intersections.Add(point1);
                }

                if (t2 >= -tolerance && t2 <= length + tolerance)
                {
                    Point2D point2 = line.StartPoint.Add(dUnit * t2);
                    intersections.Add(point2);
                }
            }

            return intersections;
        }

        /// <summary>
        /// 计算两个圆的交点
        /// </summary>
        public static IEnumerable<Point2D> GetCircleIntersections(Circle2D circle1, Circle2D circle2, double tolerance = 1e-6)
        {
            var intersections = new List<Point2D>();

            double d = circle1.Center.DistanceTo(circle2.Center);
            double r1 = circle1.Radius;
            double r2 = circle2.Radius;

            // 圆心重合
            if (d < tolerance)
                return intersections;

            // 相离或内含
            if (d > r1 + r2 + tolerance || d < Math.Abs(r1 - r2) - tolerance)
                return intersections;

            // 计算交点
            double a = (r1 * r1 - r2 * r2 + d * d) / (2 * d);
            double h = Math.Sqrt(Math.Max(0, r1 * r1 - a * a));

            // 从 circle1.Center 到 circle2.Center 的单位向量
            Vector2D dir = circle1.Center.VectorTo(circle2.Center) / d;

            // 中点
            Point2D p2 = circle1.Center.Add(dir * a);

            // 垂直方向
            Vector2D perp = new Vector2D(-dir.Y, dir.X);

            if (h < tolerance)
            {
                // 一个交点（外切或内切）
                intersections.Add(p2);
            }
            else
            {
                // 两个交点
                intersections.Add(p2.Add(perp * h));
                intersections.Add(p2.Add(perp * (-h)));
            }

            return intersections;
        }
    }
}

