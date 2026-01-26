using System;

namespace HyCADTool.Refactored.Domain.ValueObjects.Geometry
{
    /// <summary>
    /// 二维圆弧值对象（平台无关）
    /// </summary>
    public readonly struct Arc2D : IEquatable<Arc2D>
    {
        /// <summary>
        /// 圆心
        /// </summary>
        public Point2D Center { get; }

        /// <summary>
        /// 半径
        /// </summary>
        public double Radius { get; }

        /// <summary>
        /// 起始角度（弧度）
        /// </summary>
        public double StartAngle { get; }

        /// <summary>
        /// 结束角度（弧度）
        /// </summary>
        public double EndAngle { get; }

        public Arc2D(Point2D center, double radius, double startAngle, double endAngle)
        {
            if (radius <= 0)
                throw new ArgumentException("Radius must be positive", nameof(radius));

            Center = center;
            Radius = radius;
            StartAngle = startAngle;
            EndAngle = endAngle;
        }

        /// <summary>
        /// 圆弧起点
        /// </summary>
        public Point2D StartPoint => new Point2D(
            Center.X + Radius * Math.Cos(StartAngle),
            Center.Y + Radius * Math.Sin(StartAngle));

        /// <summary>
        /// 圆弧终点
        /// </summary>
        public Point2D EndPoint => new Point2D(
            Center.X + Radius * Math.Cos(EndAngle),
            Center.Y + Radius * Math.Sin(EndAngle));

        /// <summary>
        /// 圆弧长度
        /// </summary>
        public double Length
        {
            get
            {
                double sweepAngle = EndAngle - StartAngle;
                if (sweepAngle < 0)
                    sweepAngle += 2 * Math.PI;
                return Radius * sweepAngle;
            }
        }

        /// <summary>
        /// 扫掠角度（弧度）
        /// </summary>
        public double SweepAngle
        {
            get
            {
                double sweep = EndAngle - StartAngle;
                if (sweep < 0)
                    sweep += 2 * Math.PI;
                return sweep;
            }
        }

        /// <summary>
        /// 圆弧中点
        /// </summary>
        public Point2D MidPoint
        {
            get
            {
                double midAngle = StartAngle + SweepAngle / 2.0;
                return new Point2D(
                    Center.X + Radius * Math.Cos(midAngle),
                    Center.Y + Radius * Math.Sin(midAngle));
            }
        }

        #region IEquatable Implementation

        public bool Equals(Arc2D other)
        {
            return Center.Equals(other.Center) &&
                   Math.Abs(Radius - other.Radius) < 1e-10 &&
                   Math.Abs(StartAngle - other.StartAngle) < 1e-10 &&
                   Math.Abs(EndAngle - other.EndAngle) < 1e-10;
        }

        public override bool Equals(object obj)
        {
            return obj is Arc2D other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Center.GetHashCode();
                hash = (hash * 397) ^ Radius.GetHashCode();
                hash = (hash * 397) ^ StartAngle.GetHashCode();
                hash = (hash * 397) ^ EndAngle.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(Arc2D left, Arc2D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Arc2D left, Arc2D right)
        {
            return !left.Equals(right);
        }

        #endregion

        public override string ToString()
        {
            return $"Arc2D[Center={Center}, Radius={Radius:F2}, StartAngle={StartAngle:F4}, EndAngle={EndAngle:F4}]";
        }
    }
}

























