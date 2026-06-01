using System;
using HyCAD.Geometry;

namespace HyCADTool.Domain.ValueObjects.Road
{
    /// <summary>
    /// 车道分界标线类型（GB 5768-2009 §5.1）。
    ///
    /// <list type="table">
    /// <listheader><term>值</term><description>语义</description></listheader>
    /// <item><term><see cref="SolidWhite"/></term><description>白色单实线：同向车行道分界，<b>禁止跨越</b>（如路口停止线前 30~50 m 段）。</description></item>
    /// <item><term><see cref="DashedWhite"/></term><description>白色单虚线：同向车行道分界，<b>允许跨越变道</b>。GB 虚线段 2 m / 间隔 4 m（城市道路）。</description></item>
    /// <item><term><see cref="SolidYellow"/></term><description>黄色单实线：对向车行道分界，<b>禁止跨越</b>（双向 2 车道常用）。</description></item>
    /// <item><term><see cref="DoubleYellow"/></term><description>黄色双实线：对向车行道分界（≥4 车道 / 高等级路），两条平行实线，线距 15~20 cm。</description></item>
    /// </list>
    /// </summary>
    public enum LaneMarkingKind
    {
        SolidWhite = 0,
        DashedWhite = 1,
        SolidYellow = 2,
        DoubleYellow = 3,
    }

    /// <summary>
    /// 路面标线：车道分界线（GB 5768-2009 §5.1）。
    ///
    /// <para><b>几何</b></para>
    /// 由 <see cref="Start"/> / <see cref="End"/> 两端点确定一条沿车行道方向的直线段。
    /// 其上的"实线 / 虚线 / 双线"呈现由 <see cref="Kind"/> 决定，由
    /// <see cref="Services.Road.LaneMarkingDesigner"/> 展开成若干几何子段（绘制时）。
    ///
    /// <para><b>Id</b></para>
    /// 一条 <see cref="LaneMarking"/> 映射为一组 AutoCAD 图元（虚线是多段、双黄是双段），
    /// 它们共享同一 <see cref="Id"/> 作为 HY_ROAD Xdata 的 ID；KIND=<c>LaneMarking</c>。
    ///
    /// <para><b>默认线宽</b></para>
    /// <see cref="DefaultStripeWidth"/> = 0.15 m（城市道路 10~15 cm 取上限）。
    /// </summary>
    public readonly struct LaneMarking : IEquatable<LaneMarking>
    {
        /// <summary>默认线宽（米）—— GB 5768-2009 §5.1，取 10~15 cm 上限。</summary>
        public const double DefaultStripeWidth = 0.15;

        /// <summary>默认虚线段长（米）—— GB 5768-2009 §5.1 城市道路。</summary>
        public const double DefaultDashLength = 2.0;

        /// <summary>默认虚线间隔（米）—— GB 5768-2009 §5.1 城市道路。</summary>
        public const double DefaultGapLength = 4.0;

        /// <summary>双黄实线间距（米）—— GB 5768-2009 §5.1，取 15~20 cm 上限。</summary>
        public const double DefaultDoubleSpacing = 0.20;

        public Guid Id { get; }
        public Point2D Start { get; }
        public Point2D End { get; }
        public LaneMarkingKind Kind { get; }
        public double StripeWidth { get; }

        public LaneMarking(
            Guid id,
            Point2D start,
            Point2D end,
            LaneMarkingKind kind,
            double stripeWidth = DefaultStripeWidth)
        {
            if (stripeWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(stripeWidth), stripeWidth, "StripeWidth 必须 > 0");
            if (start.DistanceTo(end) < 1e-6)
                throw new ArgumentException("Start / End 不能重合", nameof(end));

            Id = id;
            Start = start;
            End = end;
            Kind = kind;
            StripeWidth = stripeWidth;
        }

        public double Length => Start.DistanceTo(End);

        public Point2D Center
            => new Point2D((Start.X + End.X) / 2.0, (Start.Y + End.Y) / 2.0);

        /// <summary>沿车行道方向的单位向量（Start→End）。</summary>
        public Vector2D Direction
        {
            get
            {
                var v = new Vector2D(End.X - Start.X, End.Y - Start.Y);
                return v.TryNormalize(out var u, 1e-9) ? u : Vector2D.UnitX;
            }
        }

        public bool Equals(LaneMarking other)
            => Id == other.Id
               && Kind == other.Kind
               && Start.IsEqualTo(other.Start, 1e-9)
               && End.IsEqualTo(other.End, 1e-9)
               && Math.Abs(StripeWidth - other.StripeWidth) < 1e-9;

        public override bool Equals(object obj) => obj is LaneMarking other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Id.GetHashCode();
                h = (h * 397) ^ (int)Kind;
                h = (h * 397) ^ Start.GetHashCode();
                h = (h * 397) ^ End.GetHashCode();
                h = (h * 397) ^ StripeWidth.GetHashCode();
                return h;
            }
        }

        public override string ToString()
            => $"LaneMarking[{Kind}, {Id:N}, {Start}→{End}, L={Length:F2}, W={StripeWidth:F2}]";
    }
}
