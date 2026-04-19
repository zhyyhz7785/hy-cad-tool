using System;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;

namespace HyCADTool.Refactored.Domain.ValueObjects.Road
{
    /// <summary>
    /// 单臂人行横道（Crosswalk）—— 依附于 <see cref="Models.Road.Intersection"/> 某一条 Leg，
    /// 由斑马线条纹 + 停止线 + 两条辅助边线（L2 内边 / L3 外边）组成。
    ///
    /// <para><b>几何定义（参照 CJJ 37-2012 §11.3 与旧 <c>CrosswalkService</c>）</b></para>
    /// <list type="bullet">
    /// <item>Leg 两侧的"路缘外角点" = 相邻两 CornerArc 的切点：
    ///   <see cref="BaseLeft"/> = 左侧相邻 CornerArc 的 <see cref="CornerArc.EndPoint"/>（该 Arc 以本 Leg 作为 <c>LegIndexB</c>），
    ///   <see cref="BaseRight"/> = 右侧相邻 CornerArc 的 <see cref="CornerArc.StartPoint"/>（该 Arc 以本 Leg 作为 <c>LegIndexA</c>）；</item>
    /// <item>沿 Leg <b>内前进</b>方向（= <see cref="IntersectionLeg.InwardDirection"/>，从 <c>ApproachPoint</c> 指向交叉口中心）前进：
    /// <list type="bullet">
    /// <item>距路缘 <see cref="GapWidth"/> → L2（横道内边），</item>
    /// <item>再前进 <see cref="Width"/> → L3（横道外边），</item>
    /// <item>再前进 <see cref="StopLineDistance"/> → L4（停止线）。</item>
    /// </list></item>
    /// <item>条纹在 L2 / L3 之间按 <see cref="StripeSpacing"/> 为中心距分布，条纹宽度由 <see cref="StripeWidth"/> 决定；
    /// 当前 v1.1 条纹以单根 <b>Line</b>（AutoCAD 常量线宽）呈现，与旧 <c>CrosswalkService.DrawCrosswalkForArm</c> 一致。</item>
    /// </list>
    ///
    /// <para><b>国标默认值（CJJ 37-2012 §11.3.1 / GB 5768-2009）</b></para>
    /// <list type="bullet">
    /// <item><see cref="DefaultGapWidth"/> = 1.0 m（自路缘到 L2 的空挡，路缘坡道 + 盲道的协调空间）；</item>
    /// <item><see cref="DefaultWidth"/> = 5.0 m（标准横道宽度，CJJ 37 给 3~6 m）；</item>
    /// <item><see cref="DefaultStopLineDistance"/> = 2.0 m（L3 到停止线的间距）；</item>
    /// <item><see cref="DefaultStripeSpacing"/> = 0.6 m（条纹中心距，国标 45~60 cm）；</item>
    /// <item><see cref="DefaultStripeWidth"/> = 0.40 m（条纹实际宽度，国标 40~45 cm）。</item>
    /// </list>
    ///
    /// <para><b>域纯净</b></para>
    /// 只依赖 <see cref="Point2D"/> / <see cref="Vector2D"/> / 基础类型；不引用 AutoCAD。
    /// </summary>
    public readonly struct Crosswalk : IEquatable<Crosswalk>
    {
        public const double DefaultGapWidth = 1.0;
        public const double DefaultWidth = 5.0;
        public const double DefaultStopLineDistance = 2.0;
        public const double DefaultStripeSpacing = 0.60;
        public const double DefaultStripeWidth = 0.40;

        /// <summary>所属 Leg 的索引（= <c>Intersection.Legs</c> 中的下标）。</summary>
        public int LegIndex { get; }

        /// <summary>从路缘到横道内边（L2）的净空宽度（米）。</summary>
        public double GapWidth { get; }

        /// <summary>横道总宽度（L2 到 L3，米）。</summary>
        public double Width { get; }

        /// <summary>横道外边（L3）到停止线（L4）的距离（米）。</summary>
        public double StopLineDistance { get; }

        /// <summary>条纹中心距（米）。</summary>
        public double StripeSpacing { get; }

        /// <summary>条纹实际宽度（米，沿 roadDir 方向的线宽；v1.1 绘图不使用此值，预留 v1.2 改为闭合填充）。</summary>
        public double StripeWidth { get; }

        /// <summary>Leg 左侧路缘外角点 —— 沿 <see cref="IntersectionLeg.InwardDirection"/> 前进时左手侧的起点。
        /// v1.2 起取自 <c>CornerArc(LegIndexB == i).EndPoint</c>（与修复后 <see cref="Services.Road.IntersectionDesigner.TryBuildCornerArc"/>
        /// 语义一致：EndPoint 位于 LegIndexB 左侧外边线）。</summary>
        public Point2D BaseLeft { get; }

        /// <summary>Leg 右侧路缘外角点 —— 沿 <see cref="IntersectionLeg.InwardDirection"/> 前进时右手侧的起点。
        /// v1.2 起取自 <c>CornerArc(LegIndexA == i).StartPoint</c>（StartPoint 位于 LegIndexA 右侧外边线）。</summary>
        public Point2D BaseRight { get; }

        /// <summary>横道"前进"方向（沿此方向从路缘依次到 L2 / L3 / L4）。
        /// 由 <see cref="Services.Road.CrosswalkDesigner.BuildCrosswalkForLeg"/> 传入 <see cref="IntersectionLeg.InwardDirection"/>
        /// 本身；InwardDirection 定义为"从 ApproachPoint 指向交叉口中心"，本字段沿用该向量参与偏移运算。
        /// <para>命名 "Outward" 仅为语义可读性（与 L2/L3/L4 相对于 base 点是"外延"），不代表它指向 Leg Alignment 的外侧。</para></summary>
        public Vector2D Outward { get; }

        public Crosswalk(
            int legIndex,
            Point2D baseLeft,
            Point2D baseRight,
            Vector2D outward,
            double gapWidth = DefaultGapWidth,
            double width = DefaultWidth,
            double stopLineDistance = DefaultStopLineDistance,
            double stripeSpacing = DefaultStripeSpacing,
            double stripeWidth = DefaultStripeWidth)
        {
            if (legIndex < 0) throw new ArgumentOutOfRangeException(nameof(legIndex));
            if (gapWidth < 0) throw new ArgumentOutOfRangeException(nameof(gapWidth));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (stopLineDistance < 0) throw new ArgumentOutOfRangeException(nameof(stopLineDistance));
            if (stripeSpacing <= 0) throw new ArgumentOutOfRangeException(nameof(stripeSpacing));
            if (stripeWidth <= 0) throw new ArgumentOutOfRangeException(nameof(stripeWidth));
            if (outward.IsZero(1e-9))
                throw new ArgumentException("Outward 不能是零向量", nameof(outward));

            LegIndex = legIndex;
            BaseLeft = baseLeft;
            BaseRight = baseRight;
            Outward = outward;
            GapWidth = gapWidth;
            Width = width;
            StopLineDistance = stopLineDistance;
            StripeSpacing = stripeSpacing;
            StripeWidth = stripeWidth;
        }

        // ============== 派生点（供 Designer / 单测引用，避免各处重算） ==============

        /// <summary>L2 左端 = BaseLeft + Outward · GapWidth。</summary>
        public Point2D InnerLeft => BaseLeft.Add(Outward * GapWidth);

        /// <summary>L2 右端。</summary>
        public Point2D InnerRight => BaseRight.Add(Outward * GapWidth);

        /// <summary>L3 左端 = BaseLeft + Outward · (GapWidth + Width)。</summary>
        public Point2D OuterLeft => BaseLeft.Add(Outward * (GapWidth + Width));

        /// <summary>L3 右端。</summary>
        public Point2D OuterRight => BaseRight.Add(Outward * (GapWidth + Width));

        /// <summary>L4 停止线左端 = BaseLeft + Outward · (GapWidth + Width + StopLineDistance)。</summary>
        public Point2D StopLineLeft => BaseLeft.Add(Outward * (GapWidth + Width + StopLineDistance));

        /// <summary>L4 停止线右端。</summary>
        public Point2D StopLineRight => BaseRight.Add(Outward * (GapWidth + Width + StopLineDistance));

        /// <summary>横道路幅宽度（米）= |BaseRight - BaseLeft|。</summary>
        public double RoadWidth => BaseLeft.DistanceTo(BaseRight);

        public bool Equals(Crosswalk other)
            => LegIndex == other.LegIndex
               && Math.Abs(GapWidth - other.GapWidth) < 1e-9
               && Math.Abs(Width - other.Width) < 1e-9
               && Math.Abs(StopLineDistance - other.StopLineDistance) < 1e-9
               && Math.Abs(StripeSpacing - other.StripeSpacing) < 1e-9
               && Math.Abs(StripeWidth - other.StripeWidth) < 1e-9
               && BaseLeft.IsEqualTo(other.BaseLeft, 1e-9)
               && BaseRight.IsEqualTo(other.BaseRight, 1e-9)
               && Outward.Equals(other.Outward);

        public override bool Equals(object obj) => obj is Crosswalk other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = LegIndex;
                h = (h * 397) ^ GapWidth.GetHashCode();
                h = (h * 397) ^ Width.GetHashCode();
                h = (h * 397) ^ StopLineDistance.GetHashCode();
                h = (h * 397) ^ StripeSpacing.GetHashCode();
                h = (h * 397) ^ BaseLeft.GetHashCode();
                h = (h * 397) ^ BaseRight.GetHashCode();
                return h;
            }
        }

        public override string ToString()
            => $"Crosswalk[Leg#{LegIndex}, RW={RoadWidth:F2}, W={Width:F2}, Gap={GapWidth:F2}, SL={StopLineDistance:F2}, s={StripeSpacing:F2}]";
    }
}
