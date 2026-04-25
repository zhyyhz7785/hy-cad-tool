using System;

namespace HyCADTool.Shared.Geometry
{
    /// <summary>
    /// 三维向量值对象（平台无关）
    /// </summary>
    public readonly struct Vector3D : IEquatable<Vector3D>
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public Vector3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// 向量长度（模）
        /// </summary>
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        /// <summary>
        /// 向量长度的平方
        /// </summary>
        public double LengthSquared => X * X + Y * Y + Z * Z;

        /// <summary>
        /// 单位化向量
        /// </summary>
        public Vector3D Normalize()
        {
            double len = Length;
            if (len < 1e-10)
                throw new InvalidOperationException("Cannot normalize a zero-length vector");
            return new Vector3D(X / len, Y / len, Z / len);
        }

        /// <summary>
        /// 点积
        /// </summary>
        public double Dot(Vector3D other)
        {
            return X * other.X + Y * other.Y + Z * other.Z;
        }

        /// <summary>
        /// 叉积（向量）
        /// </summary>
        public Vector3D Cross(Vector3D other)
        {
            return new Vector3D(
                Y * other.Z - Z * other.Y,
                Z * other.X - X * other.Z,
                X * other.Y - Y * other.X
            );
        }

        /// <summary>
        /// 与另一向量的夹角（弧度）
        /// </summary>
        public double AngleTo(Vector3D other)
        {
            double dot = Dot(other);
            double lenProduct = Length * other.Length;
            if (lenProduct < 1e-10)
                return 0;
            return Math.Acos(Math.Max(-1, Math.Min(1, dot / lenProduct)));
        }

        /// <summary>
        /// 向量加法
        /// </summary>
        public static Vector3D operator +(Vector3D a, Vector3D b)
        {
            return new Vector3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        /// <summary>
        /// 向量减法
        /// </summary>
        public static Vector3D operator -(Vector3D a, Vector3D b)
        {
            return new Vector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        /// <summary>
        /// 向量数乘
        /// </summary>
        public static Vector3D operator *(Vector3D v, double scalar)
        {
            return new Vector3D(v.X * scalar, v.Y * scalar, v.Z * scalar);
        }

        /// <summary>
        /// 向量数乘
        /// </summary>
        public static Vector3D operator *(double scalar, Vector3D v)
        {
            return new Vector3D(v.X * scalar, v.Y * scalar, v.Z * scalar);
        }

        /// <summary>
        /// 向量取反
        /// </summary>
        public static Vector3D operator -(Vector3D v)
        {
            return new Vector3D(-v.X, -v.Y, -v.Z);
        }

        #region IEquatable Implementation

        public bool Equals(Vector3D other)
        {
            return Math.Abs(X - other.X) < 1e-9 &&
                   Math.Abs(Y - other.Y) < 1e-9 &&
                   Math.Abs(Z - other.Z) < 1e-9;
        }

        public override bool Equals(object obj)
        {
            return obj is Vector3D other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(Vector3D left, Vector3D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector3D left, Vector3D right)
        {
            return !left.Equals(right);
        }

        #endregion

        public override string ToString()
        {
            return $"Vector3D({X:F3}, {Y:F3}, {Z:F3})";
        }

        /// <summary>
        /// 零向量
        /// </summary>
        public static Vector3D Zero => new Vector3D(0, 0, 0);

        /// <summary>
        /// X 轴单位向量
        /// </summary>
        public static Vector3D UnitX => new Vector3D(1, 0, 0);

        /// <summary>
        /// Y 轴单位向量
        /// </summary>
        public static Vector3D UnitY => new Vector3D(0, 1, 0);

        /// <summary>
        /// Z 轴单位向量
        /// </summary>
        public static Vector3D UnitZ => new Vector3D(0, 0, 1);
    }
}

