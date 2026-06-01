using System;
using System.Collections.Generic;
using HyCADTool.Domain.Models.Road;
using HyCAD.Geometry;
using HyCADTool.Domain.ValueObjects.Road;

namespace HyCADTool.Domain.Services.Road
{
    /// <summary>
    /// 盲道设计器（<b>纯函数 / 无状态</b>）—— 基于已完成 Legs + CornerArcs + CurbRamps 的
    /// <see cref="Intersection"/>，自动布置 <see cref="TactilePavingKind.Stop"/>（提示盲道）
    /// 与 <see cref="TactilePavingKind.Advance"/>（行进盲道）。
    ///
    /// <para><b>布置策略（默认）</b></para>
    /// <list type="bullet">
    /// <item>每个 <see cref="CurbRamp"/> 前沿紧贴布置 1 段 <b>Stop 提示盲道</b>：
    /// 中心线 = FrontLeft → FrontRight（沿前沿切线），宽度 <see cref="TactilePaving.DefaultStopWidth"/>；</item>
    /// <item>每条 <see cref="IntersectionLeg"/> 沿<b>人行道一侧路缘</b>向外延伸一段 <b>Advance 行进盲道</b>：
    /// 从 ApproachPoint 沿 −InwardDirection 退 <paramref name="advanceLengthFromApproach"/> m，
    /// 并沿 <see cref="TactilePaving.MinDistanceFromKerb"/> 偏离路缘；</item>
    /// <item>Advance 行进盲道<b>沿两侧</b>分别布置（左 / 右人行道各一条）。</item>
    /// </list>
    ///
    /// <para><b>Domain 纯净</b></para>
    /// 只依赖 <see cref="Point2D"/> / <see cref="Vector2D"/> / Domain 值对象；不引用 AutoCAD 任何类型。
    /// </summary>
    public static class TactilePavingDesigner
    {
        /// <summary>几何容差（米）。</summary>
        public const double DefaultTolerance = 1e-9;

        /// <summary>行进盲道默认自 Leg 端点向外（远离交叉口）延伸长度（米）。</summary>
        public const double DefaultAdvanceLengthFromApproach = 10.0;

        /// <summary>提示盲道默认"深度"（垂直前沿方向的长度，m）—— 略大于 Stop 默认宽度的本身，确保覆盖人行道宽度。</summary>
        public const double DefaultStopDepth = 0.60;

        /// <summary>
        /// 给定 <see cref="Intersection"/>（需先 <see cref="CurbRampDesigner.LayoutRampsOnCornerArcs"/>），
        /// 自动布置 Stop + Advance 盲道并写回 <see cref="Intersection.TactilePavings"/>（覆盖）。
        /// </summary>
        public static IReadOnlyList<TactilePaving> LayoutTactilePaving(
            Intersection intersection,
            double stopWidth = TactilePaving.DefaultStopWidth,
            double advanceWidth = TactilePaving.DefaultAdvanceWidth,
            double advanceLengthFromApproach = DefaultAdvanceLengthFromApproach)
        {
            if (intersection == null) throw new ArgumentNullException(nameof(intersection));
            if (stopWidth <= 0) throw new ArgumentOutOfRangeException(nameof(stopWidth));
            if (advanceWidth <= 0) throw new ArgumentOutOfRangeException(nameof(advanceWidth));
            if (advanceLengthFromApproach <= 0) throw new ArgumentOutOfRangeException(nameof(advanceLengthFromApproach));

            var list = new List<TactilePaving>();

            // Stop 盲道：每个 CurbRamp 前沿一条（中心线沿前沿切线，覆盖坡道宽度）。
            for (int i = 0; i < intersection.CurbRamps.Count; i++)
            {
                if (TryBuildStopForRamp(intersection.CurbRamps[i], stopWidth, out var stop))
                    list.Add(stop);
            }

            // Advance 盲道：每条 Leg 的左右两侧各一条（沿 -InwardDirection 退一段）。
            for (int i = 0; i < intersection.Legs.Count; i++)
            {
                foreach (var adv in BuildAdvanceForLeg(intersection.Legs[i], advanceWidth, advanceLengthFromApproach))
                    list.Add(adv);
            }

            intersection.TactilePavings = list;
            intersection.LastModifiedUtc = DateTime.UtcNow;
            return list;
        }

        /// <summary>
        /// 对单个 <see cref="CurbRamp"/> 构造其前沿上的 Stop 提示盲道（中心线 = FrontLeft → FrontRight）。
        /// </summary>
        internal static bool TryBuildStopForRamp(CurbRamp ramp, double stopWidth, out TactilePaving paving)
        {
            paving = null;
            if (ramp.Width <= DefaultTolerance) return false;

            paving = new TactilePaving
            {
                Kind = TactilePavingKind.Stop,
                Centerline = new List<Point2D> { ramp.FrontLeft, ramp.FrontRight },
                Width = stopWidth,
                CornerArcIndex = ramp.CornerArcIndex,
            };
            return true;
        }

        /// <summary>
        /// 对单条 <see cref="IntersectionLeg"/> 构造左 / 右两条 Advance 行进盲道。
        /// 左侧人行道中心线 ≈ ApproachPoint + perp·(halfWidth + 偏距)   <b>→</b>   再沿 −Inward 方向延伸 L。
        /// 右侧同理，perp 取反号。
        /// </summary>
        internal static IEnumerable<TactilePaving> BuildAdvanceForLeg(
            IntersectionLeg leg,
            double advanceWidth,
            double lengthFromApproach)
        {
            if (leg.HalfWidth <= DefaultTolerance) yield break;
            if (leg.InwardDirection.IsZero(DefaultTolerance)) yield break;

            var inward = leg.InwardDirection;
            var perp = inward.Perpendicular(); // 逆时针旋 90°，指向 Inward 前进的左手侧
            var back = -inward;                 // 从 ApproachPoint 向外（远离交叉口）方向

            // 人行道中心线距路缘 ≈ halfWidth + MinDistanceFromKerb + advanceWidth/2；
            // 这里保守取 halfWidth + MinDistanceFromKerb（= 紧贴路缘外第一条盲道中心线）。
            double offset = leg.HalfWidth + TactilePaving.MinDistanceFromKerb;

            // 左侧（沿 Inward 前进的左手侧 = +perp）
            var leftStart = leg.ApproachPoint.Add(perp * offset);
            var leftEnd = leftStart.Add(back * lengthFromApproach);
            yield return new TactilePaving
            {
                Kind = TactilePavingKind.Advance,
                Centerline = new List<Point2D> { leftStart, leftEnd },
                Width = advanceWidth,
                CornerArcIndex = -1,
            };

            // 右侧（−perp）
            var rightStart = leg.ApproachPoint.Add(perp * (-offset));
            var rightEnd = rightStart.Add(back * lengthFromApproach);
            yield return new TactilePaving
            {
                Kind = TactilePavingKind.Advance,
                Centerline = new List<Point2D> { rightStart, rightEnd },
                Width = advanceWidth,
                CornerArcIndex = -1,
            };
        }
    }
}
