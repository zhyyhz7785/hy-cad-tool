using System;
using Newtonsoft.Json;

namespace HyCADTool.Refactored.Domain.ValueObjects.Geometry
{
    /// <summary>
    /// 三维点值对象（平台无关）
    /// 不可变结构，适用于几何计算
    /// </summary>
    public readonly struct Point3D : IEquatable<Point3D>
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
        /// Z 坐标
        /// </summary>
        public double Z { get; }

        /// <summary>
        /// 主构造函数，同时作为 Newtonsoft.Json 反序列化入口。
        /// readonly struct 无公共无参构造函数，必须显式 <see cref="JsonConstructorAttribute"/> 告诉
        /// Newtonsoft 使用三参版本而不是 <see cref="Point3D(Point2D)"/>（后者参数名不匹配 X/Y/Z）。
        /// </summary>
        [JsonConstructor]
        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// 从二维点构造（Z = 0）
        /// </summary>
        public Point3D(Point2D point2D) : this(point2D.X, point2D.Y, 0)
        {
        }

        /// <summary>
        /// 转换为二维点（忽略 Z 坐标）
        /// </summary>
        public Point2D ToPoint2D()
        {
            return new Point2D(X, Y);
        }

        /// <summary>
        /// 计算到另一点的距离
        /// </summary>
        public double DistanceTo(Point3D other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// 判断两点是否在容差范围内相等
        /// </summary>
        public bool IsEqualTo(Point3D other, double tolerance = 1e-6)
        {
            return Math.Abs(X - other.X) < tolerance &&
                   Math.Abs(Y - other.Y) < tolerance &&
                   Math.Abs(Z - other.Z) < tolerance;
        }

        /// <summary>
        /// 点加向量运算
        /// </summary>
        public Point3D Add(Vector3D vector)
        {
            return new Point3D(X + vector.X, Y + vector.Y, Z + vector.Z);
        }

        /// <summary>
        /// 点减向量运算
        /// </summary>
        public Point3D Subtract(Vector3D vector)
        {
            return new Point3D(X - vector.X, Y - vector.Y, Z - vector.Z);
        }

        /// <summary>
        /// 两点相减得到向量
        /// </summary>
        public Vector3D VectorTo(Point3D other)
        {
            return new Vector3D(other.X - X, other.Y - Y, other.Z - Z);
        }

        #region IEquatable Implementation

        public bool Equals(Point3D other)
        {
            return IsEqualTo(other, 1e-9);
        }

        public override bool Equals(object obj)
        {
            return obj is Point3D other && Equals(other);
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

        public static bool operator ==(Point3D left, Point3D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Point3D left, Point3D right)
        {
            return !left.Equals(right);
        }

        #endregion

        public override string ToString()
        {
            return $"({X:F3}, {Y:F3}, {Z:F3})";
        }

        /// <summary>
        /// 原点 (0, 0, 0)
        /// </summary>
        public static Point3D Origin => new Point3D(0, 0, 0);
    }
}

