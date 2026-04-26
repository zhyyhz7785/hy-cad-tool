using System;

namespace HyCADTool.Shared.Geometry
{
    /// <summary>
    /// 二维线段值对象（平台无关）
    /// </summary>
    public readonly struct Line2D : IEquatable<Line2D>
    {
        /// <summary>
        /// 起点
        /// </summary>
        public Point2D StartPoint { get; }

        /// <summary>
        /// 终点
        /// </summary>
        public Point2D EndPoint { get; }

        public Line2D(Point2D start, Point2D end)
        {
            StartPoint = start;
            EndPoint = end;
        }

        /// <summary>
        /// 线段长度
        /// </summary>
        public double Length => StartPoint.DistanceTo(EndPoint);

        /// <summary>
        /// 线段方向向量
        /// </summary>
        public Vector2D Direction => StartPoint.VectorTo(EndPoint);

        /// <summary>
        /// 线段中点
        /// </summary>
        public Point2D MidPoint => new Point2D(
            (StartPoint.X + EndPoint.X) / 2.0,
            (StartPoint.Y + EndPoint.Y) / 2.0
        );

        /// <summary>
        /// 获取线段上指定参数位置的点（t = 0 为起点，t = 1 为终点）
        /// </summary>
        public Point2D GetPointAtParameter(double t)
        {
            return new Point2D(
                StartPoint.X + t * (EndPoint.X - StartPoint.X),
                StartPoint.Y + t * (EndPoint.Y - StartPoint.Y)
            );
        }

        /// <summary>
        /// 判断点是否在线段上（含容差）
        /// </summary>
        public bool IsPointOnSegment(Point2D point, double tolerance = 1e-6)
        {
            // 检查点到线段的距离
            double dist = DistanceToPoint(point);
            if (dist > tolerance)
                return false;

            // 检查点是否在线段范围内
            double minX = Math.Min(StartPoint.X, EndPoint.X) - tolerance;
            double maxX = Math.Max(StartPoint.X, EndPoint.X) + tolerance;
            double minY = Math.Min(StartPoint.Y, EndPoint.Y) - tolerance;
            double maxY = Math.Max(StartPoint.Y, EndPoint.Y) + tolerance;

            return point.X >= minX && point.X <= maxX && 
                   point.Y >= minY && point.Y <= maxY;
        }

        /// <summary>
        /// 计算点到线段的最短距离
        /// </summary>
        public double DistanceToPoint(Point2D point)
        {
            Vector2D v = StartPoint.VectorTo(EndPoint);
            Vector2D w = StartPoint.VectorTo(point);

            double c1 = w.Dot(v);
            if (c1 <= 0)
                return point.DistanceTo(StartPoint);

            double c2 = v.Dot(v);
            if (c1 >= c2)
                return point.DistanceTo(EndPoint);

            double b = c1 / c2;
            Point2D pb = StartPoint.Add(v * b);
            return point.DistanceTo(pb);
        }

        #region IEquatable Implementation

        public bool Equals(Line2D other)
        {
            return StartPoint.Equals(other.StartPoint) && 
                   EndPoint.Equals(other.EndPoint);
        }

        public override bool Equals(object obj)
        {
            return obj is Line2D other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StartPoint.GetHashCode() * 397) ^ EndPoint.GetHashCode();
            }
        }

        public static bool operator ==(Line2D left, Line2D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Line2D left, Line2D right)
        {
            return !left.Equals(right);
        }

        #endregion

        /// <summary>
        /// 获取线段长度
        /// </summary>
        public double GetLength()
        {
            return Length;
        }

        /// <summary>
        /// 判断两条线段是否平行
        /// </summary>
        public bool IsParallelTo(Line2D other, double tolerance = 1e-6)
        {
            Vector2D v1 = this.Direction;
            Vector2D v2 = other.Direction;
            
            // 叉积接近0表示平行
            double cross = Math.Abs(v1.Cross(v2));
            return cross < tolerance;
        }

        /// <summary>
        /// 判断两条线段是否共线
        /// </summary>
        public bool IsCollinear(Line2D other, double tolerance = 1e-6)
        {
            // 首先检查是否平行
            if (!IsParallelTo(other, tolerance))
                return false;

            // 检查一个线段的起点是否在另一个线段的延长线上
            Vector2D v1 = this.Direction;
            Vector2D v2 = this.StartPoint.VectorTo(other.StartPoint);
            
            double cross = Math.Abs(v1.Cross(v2));
            return cross < tolerance;
        }

        /// <summary>
        /// 计算与另一条线段的交点
        /// 如果没有交点返回 null
        /// </summary>
        public Point2D GetIntersection(Line2D other, double tolerance = 1e-6)
        {
            double x1 = StartPoint.X, y1 = StartPoint.Y;
            double x2 = EndPoint.X, y2 = EndPoint.Y;
            double x3 = other.StartPoint.X, y3 = other.StartPoint.Y;
            double x4 = other.EndPoint.X, y4 = other.EndPoint.Y;

            double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);

            // 平行或重合
            if (Math.Abs(denom) < tolerance)
                return default;

            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
            double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denom;

            // 检查交点是否在两条线段内
            if (t >= -tolerance && t <= 1.0 + tolerance && 
                u >= -tolerance && u <= 1.0 + tolerance)
            {
                double ix = x1 + t * (x2 - x1);
                double iy = y1 + t * (y2 - y1);
                return new Point2D(ix, iy);
            }

            return default;
        }

        /// <summary>
        /// 获取与另一条线段的无限延长线交点（用于 FILLET R=0）
        /// 使用参数方程求交点，不限制在线段范围内
        /// </summary>
        public Point2D GetIntersectionWithInfiniteLine(Line2D other, double tolerance = 1e-6)
        {
            // 参数方程：
            // Line1: P1 + t × D1
            // Line2: P2 + s × D2
            
            Point2D p1 = this.StartPoint;
            if (!this.Direction.TryNormalize(out Vector2D d1))
                return default; // 零长度线段
            
            Point2D p2 = other.StartPoint;
            if (!other.Direction.TryNormalize(out Vector2D d2))
                return default; // 零长度线段
            
            // 向量叉积判断平行
            double cross = d1.X * d2.Y - d1.Y * d2.X;
            
            if (Math.Abs(cross) < tolerance)
                return default;  // 平行或共线
            
            // 求解参数 t
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double t = (dx * d2.Y - dy * d2.X) / cross;
            
            // 计算交点
            return new Point2D(p1.X + t * d1.X, p1.Y + t * d1.Y);
        }

        public override string ToString()
        {
            return $"Line2D[{StartPoint} -> {EndPoint}]";
        }
    }
}

