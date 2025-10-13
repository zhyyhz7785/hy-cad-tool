using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Domain.Services.GeometryAlgorithms
{
    /// <summary>
    /// 线段算法服务实现（平台无关）
    /// 提供线段相关的几何算法实现
    /// </summary>
    public class LineAlgorithmService : ILineAlgorithmService
    {
        // === 线段重叠检查 ===

        public bool CheckOverlap(Line2D line1, Line2D line2, Tolerance tolerance, out Line2D? mergedLine)
        {
            mergedLine = null;

            if (line1 == default || line2 == default)
                return false;

            // 先检查是否共线
            if (!IsCollinear(line1, line2, tolerance))
                return false;

            // 根据线段方向选择合适的重叠检查方法
            if (IsHorizontal(line1, tolerance))
                return CheckHorizontalOverlap(line1, line2, tolerance, out mergedLine);
            else if (IsVertical(line1, tolerance))
                return CheckVerticalOverlap(line1, line2, tolerance, out mergedLine);
            else
                return CheckDiagonalOverlap(line1, line2, tolerance, out mergedLine);
        }

        public List<Line2D> MergeOverlappingLines(List<Line2D> lines, Tolerance tolerance)
        {
            if (lines == default || lines.Count == 0)
                return new List<Line2D>();

            var result = new List<Line2D>(lines);
            bool merged = true;

            while (merged)
            {
                merged = false;
                for (int i = 0; i < result.Count - 1; i++)
                {
                    for (int j = i + 1; j < result.Count; j++)
                    {
                        if (CheckOverlap(result[i], result[j], tolerance, out Line2D? mergedLine) && mergedLine.HasValue)
                        {
                            result[i] = mergedLine.Value;
                            result.RemoveAt(j);
                            merged = true;
                            break;
                        }
                    }
                    if (merged) break;
                }
            }

            return result;
        }

        // === 共线性检查 ===

        public bool IsCollinear(Line2D line1, Line2D line2, Tolerance tolerance)
        {
            if (line1 == default || line2 == default)
                return false;

            // 计算方向向量
            var dir1 = new Vector2D(line1.EndPoint.X - line1.StartPoint.X, line1.EndPoint.Y - line1.StartPoint.Y);
            var dir2 = new Vector2D(line2.EndPoint.X - line2.StartPoint.X, line2.EndPoint.Y - line2.StartPoint.Y);

            // 检查方向是否平行（叉积接近零）
            double crossProduct = dir1.X * dir2.Y - dir1.Y * dir2.X;
            if (Math.Abs(crossProduct) > tolerance.Value)
                return false;

            // 检查点是否在同一直线上
            var vec = new Vector2D(line2.StartPoint.X - line1.StartPoint.X, line2.StartPoint.Y - line1.StartPoint.Y);
            double cross = vec.X * dir1.Y - vec.Y * dir1.X;
            return Math.Abs(cross) <= tolerance.Value;
        }

        public List<List<Line2D>> FindCollinearGroups(List<Line2D> lines, Tolerance tolerance)
        {
            if (lines == default || lines.Count == 0)
                return new List<List<Line2D>>();

            var groups = new List<List<Line2D>>();
            var processed = new HashSet<int>();

            for (int i = 0; i < lines.Count; i++)
            {
                if (processed.Contains(i))
                    continue;

                var group = new List<Line2D> { lines[i] };
                processed.Add(i);

                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (processed.Contains(j))
                        continue;

                    if (IsCollinear(lines[i], lines[j], tolerance))
                    {
                        group.Add(lines[j]);
                        processed.Add(j);
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        // === 线段交点计算 ===

        public bool FindIntersection(Line2D line1, Line2D line2, Tolerance tolerance, out Point2D? intersection)
        {
            intersection = null;

            if (line1 == default || line2 == default)
                return false;

            double x1 = line1.StartPoint.X, y1 = line1.StartPoint.Y;
            double x2 = line1.EndPoint.X, y2 = line1.EndPoint.Y;
            double x3 = line2.StartPoint.X, y3 = line2.StartPoint.Y;
            double x4 = line2.EndPoint.X, y4 = line2.EndPoint.Y;

            double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            
            // 线段平行
            if (Math.Abs(denom) < tolerance.Value)
                return false;

            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
            double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denom;

            // 检查交点是否在两线段上
            if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
            {
                double intersectX = x1 + t * (x2 - x1);
                double intersectY = y1 + t * (y2 - y1);
                intersection = new Point2D(intersectX, intersectY);
                return true;
            }

            return false;
        }

        public List<Point2D> FindAllIntersections(Line2D line, List<Line2D> lines, Tolerance tolerance)
        {
            var intersections = new List<Point2D>();

            if (line == default || lines == default)
                return intersections;

            foreach (var otherLine in lines)
            {
                if (FindIntersection(line, otherLine, tolerance, out Point2D? intersection) && intersection.HasValue)
                {
                    intersections.Add(intersection.Value);
                }
            }

            return intersections;
        }

        // === 距离计算 ===

        public double CalculateDistanceToPoint(Point2D point, Line2D line)
        {
            if (point == default || line == default)
                return double.MaxValue;

            double A = line.EndPoint.Y - line.StartPoint.Y;
            double B = line.StartPoint.X - line.EndPoint.X;
            double C = line.EndPoint.X * line.StartPoint.Y - line.StartPoint.X * line.EndPoint.Y;

            return Math.Abs(A * point.X + B * point.Y + C) / Math.Sqrt(A * A + B * B);
        }

        public double CalculateDistanceBetweenLines(Line2D line1, Line2D line2)
        {
            if (line1 == default || line2 == default)
                return double.MaxValue;

            // 计算所有端点之间的距离，返回最小值
            var distances = new[]
            {
                CalculateDistanceToPoint(line1.StartPoint, line2),
                CalculateDistanceToPoint(line1.EndPoint, line2),
                CalculateDistanceToPoint(line2.StartPoint, line1),
                CalculateDistanceToPoint(line2.EndPoint, line1)
            };

            return distances.Min();
        }

        // === 线段连接与排序 ===

        public List<Line2D> SortByConnectivity(List<Line2D> lines, Tolerance tolerance)
        {
            if (lines == default || lines.Count == 0)
                return new List<Line2D>();

            var used = new HashSet<int>();
            var ordered = new List<Line2D>();
            var current = lines[0];
            ordered.Add(current);
            used.Add(0);

            while (ordered.Count < lines.Count)
            {
                bool found = false;
                var currentEnd = current.EndPoint;

                for (int i = 0; i < lines.Count; i++)
                {
                    if (used.Contains(i)) continue;

                    var candidate = lines[i];
                    if (IsPointsEqual(candidate.StartPoint, currentEnd, tolerance))
                    {
                        ordered.Add(candidate);
                        used.Add(i);
                        current = candidate;
                        found = true;
                        break;
                    }
                    else if (IsPointsEqual(candidate.EndPoint, currentEnd, tolerance))
                    {
                        // 反转线段
                        var reversed = new Line2D(candidate.EndPoint, candidate.StartPoint);
                        ordered.Add(reversed);
                        used.Add(i);
                        current = reversed;
                        found = true;
                        break;
                    }
                }

                if (!found) break;
            }

            return ordered;
        }

        public bool FindCommonPoint(Line2D line1, Line2D line2, Tolerance tolerance, out Point2D? commonPoint)
        {
            commonPoint = null;

            if (line1 == default || line2 == default)
                return false;

            if (IsPointsEqual(line1.StartPoint, line2.StartPoint, tolerance))
            {
                commonPoint = line1.StartPoint;
                return true;
            }
            if (IsPointsEqual(line1.StartPoint, line2.EndPoint, tolerance))
            {
                commonPoint = line1.StartPoint;
                return true;
            }
            if (IsPointsEqual(line1.EndPoint, line2.StartPoint, tolerance))
            {
                commonPoint = line1.EndPoint;
                return true;
            }
            if (IsPointsEqual(line1.EndPoint, line2.EndPoint, tolerance))
            {
                commonPoint = line1.EndPoint;
                return true;
            }

            return false;
        }

        // === 线段属性计算 ===

        public double CalculateLength(Line2D line)
        {
            if (line == default)
                return 0;

            double dx = line.EndPoint.X - line.StartPoint.X;
            double dy = line.EndPoint.Y - line.StartPoint.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public Point2D? CalculateMidpoint(Line2D line)
        {
            if (line == default)
                return null;

            double midX = (line.StartPoint.X + line.EndPoint.X) / 2;
            double midY = (line.StartPoint.Y + line.EndPoint.Y) / 2;
            return new Point2D(midX, midY);
        }

        public double CalculateAngle(Line2D line)
        {
            if (line == default)
                return 0;

            double dx = line.EndPoint.X - line.StartPoint.X;
            double dy = line.EndPoint.Y - line.StartPoint.Y;
            return Math.Atan2(dy, dx);
        }

        // === 线段分类 ===

        public bool IsHorizontal(Line2D line, Tolerance tolerance)
        {
            if (line == default)
                return false;

            return Math.Abs(line.EndPoint.Y - line.StartPoint.Y) <= tolerance.Value;
        }

        public bool IsVertical(Line2D line, Tolerance tolerance)
        {
            if (line == default)
                return false;

            return Math.Abs(line.EndPoint.X - line.StartPoint.X) <= tolerance.Value;
        }

        public bool AreParallel(Line2D line1, Line2D line2, Tolerance tolerance)
        {
            if (line1 == default || line2 == default)
                return false;

            var dir1 = new Vector2D(line1.EndPoint.X - line1.StartPoint.X, line1.EndPoint.Y - line1.StartPoint.Y);
            var dir2 = new Vector2D(line2.EndPoint.X - line2.StartPoint.X, line2.EndPoint.Y - line2.StartPoint.Y);

            double crossProduct = dir1.X * dir2.Y - dir1.Y * dir2.X;
            return Math.Abs(crossProduct) <= tolerance.Value;
        }

        public bool ArePerpendicular(Line2D line1, Line2D line2, Tolerance tolerance)
        {
            if (line1 == default || line2 == default)
                return false;

            var dir1 = new Vector2D(line1.EndPoint.X - line1.StartPoint.X, line1.EndPoint.Y - line1.StartPoint.Y);
            var dir2 = new Vector2D(line2.EndPoint.X - line2.StartPoint.X, line2.EndPoint.Y - line2.StartPoint.Y);

            double dotProduct = dir1.X * dir2.X + dir1.Y * dir2.Y;
            return Math.Abs(dotProduct) <= tolerance.Value;
        }

        // === 私有辅助方法 ===

        private bool CheckHorizontalOverlap(Line2D line1, Line2D line2, Tolerance tolerance, out Line2D? mergedLine)
        {
            mergedLine = null;

            // 检查Y坐标是否在容差范围内
            if (Math.Abs(line1.StartPoint.Y - line2.StartPoint.Y) > tolerance.Value)
                return false;

            // 计算X方向的范围
            double line1MinX = Math.Min(line1.StartPoint.X, line1.EndPoint.X);
            double line1MaxX = Math.Max(line1.StartPoint.X, line1.EndPoint.X);
            double line2MinX = Math.Min(line2.StartPoint.X, line2.EndPoint.X);
            double line2MaxX = Math.Max(line2.StartPoint.X, line2.EndPoint.X);

            // 检查X方向是否有重叠
            if (line1MinX > line2MaxX + tolerance.Value || line2MinX > line1MaxX + tolerance.Value)
                return false;

            // 计算合并后的范围
            double minX = Math.Min(line1MinX, line2MinX);
            double maxX = Math.Max(line1MaxX, line2MaxX);
            double y = line1.StartPoint.Y;

            mergedLine = new Line2D(new Point2D(minX, y), new Point2D(maxX, y));
            return true;
        }

        private bool CheckVerticalOverlap(Line2D line1, Line2D line2, Tolerance tolerance, out Line2D? mergedLine)
        {
            mergedLine = null;

            // 检查X坐标是否在容差范围内
            if (Math.Abs(line1.StartPoint.X - line2.StartPoint.X) > tolerance.Value)
                return false;

            // 计算Y方向的范围
            double line1MinY = Math.Min(line1.StartPoint.Y, line1.EndPoint.Y);
            double line1MaxY = Math.Max(line1.StartPoint.Y, line1.EndPoint.Y);
            double line2MinY = Math.Min(line2.StartPoint.Y, line2.EndPoint.Y);
            double line2MaxY = Math.Max(line2.StartPoint.Y, line2.EndPoint.Y);

            // 检查Y方向是否有重叠
            if (line1MinY > line2MaxY + tolerance.Value || line2MinY > line1MaxY + tolerance.Value)
                return false;

            // 计算合并后的范围
            double minY = Math.Min(line1MinY, line2MinY);
            double maxY = Math.Max(line1MaxY, line2MaxY);
            double x = line1.StartPoint.X;

            mergedLine = new Line2D(new Point2D(x, minY), new Point2D(x, maxY));
            return true;
        }

        private bool CheckDiagonalOverlap(Line2D line1, Line2D line2, Tolerance tolerance, out Line2D? mergedLine)
        {
            mergedLine = null;

            // 对于斜线，使用参数化方法检查重叠
            // 这里实现简化版本，实际项目中可能需要更复杂的算法
            
            // 检查四个端点，找出最远的两个点作为合并线段
            var allPoints = new[] { line1.StartPoint, line1.EndPoint, line2.StartPoint, line2.EndPoint };
            double maxDistance = 0;
            Point2D farthestPoint1 = allPoints[0];
            Point2D farthestPoint2 = allPoints[1];

            for (int i = 0; i < allPoints.Length - 1; i++)
            {
                for (int j = i + 1; j < allPoints.Length; j++)
                {
                    double distance = CalculateDistance(allPoints[i], allPoints[j]);
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        farthestPoint1 = allPoints[i];
                        farthestPoint2 = allPoints[j];
                    }
                }
            }

            if (farthestPoint1 != null && farthestPoint2 != null)
            {
                mergedLine = new Line2D(farthestPoint1, farthestPoint2);
                return true;
            }

            return false;
        }

        private bool IsPointsEqual(Point2D point1, Point2D point2, Tolerance tolerance)
        {
            if (point1 == default || point2 == default)
                return false;

            return Math.Abs(point1.X - point2.X) <= tolerance.Value &&
                   Math.Abs(point1.Y - point2.Y) <= tolerance.Value;
        }

        private double CalculateDistance(Point2D point1, Point2D point2)
        {
            if (point1 == default || point2 == default)
                return double.MaxValue;

            double dx = point2.X - point1.X;
            double dy = point2.Y - point1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }

}
