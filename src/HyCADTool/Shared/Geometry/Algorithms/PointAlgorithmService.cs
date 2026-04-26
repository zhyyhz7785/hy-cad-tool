using HyCADTool.Shared.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Shared.Geometry.Algorithms
{
    /// <summary>
    /// 点算法服务实现（平台无关）
    /// 提供点相关的几何算法实现
    /// </summary>
    public class PointAlgorithmService : IPointAlgorithmService
    {
        // === 距离计算 ===

        public double CalculateDistance(Point2D point1, Point2D point2)
        {
            if (point1 == default || point2 == default)
                return double.MaxValue;

            double dx = point2.X - point1.X;
            double dy = point2.Y - point1.Y;
            return System.Math.Sqrt(dx * dx + dy * dy);
        }

        public double CalculateDistanceFromOrigin(Point2D point)
        {
            if (point == default)
                return double.MaxValue;

            return System.Math.Sqrt(point.X * point.X + point.Y * point.Y);
        }

        public double FindMinimumDistance(List<Point2D> points)
        {
            if (points == default || points.Count < 2)
                return double.MaxValue;

            double minDistance = double.MaxValue;
            for (int i = 0; i < points.Count - 1; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    double distance = CalculateDistance(points[i], points[j]);
                    if (distance < minDistance)
                        minDistance = distance;
                }
            }

            return minDistance;
        }

        public double FindMaximumDistance(List<Point2D> points)
        {
            if (points == default || points.Count < 2)
                return 0;

            double maxDistance = 0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    double distance = CalculateDistance(points[i], points[j]);
                    if (distance > maxDistance)
                        maxDistance = distance;
                }
            }

            return maxDistance;
        }

        // === 点的搜索与排序 ===

        public Point2D? FindClosestPoint(Point2D targetPoint, List<Point2D> points)
        {
            if (targetPoint == default || points == default || points.Count == 0)
                return null;

            Point2D closest = points[0];
            double minDistance = CalculateDistance(targetPoint, closest);

            foreach (var point in points.Skip(1))
            {
                double distance = CalculateDistance(targetPoint, point);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = point;
                }
            }

            return closest;
        }

        public Point2D? FindFarthestPoint(Point2D targetPoint, List<Point2D> points)
        {
            if (targetPoint == default || points == default || points.Count == 0)
                return null;

            Point2D farthest = points[0];
            double maxDistance = CalculateDistance(targetPoint, farthest);

            foreach (var point in points.Skip(1))
            {
                double distance = CalculateDistance(targetPoint, point);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    farthest = point;
                }
            }

            return farthest;
        }

        public List<Point2D> SortByDistance(Point2D targetPoint, List<Point2D> points, bool ascending = true)
        {
            if (points == default)
                return new List<Point2D>();

            var sortedPoints = points.Select(p => new { Point = p, Distance = CalculateDistance(targetPoint, p) });

            if (ascending)
                sortedPoints = sortedPoints.OrderBy(p => p.Distance);
            else
                sortedPoints = sortedPoints.OrderByDescending(p => p.Distance);

            return sortedPoints.Select(p => p.Point).ToList();
        }

        public List<Point2D> FindPointsInRadius(Point2D centerPoint, double radius, List<Point2D> points)
        {
            if (points == default || radius < 0)
                return new List<Point2D>();

            return points.Where(p => CalculateDistance(centerPoint, p) <= radius).ToList();
        }

        // === 点的空间关系判断 ===

        public bool IsPointsEqual(Point2D point1, Point2D point2, Tolerance tolerance)
        {
            return System.Math.Abs(point1.X - point2.X) <= tolerance.Value &&
                   System.Math.Abs(point1.Y - point2.Y) <= tolerance.Value;
        }

        public bool IsPointInRectangle(Point2D point, Point2D minPoint, Point2D maxPoint)
        {
            return point.X >= minPoint.X && point.X <= maxPoint.X &&
                   point.Y >= minPoint.Y && point.Y <= maxPoint.Y;
        }

        public bool IsPointInCircle(Point2D point, Point2D center, double radius)
        {
            if (radius < 0)
                return false;

            return CalculateDistance(point, center) <= radius;
        }

        public bool AreCollinear(Point2D point1, Point2D point2, Point2D point3, Tolerance tolerance)
        {
            double crossProduct = CalculateCrossProduct(point1, point2, point3);
            return System.Math.Abs(crossProduct) <= tolerance.Value;
        }

        // === 角度和方向计算 ===

        public double CalculateAngle(Point2D fromPoint, Point2D toPoint)
        {
            if (fromPoint == default || toPoint == default)
                return 0;

            double dx = toPoint.X - fromPoint.X;
            double dy = toPoint.Y - fromPoint.Y;
            return System.Math.Atan2(dy, dx);
        }

        public double CalculateAngleAt(Point2D point1, Point2D vertex, Point2D point2)
        {
            if (point1 == default || vertex == default || point2 == default)
                return 0;

            double angle1 = CalculateAngle(vertex, point1);
            double angle2 = CalculateAngle(vertex, point2);
            double angle = angle2 - angle1;

            // 规范化角度到 [0, 2π]
            while (angle < 0) angle += 2 * System.Math.PI;
            while (angle > 2 * System.Math.PI) angle -= 2 * System.Math.PI;

            return angle;
        }

        public double CalculateCrossProduct(Point2D point1, Point2D point2, Point2D point3)
        {
            if (point1 == default || point2 == default || point3 == default)
                return 0;

            return (point2.X - point1.X) * (point3.Y - point1.Y) - (point2.Y - point1.Y) * (point3.X - point1.X);
        }

        // === 点的变换 ===

        public Point2D? Translate(Point2D point, double deltaX, double deltaY)
        {
            if (point == default)
                return null;

            return new Point2D(point.X + deltaX, point.Y + deltaY);
        }

        public Point2D? Rotate(Point2D point, Point2D center, double angle)
        {
            if (point == default || center == default)
                return point;

            double cos = System.Math.Cos(angle);
            double sin = System.Math.Sin(angle);

            double dx = point.X - center.X;
            double dy = point.Y - center.Y;

            double newX = center.X + dx * cos - dy * sin;
            double newY = center.Y + dx * sin + dy * cos;

            return new Point2D(newX, newY);
        }

        public Point2D? Scale(Point2D point, Point2D center, double scaleX, double scaleY)
        {
            if (point == default || center == default)
                return point;

            double dx = point.X - center.X;
            double dy = point.Y - center.Y;

            double newX = center.X + dx * scaleX;
            double newY = center.Y + dy * scaleY;

            return new Point2D(newX, newY);
        }

        public Point2D? ProjectToLine(Point2D point, Line2D line)
        {
            if (point == default || line == default)
                return null;

            double dx = line.EndPoint.X - line.StartPoint.X;
            double dy = line.EndPoint.Y - line.StartPoint.Y;

            if (System.Math.Abs(dx) < 1e-10 && System.Math.Abs(dy) < 1e-10)
                return line.StartPoint; // 线段长度为0

            double t = ((point.X - line.StartPoint.X) * dx + (point.Y - line.StartPoint.Y) * dy) / (dx * dx + dy * dy);

            double projX = line.StartPoint.X + t * dx;
            double projY = line.StartPoint.Y + t * dy;

            return new Point2D(projX, projY);
        }

        // === 点集合处理 ===

        public List<Point2D> RemoveDuplicates(List<Point2D> points, Tolerance tolerance)
        {
            if (points == default || points.Count == 0)
                return new List<Point2D>();

            var uniquePoints = new List<Point2D>();
            
            foreach (var point in points)
            {
                bool isDuplicate = false;
                foreach (var uniquePoint in uniquePoints)
                {
                    if (IsPointsEqual(point, uniquePoint, tolerance))
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                    uniquePoints.Add(point);
            }

            return uniquePoints;
        }

        public Point2D? CalculateCentroid(List<Point2D> points)
        {
            if (points == default || points.Count == 0)
                return null;

            double sumX = points.Sum(p => p.X);
            double sumY = points.Sum(p => p.Y);

            return new Point2D(sumX / points.Count, sumY / points.Count);
        }

        public (Point2D? MinPoint, Point2D? MaxPoint) CalculateBoundingBox(List<Point2D> points)
        {
            if (points == default || points.Count == 0)
                return (null, null);

            double minX = points.Min(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxX = points.Max(p => p.X);
            double maxY = points.Max(p => p.Y);

            return (new Point2D(minX, minY), new Point2D(maxX, maxY));
        }

        public List<Point2D> CalculateConvexHull(List<Point2D> points)
        {
            if (points == default || points.Count < 3)
                return new List<Point2D>(points ?? new List<Point2D>());

            // Graham扫描算法
            var sortedPoints = points.OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
            var start = sortedPoints[0];

            // 按极角排序
            var polarSorted = sortedPoints.Skip(1)
                .OrderBy(p => CalculateAngle(start, p))
                .ThenBy(p => CalculateDistance(start, p))
                .ToList();

            var hull = new List<Point2D> { start };
            
            foreach (var point in polarSorted)
            {
                // 移除不在凸包上的点
                while (hull.Count > 1 && 
                       CalculateCrossProduct(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0)
                {
                    hull.RemoveAt(hull.Count - 1);
                }
                
                hull.Add(point);
            }

            return hull;
        }

        // === 点的比较器创建 ===

        public IEqualityComparer<Point2D> CreateComparer(Tolerance tolerance)
        {
            return new Point2DComparer(tolerance);
        }

        public IComparer<Point2D> CreateDistanceComparer(Point2D referencePoint)
        {
            return new Point2DDistanceComparer(referencePoint, this);
        }

        public IComparer<Point2D> CreatePolarAngleComparer(Point2D referencePoint)
        {
            return new Point2DPolarComparer(referencePoint, this);
        }

        // === 特殊点计算 ===

        public Point2D? CalculateMidpoint(Point2D point1, Point2D point2)
        {
            if (point1 == default || point2 == default)
                return null;

            return new Point2D((point1.X + point2.X) / 2, (point1.Y + point2.Y) / 2);
        }

        public Point2D? Interpolate(Point2D point1, Point2D point2, double ratio)
        {
            if (point1 == default || point2 == default)
                return null;

            ratio = System.Math.Max(0, System.Math.Min(1, ratio)); // 限制在[0,1]范围内

            double x = point1.X + ratio * (point2.X - point1.X);
            double y = point1.Y + ratio * (point2.Y - point1.Y);

            return new Point2D(x, y);
        }

        public Point2D? CalculateCircumcenter(Point2D point1, Point2D point2, Point2D point3)
        {
            if (point1 == default || point2 == default || point3 == default)
                return null;

            // 检查三点是否共线
            if (AreCollinear(point1, point2, point3, new Tolerance(1e-10)))
                return null;

            double ax = point1.X, ay = point1.Y;
            double bx = point2.X, by = point2.Y;
            double cx = point3.X, cy = point3.Y;

            double d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            
            if (System.Math.Abs(d) < 1e-10)
                return null; // 三点共线

            double ux = ((ax * ax + ay * ay) * (by - cy) + 
                         (bx * bx + by * by) * (cy - ay) + 
                         (cx * cx + cy * cy) * (ay - by)) / d;

            double uy = ((ax * ax + ay * ay) * (cx - bx) + 
                         (bx * bx + by * by) * (ax - cx) + 
                         (cx * cx + cy * cy) * (bx - ax)) / d;

            return new Point2D(ux, uy);
        }

        public Point2D? CalculateIncenter(Point2D point1, Point2D point2, Point2D point3)
        {
            if (point1 == default || point2 == default || point3 == default)
                return null;

            double a = CalculateDistance(point2, point3);
            double b = CalculateDistance(point1, point3);
            double c = CalculateDistance(point1, point2);

            double perimeter = a + b + c;
            if (perimeter < 1e-10)
                return null;

            double x = (a * point1.X + b * point2.X + c * point3.X) / perimeter;
            double y = (a * point1.Y + b * point2.Y + c * point3.Y) / perimeter;

            return new Point2D(x, y);
        }
    }

    // === 比较器实现 ===

    /// <summary>
    /// 基于容差的点相等比较器
    /// </summary>
    public class Point2DComparer : IEqualityComparer<Point2D>
    {
        private readonly Tolerance _tolerance;

        public Point2DComparer(Tolerance tolerance)
        {
            _tolerance = tolerance ?? throw new ArgumentNullException(nameof(tolerance));
        }

        public bool Equals(Point2D x, Point2D y)
        {
            if (x == default && y == default) return true;
            if (x == default || y == default) return false;

            return System.Math.Abs(x.X - y.X) <= _tolerance.Value &&
                   System.Math.Abs(x.Y - y.Y) <= _tolerance.Value;
        }

        public int GetHashCode(Point2D obj)
        {
            if (obj == default) return 0;

            // 基于容差的哈希码生成
            int xHash = ((int)(obj.X / _tolerance.Value)).GetHashCode();
            int yHash = ((int)(obj.Y / _tolerance.Value)).GetHashCode();
            return xHash ^ (yHash << 1);
        }
    }

    /// <summary>
    /// 基于距离的点比较器
    /// </summary>
    public class Point2DDistanceComparer : IComparer<Point2D>
    {
        private readonly Point2D _referencePoint;
        private readonly IPointAlgorithmService _pointService;

        public Point2DDistanceComparer(Point2D referencePoint, IPointAlgorithmService pointService)
        {
            _referencePoint = referencePoint;
            _pointService = pointService ?? throw new ArgumentNullException(nameof(pointService));
        }

        public int Compare(Point2D x, Point2D y)
        {
            if (x == default && y == default) return 0;
            if (x == default) return -1;
            if (y == default) return 1;

            double distanceX = _pointService.CalculateDistance(_referencePoint, x);
            double distanceY = _pointService.CalculateDistance(_referencePoint, y);

            return distanceX.CompareTo(distanceY);
        }
    }

    /// <summary>
    /// 基于极角的点比较器
    /// </summary>
    public class Point2DPolarComparer : IComparer<Point2D>
    {
        private readonly Point2D _referencePoint;
        private readonly IPointAlgorithmService _pointService;

        public Point2DPolarComparer(Point2D referencePoint, IPointAlgorithmService pointService)
        {
            _referencePoint = referencePoint;
            _pointService = pointService ?? throw new ArgumentNullException(nameof(pointService));
        }

        public int Compare(Point2D x, Point2D y)
        {
            if (x == default && y == default) return 0;
            if (x == default) return -1;
            if (y == default) return 1;

            double angleX = _pointService.CalculateAngle(_referencePoint, x);
            double angleY = _pointService.CalculateAngle(_referencePoint, y);

            int angleComparison = angleX.CompareTo(angleY);
            if (angleComparison != 0)
                return angleComparison;

            // 如果角度相同，按距离排序
            double distanceX = _pointService.CalculateDistance(_referencePoint, x);
            double distanceY = _pointService.CalculateDistance(_referencePoint, y);

            return distanceX.CompareTo(distanceY);
        }
    }
}
