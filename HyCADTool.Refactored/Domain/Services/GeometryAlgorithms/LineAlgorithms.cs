using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Domain.Services.GeometryAlgorithms
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
            bool isHorizontal = Math.Abs(dir.Y) < Math.Abs(dir.X);

            if (isHorizontal)
            {
                // 水平方向，比较 X 坐标
                double min1 = Math.Min(line1.StartPoint.X, line1.EndPoint.X);
                double max1 = Math.Max(line1.StartPoint.X, line1.EndPoint.X);
                double min2 = Math.Min(line2.StartPoint.X, line2.EndPoint.X);
                double max2 = Math.Max(line2.StartPoint.X, line2.EndPoint.X);

                // 检查是否有重叠
                if (max1 < min2 - tolerance || max2 < min1 - tolerance)
                    return default;

                // 计算重叠区域
                double overlapMin = Math.Max(min1, min2);
                double overlapMax = Math.Min(max1, max2);

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
                double min1 = Math.Min(line1.StartPoint.Y, line1.EndPoint.Y);
                double max1 = Math.Max(line1.StartPoint.Y, line1.EndPoint.Y);
                double min2 = Math.Min(line2.StartPoint.Y, line2.EndPoint.Y);
                double max2 = Math.Max(line2.StartPoint.Y, line2.EndPoint.Y);

                if (max1 < min2 - tolerance || max2 < min1 - tolerance)
                    return default;

                double overlapMin = Math.Max(min1, min2);
                double overlapMax = Math.Min(max1, max2);

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
                            bool isHorizontal = Math.Abs(dir.Y) < Math.Abs(dir.X);

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
    }
}

