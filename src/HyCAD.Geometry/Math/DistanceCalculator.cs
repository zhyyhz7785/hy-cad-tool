using HyCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCAD.Geometry.Math
{
    /// <summary>
    /// 距离计算器 - 纯数学计算，平台无关
    /// Distance Calculator - Pure mathematical calculations, platform-independent
    /// </summary>
    public static class DistanceCalculator
    {
        /// <summary>
        /// 计算两点之间的欧几里得距离
        /// Calculate Euclidean distance between two points
        /// </summary>
        /// <param name="p1">第一个点 First point</param>
        /// <param name="p2">第二个点 Second point</param>
        /// <returns>距离 Distance</returns>
        public static double EuclideanDistance(Point2D p1, Point2D p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return System.Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 计算两点之间的曼哈顿距离
        /// Calculate Manhattan distance between two points
        /// </summary>
        /// <param name="p1">第一个点 First point</param>
        /// <param name="p2">第二个点 Second point</param>
        /// <returns>距离 Distance</returns>
        public static double ManhattanDistance(Point2D p1, Point2D p2)
        {
            return System.Math.Abs(p2.X - p1.X) + System.Math.Abs(p2.Y - p1.Y);
        }

        /// <summary>
        /// 计算两点之间的切比雪夫距离（棋盘距离）
        /// Calculate Chebyshev distance between two points
        /// </summary>
        /// <param name="p1">第一个点 First point</param>
        /// <param name="p2">第二个点 Second point</param>
        /// <returns>距离 Distance</returns>
        public static double ChebyshevDistance(Point2D p1, Point2D p2)
        {
            return System.Math.Max(System.Math.Abs(p2.X - p1.X), System.Math.Abs(p2.Y - p1.Y));
        }

        /// <summary>
        /// 计算点到线段的最短距离
        /// Calculate shortest distance from point to line segment
        /// </summary>
        /// <param name="point">点 Point</param>
        /// <param name="lineStart">线段起点 Line start</param>
        /// <param name="lineEnd">线段终点 Line end</param>
        /// <returns>距离 Distance</returns>
        public static double PointToLineSegmentDistance(Point2D point, Point2D lineStart, Point2D lineEnd)
        {
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            
            double lineLengthSquared = dx * dx + dy * dy;
            
            if (lineLengthSquared < 1e-10)
            {
                // 线段退化为点
                return EuclideanDistance(point, lineStart);
            }
            
            // 计算投影参数 t
            double t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / lineLengthSquared;
            
            // 限制 t 在 [0, 1] 范围内
            t = System.Math.Max(0, System.Math.Min(1, t));
            
            // 计算最近点
            var closestPoint = new Point2D(
                lineStart.X + t * dx,
                lineStart.Y + t * dy
            );
            
            return EuclideanDistance(point, closestPoint);
        }

        /// <summary>
        /// 计算点到直线（无限延伸）的距离
        /// Calculate distance from point to infinite line
        /// </summary>
        /// <param name="point">点 Point</param>
        /// <param name="lineStart">直线上一点 Point on line</param>
        /// <param name="lineEnd">直线上另一点 Another point on line</param>
        /// <returns>距离 Distance</returns>
        public static double PointToLineDistance(Point2D point, Point2D lineStart, Point2D lineEnd)
        {
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            
            double numerator = System.Math.Abs(
                dy * point.X - dx * point.Y + 
                lineEnd.X * lineStart.Y - 
                lineEnd.Y * lineStart.X
            );
            
            double denominator = System.Math.Sqrt(dx * dx + dy * dy);
            
            if (denominator < 1e-10)
                return EuclideanDistance(point, lineStart);
            
            return numerator / denominator;
        }

        /// <summary>
        /// 计算两条线段之间的最短距离
        /// Calculate shortest distance between two line segments
        /// </summary>
        /// <param name="line1Start">第一条线段起点 First line start</param>
        /// <param name="line1End">第一条线段终点 First line end</param>
        /// <param name="line2Start">第二条线段起点 Second line start</param>
        /// <param name="line2End">第二条线段终点 Second line end</param>
        /// <returns>距离 Distance</returns>
        public static double LineSegmentToLineSegmentDistance(
            Point2D line1Start, Point2D line1End,
            Point2D line2Start, Point2D line2End)
        {
            // 检查线段是否相交
            var line1 = new Line2D(line1Start, line1End);
            var line2 = new Line2D(line2Start, line2End);
            
            if (line1.GetIntersection(line2) != default)
                return 0; // 相交，距离为 0
            
            // 计算四个端点到对方线段的距离，取最小值
            double d1 = PointToLineSegmentDistance(line1Start, line2Start, line2End);
            double d2 = PointToLineSegmentDistance(line1End, line2Start, line2End);
            double d3 = PointToLineSegmentDistance(line2Start, line1Start, line1End);
            double d4 = PointToLineSegmentDistance(line2End, line1Start, line1End);
            
            return System.Math.Min(System.Math.Min(d1, d2), System.Math.Min(d3, d4));
        }

        /// <summary>
        /// 计算点到多边形边界的最短距离
        /// Calculate shortest distance from point to polygon boundary
        /// </summary>
        /// <param name="point">点 Point</param>
        /// <param name="polygonVertices">多边形顶点 Polygon vertices</param>
        /// <returns>距离 Distance</returns>
        public static double PointToPolygonBoundaryDistance(Point2D point, IEnumerable<Point2D> polygonVertices)
        {
            var vertices = polygonVertices.ToList();
            if (vertices.Count < 3)
                return double.MaxValue;
            
            double minDistance = double.MaxValue;
            
            for (int i = 0; i < vertices.Count; i++)
            {
                int j = (i + 1) % vertices.Count;
                double distance = PointToLineSegmentDistance(point, vertices[i], vertices[j]);
                minDistance = System.Math.Min(minDistance, distance);
            }
            
            return minDistance;
        }

        /// <summary>
        /// 计算点到多边形的有向距离（点在内部为负，外部为正）
        /// Calculate signed distance from point to polygon (negative inside, positive outside)
        /// </summary>
        /// <param name="point">点 Point</param>
        /// <param name="polygonVertices">多边形顶点 Polygon vertices</param>
        /// <returns>有向距离 Signed distance</returns>
        public static double PointToPolygonSignedDistance(Point2D point, IEnumerable<Point2D> polygonVertices)
        {
            var vertices = polygonVertices.ToList();
            bool isInside = HyCAD.Geometry.Algorithms.PolygonAlgorithms.ContainsPoint(vertices, point);
            
            double distance = PointToPolygonBoundaryDistance(point, vertices);
            
            return isInside ? -distance : distance;
        }

        /// <summary>
        /// 找到最近的点
        /// Find closest point
        /// </summary>
        /// <param name="target">目标点 Target point</param>
        /// <param name="points">候选点集合 Candidate points</param>
        /// <returns>最近的点和距离 Closest point and distance</returns>
        public static (Point2D closestPoint, double distance) FindClosestPoint(Point2D target, IEnumerable<Point2D> points)
        {
            var pointList = points.ToList();
            if (pointList.Count == 0)
                throw new ArgumentException("Points collection cannot be empty");
            
            Point2D closestPoint = pointList[0];
            double minDistance = EuclideanDistance(target, closestPoint);
            
            foreach (var point in pointList.Skip(1))
            {
                double distance = EuclideanDistance(target, point);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPoint = point;
                }
            }
            
            return (closestPoint, minDistance);
        }

        /// <summary>
        /// 找到最远的点
        /// Find farthest point
        /// </summary>
        /// <param name="target">目标点 Target point</param>
        /// <param name="points">候选点集合 Candidate points</param>
        /// <returns>最远的点和距离 Farthest point and distance</returns>
        public static (Point2D farthestPoint, double distance) FindFarthestPoint(Point2D target, IEnumerable<Point2D> points)
        {
            var pointList = points.ToList();
            if (pointList.Count == 0)
                throw new ArgumentException("Points collection cannot be empty");
            
            Point2D farthestPoint = pointList[0];
            double maxDistance = EuclideanDistance(target, farthestPoint);
            
            foreach (var point in pointList.Skip(1))
            {
                double distance = EuclideanDistance(target, point);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    farthestPoint = point;
                }
            }
            
            return (farthestPoint, maxDistance);
        }

        /// <summary>
        /// 计算一组点的总路径长度
        /// Calculate total path length of a sequence of points
        /// </summary>
        /// <param name="points">点序列 Point sequence</param>
        /// <param name="closed">是否闭合路径 Whether path is closed</param>
        /// <returns>总长度 Total length</returns>
        public static double CalculatePathLength(IEnumerable<Point2D> points, bool closed = false)
        {
            var pointList = points.ToList();
            if (pointList.Count < 2)
                return 0;
            
            double totalLength = 0;
            
            for (int i = 0; i < pointList.Count - 1; i++)
            {
                totalLength += EuclideanDistance(pointList[i], pointList[i + 1]);
            }
            
            if (closed && pointList.Count > 2)
            {
                totalLength += EuclideanDistance(pointList[pointList.Count - 1], pointList[0]);
            }
            
            return totalLength;
        }

        /// <summary>
        /// 判断点是否在指定距离内
        /// Check if point is within distance
        /// </summary>
        /// <param name="p1">第一个点 First point</param>
        /// <param name="p2">第二个点 Second point</param>
        /// <param name="maxDistance">最大距离 Maximum distance</param>
        /// <returns>是否在范围内 True if within distance</returns>
        public static bool IsWithinDistance(Point2D p1, Point2D p2, double maxDistance)
        {
            // 使用平方距离比较以避免开方运算
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double distanceSquared = dx * dx + dy * dy;
            
            return distanceSquared <= maxDistance * maxDistance;
        }
    }
}

