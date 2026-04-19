using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// 车道分界标线几何服务（纯函数 / 无状态）。
    ///
    /// <para><b>职责</b></para>
    /// <list type="bullet">
    /// <item><see cref="Create"/>：构造 <see cref="LaneMarking"/>；</item>
    /// <item><see cref="ComputeDashSegments"/>：把一条 <see cref="LaneMarkingKind.DashedWhite"/> 展开为多段"画段-间隔"（首段起于 Start，末段可能被 End 截断）；</item>
    /// <item><see cref="ComputeDoubleOffsets"/>：把一条 <see cref="LaneMarkingKind.DoubleYellow"/> 展开为两条平行线（与 Direction 的 <see cref="Vector2D.Perpendicular"/> 两侧各偏移 spacing/2）；</item>
    /// <item><see cref="ExpandSegments"/>：统一入口——按 <see cref="LaneMarking.Kind"/> 自动选择上述之一 / 单实线原样返回，返回 <see cref="IReadOnlyList{T}"/> 的 (Start, End) 对。</item>
    /// </list>
    ///
    /// <para><b>不涉及</b></para>
    /// 不涉及颜色、图层、Xdata、AutoCAD；上述由 Infrastructure 层在绘制时决定。
    /// </summary>
    public static class LaneMarkingDesigner
    {
        /// <summary>按两端点 + 类型构造 <see cref="LaneMarking"/>（Id 自动生成）。</summary>
        public static LaneMarking Create(
            Point2D start,
            Point2D end,
            LaneMarkingKind kind,
            double stripeWidth = LaneMarking.DefaultStripeWidth)
        {
            return new LaneMarking(Guid.NewGuid(), start, end, kind, stripeWidth);
        }

        /// <summary>
        /// 把 <see cref="LaneMarkingKind.DashedWhite"/> 的一条虚线展开为若干"画段"。
        ///
        /// <para><b>算法</b></para>
        /// <code>t ∈ [0, L]：dash = [t, t+dashLen]，gap = [t+dashLen, t+dashLen+gapLen]</code>
        /// <para>末段 dash 如果越过 End，会被 <c>L</c> 截断（保留可见部分；若被截后长度 &lt; <paramref name="minDashLength"/>，则整段丢弃，避免尾部出现一个"大点"）。</para>
        /// </summary>
        public static IReadOnlyList<(Point2D Start, Point2D End)> ComputeDashSegments(
            LaneMarking marking,
            double dashLength = LaneMarking.DefaultDashLength,
            double gapLength = LaneMarking.DefaultGapLength,
            double minDashLength = 0.5)
        {
            if (dashLength <= 0) throw new ArgumentOutOfRangeException(nameof(dashLength), dashLength, "> 0");
            if (gapLength <= 0) throw new ArgumentOutOfRangeException(nameof(gapLength), gapLength, "> 0");
            if (minDashLength < 0) throw new ArgumentOutOfRangeException(nameof(minDashLength), minDashLength, "≥ 0");

            var dir = marking.Direction;
            double L = marking.Length;
            double step = dashLength + gapLength;
            var result = new List<(Point2D, Point2D)>();

            for (double t = 0; t < L - 1e-9; t += step)
            {
                double end = Math.Min(t + dashLength, L);
                if (end - t < minDashLength - 1e-9) continue;
                var p1 = new Point2D(marking.Start.X + dir.X * t, marking.Start.Y + dir.Y * t);
                var p2 = new Point2D(marking.Start.X + dir.X * end, marking.Start.Y + dir.Y * end);
                result.Add((p1, p2));
            }
            return result;
        }

        /// <summary>
        /// 把 <see cref="LaneMarkingKind.DoubleYellow"/> 的一条双黄线展开为两条平行线（中线偏移 ±spacing/2）。
        ///
        /// <para><b>几何</b></para>
        /// 法线 <c>n = Direction.Perpendicular()</c>（逆时针 90°，左手法向）；
        /// <see cref="LeftStart"/> = Start + n·(spacing/2)、<see cref="RightStart"/> = Start − n·(spacing/2)。
        /// </summary>
        public static (Point2D LeftStart, Point2D LeftEnd, Point2D RightStart, Point2D RightEnd)
            ComputeDoubleOffsets(LaneMarking marking, double spacing = LaneMarking.DefaultDoubleSpacing)
        {
            if (spacing <= 0) throw new ArgumentOutOfRangeException(nameof(spacing), spacing, "> 0");

            var dir = marking.Direction;
            var n = dir.Perpendicular();
            double h = spacing / 2.0;

            var ls = new Point2D(marking.Start.X + n.X * h, marking.Start.Y + n.Y * h);
            var le = new Point2D(marking.End.X + n.X * h, marking.End.Y + n.Y * h);
            var rs = new Point2D(marking.Start.X - n.X * h, marking.Start.Y - n.Y * h);
            var re = new Point2D(marking.End.X - n.X * h, marking.End.Y - n.Y * h);

            return (ls, le, rs, re);
        }

        /// <summary>
        /// 统一展开：按 <see cref="LaneMarking.Kind"/> 选择 <see cref="ComputeDashSegments"/> / <see cref="ComputeDoubleOffsets"/> / 单段返回。
        ///
        /// <para><b>返回约定</b></para>
        /// <list type="bullet">
        /// <item><see cref="LaneMarkingKind.SolidWhite"/> / <see cref="LaneMarkingKind.SolidYellow"/>：返回 1 段（= Start→End）；</item>
        /// <item><see cref="LaneMarkingKind.DashedWhite"/>：返回若干段（每段即一个"画段"）；</item>
        /// <item><see cref="LaneMarkingKind.DoubleYellow"/>：返回 2 段（左 + 右平行线）。</item>
        /// </list>
        /// </summary>
        public static IReadOnlyList<(Point2D Start, Point2D End)> ExpandSegments(
            LaneMarking marking,
            double dashLength = LaneMarking.DefaultDashLength,
            double gapLength = LaneMarking.DefaultGapLength,
            double doubleSpacing = LaneMarking.DefaultDoubleSpacing)
        {
            switch (marking.Kind)
            {
                case LaneMarkingKind.DashedWhite:
                    return ComputeDashSegments(marking, dashLength, gapLength);

                case LaneMarkingKind.DoubleYellow:
                {
                    var (ls, le, rs, re) = ComputeDoubleOffsets(marking, doubleSpacing);
                    return new[] { (ls, le), (rs, re) };
                }

                case LaneMarkingKind.SolidWhite:
                case LaneMarkingKind.SolidYellow:
                default:
                    return new[] { (marking.Start, marking.End) };
            }
        }
    }
}
