using HyCADTool.Shared.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Shared.Geometry.Algorithms
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

            // 找到最左和最右的点
            Point2D minPoint = pointList.OrderBy(p => p.X).First();
            Point2D maxPoint = pointList.OrderByDescending(p => p.X).First();

            var hull = new List<Point2D>();

            // 上凸包
            QuickHullRecursive(pointList, minPoint, maxPoint, hull);

            // 下凸包
            QuickHullRecursive(pointList, maxPoint, minPoint, hull);

            // 去重
            var uniqueHull = hull.Distinct().ToList();

            return new Polygon2D(uniqueHull);
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
        /// Quick Hull 递归函数
        /// </summary>
        private static void QuickHullRecursive(List<Point2D> points, Point2D p1, Point2D p2, List<Point2D> hull)
        {
            if (points.Count == 0)
                return;

            // 找到距离线段 p1-p2 最远的点
            Point2D farthest = default;
            bool foundFarthest = false;
            double maxDistance = 0;

            foreach (var point in points)
            {
                double dist = DistanceToLine(point, p1, p2);
                if (dist > maxDistance)
                {
                    maxDistance = dist;
                    farthest = point;
                    foundFarthest = true;
                }
            }

            if (!foundFarthest)
            {
                hull.Add(p1);
                return;
            }

            // 分割点集
            var s1 = points.Where(p => IsLeftOf(p, p1, farthest)).ToList();
            var s2 = points.Where(p => IsLeftOf(p, farthest, p2)).ToList();

            // 递归处理
            QuickHullRecursive(s1, p1, farthest, hull);
            QuickHullRecursive(s2, farthest, p2, hull);
        }

        /// <summary>
        /// 点到直线的距离
        /// </summary>
        private static double DistanceToLine(Point2D point, Point2D lineStart, Point2D lineEnd)
        {
            double area = System.Math.Abs(CrossProduct(lineStart, lineEnd, point));
            double baseLength = lineStart.DistanceTo(lineEnd);
            return baseLength > 1e-10 ? area / baseLength : 0;
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

