using HyCADTool.Shared.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Shared.Geometry.Algorithms
{
    /// <summary>
    /// 多边形算法服务（平台无关）
    /// 提供多边形相关的几何算法
    /// </summary>
    public static class PolygonAlgorithms
    {
        /// <summary>
        /// 确保多边形顶点逆时针排列
        /// 如果是顺时针则反转
        /// </summary>
        public static Polygon2D EnsureCounterClockwise(Polygon2D polygon)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));

            // 如果已经是逆时针，直接返回
            if (polygon.IsCounterClockwise())
                return polygon;

            // 反转顶点顺序
            return polygon.Reverse();
        }

        /// <summary>
        /// 确保多边形顶点顺时针排列
        /// 如果是逆时针则反转
        /// </summary>
        public static Polygon2D EnsureClockwise(Polygon2D polygon)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));

            // 如果已经是顺时针，直接返回
            if (!polygon.IsCounterClockwise())
                return polygon;

            // 反转顶点顺序
            return polygon.Reverse();
        }

        /// <summary>
        /// 去除多边形中重复的顶点
        /// </summary>
        public static Polygon2D RemoveDuplicateVertices(Polygon2D polygon, double tolerance = 1e-6)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));

            var uniqueVertices = new List<Point2D>();
            var vertices = polygon.Vertices;

            for (int i = 0; i < vertices.Count; i++)
            {
                Point2D current = vertices[i];
                Point2D next = vertices[(i + 1) % vertices.Count];

                // 如果当前点与下一个点不重复，添加当前点
                if (current.DistanceTo(next) > tolerance)
                {
                    uniqueVertices.Add(current);
                }
            }

            // 确保至少有3个顶点
            if (uniqueVertices.Count < 3)
                return polygon;

            return new Polygon2D(uniqueVertices, polygon.IsClosed);
        }

        /// <summary>
        /// 用线段分割多边形
        /// 返回分割后的多个多边形
        /// </summary>
        public static IEnumerable<Polygon2D> SplitByLine(Polygon2D polygon, Line2D line)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));

            // 找出所有与线段相交的边
            var intersections = new List<(int edgeIndex, Point2D point)>();

            var edges = polygon.GetEdges().ToList();
            for (int i = 0; i < edges.Count; i++)
            {
                var intersection = edges[i].GetIntersection(line);
                if (intersection != default)
                {
                    intersections.Add((i, intersection));
                }
            }

            // 如果没有交点或只有一个交点，无法分割
            if (intersections.Count < 2)
            {
                yield return polygon;
                yield break;
            }

            // 简化实现：只处理两个交点的情况
            if (intersections.Count == 2)
            {
                var vertices = polygon.Vertices.ToList();
                int idx1 = intersections[0].edgeIndex;
                int idx2 = intersections[1].edgeIndex;
                Point2D pt1 = intersections[0].point;
                Point2D pt2 = intersections[1].point;

                // 构建第一个多边形
                var polygon1Vertices = new List<Point2D>();
                for (int i = 0; i <= idx1; i++)
                {
                    polygon1Vertices.Add(vertices[i]);
                }
                polygon1Vertices.Add(pt1);
                polygon1Vertices.Add(pt2);
                for (int i = idx2 + 1; i < vertices.Count; i++)
                {
                    polygon1Vertices.Add(vertices[i]);
                }

                // 构建第二个多边形
                var polygon2Vertices = new List<Point2D> { pt1 };
                for (int i = idx1 + 1; i <= idx2; i++)
                {
                    polygon2Vertices.Add(vertices[i]);
                }
                polygon2Vertices.Add(pt2);

                if (polygon1Vertices.Count >= 3)
                    yield return new Polygon2D(polygon1Vertices);
                if (polygon2Vertices.Count >= 3)
                    yield return new Polygon2D(polygon2Vertices);
            }
            else
            {
                // 多个交点情况：返回原多边形
                yield return polygon;
            }
        }

        /// <summary>
        /// 计算多边形的最小边界矩形（轴对齐）
        /// </summary>
        public static Polygon2D GetAxisAlignedBoundingRectangle(Polygon2D polygon)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));

            var bbox = polygon.GetBoundingBox();

            var vertices = new[]
            {
                bbox.MinPoint,
                new Point2D(bbox.MaxPoint.X, bbox.MinPoint.Y),
                bbox.MaxPoint,
                new Point2D(bbox.MinPoint.X, bbox.MaxPoint.Y)
            };

            return new Polygon2D(vertices);
        }

        /// <summary>
        /// 判断多边形是否自交
        /// </summary>
        public static bool HasSelfIntersection(Polygon2D polygon, double tolerance = 1e-6)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));

            var edges = polygon.GetEdges().ToList();

            // 检查每对不相邻的边是否相交
            for (int i = 0; i < edges.Count; i++)
            {
                for (int j = i + 2; j < edges.Count; j++)
                {
                    // 跳过首尾边的检查（它们可能共享顶点）
                    if (i == 0 && j == edges.Count - 1)
                        continue;

                    if (edges[i].GetIntersection(edges[j], tolerance) != default)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 计算多边形面积（有向面积）
        /// Calculate polygon area (signed area)
        /// </summary>
        /// <param name="vertices">顶点列表 Vertex list</param>
        /// <returns>面积 Area</returns>
        public static double CalculateArea(IEnumerable<Point2D> vertices)
        {
            var points = vertices.ToList();
            if (points.Count < 3)
                return 0;
            
            double area = 0;
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                area += points[i].X * points[j].Y;
                area -= points[j].X * points[i].Y;
            }
            
            return System.Math.Abs(area / 2.0);
        }

        /// <summary>
        /// 计算多边形周长
        /// Calculate polygon perimeter
        /// </summary>
        /// <param name="vertices">顶点列表 Vertex list</param>
        /// <returns>周长 Perimeter</returns>
        public static double CalculatePerimeter(IEnumerable<Point2D> vertices)
        {
            var points = vertices.ToList();
            if (points.Count < 2)
                return 0;
            
            double perimeter = 0;
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                // TODO: Replace with IPointAlgorithmService
                var dx = points[j].X - points[i].X;
                var dy = points[j].Y - points[i].Y;
                perimeter += System.Math.Sqrt(dx * dx + dy * dy);
            }
            
            return perimeter;
        }

        /// <summary>
        /// 判断点是否在多边形内（射线法）
        /// Check if point is inside polygon (ray casting method)
        /// </summary>
        /// <param name="vertices">顶点列表 Vertex list</param>
        /// <param name="point">点 Point</param>
        /// <returns>是否在内部 True if inside</returns>
        public static bool ContainsPoint(IEnumerable<Point2D> vertices, Point2D point)
        {
            var points = vertices.ToList();
            if (points.Count < 3)
                return false;
            
            int intersections = 0;
            
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                var p1 = points[i];
                var p2 = points[j];
                
                if (RayIntersectsSegment(point, p1, p2))
                {
                    intersections++;
                }
            }
            
            return (intersections % 2) == 1;
        }

        /// <summary>
        /// 射线与线段相交判断（内部使用）
        /// Ray-segment intersection (internal use)
        /// </summary>
        private static bool RayIntersectsSegment(Point2D point, Point2D p1, Point2D p2)
        {
            if (p1.Y == p2.Y)
                return false;
            
            if (point.Y < System.Math.Min(p1.Y, p2.Y) || point.Y >= System.Math.Max(p1.Y, p2.Y))
                return false;
            
            double xIntersect = p1.X + (point.Y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y);
            
            return xIntersect >= point.X;
        }

        /// <summary>
        /// 判断多边形是否为凸多边形
        /// Check if polygon is convex
        /// </summary>
        /// <param name="vertices">顶点列表 Vertex list</param>
        /// <returns>是否为凸多边形 True if convex</returns>
        public static bool IsConvex(IEnumerable<Point2D> vertices)
        {
            var points = vertices.ToList();
            if (points.Count < 4)
                return true;
            
            bool? isPositive = null;
            
            for (int i = 0; i < points.Count; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Count];
                var p3 = points[(i + 2) % points.Count];
                
                double cross = (p2.X - p1.X) * (p3.Y - p2.Y) - (p2.Y - p1.Y) * (p3.X - p2.X);
                
                if (System.Math.Abs(cross) < 1e-10)
                    continue;
                
                if (isPositive == null)
                {
                    isPositive = cross > 0;
                }
                else if ((cross > 0) != isPositive)
                {
                    return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// 计算多边形的质心（形心）
        /// Calculate polygon centroid
        /// </summary>
        /// <param name="vertices">顶点列表 Vertex list</param>
        /// <returns>质心 Centroid</returns>
        public static Point2D CalculateCentroid(IEnumerable<Point2D> vertices)
        {
            var points = vertices.ToList();
            if (points.Count == 0)
                throw new ArgumentException("Vertices cannot be empty");
            
            if (points.Count < 3)
            {
                // 对于点或线段，返回平均位置
                double sumX = 0, sumY = 0;
                foreach (var p in points)
                {
                    sumX += p.X;
                    sumY += p.Y;
                }
                return new Point2D(sumX / points.Count, sumY / points.Count);
            }
            
            // 对于多边形，使用加权质心公式
            double cx = 0, cy = 0;
            double signedArea = 0;
            
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                double factor = points[i].X * points[j].Y - points[j].X * points[i].Y;
                signedArea += factor;
                cx += (points[i].X + points[j].X) * factor;
                cy += (points[i].Y + points[j].Y) * factor;
            }
            
            signedArea *= 0.5;
            
            if (System.Math.Abs(signedArea) < 1e-10)
            {
                // 退化情况，使用简单平均
                double sumX = 0, sumY = 0;
                foreach (var p in points)
                {
                    sumX += p.X;
                    sumY += p.Y;
                }
                return new Point2D(sumX / points.Count, sumY / points.Count);
            }
            
            cx /= (6.0 * signedArea);
            cy /= (6.0 * signedArea);
            
            return new Point2D(cx, cy);
        }

        /// <summary>
        /// 简化多边形（Douglas-Peucker 算法）
        /// Simplify polygon (Douglas-Peucker algorithm)
        /// </summary>
        /// <param name="vertices">顶点列表 Vertex list</param>
        /// <param name="tolerance">容差 Tolerance</param>
        /// <returns>简化后的顶点列表 Simplified vertex list</returns>
        public static List<Point2D> SimplifyPolygon(IEnumerable<Point2D> vertices, double tolerance)
        {
            var points = vertices.ToList();
            if (points.Count < 3)
                return points;
            
            return DouglasPeucker(points, 0, points.Count - 1, tolerance);
        }

        /// <summary>
        /// Douglas-Peucker 算法实现（递归）
        /// Douglas-Peucker algorithm implementation (recursive)
        /// </summary>
        private static List<Point2D> DouglasPeucker(List<Point2D> points, int startIndex, int endIndex, double tolerance)
        {
            if (endIndex <= startIndex + 1)
            {
                return new List<Point2D> { points[startIndex], points[endIndex] };
            }
            
            // 找到距离起点和终点连线最远的点
            var line = new Line2D(points[startIndex], points[endIndex]);
            double maxDistance = 0;
            int maxIndex = startIndex;
            
            for (int i = startIndex + 1; i < endIndex; i++)
            {
                // TODO: Replace with IPointAlgorithmService.DistanceToLine
                // 计算点到直线的距离（垂直距离）
                var p = points[i];
                var p1 = line.StartPoint;
                var p2 = line.EndPoint;
                var numerator = System.Math.Abs((p2.Y - p1.Y) * p.X - (p2.X - p1.X) * p.Y + p2.X * p1.Y - p2.Y * p1.X);
                var denominator = System.Math.Sqrt(System.Math.Pow(p2.Y - p1.Y, 2) + System.Math.Pow(p2.X - p1.X, 2));
                double distance = numerator / denominator;
                
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    maxIndex = i;
                }
            }
            
            // 如果最大距离小于容差，直接返回起点和终点
            if (maxDistance < tolerance)
            {
                return new List<Point2D> { points[startIndex], points[endIndex] };
            }
            
            // 递归处理两段
            var leftPart = DouglasPeucker(points, startIndex, maxIndex, tolerance);
            var rightPart = DouglasPeucker(points, maxIndex, endIndex, tolerance);
            
            // 合并结果（去除重复的中间点）
            var result = new List<Point2D>(leftPart);
            result.AddRange(rightPart.Skip(1));
            
            return result;
        }

        /// <summary>
        /// 偏移多边形（向外或向内）
        /// Offset polygon (outward or inward)
        /// 注意：这是简化实现，复杂情况建议使用 Clipper2
        /// Note: This is a simplified implementation, use Clipper2 for complex cases
        /// </summary>
        /// <param name="vertices">顶点列表 Vertex list</param>
        /// <param name="distance">偏移距离（正值向外，负值向内）Offset distance (positive outward, negative inward)</param>
        /// <returns>偏移后的顶点列表 Offset vertex list</returns>
        public static List<Point2D> OffsetPolygon(IEnumerable<Point2D> vertices, double distance)
        {
            var points = vertices.ToList();
            if (points.Count < 3)
                return points;
            
            var offsetPoints = new List<Point2D>();
            
            for (int i = 0; i < points.Count; i++)
            {
                int prev = (i - 1 + points.Count) % points.Count;
                int next = (i + 1) % points.Count;
                
                // 计算两条边的单位法向量
                var edge1 = new Line2D(points[prev], points[i]);
                var edge2 = new Line2D(points[i], points[next]);
                
                var normal1 = GetLeftNormal(edge1);
                var normal2 = GetLeftNormal(edge2);
                
                // 平均法向量
                var avgNormal = new Vector2D(
                    (normal1.X + normal2.X) / 2,
                    (normal2.Y + normal2.Y) / 2
                );
                
                double length = System.Math.Sqrt(avgNormal.X * avgNormal.X + avgNormal.Y * avgNormal.Y);
                if (length > 1e-10)
                {
                    avgNormal = new Vector2D(avgNormal.X / length, avgNormal.Y / length);
                }
                
                // 偏移点
                var offsetPoint = new Point2D(
                    points[i].X + avgNormal.X * distance,
                    points[i].Y + avgNormal.Y * distance
                );
                
                offsetPoints.Add(offsetPoint);
            }
            
            return offsetPoints;
        }

        /// <summary>
        /// 获取线段的左侧单位法向量
        /// Get left unit normal vector of line segment
        /// </summary>
        private static Vector2D GetLeftNormal(Line2D line)
        {
            var dir = line.Direction;
            double length = System.Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
            
            if (length < 1e-10)
                return new Vector2D(0, 0);
            
            // 左侧法向量 = (-dy/length, dx/length)
            return new Vector2D(-dir.Y / length, dir.X / length);
        }

        /// <summary>
        /// 判断多边形是否顺时针
        /// Check if polygon is clockwise
        /// </summary>
        /// <param name="vertices">顶点列表 Vertex list</param>
        /// <returns>是否顺时针 True if clockwise</returns>
        public static bool IsClockwise(IEnumerable<Point2D> vertices)
        {
            var points = vertices.ToList();
            if (points.Count < 3)
                return false;
            
            double signedArea = 0;
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                signedArea += (points[j].X - points[i].X) * (points[j].Y + points[i].Y);
            }
            
            return signedArea > 0;
        }

        /// <summary>
        /// 反转多边形顶点顺序
        /// Reverse polygon vertex order
        /// </summary>
        /// <param name="vertices">顶点列表 Vertex list</param>
        /// <returns>反转后的顶点列表 Reversed vertex list</returns>
        public static List<Point2D> ReverseVertices(IEnumerable<Point2D> vertices)
        {
            var points = vertices.ToList();
            points.Reverse();
            return points;
        }
    }
}

