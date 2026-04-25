using System;

namespace HyCADTool.Shared.Geometry
{
    /// <summary>
    /// 二维点值对象（平台无关）
    /// 不可变结构，适用于几何计算
    /// </summary>
    public readonly struct Point2D : IEquatable<Point2D>
    {
        /// <summary>
        /// X 坐标
        /// </summary>
        public double X { get; }

        /// <summary>
        /// Y 坐标
        /// </summary>
        public double Y { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="x">X 坐标</param>
        /// <param name="y">Y 坐标</param>
        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// 计算到另一点的距离
        /// </summary>
        /// <param name="other">另一点</param>
        /// <returns>欧几里得距离</returns>
        public double DistanceTo(Point2D other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 判断两点是否在容差范围内相等
        /// </summary>
        /// <param name="other">另一点</param>
        /// <param name="tolerance">容差值</param>
        /// <returns>如果距离小于容差返回 true</returns>
        public bool IsEqualTo(Point2D other, double tolerance = 1e-6)
        {
            return Math.Abs(X - other.X) < tolerance && 
                   Math.Abs(Y - other.Y) < tolerance;
        }

        /// <summary>
        /// 点加向量运算
        /// </summary>
        public Point2D Add(Vector2D vector)
        {
            return new Point2D(X + vector.X, Y + vector.Y);
        }

        /// <summary>
        /// 点减向量运算
        /// </summary>
        public Point2D Subtract(Vector2D vector)
        {
            return new Point2D(X - vector.X, Y - vector.Y);
        }

        /// <summary>
        /// 两点相减得到向量
        /// </summary>
        public Vector2D VectorTo(Point2D other)
        {
            return new Vector2D(other.X - X, other.Y - Y);
        }

        #region IEquatable Implementation

        public bool Equals(Point2D other)
        {
            return IsEqualTo(other, 1e-9);
        }

        public override bool Equals(object obj)
        {
            return obj is Point2D other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public static bool operator ==(Point2D left, Point2D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Point2D left, Point2D right)
        {
            return !left.Equals(right);
        }

        #endregion

        public override string ToString()
        {
            return $"({X:F3}, {Y:F3})";
        }

        /// <summary>
        /// 原点 (0, 0)
        /// </summary>
        public static Point2D Origin => new Point2D(0, 0);
    }
}

