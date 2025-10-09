using System;

namespace HyCADTool.Refactored.Domain.ValueObjects.Geometry
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
        /// 扩展边界框以包含指定点
        /// </summary>
        public BoundingBox Expand(Point2D point)
        {
            return new BoundingBox(
                new Point2D(Math.Min(MinPoint.X, point.X), Math.Min(MinPoint.Y, point.Y)),
                new Point2D(Math.Max(MaxPoint.X, point.X), Math.Max(MaxPoint.Y, point.Y))
            );
        }

        /// <summary>
        /// 扩展边界框以包含另一个边界框
        /// </summary>
        public BoundingBox Union(BoundingBox other)
        {
            return new BoundingBox(
                new Point2D(Math.Min(MinPoint.X, other.MinPoint.X), Math.Min(MinPoint.Y, other.MinPoint.Y)),
                new Point2D(Math.Max(MaxPoint.X, other.MaxPoint.X), Math.Max(MaxPoint.Y, other.MaxPoint.Y))
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
                new Point2D(Math.Max(MinPoint.X, other.MinPoint.X), Math.Max(MinPoint.Y, other.MinPoint.Y)),
                new Point2D(Math.Min(MaxPoint.X, other.MaxPoint.X), Math.Min(MaxPoint.Y, other.MaxPoint.Y))
            );
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

