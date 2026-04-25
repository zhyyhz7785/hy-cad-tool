using System;

namespace HyCADTool.Shared.Geometry
{
    /// <summary>
    /// 二维圆值对象（平台无关）
    /// </summary>
    public readonly struct Circle2D : IEquatable<Circle2D>
    {
        /// <summary>
        /// 圆心
        /// </summary>
        public Point2D Center { get; }

        /// <summary>
        /// 半径
        /// </summary>
        public double Radius { get; }

        public Circle2D(Point2D center, double radius)
        {
            if (radius <= 0)
                throw new ArgumentException("Radius must be positive", nameof(radius));

            Center = center;
            Radius = radius;
        }

        /// <summary>
        /// 计算圆的面积
        /// </summary>
        public double GetArea()
        {
            return Math.PI * Radius * Radius;
        }

        /// <summary>
        /// 计算圆的周长
        /// </summary>
        public double GetCircumference()
        {
            return 2.0 * Math.PI * Radius;
        }

        /// <summary>
        /// 判断点是否在圆内（不含边界）
        /// </summary>
        public bool Contains(Point2D point)
        {
            double distance = Center.DistanceTo(point);
            return distance < Radius;
        }

        /// <summary>
        /// 判断点是否在圆内或圆上（含边界）
        /// </summary>
        public bool ContainsOrOn(Point2D point, double tolerance = 1e-6)
        {
            double distance = Center.DistanceTo(point);
            return distance <= Radius + tolerance;
        }

        /// <summary>
        /// 判断点是否在圆上（边界）
        /// </summary>
        public bool IsPointOnCircle(Point2D point, double tolerance = 1e-6)
        {
            double distance = Center.DistanceTo(point);
            return Math.Abs(distance - Radius) < tolerance;
        }

        /// <summary>
        /// 获取边界框
        /// </summary>
        public BoundingBox GetBoundingBox()
        {
            return new BoundingBox(
                new Point2D(Center.X - Radius, Center.Y - Radius),
                new Point2D(Center.X + Radius, Center.Y + Radius)
            );
        }

        /// <summary>
        /// 判断两圆是否相交
        /// </summary>
        public bool IntersectsWith(Circle2D other)
        {
            double distance = Center.DistanceTo(other.Center);
            double radiusSum = Radius + other.Radius;
            double radiusDiff = Math.Abs(Radius - other.Radius);

            // 相交条件：圆心距离在 (|r1-r2|, r1+r2) 之间
            return distance < radiusSum && distance > radiusDiff;
        }

        /// <summary>
        /// 判断圆是否包含另一个圆
        /// </summary>
        public bool Contains(Circle2D other)
        {
            double distance = Center.DistanceTo(other.Center);
            return distance + other.Radius <= Radius;
        }

        #region IEquatable Implementation

        public bool Equals(Circle2D other)
        {
            return Center.Equals(other.Center) && 
                   Math.Abs(Radius - other.Radius) < 1e-10;
        }

        public override bool Equals(object obj)
        {
            return obj is Circle2D other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Center.GetHashCode() * 397) ^ Radius.GetHashCode();
            }
        }

        public static bool operator ==(Circle2D left, Circle2D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Circle2D left, Circle2D right)
        {
            return !left.Equals(right);
        }

        #endregion

        public override string ToString()
        {
            return $"Circle2D[Center={Center}, Radius={Radius:F2}]";
        }
    }
}


