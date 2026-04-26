using HyCADTool.Shared.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Shared.Geometry.Algorithms
{
    /// <summary>
    /// 线段算法服务（平台无关）
    /// 提供线段相关的几何算法
    /// </summary>
    public static class LineAlgorithms
    {
        /// <summary>
        /// 检测两条线段是否重叠
        /// 返回重叠部分的线段，如果不重叠返回 default
        /// </summary>
        public static Line2D CheckOverlap(Line2D line1, Line2D line2, double tolerance = 1e-6)
        {
            // 首先检查是否共线
            if (!line1.IsCollinear(line2, tolerance))
                return default;

            // 检查重叠
            // 将线段投影到主方向上
            Vector2D dir = line1.Direction;
            bool isHorizontal = System.Math.Abs(dir.Y) < System.Math.Abs(dir.X);

            if (isHorizontal)
            {
                // 水平方向，比较 X 坐标
                double min1 = System.Math.Min(line1.StartPoint.X, line1.EndPoint.X);
                double max1 = System.Math.Max(line1.StartPoint.X, line1.EndPoint.X);
                double min2 = System.Math.Min(line2.StartPoint.X, line2.EndPoint.X);
                double max2 = System.Math.Max(line2.StartPoint.X, line2.EndPoint.X);

                // 检查是否有重叠
                if (max1 < min2 - tolerance || max2 < min1 - tolerance)
                    return default;

                // 计算重叠区域
                double overlapMin = System.Math.Max(min1, min2);
                double overlapMax = System.Math.Min(max1, max2);

                if (overlapMax - overlapMin < tolerance)
                    return default;

                double y = line1.StartPoint.Y; // 共线，Y坐标相同
                return new Line2D(
                    new Point2D(overlapMin, y),
                    new Point2D(overlapMax, y)
                );
            }
            else
            {
                // 垂直方向，比较 Y 坐标
                double min1 = System.Math.Min(line1.StartPoint.Y, line1.EndPoint.Y);
                double max1 = System.Math.Max(line1.StartPoint.Y, line1.EndPoint.Y);
                double min2 = System.Math.Min(line2.StartPoint.Y, line2.EndPoint.Y);
                double max2 = System.Math.Max(line2.StartPoint.Y, line2.EndPoint.Y);

                if (max1 < min2 - tolerance || max2 < min1 - tolerance)
                    return default;

                double overlapMin = System.Math.Max(min1, min2);
                double overlapMax = System.Math.Min(max1, max2);

                if (overlapMax - overlapMin < tolerance)
                    return default;

                double x = line1.StartPoint.X;
                return new Line2D(
                    new Point2D(x, overlapMin),
                    new Point2D(x, overlapMax)
                );
            }
        }

        /// <summary>
        /// 合并共线的线段
        /// </summary>
        public static IEnumerable<Line2D> MergeCollinearLines(IEnumerable<Line2D> lines, double tolerance = 1e-6)
        {
            var lineList = lines.ToList();
            if (lineList.Count == 0)
                yield break;

            if (lineList.Count == 1)
            {
                yield return lineList[0];
                yield break;
            }

            var merged = new HashSet<int>();
            var result = new List<Line2D>();

            for (int i = 0; i < lineList.Count; i++)
            {
                if (merged.Contains(i))
                    continue;

                Line2D current = lineList[i];

                for (int j = i + 1; j < lineList.Count; j++)
                {
                    if (merged.Contains(j))
                        continue;

                    // 检查是否可以合并
                    if (current.IsCollinear(lineList[j], tolerance))
                    {
                        // 尝试合并
                        var overlap = CheckOverlap(current, lineList[j], tolerance);
                        if (overlap != default)
                        {
                            // 找到包含两条线段的最大范围
                            var allPoints = new[]
                            {
                                current.StartPoint, current.EndPoint,
                                lineList[j].StartPoint, lineList[j].EndPoint
                            };

                            // 根据主方向排序
                            Vector2D dir = current.Direction;
                            bool isHorizontal = System.Math.Abs(dir.Y) < System.Math.Abs(dir.X);

                            Point2D start, end;
                            if (isHorizontal)
                            {
                                start = allPoints.OrderBy(p => p.X).First();
                                end = allPoints.OrderBy(p => p.X).Last();
                            }
                            else
                            {
                                start = allPoints.OrderBy(p => p.Y).First();
                                end = allPoints.OrderBy(p => p.Y).Last();
                            }

                            current = new Line2D(start, end);
                            merged.Add(j);
                        }
                    }
                }

                result.Add(current);
                merged.Add(i);
            }

            foreach (var line in result)
            {
                yield return line;
            }
        }

        /// <summary>
        /// 按连通性排序线段
        /// 将线段按首尾相连的顺序排列
        /// </summary>
        public static List<Line2D> SortLinesByConnectivity(IEnumerable<Line2D> lines, double tolerance = 1e-6)
        {
            var lineList = lines.ToList();
            if (lineList.Count == 0)
                return new List<Line2D>();

            var used = new HashSet<int>();
            var sorted = new List<Line2D>();

            // 从第一条线段开始
            Line2D current = lineList[0];
            sorted.Add(current);
            used.Add(0);

            while (sorted.Count < lineList.Count)
            {
                bool found = false;
                Point2D endPoint = current.EndPoint;

                for (int i = 0; i < lineList.Count; i++)
                {
                    if (used.Contains(i))
                        continue;

                    Line2D candidate = lineList[i];

                    // 检查起点是否连接
                    if (endPoint.DistanceTo(candidate.StartPoint) < tolerance)
                    {
                        sorted.Add(candidate);
                        used.Add(i);
                        current = candidate;
                        found = true;
                        break;
                    }
                    // 检查终点是否连接（需要反转）
                    else if (endPoint.DistanceTo(candidate.EndPoint) < tolerance)
                    {
                        Line2D reversed = new Line2D(candidate.EndPoint, candidate.StartPoint);
                        sorted.Add(reversed);
                        used.Add(i);
                        current = reversed;
                        found = true;
                        break;
                    }
                }

                // 如果找不到连接的线段，跳出循环
                if (!found)
                    break;
            }

            return sorted;
        }

        /// <summary>
        /// 将多个线段连接成多边形
        /// </summary>
        public static Polygon2D JoinLinesToPolygon(IEnumerable<Line2D> lines, double tolerance = 1e-6)
        {
            var sorted = SortLinesByConnectivity(lines, tolerance);

            if (sorted.Count < 3)
                return default;

            // 提取顶点
            var vertices = new List<Point2D>();
            foreach (var line in sorted)
            {
                vertices.Add(line.StartPoint);
            }

            // 检查是否闭合
            if (vertices[0].DistanceTo(sorted[sorted.Count - 1].EndPoint) > tolerance)
                return default;

            return new Polygon2D(vertices);
        }

        /// <summary>
        /// 判断两线段是否平行
        /// Check if two lines are parallel
        /// </summary>
        /// <param name="line1">第一条线段 First line</param>
        /// <param name="line2">第二条线段 Second line</param>
        /// <param name="tolerance">容差 Tolerance</param>
        /// <returns>是否平行 True if parallel</returns>
        public static bool AreParallel(Line2D line1, Line2D line2, double tolerance = 1e-10)
        {
            var dir1 = line1.Direction;
            var dir2 = line2.Direction;
            
            // 叉积为零表示平行
            // Cross product equals zero means parallel
            double cross = dir1.X * dir2.Y - dir1.Y * dir2.X;
            return System.Math.Abs(cross) < tolerance;
        }

        /// <summary>
        /// 判断两线段是否垂直
        /// Check if two lines are perpendicular
        /// </summary>
        /// <param name="line1">第一条线段 First line</param>
        /// <param name="line2">第二条线段 Second line</param>
        /// <param name="tolerance">容差 Tolerance</param>
        /// <returns>是否垂直 True if perpendicular</returns>
        public static bool ArePerpendicular(Line2D line1, Line2D line2, double tolerance = 1e-10)
        {
            var dir1 = line1.Direction;
            var dir2 = line2.Direction;
            
            // 点积为零表示垂直
            // Dot product equals zero means perpendicular
            double dot = dir1.X * dir2.X + dir1.Y * dir2.Y;
            return System.Math.Abs(dot) < tolerance;
        }

        /// <summary>
        /// 计算两线段的交点
        /// Calculate intersection point of two line segments
        /// </summary>
        /// <param name="line1">第一条线段 First line</param>
        /// <param name="line2">第二条线段 Second line</param>
        /// <param name="extendLines">是否延长线段 Whether to extend lines</param>
        /// <param name="tolerance">容差 Tolerance</param>
        /// <returns>交点（如果不相交返回 null）Intersection point (null if no intersection)</returns>
        public static Point2D? GetIntersection(Line2D line1, Line2D line2, bool extendLines = false, double tolerance = 1e-10)
        {
            // 使用参数方程求解
            // Using parametric equations
            // Line1: P = P1 + t * (P2 - P1)
            // Line2: Q = Q1 + s * (Q2 - Q1)
            
            double x1 = line1.StartPoint.X, y1 = line1.StartPoint.Y;
            double x2 = line1.EndPoint.X, y2 = line1.EndPoint.Y;
            double x3 = line2.StartPoint.X, y3 = line2.StartPoint.Y;
            double x4 = line2.EndPoint.X, y4 = line2.EndPoint.Y;
            
            double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            
            if (System.Math.Abs(denom) < tolerance)
                return null; // 平行或重合 Parallel or coincident
            
            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
            double s = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denom;
            
            // 检查交点是否在两条线段内
            // Check if intersection is within both segments
            if (extendLines || (t >= 0 && t <= 1 && s >= 0 && s <= 1))
            {
                return new Point2D(
                    x1 + t * (x2 - x1),
                    y1 + t * (y2 - y1)
                );
            }
            
            return null;
        }

        /// <summary>
        /// 计算线段的长度
        /// Calculate length of line segment
        /// </summary>
        /// <param name="line">线段 Line</param>
        /// <returns>长度 Length</returns>
        public static double GetLength(Line2D line)
        {
            // TODO: Replace with IPointAlgorithmService
            var dx = line.EndPoint.X - line.StartPoint.X;
            var dy = line.EndPoint.Y - line.StartPoint.Y;
            return System.Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 获取线段的中点
        /// Get midpoint of line segment
        /// </summary>
        /// <param name="line">线段 Line</param>
        /// <returns>中点 Midpoint</returns>
        public static Point2D GetMidpoint(Line2D line)
        {
            // TODO: Replace with IPointAlgorithmService
            return new Point2D(
                (line.StartPoint.X + line.EndPoint.X) / 2,
                (line.StartPoint.Y + line.EndPoint.Y) / 2
            );
        }

        /// <summary>
        /// 计算线段与 X 轴的夹角
        /// Calculate angle between line and X axis
        /// </summary>
        /// <param name="line">线段 Line</param>
        /// <returns>角度（弧度）[-π, π] Angle in radians [-π, π]</returns>
        public static double GetAngle(Line2D line)
        {
            var dir = line.Direction;
            return System.Math.Atan2(dir.Y, dir.X);
        }

        /// <summary>
        /// 反转线段
        /// Reverse line segment
        /// </summary>
        /// <param name="line">线段 Line</param>
        /// <returns>反转后的线段 Reversed line</returns>
        public static Line2D Reverse(Line2D line)
        {
            return new Line2D(line.EndPoint, line.StartPoint);
        }

        /// <summary>
        /// 延长或缩短线段
        /// Extend or shorten line segment
        /// </summary>
        /// <param name="line">线段 Line</param>
        /// <param name="startDistance">起点延长距离（负值为缩短）Start extension distance (negative to shorten)</param>
        /// <param name="endDistance">终点延长距离（负值为缩短）End extension distance (negative to shorten)</param>
        /// <returns>延长后的线段 Extended line</returns>
        public static Line2D Extend(Line2D line, double startDistance, double endDistance)
        {
            var dir = line.Direction;
            double length = GetLength(line);
            
            if (length < 1e-10)
                return line;
            
            var normalizedDir = new Vector2D(dir.X / length, dir.Y / length);
            
            var newStart = new Point2D(
                line.StartPoint.X - normalizedDir.X * startDistance,
                line.StartPoint.Y - normalizedDir.Y * startDistance
            );
            
            var newEnd = new Point2D(
                line.EndPoint.X + normalizedDir.X * endDistance,
                line.EndPoint.Y + normalizedDir.Y * endDistance
            );
            
            return new Line2D(newStart, newEnd);
        }

        /// <summary>
        /// 偏移线段
        /// Offset line segment
        /// </summary>
        /// <param name="line">线段 Line</param>
        /// <param name="distance">偏移距离（正值为左侧，负值为右侧）Offset distance (positive for left, negative for right)</param>
        /// <returns>偏移后的线段 Offset line</returns>
        public static Line2D Offset(Line2D line, double distance)
        {
            var dir = line.Direction;
            double length = System.Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
            
            if (length < 1e-10)
                return line;
            
            // 垂直向量（左侧）
            // Perpendicular vector (left side)
            var perpendicular = new Vector2D(-dir.Y / length, dir.X / length);
            
            var offsetVector = new Vector2D(
                perpendicular.X * distance,
                perpendicular.Y * distance
            );
            
            var newStart = new Point2D(
                line.StartPoint.X + offsetVector.X,
                line.StartPoint.Y + offsetVector.Y
            );
            
            var newEnd = new Point2D(
                line.EndPoint.X + offsetVector.X,
                line.EndPoint.Y + offsetVector.Y
            );
            
            return new Line2D(newStart, newEnd);
        }

        /// <summary>
        /// 判断点是否在线段上
        /// Check if point is on line segment
        /// </summary>
        /// <param name="line">线段 Line</param>
        /// <param name="point">点 Point</param>
        /// <param name="tolerance">容差 Tolerance</param>
        /// <returns>是否在线段上 True if on segment</returns>
        public static bool ContainsPoint(Line2D line, Point2D point, double tolerance = 1e-10)
        {
            // 检查距离
            // TODO: Replace with IPointAlgorithmService.DistanceToLine
            // 计算点到直线的垂直距离
            var p1 = line.StartPoint;
            var p2 = line.EndPoint;
            var numerator = System.Math.Abs((p2.Y - p1.Y) * point.X - (p2.X - p1.X) * point.Y + p2.X * p1.Y - p2.Y * p1.X);
            var denominator = System.Math.Sqrt(System.Math.Pow(p2.Y - p1.Y, 2) + System.Math.Pow(p2.X - p1.X, 2));
            double distToLine = denominator > 1e-10 ? numerator / denominator : 0;
            
            if (distToLine > tolerance)
                return false;
            
            // 检查是否在线段范围内
            double minX = System.Math.Min(line.StartPoint.X, line.EndPoint.X) - tolerance;
            double maxX = System.Math.Max(line.StartPoint.X, line.EndPoint.X) + tolerance;
            double minY = System.Math.Min(line.StartPoint.Y, line.EndPoint.Y) - tolerance;
            double maxY = System.Math.Max(line.StartPoint.Y, line.EndPoint.Y) + tolerance;
            
            return point.X >= minX && point.X <= maxX && point.Y >= minY && point.Y <= maxY;
        }

        /// <summary>
        /// 分割线段
        /// Split line segment
        /// </summary>
        /// <param name="line">线段 Line</param>
        /// <param name="parameter">分割参数 [0,1] Split parameter [0,1]</param>
        /// <returns>分割后的两条线段 Two split line segments</returns>
        public static (Line2D, Line2D) Split(Line2D line, double parameter)
        {
            // TODO: Replace with IPointAlgorithmService.Lerp
            // 线性插值计算分割点
            parameter = System.Math.Max(0, System.Math.Min(1, parameter)); // 限制在[0,1]
            var splitPoint = new Point2D(
                line.StartPoint.X + parameter * (line.EndPoint.X - line.StartPoint.X),
                line.StartPoint.Y + parameter * (line.EndPoint.Y - line.StartPoint.Y)
            );
            
            return (
                new Line2D(line.StartPoint, splitPoint),
                new Line2D(splitPoint, line.EndPoint)
            );
        }

        /// <summary>
        /// 判断线段是否水平
        /// Check if line is horizontal
        /// </summary>
        /// <param name="line">线段 Line</param>
        /// <param name="tolerance">容差 Tolerance</param>
        /// <returns>是否水平 True if horizontal</returns>
        public static bool IsHorizontal(Line2D line, double tolerance = 1e-10)
        {
            return System.Math.Abs(line.EndPoint.Y - line.StartPoint.Y) < tolerance;
        }

        /// <summary>
        /// 判断线段是否垂直
        /// Check if line is vertical
        /// </summary>
        /// <param name="line">线段 Line</param>
        /// <param name="tolerance">容差 Tolerance</param>
        /// <returns>是否垂直 True if vertical</returns>
        public static bool IsVertical(Line2D line, double tolerance = 1e-10)
        {
            return System.Math.Abs(line.EndPoint.X - line.StartPoint.X) < tolerance;
        }
    }
}

