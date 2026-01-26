using System;

namespace HyCADTool.Refactored.Domain.ValueObjects.Geometry
{
    /// <summary>
    /// 二维椭圆值对象（平台无关）
    /// 支持完整椭圆或椭圆弧
    /// </summary>
    public readonly struct Ellipse2D : IEquatable<Ellipse2D>
    {
        /// <summary>
        /// 椭圆中心
        /// </summary>
        public Point2D Center { get; }

        /// <summary>
        /// 主半轴长度
        /// </summary>
        public double MajorRadius { get; }

        /// <summary>
        /// 副半轴长度
        /// </summary>
        public double MinorRadius { get; }

        /// <summary>
        /// 旋转角度（弧度），主轴相对于 X 轴的旋转
        /// </summary>
        public double Rotation { get; }

        /// <summary>
        /// 起始参数（弧度）
        /// </summary>
        public double StartParam { get; }

        /// <summary>
        /// 结束参数（弧度）
        /// </summary>
        public double EndParam { get; }

        public Ellipse2D(
            Point2D center,
            double majorRadius,
            double minorRadius,
            double rotation,
            double startParam,
            double endParam)
        {
            if (majorRadius <= 0)
                throw new ArgumentException("Major radius must be positive", nameof(majorRadius));
            if (minorRadius <= 0)
                throw new ArgumentException("Minor radius must be positive", nameof(minorRadius));

            Center = center;
            MajorRadius = majorRadius;
            MinorRadius = minorRadius;
            Rotation = rotation;
            StartParam = startParam;
            EndParam = endParam;
        }

        /// <summary>
        /// 椭圆起点
        /// </summary>
        public Point2D StartPoint => GetPointAtParameter(StartParam);

        /// <summary>
        /// 椭圆终点
        /// </summary>
        public Point2D EndPoint => GetPointAtParameter(EndParam);

        /// <summary>
        /// 是否为完整椭圆
        /// </summary>
        public bool IsFullEllipse => Math.Abs(EndParam - StartParam - 2 * Math.PI) < 1e-10;

        /// <summary>
        /// 根据参数获取椭圆上的点
        /// </summary>
        private Point2D GetPointAtParameter(double param)
        {
            // 椭圆参数方程
            double x = MajorRadius * Math.Cos(param);
            double y = MinorRadius * Math.Sin(param);

            // 旋转变换
            double cosR = Math.Cos(Rotation);
            double sinR = Math.Sin(Rotation);

            double rotatedX = x * cosR - y * sinR;
            double rotatedY = x * sinR + y * cosR;

            return new Point2D(
                Center.X + rotatedX,
                Center.Y + rotatedY);
        }

        #region IEquatable Implementation

        public bool Equals(Ellipse2D other)
        {
            return Center.Equals(other.Center) &&
                   Math.Abs(MajorRadius - other.MajorRadius) < 1e-10 &&
                   Math.Abs(MinorRadius - other.MinorRadius) < 1e-10 &&
                   Math.Abs(Rotation - other.Rotation) < 1e-10 &&
                   Math.Abs(StartParam - other.StartParam) < 1e-10 &&
                   Math.Abs(EndParam - other.EndParam) < 1e-10;
        }

        public override bool Equals(object obj)
        {
            return obj is Ellipse2D other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Center.GetHashCode();
                hash = (hash * 397) ^ MajorRadius.GetHashCode();
                hash = (hash * 397) ^ MinorRadius.GetHashCode();
                hash = (hash * 397) ^ Rotation.GetHashCode();
                hash = (hash * 397) ^ StartParam.GetHashCode();
                hash = (hash * 397) ^ EndParam.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(Ellipse2D left, Ellipse2D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Ellipse2D left, Ellipse2D right)
        {
            return !left.Equals(right);
        }

        #endregion

        public override string ToString()
        {
            return $"Ellipse2D[Center={Center}, MajorRadius={MajorRadius:F2}, MinorRadius={MinorRadius:F2}, Rotation={Rotation:F4}]";
        }
    }
}

























