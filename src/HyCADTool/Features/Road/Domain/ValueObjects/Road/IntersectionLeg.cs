using System;
using HyCAD.Geometry;

namespace HyCADTool.Domain.ValueObjects.Road
{
    /// <summary>
    /// 交叉口单臂（Leg / Approach）—— 一条进入 / 离开交叉口的 <c>Alignment</c> 端段抽象。
    ///
    /// <para><b>语义</b></para>
    /// <list type="bullet">
    /// <item>指一条 <see cref="Models.Road.Alignment"/>（通过 <see cref="AlignmentId"/> 引用）在
    /// 交叉口附近的 <b>一个端点</b>（靠近交叉口那一端）以及该端点处的入向 / 半宽 / 标签元数据。</item>
    /// <item>一条 Alignment 可以贡献 1~2 条 Leg（一个十字口两条相对臂可能同属一条直穿 Alignment）。</item>
    /// <item>"Inward Direction" 的定义：<b>从 Leg 端点指向交叉口中心</b>，与 Alignment 自然切线方向相反或相同取决于
    /// 接入的是 Alignment 起端还是终端。</item>
    /// </list>
    ///
    /// <para><b>与 AutoCAD 的解耦</b></para>
    /// 本值对象只使用 <see cref="Point2D"/> / <see cref="Vector2D"/> / <see cref="Guid"/>，
    /// 不引用 Autodesk.AutoCAD.* 任何类型。
    ///
    /// <para><b>不可变语义</b></para>
    /// 所有字段只读（readonly struct 的公共 get-only 属性），构造后不可变；
    /// 需要修改请生成新实例（<see cref="WithRadius"/> / <see cref="WithHalfWidth"/> / <see cref="WithTag"/>）。
    /// </summary>
    public readonly struct IntersectionLeg : IEquatable<IntersectionLeg>
    {
        /// <summary>本臂所属 <c>Alignment</c> 的稳定 Id。</summary>
        public Guid AlignmentId { get; }

        /// <summary>本臂端点（从 Alignment BP 起算的 raw 距离，米）。
        /// <c>0</c>=起端臂，<c>=Centerline.GetTotalLength()</c>=末端臂。</summary>
        public double ApproachRawDistance { get; }

        /// <summary>本臂端点平面坐标（= Alignment 在 <see cref="ApproachRawDistance"/> 处的 XY）。</summary>
        public Point2D ApproachPoint { get; }

        /// <summary>从 <see cref="ApproachPoint"/> <b>指向交叉口中心</b>的单位向量（入向）。
        /// 长度必须 ≈ 1（构造时不做强校验，由 Designer 保证）。</summary>
        public Vector2D InwardDirection { get; }

        /// <summary>本臂半宽（米）。用于转角圆弧计算时决定路缘外边线的偏移距离。
        /// 默认 <see cref="DefaultHalfWidth"/> = 7.5（= 单向 3 车道 × 3.5 m 的粗略值）。</summary>
        public double HalfWidth { get; }

        /// <summary>用户可选标签（如 "North" / "东接线"）；可为 null / 空。</summary>
        public string Tag { get; }

        /// <summary>Leg 默认半宽（m）。</summary>
        public const double DefaultHalfWidth = 7.5;

        public IntersectionLeg(
            Guid alignmentId,
            double approachRawDistance,
            Point2D approachPoint,
            Vector2D inwardDirection,
            double halfWidth = DefaultHalfWidth,
            string tag = null)
        {
            if (halfWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(halfWidth), halfWidth, "HalfWidth 必须 > 0");
            if (inwardDirection.IsZero(1e-9))
                throw new ArgumentException("InwardDirection 不能是零向量", nameof(inwardDirection));

            AlignmentId = alignmentId;
            ApproachRawDistance = approachRawDistance;
            ApproachPoint = approachPoint;
            InwardDirection = inwardDirection;
            HalfWidth = halfWidth;
            Tag = tag;
        }

        /// <summary>生成"修改半宽"的新实例。</summary>
        public IntersectionLeg WithHalfWidth(double halfWidth)
            => new IntersectionLeg(AlignmentId, ApproachRawDistance, ApproachPoint, InwardDirection, halfWidth, Tag);

        /// <summary>生成"修改标签"的新实例。</summary>
        public IntersectionLeg WithTag(string tag)
            => new IntersectionLeg(AlignmentId, ApproachRawDistance, ApproachPoint, InwardDirection, HalfWidth, tag);

        /// <summary>生成"修改入向（重新单位化）"的新实例。</summary>
        public IntersectionLeg WithInward(Vector2D inward)
            => new IntersectionLeg(AlignmentId, ApproachRawDistance, ApproachPoint, inward, HalfWidth, Tag);

        /// <summary>本臂入向的极角（弧度，范围 [-π, π]），供 Designer 按方位角排序 Legs 使用。</summary>
        public double InwardAngle => Math.Atan2(InwardDirection.Y, InwardDirection.X);

        public bool Equals(IntersectionLeg other)
            => AlignmentId == other.AlignmentId
               && Math.Abs(ApproachRawDistance - other.ApproachRawDistance) < 1e-9
               && ApproachPoint.IsEqualTo(other.ApproachPoint, 1e-9)
               && InwardDirection.Equals(other.InwardDirection)
               && Math.Abs(HalfWidth - other.HalfWidth) < 1e-9
               && string.Equals(Tag, other.Tag, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is IntersectionLeg other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = AlignmentId.GetHashCode();
                h = (h * 397) ^ ApproachRawDistance.GetHashCode();
                h = (h * 397) ^ ApproachPoint.GetHashCode();
                h = (h * 397) ^ InwardDirection.GetHashCode();
                h = (h * 397) ^ HalfWidth.GetHashCode();
                h = (h * 397) ^ (Tag?.GetHashCode() ?? 0);
                return h;
            }
        }

        public override string ToString()
            => $"Leg[{Tag ?? "-"} @ {ApproachPoint}, w={HalfWidth:F2}, θ={InwardAngle * 180 / Math.PI:F1}°]";
    }
}
