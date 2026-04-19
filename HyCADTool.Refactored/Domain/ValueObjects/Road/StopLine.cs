using System;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;

namespace HyCADTool.Refactored.Domain.ValueObjects.Road
{
    /// <summary>
    /// 路面标线：独立停止线（不依附 <see cref="Models.Road.Intersection"/> 的 <see cref="Crosswalk"/>）。
    ///
    /// <para><b>几何</b></para>
    /// 由 <see cref="Left"/> / <see cref="Right"/> 两端点确定一条跨越车道的线段，
    /// <see cref="StripeWidth"/> 定义停止线的线宽（垂直于线段方向的宽度；AutoCAD 绘制时用作 <c>Polyline.ConstantWidth</c>）。
    /// 线段自身方向 = 跨车道方向（= 垂直于车辆前进方向）。
    ///
    /// <para><b>与 Crosswalk 的 StopLine 区分</b></para>
    /// <see cref="Crosswalk"/> 内嵌的停止线依附于交叉口某条 Leg，按横道几何自动派生；
    /// 本 <see cref="StopLine"/> 是独立存在的（支路口 / 小区出入口 / T 形末端等），
    /// 由用户显式指定两端点创建，<see cref="Id"/> 独立于任何 <see cref="Models.Road.Intersection"/>。
    ///
    /// <para><b>国标默认</b></para>
    /// <list type="bullet">
    /// <item><see cref="DefaultStripeWidth"/> = 0.40 m（GB 5768-2009 §5.2.2，普通停止线 20 ~ 40 cm）。</item>
    /// </list>
    /// </summary>
    public readonly struct StopLine : IEquatable<StopLine>
    {
        /// <summary>默认线宽（米）—— GB 5768-2009 §5.2.2。</summary>
        public const double DefaultStripeWidth = 0.40;

        /// <summary>实体 Id（用于 HY_ROAD Xdata 定位）。</summary>
        public Guid Id { get; }

        /// <summary>跨车道方向的左端点。</summary>
        public Point2D Left { get; }

        /// <summary>跨车道方向的右端点。</summary>
        public Point2D Right { get; }

        /// <summary>线宽（米）。</summary>
        public double StripeWidth { get; }

        public StopLine(Guid id, Point2D left, Point2D right, double stripeWidth = DefaultStripeWidth)
        {
            if (stripeWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(stripeWidth), stripeWidth, "StripeWidth 必须 > 0");
            if (left.DistanceTo(right) < 1e-6)
                throw new ArgumentException("Left / Right 不能重合", nameof(right));

            Id = id;
            Left = left;
            Right = right;
            StripeWidth = stripeWidth;
        }

        /// <summary>中点。</summary>
        public Point2D Center => new Point2D((Left.X + Right.X) / 2.0, (Left.Y + Right.Y) / 2.0);

        /// <summary>线段长度（= 车道横向宽度）。</summary>
        public double Length => Left.DistanceTo(Right);

        /// <summary>跨车道方向（单位向量，Left→Right）。</summary>
        public Vector2D Direction
        {
            get
            {
                var v = new Vector2D(Right.X - Left.X, Right.Y - Left.Y);
                return v.TryNormalize(out var u, 1e-9) ? u : Vector2D.UnitX;
            }
        }

        public bool Equals(StopLine other)
            => Id == other.Id
               && Left.IsEqualTo(other.Left, 1e-9)
               && Right.IsEqualTo(other.Right, 1e-9)
               && Math.Abs(StripeWidth - other.StripeWidth) < 1e-9;

        public override bool Equals(object obj) => obj is StopLine other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Id.GetHashCode();
                h = (h * 397) ^ Left.GetHashCode();
                h = (h * 397) ^ Right.GetHashCode();
                h = (h * 397) ^ StripeWidth.GetHashCode();
                return h;
            }
        }

        public override string ToString()
            => $"StopLine[{Id:N}, {Left}→{Right}, L={Length:F2}, W={StripeWidth:F2}]";
    }
}
