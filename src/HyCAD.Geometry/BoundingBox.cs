using System;

namespace HyCAD.Geometry
{
    /// <summary>
    /// 二维边界框值对象（平台无关）
    /// Axis-Aligned Bounding Box (AABB)
    /// </summary>
    public readonly struct BoundingBox : IEquatable<BoundingBox>
    {
        /// <summary>
        /// 最小点（左下角）
        /// </summary>
        public Point2D MinPoint { get; }

        /// <summary>
        /// 最大点（右上角）
        /// </summary>
        public Point2D MaxPoint { get; }

        public BoundingBox(Point2D minPoint, Point2D maxPoint)
        {
            if (minPoint.X > maxPoint.X || minPoint.Y > maxPoint.Y)
                throw new ArgumentException("MinPoint must be less than or equal to MaxPoint");

            MinPoint = minPoint;
            MaxPoint = maxPoint;
        }

        /// <summary>
        /// 从两个点创建边界框（自动确定最小和最大点）
        /// </summary>
        public static BoundingBox FromPoints(Point2D p1, Point2D p2)
        {
            double minX = System.Math.Min(p1.X, p2.X);
            double minY = System.Math.Min(p1.Y, p2.Y);
            double maxX = System.Math.Max(p1.X, p2.X);
            double maxY = System.Math.Max(p1.Y, p2.Y);

            return new BoundingBox(
                new Point2D(minX, minY),
                new Point2D(maxX, maxY));
        }

        /// <summary>
        /// 从中心点和尺寸创建边界框
        /// </summary>
        public static BoundingBox FromCenterAndSize(Point2D center, double width, double height)
        {
            double halfWidth = width / 2.0;
            double halfHeight = height / 2.0;

            return new BoundingBox(
                new Point2D(center.X - halfWidth, center.Y - halfHeight),
                new Point2D(center.X + halfWidth, center.Y + halfHeight)
            );
        }

        /// <summary>
        /// 宽度
        /// </summary>
        public double Width => MaxPoint.X - MinPoint.X;

        /// <summary>
        /// 高度
        /// </summary>
        public double Height => MaxPoint.Y - MinPoint.Y;

        /// <summary>
        /// 中心点
        /// </summary>
        public Point2D Center => new Point2D(
            (MinPoint.X + MaxPoint.X) / 2.0,
            (MinPoint.Y + MaxPoint.Y) / 2.0
        );

        /// <summary>
        /// 面积
        /// </summary>
        public double Area => Width * Height;

        /// <summary>
        /// 判断点是否在边界框内
        /// </summary>
        public bool Contains(Point2D point)
        {
            return point.X >= MinPoint.X && point.X <= MaxPoint.X &&
                   point.Y >= MinPoint.Y && point.Y <= MaxPoint.Y;
        }

        /// <summary>
        /// 判断边界框是否与另一个边界框相交
        /// </summary>
        public bool Intersects(BoundingBox other)
        {
            return !(MaxPoint.X < other.MinPoint.X || MinPoint.X > other.MaxPoint.X ||
                     MaxPoint.Y < other.MinPoint.Y || MinPoint.Y > other.MaxPoint.Y);
        }

        /// <summary>
        /// 扩展边界框（按指定距离）
        /// </summary>
        /// <param name="dx">X方向扩展距离</param>
        /// <param name="dy">Y方向扩展距离</param>
        /// <returns>扩展后的边界框</returns>
        public BoundingBox Expand(double dx, double dy)
        {
            return new BoundingBox(
                new Point2D(MinPoint.X - dx, MinPoint.Y - dy),
                new Point2D(MaxPoint.X + dx, MaxPoint.Y + dy));
        }

        /// <summary>
        /// 扩展边界框以包含指定点
        /// </summary>
        public BoundingBox Expand(Point2D point)
        {
            return new BoundingBox(
                new Point2D(System.Math.Min(MinPoint.X, point.X), System.Math.Min(MinPoint.Y, point.Y)),
                new Point2D(System.Math.Max(MaxPoint.X, point.X), System.Math.Max(MaxPoint.Y, point.Y))
            );
        }

        /// <summary>
        /// 扩展边界框以包含另一个边界框
        /// </summary>
        public BoundingBox Union(BoundingBox other)
        {
            return new BoundingBox(
                new Point2D(System.Math.Min(MinPoint.X, other.MinPoint.X), System.Math.Min(MinPoint.Y, other.MinPoint.Y)),
                new Point2D(System.Math.Max(MaxPoint.X, other.MaxPoint.X), System.Math.Max(MaxPoint.Y, other.MaxPoint.Y))
            );
        }

        /// <summary>
        /// 计算与另一个边界框的交集
        /// </summary>
        /// <returns>如果不相交返回 default</returns>
        public BoundingBox Intersection(BoundingBox other)
        {
            if (!Intersects(other))
                return default;

            return new BoundingBox(
                new Point2D(System.Math.Max(MinPoint.X, other.MinPoint.X), System.Math.Max(MinPoint.Y, other.MinPoint.Y)),
                new Point2D(System.Math.Min(MaxPoint.X, other.MaxPoint.X), System.Math.Min(MaxPoint.Y, other.MaxPoint.Y))
            );
        }

        /// <summary>
        /// 轴分离间隙的平方（相交时为 0），聚类热路径用，避免 Sqrt。
        /// </summary>
        public double GapDistanceSquared(BoundingBox other)
        {
            double dx = System.Math.Max(0, System.Math.Max(MinPoint.X - other.MaxPoint.X, other.MinPoint.X - MaxPoint.X));
            double dy = System.Math.Max(0, System.Math.Max(MinPoint.Y - other.MaxPoint.Y, other.MinPoint.Y - MaxPoint.Y));
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// 两 bbox 最小间隙是否 ≤ maxDistance（含相交）。
        /// </summary>
        public bool IsWithinDistance(BoundingBox other, double maxDistance)
        {
            double limit = maxDistance * maxDistance;
            return GapDistanceSquared(other) <= limit;
        }

        /// <summary>
        /// 计算到另一个边界框的最小距离
        /// </summary>
        /// <param name="other">另一个边界框</param>
        /// <returns>最小距离（相交返回0）</returns>
        public double DistanceTo(BoundingBox other)
        {
            return System.Math.Sqrt(GapDistanceSquared(other));
        }

        /// <summary>
        /// 转换为多边形的四个顶点（逆时针）
        /// </summary>
        public Point2D[] ToVertices()
        {
            return new Point2D[]
            {
                new Point2D(MinPoint.X, MinPoint.Y), // 左下
                new Point2D(MaxPoint.X, MinPoint.Y), // 右下
                new Point2D(MaxPoint.X, MaxPoint.Y), // 右上
                new Point2D(MinPoint.X, MaxPoint.Y)  // 左上
            };
        }

        #region IEquatable Implementation

        public bool Equals(BoundingBox other)
        {
            return MinPoint.Equals(other.MinPoint) && MaxPoint.Equals(other.MaxPoint);
        }

        public override bool Equals(object obj)
        {
            return obj is BoundingBox other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (MinPoint.GetHashCode() * 397) ^ MaxPoint.GetHashCode();
            }
        }

        public static bool operator ==(BoundingBox left, BoundingBox right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BoundingBox left, BoundingBox right)
        {
            return !left.Equals(right);
        }

        #endregion

        public override string ToString()
        {
            return $"BoundingBox[{MinPoint} - {MaxPoint}]";
        }
    }
}

