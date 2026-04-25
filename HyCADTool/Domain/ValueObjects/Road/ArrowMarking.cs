using System;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Domain.ValueObjects.Road
{
    /// <summary>
    /// 导流箭头类型（GB 5768-2009 §5.3 导向箭头，共 6 型）。
    /// </summary>
    public enum ArrowMarkingKind
    {
        /// <summary>直行。</summary>
        Straight = 0,
        /// <summary>左转（90° 左拐）。</summary>
        Left = 1,
        /// <summary>右转（90° 右拐，= Left 的 y 镜像）。</summary>
        Right = 2,
        /// <summary>直行 + 左转（组合箭头）。</summary>
        StraightLeft = 3,
        /// <summary>直行 + 右转。</summary>
        StraightRight = 4,
        /// <summary>左转 + 右转（双向）。</summary>
        LeftRight = 5,
    }

    /// <summary>
    /// 路面标线：导流箭头（GB 5768-2009 §5.3）。
    ///
    /// <para><b>坐标系</b></para>
    /// <list type="bullet">
    /// <item><see cref="Anchor"/>：箭尾中点（local origin）；</item>
    /// <item><see cref="Direction"/>：箭头主方向（local +X，单位向量；由 <see cref="Services.Road.ArrowMarkingDesigner.Create"/>
    /// 自动归一化为 <see cref="Vector2D.UnitX"/> 若退化）；</item>
    /// <item>local +Y = <see cref="Vector2D.Perpendicular"/>（主方向逆时针 90°，即车辆行进方向的"左侧"）；</item>
    /// <item><see cref="Length"/>：主箭身总长（anchor → 直行箭尖 的 local X 距离），GB 推荐 3 / 6 / 9 m 三档按限速取。</item>
    /// </list>
    ///
    /// <para><b>Id</b></para>
    /// 一个 <see cref="ArrowMarking"/> 映射为 1~2 个闭合多边形（Straight/Left/Right = 1 个；
    /// StraightLeft/StraightRight/LeftRight = 2 个），它们共享同一 <see cref="Id"/>（HY_ROAD Xdata KIND=<c>ArrowMarking</c>）。
    /// </summary>
    public readonly struct ArrowMarking : IEquatable<ArrowMarking>
    {
        /// <summary>默认主长度（米）—— GB 5768-2009 §5.3 中档（60 km/h 档）。</summary>
        public const double DefaultLength = 6.0;

        public Guid Id { get; }
        public Point2D Anchor { get; }
        public Vector2D Direction { get; }
        public ArrowMarkingKind Kind { get; }
        public double Length { get; }

        public ArrowMarking(Guid id, Point2D anchor, Vector2D direction, ArrowMarkingKind kind, double length = DefaultLength)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length), length, "Length 必须 > 0");
            if (!direction.TryNormalize(out var unit, 1e-9))
                throw new ArgumentException("Direction 不能为零向量", nameof(direction));

            Id = id;
            Anchor = anchor;
            Direction = unit;
            Kind = kind;
            Length = length;
        }

        public bool Equals(ArrowMarking other)
            => Id == other.Id
               && Kind == other.Kind
               && Anchor.IsEqualTo(other.Anchor, 1e-9)
               && Direction.Equals(other.Direction)
               && Math.Abs(Length - other.Length) < 1e-9;

        public override bool Equals(object obj) => obj is ArrowMarking other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Id.GetHashCode();
                h = (h * 397) ^ (int)Kind;
                h = (h * 397) ^ Anchor.GetHashCode();
                h = (h * 397) ^ Direction.GetHashCode();
                h = (h * 397) ^ Length.GetHashCode();
                return h;
            }
        }

        public override string ToString()
            => $"ArrowMarking[{Kind}, {Id:N}, Anchor={Anchor}, Dir={Direction}, L={Length:F2}]";
    }
}
