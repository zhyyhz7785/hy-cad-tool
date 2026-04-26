using System;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Domain.ValueObjects.Road
{
    /// <summary>
    /// 人行横道单根斑马线条纹（readonly struct，纯派生量，不入 JSON）。
    ///
    /// <para><b>语义</b></para>
    /// 从 <see cref="From"/>（L2 内边上的一点）沿 <see cref="Crosswalk.Outward"/> 方向延伸
    /// 到 <see cref="To"/>（L3 外边上的对应点）。条纹方向理论上等于 <c>Outward</c>。
    /// </summary>
    public readonly struct CrosswalkStripe : IEquatable<CrosswalkStripe>
    {
        /// <summary>条纹在横道内的序号（从 Leg 左侧起 0，1，2 ...）。</summary>
        public int Index { get; }

        /// <summary>条纹内端（L2 内边线上的点）。</summary>
        public Point2D From { get; }

        /// <summary>条纹外端（L3 外边线上的点）。</summary>
        public Point2D To { get; }

        public CrosswalkStripe(int index, Point2D from, Point2D to)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            Index = index;
            From = from;
            To = to;
        }

        /// <summary>条纹长度（米）。</summary>
        public double Length => From.DistanceTo(To);

        public bool Equals(CrosswalkStripe other)
            => Index == other.Index && From.IsEqualTo(other.From, 1e-9) && To.IsEqualTo(other.To, 1e-9);

        public override bool Equals(object obj) => obj is CrosswalkStripe other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Index;
                h = (h * 397) ^ From.GetHashCode();
                h = (h * 397) ^ To.GetHashCode();
                return h;
            }
        }

        public override string ToString()
            => $"Stripe#{Index}[{From}→{To}, L={Length:F2}]";
    }
}
