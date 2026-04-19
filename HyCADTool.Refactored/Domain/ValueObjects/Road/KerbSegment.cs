using System;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;

namespace HyCADTool.Refactored.Domain.ValueObjects.Road
{
    /// <summary>路缘外边线所在 Leg 的左 / 右侧（沿 Leg.InwardDirection 前进时的左右手）。</summary>
    public enum KerbSide
    {
        /// <summary>沿 InwardDirection 前进时的左手侧 = +Perpendicular(uA)·HalfWidth 方向。</summary>
        Left = 0,

        /// <summary>沿 InwardDirection 前进时的右手侧 = -Perpendicular(uA)·HalfWidth 方向。</summary>
        Right = 1,
    }

    /// <summary>
    /// 路缘外边线直段 —— <see cref="Models.Road.Intersection"/> 某条 Leg 在某一侧的
    /// "从 <see cref="IntersectionLeg.ApproachPoint"/> 的侧向锚点延伸到相邻
    /// <see cref="CornerArc"/> 切点"的一小段直线。
    ///
    /// <para><b>几何定义</b></para>
    /// <list type="bullet">
    /// <item><see cref="From"/> = <c>Leg.ApproachPoint ± Perpendicular(uA)·HalfWidth</c>（±取决于 <see cref="Side"/>）；</item>
    /// <item><see cref="To"/> = 同 Leg 同侧相邻 CornerArc 的切点（Left → CornerArc[i].StartPoint；Right → CornerArc[(i-1+N)%N].EndPoint）；</item>
    /// <item>方向与 Leg <c>InwardDirection</c> 共线（理论上反平行），供视觉检查 / 单测断言。</item>
    /// </list>
    ///
    /// <para><b>派生语义</b></para>
    /// 本段是 <see cref="IntersectionLeg"/> + <see cref="CornerArc"/> 的纯派生量，
    /// <b>不参与 JSON 持久化</b>；由 <see cref="Services.Road.KerbChainDesigner"/> 每次按需计算。
    /// </summary>
    public readonly struct KerbSegment : IEquatable<KerbSegment>
    {
        /// <summary>所属 Leg 在 <c>Intersection.Legs</c> 的索引。</summary>
        public int LegIndex { get; }

        /// <summary>侧位 —— Leg 左手侧 / 右手侧。</summary>
        public KerbSide Side { get; }

        /// <summary>锚点（路缘线上 ApproachPoint 的侧向投影）。</summary>
        public Point2D From { get; }

        /// <summary>相邻 CornerArc 切点。</summary>
        public Point2D To { get; }

        public KerbSegment(int legIndex, KerbSide side, Point2D from, Point2D to)
        {
            if (legIndex < 0) throw new ArgumentOutOfRangeException(nameof(legIndex));
            LegIndex = legIndex;
            Side = side;
            From = from;
            To = to;
        }

        /// <summary>直段长度（m）。</summary>
        public double Length => From.DistanceTo(To);

        public bool Equals(KerbSegment other)
            => LegIndex == other.LegIndex
               && Side == other.Side
               && From.IsEqualTo(other.From, 1e-9)
               && To.IsEqualTo(other.To, 1e-9);

        public override bool Equals(object obj) => obj is KerbSegment other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = LegIndex;
                h = (h * 397) ^ (int)Side;
                h = (h * 397) ^ From.GetHashCode();
                h = (h * 397) ^ To.GetHashCode();
                return h;
            }
        }

        public override string ToString()
            => $"Kerb[Leg#{LegIndex} {Side}, {From}→{To}, L={Length:F2}]";
    }
}
