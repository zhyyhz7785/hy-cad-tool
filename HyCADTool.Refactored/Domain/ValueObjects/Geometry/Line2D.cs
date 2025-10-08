using System;

namespace HyCADTool.Refactored.Domain.ValueObjects.Geometry
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

        public override string ToString()
        {
            return $"Line2D[{StartPoint} -> {EndPoint}]";
        }
    }
}

