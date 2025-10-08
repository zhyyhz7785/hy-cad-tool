using System;

namespace HyCADTool.Refactored.Domain.ValueObjects.Geometry
{
    /// <summary>
    /// 二维向量值对象（平台无关）
    /// </summary>
    public readonly struct Vector2D : IEquatable<Vector2D>
    {
        public double X { get; }
        public double Y { get; }

        public Vector2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// 向量长度（模）
        /// </summary>
        public double Length => Math.Sqrt(X * X + Y * Y);

        /// <summary>
        /// 向量长度的平方（避免开方运算）
        /// </summary>
        public double LengthSquared => X * X + Y * Y;

        /// <summary>
        /// 单位化向量
        /// </summary>
        public Vector2D Normalize()
        {
            double len = Length;
            if (len < 1e-10)
                throw new InvalidOperationException("Cannot normalize a zero-length vector");
            return new Vector2D(X / len, Y / len);
        }

        /// <summary>
        /// 点积
        /// </summary>
        public double Dot(Vector2D other)
        {
            return X * other.X + Y * other.Y;
        }

        /// <summary>
        /// 叉积（标量，表示 z 分量）
        /// </summary>
        public double Cross(Vector2D other)
        {
            return X * other.Y - Y * other.X;
        }

        /// <summary>
        /// 与另一向量的夹角（弧度）
        /// </summary>
        public double AngleTo(Vector2D other)
        {
            double dot = Dot(other);
            double cross = Cross(other);
            return Math.Atan2(cross, dot);
        }

        /// <summary>
        /// 向量加法
        /// </summary>
        public static Vector2D operator +(Vector2D a, Vector2D b)
        {
            return new Vector2D(a.X + b.X, a.Y + b.Y);
        }

        /// <summary>
        /// 向量减法
        /// </summary>
        public static Vector2D operator -(Vector2D a, Vector2D b)
        {
            return new Vector2D(a.X - b.X, a.Y - b.Y);
        }

        /// <summary>
        /// 向量数乘
        /// </summary>
        public static Vector2D operator *(Vector2D v, double scalar)
        {
            return new Vector2D(v.X * scalar, v.Y * scalar);
        }

        /// <summary>
        /// 向量数乘
        /// </summary>
        public static Vector2D operator *(double scalar, Vector2D v)
        {
            return new Vector2D(v.X * scalar, v.Y * scalar);
        }

        /// <summary>
        /// 向量取反
        /// </summary>
        public static Vector2D operator -(Vector2D v)
        {
            return new Vector2D(-v.X, -v.Y);
        }

        #region IEquatable Implementation

        public bool Equals(Vector2D other)
        {
            return Math.Abs(X - other.X) < 1e-9 && Math.Abs(Y - other.Y) < 1e-9;
        }

        public override bool Equals(object obj)
        {
            return obj is Vector2D other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public static bool operator ==(Vector2D left, Vector2D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector2D left, Vector2D right)
        {
            return !left.Equals(right);
        }

        #endregion

        public override string ToString()
        {
            return $"Vector2D({X:F3}, {Y:F3})";
        }

        /// <summary>
        /// 零向量
        /// </summary>
        public static Vector2D Zero => new Vector2D(0, 0);

        /// <summary>
        /// X 轴单位向量
        /// </summary>
        public static Vector2D UnitX => new Vector2D(1, 0);

        /// <summary>
        /// Y 轴单位向量
        /// </summary>
        public static Vector2D UnitY => new Vector2D(0, 1);
    }
}

