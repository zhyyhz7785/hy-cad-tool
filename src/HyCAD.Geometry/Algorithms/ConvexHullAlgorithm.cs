using HyCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCAD.Geometry.Algorithms
{
    /// <summary>
    /// 凸包算法服务（平台无关）
    /// 提供凸包计算的几何算法
    /// </summary>
    public static class ConvexHullAlgorithm
    {
        /// <summary>
        /// Graham扫描法计算凸包
        /// 时间复杂度 O(n log n)
        /// </summary>
        public static Polygon2D GrahamScan(IEnumerable<Point2D> points)
        {
            var pointList = points.ToList();
            if (pointList.Count < 3)
                throw new ArgumentException("At least 3 points required for convex hull", nameof(points));

            // 1. 找到最下方的点（Y坐标最小，相同则取X最小）
            Point2D pivot = pointList.OrderBy(p => p.Y).ThenBy(p => p.X).First();

            // 2. 按极角排序（相对于pivot）
            var sorted = pointList
                .Where(p => p != pivot)
                .OrderBy(p => PolarAngle(pivot, p))
                .ThenBy(p => pivot.DistanceTo(p))
                .ToList();

            // 3. Graham扫描
            var hull = new Stack<Point2D>();
            hull.Push(pivot);

            if (sorted.Count > 0)
                hull.Push(sorted[0]);

            for (int i = 1; i < sorted.Count; i++)
            {
                Point2D top = hull.Pop();

                // 移除非左转的点
                while (hull.Count > 0 && CrossProduct(hull.Peek(), top, sorted[i]) <= 0)
                {
                    top = hull.Pop();
                }

                hull.Push(top);
                hull.Push(sorted[i]);
            }

            // 转换为多边形
            var hullPoints = hull.Reverse().ToList();
            return new Polygon2D(hullPoints);
        }

        /// <summary>
        /// 判断多边形是否为凸多边形
        /// </summary>
        public static bool IsConvex(Polygon2D polygon, double tolerance = 1e-6)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));

            if (polygon.VertexCount < 3)
                return false;

            var vertices = polygon.Vertices;
            int n = vertices.Count;
            bool? isPositive = null;

            for (int i = 0; i < n; i++)
            {
                Point2D p1 = vertices[i];
                Point2D p2 = vertices[(i + 1) % n];
                Point2D p3 = vertices[(i + 2) % n];

                double cross = CrossProduct(p1, p2, p3);

                if (System.Math.Abs(cross) < tolerance)
                    continue; // 共线点，跳过

                if (isPositive == null)
                {
                    isPositive = cross > 0;
                }
                else if (isPositive != (cross > 0))
                {
                    // 方向不一致，不是凸多边形
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Quick Hull 算法（另一种凸包算法）
        /// 平均时间复杂度 O(n log n)，最坏 O(n²)
        /// </summary>
        public static Polygon2D QuickHull(IEnumerable<Point2D> points)
        {
            var pointList = points.ToList();
            if (pointList.Count < 3)
                throw new ArgumentException("At least 3 points required for convex hull", nameof(points));

            Point2D minPoint = pointList.OrderBy(p => p.X).ThenBy(p => p.Y).First();
            Point2D maxPoint = pointList.OrderByDescending(p => p.X).ThenByDescending(p => p.Y).First();

            if (minPoint.Equals(maxPoint))
                return GrahamScan(pointList);

            var hull = new List<Point2D>();
            var upper = pointList.Where(p => IsLeftOf(p, minPoint, maxPoint)).ToList();
            var lower = pointList.Where(p => IsLeftOf(p, maxPoint, minPoint)).ToList();

            hull.Add(minPoint);
            QuickHullRecursive(upper, minPoint, maxPoint, hull);
            hull.Add(maxPoint);
            QuickHullRecursive(lower, maxPoint, minPoint, hull);

            var uniqueHull = DedupPreserveOrder(hull);
            if (uniqueHull.Count < 3)
                return GrahamScan(pointList);

            var poly = new Polygon2D(uniqueHull);
            if (poly.GetSignedArea() < 0)
            {
                uniqueHull.Reverse();
                poly = new Polygon2D(uniqueHull);
            }

            return poly;
        }

        #region 辅助方法

        /// <summary>
        /// 计算极角（弧度）
        /// </summary>
        private static double PolarAngle(Point2D pivot, Point2D point)
        {
            double dx = point.X - pivot.X;
            double dy = point.Y - pivot.Y;
            return System.Math.Atan2(dy, dx);
        }

        /// <summary>
        /// 计算叉积（判断转向）
        /// 正值：左转，负值：右转，零：共线
        /// </summary>
        private static double CrossProduct(Point2D p1, Point2D p2, Point2D p3)
        {
            return (p2.X - p1.X) * (p3.Y - p1.Y) - (p2.Y - p1.Y) * (p3.X - p1.X);
        }

        /// <summary>
        /// Quick Hull 递归：只处理有向线段 p1→p2 左侧的点，按逆时针插入 farthest。
        /// </summary>
        private static void QuickHullRecursive(List<Point2D> points, Point2D p1, Point2D p2, List<Point2D> hull)
        {
            if (points.Count == 0)
                return;

            Point2D farthest = default;
            bool foundFarthest = false;
            double maxSigned = 0;

            foreach (var point in points)
            {
                double signed = CrossProduct(p1, p2, point);
                if (!foundFarthest || signed > maxSigned)
                {
                    maxSigned = signed;
                    farthest = point;
                    foundFarthest = true;
                }
            }

            if (!foundFarthest)
                return;

            var s1 = points.Where(p => IsLeftOf(p, p1, farthest)).ToList();
            var s2 = points.Where(p => IsLeftOf(p, farthest, p2)).ToList();

            QuickHullRecursive(s1, p1, farthest, hull);
            hull.Add(farthest);
            QuickHullRecursive(s2, farthest, p2, hull);
        }

        private static List<Point2D> DedupPreserveOrder(List<Point2D> points)
        {
            var result = new List<Point2D>(points.Count);
            foreach (var point in points)
            {
                if (result.Count == 0 || !result[result.Count - 1].Equals(point))
                    result.Add(point);
            }

            if (result.Count >= 2 && result[0].Equals(result[result.Count - 1]))
                result.RemoveAt(result.Count - 1);

            return result;
        }

        /// <summary>
        /// 判断点是否在有向线段的左侧
        /// </summary>
        private static bool IsLeftOf(Point2D point, Point2D lineStart, Point2D lineEnd)
        {
            return CrossProduct(lineStart, lineEnd, point) > 0;
        }

        #endregion
    }
}

