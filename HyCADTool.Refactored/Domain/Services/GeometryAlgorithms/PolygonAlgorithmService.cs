using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Domain.Services.GeometryAlgorithms
{
    /// <summary>
    /// 多边形算法服务实现（平台无关）
    /// 提供多边形相关的几何算法实现
    /// </summary>
    public class PolygonAlgorithmService : IPolygonAlgorithmService
    {
        private readonly ILineAlgorithmService _lineAlgorithms;

        public PolygonAlgorithmService(ILineAlgorithmService lineAlgorithms)
        {
            _lineAlgorithms = lineAlgorithms ?? throw new ArgumentNullException(nameof(lineAlgorithms));
        }

        // === 多边形顶点处理 ===

        public Polygon2D RemoveDuplicateVertices(Polygon2D polygon, Tolerance tolerance)
        {
        if (polygon?.Vertices == default || polygon.Vertices.Count == 0)
            return polygon;

        var uniquePoints = new List<Point2D>();
        var lastPoint = polygon.Vertices[0];
        uniquePoints.Add(lastPoint);

        for (int i = 1; i < polygon.Vertices.Count; i++)
        {
            var currentPoint = polygon.Vertices[i];
            if (!IsPointsEqual(currentPoint, lastPoint, tolerance))
            {
                uniquePoints.Add(currentPoint);
                lastPoint = currentPoint;
            }
        }

        // 检查首尾是否重复
        if (uniquePoints.Count > 1 && IsPointsEqual(uniquePoints[0], uniquePoints.Last(), tolerance))
        {
            uniquePoints.RemoveAt(uniquePoints.Count - 1);
        }

        return new Polygon2D(uniquePoints, polygon.IsClosed);
        }

        public Polygon2D SetClockwise(Polygon2D polygon)
        {
            if (polygon?.Vertices == default || polygon.Vertices.Count < 3)
                return polygon;

            if (IsClockwise(polygon))
                return polygon;

            var reversedPoints = new List<Point2D>(polygon.Vertices);
            reversedPoints.Reverse();
            return new Polygon2D(reversedPoints, polygon.IsClosed);
        }

        public Polygon2D SetCounterclockwise(Polygon2D polygon)
        {
            if (polygon?.Vertices == default || polygon.Vertices.Count < 3)
                return polygon;

            if (!IsClockwise(polygon))
                return polygon;

            var reversedPoints = new List<Point2D>(polygon.Vertices);
            reversedPoints.Reverse();
            return new Polygon2D(reversedPoints, polygon.IsClosed);
        }

        public bool IsClockwise(Polygon2D polygon)
        {
            if (polygon?.Vertices == default || polygon.Vertices.Count < 3)
                return false;

            double signedArea = 0;
            var points = polygon.Vertices;
            
            for (int i = 0; i < points.Count; i++)
            {
                int next = (i + 1) % points.Count;
                signedArea += (points[next].X - points[i].X) * (points[next].Y + points[i].Y);
            }

            return signedArea > 0;
        }

        public Polygon2D ResetVertexOrder(Polygon2D polygon)
        {
            if (polygon?.Vertices == default || polygon.Vertices.Count == 0)
                return polygon;

            // 找到最小点（先按X坐标，再按Y坐标）
            var minPoint = polygon.Vertices.OrderBy(p => p.X).ThenBy(p => p.Y).First();
            int minIndex = polygon.Vertices.ToList().IndexOf(minPoint);

            if (minIndex == 0)
                return polygon;

            // 重新排列顶点，从最小点开始
            var reorderedPoints = new List<Point2D>();
            for (int i = 0; i < polygon.Vertices.Count; i++)
            {
                int index = (minIndex + i) % polygon.Vertices.Count;
                reorderedPoints.Add(polygon.Vertices[index]);
            }

            return new Polygon2D(reorderedPoints, polygon.IsClosed);
        }

        // === 多边形属性计算 ===

        public double CalculateArea(Polygon2D polygon)
        {
            if (polygon?.Vertices == default || polygon.Vertices.Count < 3)
                return 0;

            double area = 0;
            var points = polygon.Vertices;

            for (int i = 0; i < points.Count; i++)
            {
                int next = (i + 1) % points.Count;
                area += points[i].X * points[next].Y - points[next].X * points[i].Y;
            }

            return Math.Abs(area) / 2.0;
        }

        public double CalculatePerimeter(Polygon2D polygon)
        {
            if (polygon?.Vertices == default || polygon.Vertices.Count < 2)
                return 0;

            double perimeter = 0;
            var points = polygon.Vertices;

            for (int i = 0; i < points.Count; i++)
            {
                int next = (i + 1) % points.Count;
                if (i == points.Count - 1 && !polygon.IsClosed)
                    break;
                
                perimeter += CalculateDistance(points[i], points[next]);
            }

            return perimeter;
        }

        public Point2D CalculateCentroid(Polygon2D polygon)
        {
            if (polygon?.Vertices == default || polygon.Vertices.Count == 0)
                return default;

            if (polygon.Vertices.Count == 1)
                return polygon.Vertices[0];

            double area = CalculateArea(polygon);
            if (Math.Abs(area) < 1e-10)
            {
                // 面积为0，返回顶点的平均位置
                double avgX = polygon.Vertices.Average(p => p.X);
                double avgY = polygon.Vertices.Average(p => p.Y);
                return new Point2D(avgX, avgY);
            }

            double cx = 0, cy = 0;
            var points = polygon.Vertices;

            for (int i = 0; i < points.Count; i++)
            {
                int next = (i + 1) % points.Count;
                double crossProduct = points[i].X * points[next].Y - points[next].X * points[i].Y;
                cx += (points[i].X + points[next].X) * crossProduct;
                cy += (points[i].Y + points[next].Y) * crossProduct;
            }

            double factor = 1.0 / (6.0 * area);
            return new Point2D(cx * factor, cy * factor);
        }

        public (Point2D MinPoint, Point2D MaxPoint) CalculateBoundingBox(Polygon2D polygon)
        {
            if (polygon?.Vertices == default || polygon.Vertices.Count == 0)
                return (default, default);

            double minX = polygon.Vertices.Min(p => p.X);
            double minY = polygon.Vertices.Min(p => p.Y);
            double maxX = polygon.Vertices.Max(p => p.X);
            double maxY = polygon.Vertices.Max(p => p.Y);

            return (new Point2D(minX, minY), new Point2D(maxX, maxY));
        }

        // === 多边形空间关系 ===

        public bool IsPointInside(Point2D point, Polygon2D polygon, Tolerance tolerance)
        {
            if (point == default || polygon?.Vertices == default || polygon.Vertices.Count < 3)
                return false;

            // 使用射线投射算法
            int intersectionCount = 0;
            var points = polygon.Vertices;

            for (int i = 0; i < points.Count; i++)
            {
                int next = (i + 1) % points.Count;
                
                // 检查射线是否与边相交
                if (DoesRayIntersectSegment(point, points[i], points[next]))
                {
                    intersectionCount++;
                }
            }

            return intersectionCount % 2 == 1;
        }

        public bool IsPointOnBoundary(Point2D point, Polygon2D polygon, Tolerance tolerance)
        {
            if (point == default || polygon?.Vertices == default)
                return false;

            var edges = GetEdges(polygon);
            return edges.Any(edge => _lineAlgorithms.CalculateDistanceToPoint(point, edge) <= tolerance.Value);
        }

        public bool DoPolygonsIntersect(Polygon2D polygon1, Polygon2D polygon2, Tolerance tolerance)
        {
            if (polygon1?.Vertices == default || polygon2?.Vertices == default)
                return false;

            var edges1 = GetEdges(polygon1);
            var edges2 = GetEdges(polygon2);

            // 检查边是否相交
            foreach (var edge1 in edges1)
            {
                foreach (var edge2 in edges2)
                {
                    if (_lineAlgorithms.FindIntersection(edge1, edge2, tolerance, out _))
                        return true;
                }
            }

            // 检查一个多边形的顶点是否在另一个多边形内
            return polygon1.Vertices.Any(p => IsPointInside(p, polygon2, tolerance)) ||
                   polygon2.Vertices.Any(p => IsPointInside(p, polygon1, tolerance));
        }

        public List<Polygon2D> CalculateIntersection(Polygon2D polygon1, Polygon2D polygon2, Tolerance tolerance)
        {
            // 这是一个复杂的算法，通常需要使用专门的几何计算库
            // 这里提供简化版本，实际项目中建议使用Clipper2等专业库
            
            var result = new List<Polygon2D>();
            
            if (!DoPolygonsIntersect(polygon1, polygon2, tolerance))
                return result;

            // 简化实现：如果相交，返回重叠区域的近似多边形
            // 实际应用中需要更复杂的算法
            
            return result;
        }

        public List<Polygon2D> CalculateUnion(Polygon2D polygon1, Polygon2D polygon2, Tolerance tolerance)
        {
            // 类似于交集，并集也是复杂的算法
            // 这里提供框架，实际实现需要专业的几何计算库
            
            var result = new List<Polygon2D>();
            
            if (!DoPolygonsIntersect(polygon1, polygon2, tolerance))
            {
                result.Add(polygon1);
                result.Add(polygon2);
            }
            else
            {
                // 简化处理：返回包含两个多边形的边界框
                var bbox1 = CalculateBoundingBox(polygon1);
                var bbox2 = CalculateBoundingBox(polygon2);
                
                var minX = Math.Min(bbox1.MinPoint.X, bbox2.MinPoint.X);
                var minY = Math.Min(bbox1.MinPoint.Y, bbox2.MinPoint.Y);
                var maxX = Math.Max(bbox1.MaxPoint.X, bbox2.MaxPoint.X);
                var maxY = Math.Max(bbox1.MaxPoint.Y, bbox2.MaxPoint.Y);
                
                result.Add(CreateRectangle(new Point2D(minX, minY), new Point2D(maxX, maxY)));
            }
            
            return result;
        }

        // === 多边形变换 ===

        public Polygon2D Translate(Polygon2D polygon, double deltaX, double deltaY)
        {
            if (polygon?.Vertices == default)
                return polygon;

            var translatedPoints = polygon.Vertices.Select(p => new Point2D(p.X + deltaX, p.Y + deltaY)).ToList();
            return new Polygon2D(translatedPoints, polygon.IsClosed);
        }

        public Polygon2D Rotate(Polygon2D polygon, double angle, Point2D center)
        {
            if (polygon?.Vertices == default || center == default)
                return polygon;

            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);

            var rotatedPoints = polygon.Vertices.Select(p =>
            {
                double dx = p.X - center.X;
                double dy = p.Y - center.Y;
                double newX = center.X + dx * cos - dy * sin;
                double newY = center.Y + dx * sin + dy * cos;
                return new Point2D(newX, newY);
            }).ToList();

            return new Polygon2D(rotatedPoints, polygon.IsClosed);
        }

        public Polygon2D Scale(Polygon2D polygon, double scaleX, double scaleY, Point2D center)
        {
            if (polygon?.Vertices == default || center == default)
                return polygon;

            var scaledPoints = polygon.Vertices.Select(p =>
            {
                double dx = p.X - center.X;
                double dy = p.Y - center.Y;
                double newX = center.X + dx * scaleX;
                double newY = center.Y + dy * scaleY;
                return new Point2D(newX, newY);
            }).ToList();

            return new Polygon2D(scaledPoints, polygon.IsClosed);
        }

        // === 多边形构造 ===

        public Polygon2D CreateFromLines(List<Line2D> lines, Tolerance tolerance)
        {
            if (lines == default || lines.Count == 0)
                return null;

            var sortedLines = _lineAlgorithms.SortByConnectivity(lines, tolerance);
            if (sortedLines.Count < 3)
                return null;

            var points = new List<Point2D> { sortedLines[0].StartPoint };
            points.AddRange(sortedLines.Select(line => line.EndPoint));

            // 检查是否闭合
            bool isClosed = IsPointsEqual(points[0], points.Last(), tolerance);
            if (isClosed)
                points.RemoveAt(points.Count - 1);

            return new Polygon2D(points, isClosed);
        }

        public Polygon2D CreateFromPoints(List<Point2D> points, bool isClosed = true)
        {
            if (points == default || points.Count < 2)
                return null;

            return new Polygon2D(new List<Point2D>(points), isClosed);
        }

        public Polygon2D CreateRectangle(Point2D minPoint, Point2D maxPoint)
        {
            if (minPoint == default || maxPoint == default)
                return null;

            var points = new List<Point2D>
            {
                new Point2D(minPoint.X, minPoint.Y),
                new Point2D(maxPoint.X, minPoint.Y),
                new Point2D(maxPoint.X, maxPoint.Y),
                new Point2D(minPoint.X, maxPoint.Y)
            };

            return new Polygon2D(points, true);
        }

        public Polygon2D CreateCircle(Point2D center, double radius, int segments = 32)
        {
            if (center == default || radius <= 0 || segments < 3)
                return null;

            var points = new List<Point2D>();
            double angleStep = 2 * Math.PI / segments;

            for (int i = 0; i < segments; i++)
            {
                double angle = i * angleStep;
                double x = center.X + radius * Math.Cos(angle);
                double y = center.Y + radius * Math.Sin(angle);
                points.Add(new Point2D(x, y));
            }

            return new Polygon2D(points, true);
        }

        // === 多边形简化 ===

        public Polygon2D Simplify(Polygon2D polygon, double tolerance)
        {
            if (polygon?.Vertices == default || polygon.Vertices.Count <= 3)
                return polygon;

            // 使用Douglas-Peucker算法的简化版本
            var simplified = DouglasPeuckerSimplify(polygon.Vertices.ToList(), tolerance);
            return new Polygon2D(simplified, polygon.IsClosed);
        }

        public Polygon2D Smooth(Polygon2D polygon, int iterations = 1)
        {
            if (polygon?.Vertices == default || polygon.Vertices.Count < 3 || iterations <= 0)
                return polygon;

            var smoothed = new List<Point2D>(polygon.Vertices);

            for (int iter = 0; iter < iterations; iter++)
            {
                var newPoints = new List<Point2D>();
                
                for (int i = 0; i < smoothed.Count; i++)
                {
                    int prev = (i - 1 + smoothed.Count) % smoothed.Count;
                    int next = (i + 1) % smoothed.Count;
                    
                    double newX = (smoothed[prev].X + 2 * smoothed[i].X + smoothed[next].X) / 4.0;
                    double newY = (smoothed[prev].Y + 2 * smoothed[i].Y + smoothed[next].Y) / 4.0;
                    
                    newPoints.Add(new Point2D(newX, newY));
                }
                
                smoothed = newPoints;
            }

            return new Polygon2D(smoothed, polygon.IsClosed);
        }

        // === 多边形分析 ===

        public bool IsConvex(Polygon2D polygon, Tolerance tolerance)
        {
            if (polygon?.Vertices == default || polygon.Vertices.Count < 3)
                return false;

            var points = polygon.Vertices;
            bool? isPositive = null;

            for (int i = 0; i < points.Count; i++)
            {
                int prev = (i - 1 + points.Count) % points.Count;
                int next = (i + 1) % points.Count;

                double crossProduct = CrossProduct(
                    points[i].X - points[prev].X, points[i].Y - points[prev].Y,
                    points[next].X - points[i].X, points[next].Y - points[i].Y);

                if (Math.Abs(crossProduct) > tolerance.Value)
                {
                    bool currentPositive = crossProduct > 0;
                    if (isPositive == default)
                        isPositive = currentPositive;
                    else if (isPositive != currentPositive)
                        return false;
                }
            }

            return true;
        }

        public bool IsSimple(Polygon2D polygon, Tolerance tolerance)
        {
            if (polygon?.Vertices == default || polygon.Vertices.Count < 3)
                return true;

            var edges = GetEdges(polygon);
            
            // 检查是否有边相交（除了相邻边的端点）
            for (int i = 0; i < edges.Count; i++)
            {
                for (int j = i + 2; j < edges.Count; j++)
                {
                    // 跳过相邻边
                    if (i == 0 && j == edges.Count - 1)
                        continue;

                    if (_lineAlgorithms.FindIntersection(edges[i], edges[j], tolerance, out _))
                        return false;
                }
            }

            return true;
        }

        public List<Line2D> GetEdges(Polygon2D polygon)
        {
            var edges = new List<Line2D>();
            
            if (polygon?.Vertices == default || polygon.Vertices.Count < 2)
                return edges;

            var points = polygon.Vertices;
            for (int i = 0; i < points.Count; i++)
            {
                int next = (i + 1) % points.Count;
                if (i == points.Count - 1 && !polygon.IsClosed)
                    break;
                    
                edges.Add(new Line2D(points[i], points[next]));
            }

            return edges;
        }

        public List<Point2D> GetIntersectionPoints(Polygon2D polygon, Line2D line, Tolerance tolerance)
        {
            var intersections = new List<Point2D>();
            
            if (polygon?.Vertices == default || line == default)
                return intersections;

            var edges = GetEdges(polygon);
            foreach (var edge in edges)
            {
                if (_lineAlgorithms.FindIntersection(edge, line, tolerance, out Point2D? intersection) && intersection.HasValue)
                {
                    intersections.Add(intersection.Value);
                }
            }

            return intersections;
        }

        // === 私有辅助方法 ===

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

        private bool DoesRayIntersectSegment(Point2D rayStart, Point2D segmentStart, Point2D segmentEnd)
        {
            // 检查射线（从点向右的水平射线）是否与线段相交
            double minY = Math.Min(segmentStart.Y, segmentEnd.Y);
            double maxY = Math.Max(segmentStart.Y, segmentEnd.Y);

            // 射线的Y坐标不在线段的Y范围内
            if (rayStart.Y < minY || rayStart.Y > maxY)
                return false;

            // 线段是水平的
            if (Math.Abs(segmentStart.Y - segmentEnd.Y) < 1e-10)
                return false;

            // 计算射线与线段的交点的X坐标
            double intersectionX = segmentStart.X + 
                (rayStart.Y - segmentStart.Y) * (segmentEnd.X - segmentStart.X) / (segmentEnd.Y - segmentStart.Y);

            return intersectionX > rayStart.X;
        }

        private double CrossProduct(double ax, double ay, double bx, double by)
        {
            return ax * by - ay * bx;
        }

        private List<Point2D> DouglasPeuckerSimplify(List<Point2D> points, double tolerance)
        {
            if (points.Count <= 2)
                return new List<Point2D>(points);

            // 找到距离首末连线最远的点
            double maxDistance = 0;
            int maxIndex = 0;
            var startPoint = points[0];
            var endPoint = points[points.Count - 1];
            var line = new Line2D(startPoint, endPoint);

            for (int i = 1; i < points.Count - 1; i++)
            {
                double distance = _lineAlgorithms.CalculateDistanceToPoint(points[i], line);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    maxIndex = i;
                }
            }

            if (maxDistance > tolerance)
            {
                // 递归简化两个子段
                var leftPoints = points.Take(maxIndex + 1).ToList();
                var rightPoints = points.Skip(maxIndex).ToList();

                var leftSimplified = DouglasPeuckerSimplify(leftPoints, tolerance);
                var rightSimplified = DouglasPeuckerSimplify(rightPoints, tolerance);

                // 合并结果，去除重复的中间点
                var result = new List<Point2D>(leftSimplified);
                result.AddRange(rightSimplified.Skip(1));
                return result;
            }
            else
            {
                // 距离不够大，只保留首末点
                return new List<Point2D> { startPoint, endPoint };
            }
        }
    }
}
