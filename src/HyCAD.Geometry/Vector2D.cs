using System;

namespace HyCAD.Geometry
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
        public double Length => System.Math.Sqrt(X * X + Y * Y);

        /// <summary>
        /// 向量长度的平方（避免开方运算）
        /// </summary>
        public double LengthSquared => X * X + Y * Y;

        /// <summary>
        /// 单位化向量（零向量会抛异常）
        /// </summary>
        public Vector2D Normalize()
        {
            double len = Length;
            if (len < 1e-10)
                throw new InvalidOperationException("Cannot normalize a zero-length vector");
            return new Vector2D(X / len, Y / len);
        }

        /// <summary>
        /// 安全单位化：零向量或极短向量返回 false + fallback
        /// </summary>
        public bool TryNormalize(out Vector2D result, double tolerance = 1e-6)
        {
            double len = Length;
            if (len < tolerance)
            {
                result = Zero;
                return false;
            }
            result = new Vector2D(X / len, Y / len);
            return true;
        }

        /// <summary>
        /// 是否为零向量（长度小于容差）
        /// </summary>
        public bool IsZero(double tolerance = 1e-6) => Length < tolerance;

        /// <summary>
        /// 点积（实例方法）
        /// </summary>
        public double Dot(Vector2D other)
        {
            return X * other.X + Y * other.Y;
        }

        /// <summary>
        /// 点积（静态方法）
        /// </summary>
        public static double Dot(Vector2D a, Vector2D b)
        {
            return a.X * b.X + a.Y * b.Y;
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
            return System.Math.Atan2(cross, dot);
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

        /// <summary>
        /// 向量除法
        /// </summary>
        public static Vector2D operator /(Vector2D v, double scalar)
        {
            if (System.Math.Abs(scalar) < 1e-10)
                throw new DivideByZeroException("Cannot divide vector by zero");
            return new Vector2D(v.X / scalar, v.Y / scalar);
        }

        #region IEquatable Implementation

        public bool Equals(Vector2D other)
        {
            return System.Math.Abs(X - other.X) < 1e-9 && System.Math.Abs(Y - other.Y) < 1e-9;
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

        /// <summary>
        /// 将向量绕原点旋转指定角度（弧度，逆时针为正）
        /// </summary>
        public Vector2D Rotate(double angle)
        {
            double cos = System.Math.Cos(angle);
            double sin = System.Math.Sin(angle);
            return new Vector2D(X * cos - Y * sin, X * sin + Y * cos);
        }

        /// <summary>
        /// 获取垂直向量（逆时针旋转 90 度）
        /// </summary>
        public Vector2D Perpendicular()
        {
            return new Vector2D(-Y, X);
        }
    }
}

