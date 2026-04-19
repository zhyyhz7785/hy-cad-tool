using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// v1.1 人行横道设计器（纯 C#，替代旧 <c>Infrastructure.AutoCAD.Services.CrosswalkService</c>
    /// 的"条纹生成"部分；"几何分析"部分由 <see cref="Intersection"/> 聚合天然提供，不再需要）。
    ///
    /// <para><b>职责</b></para>
    /// <list type="number">
    /// <item><see cref="BuildCrosswalkForLeg"/>：给定 <see cref="Intersection"/> 与 <paramref name="legIndex"/>，
    /// 从相邻两 <see cref="CornerArc"/> 的切点自动推导 BaseLeft / BaseRight，构造 <see cref="Crosswalk"/>；</item>
    /// <item><see cref="LayoutForAllLegs"/>：为交叉口每条 Leg 生成一个横道；</item>
    /// <item><see cref="ComputeStripes"/>：按 <see cref="Crosswalk.StripeSpacing"/> 在 L2/L3 之间均匀分布条纹；</item>
    /// <item><see cref="ComputeStopLine"/>：给出停止线两端点。</item>
    /// </list>
    ///
    /// <para><b>条纹分布规则（与旧 <c>CrosswalkService.DrawCrosswalkForArm</c> Step 2 一致，去掉弧线裁切）</b></para>
    /// <list type="bullet">
    /// <item>令 roadDir = (BaseRight − BaseLeft).Normalize()，条纹 i 的内端 = InnerLeft + roadDir · (i·spacing)；</item>
    /// <item>仅保留 0 ≤ i·spacing &lt; RoadWidth 的条纹（首条从 0 开始，尾条不越过 BaseRight）；</item>
    /// <item>每条条纹的外端 = 对应内端 + Outward · Width。</item>
    /// </list>
    ///
    /// <para><b>弧线裁切（v1.2 计划）</b></para>
    /// 旧服务的条纹双端弧线裁切（<c>RayHitArc</c>）在 v1.1 中暂缓：
    /// <list type="bullet">
    /// <item>当 <see cref="Crosswalk.GapWidth"/> ≥ <see cref="CornerArc.Radius"/> 时天然无需裁切（L2/L3 都在弧外）；</item>
    /// <item>GB 50763 / CJJ 37 的"缘石坡道 + 盲道"要求坡道前有净空，工程上 GapWidth 通常 ≥ 1.0 m 即避开弧；</item>
    /// <item>极端小 R 交叉口（城市支路 R=5m）需要裁切 → v1.2 引入。</item>
    /// </list>
    /// </summary>
    public static class CrosswalkDesigner
    {
        /// <summary>
        /// 为 <paramref name="intersection"/> 的第 <paramref name="legIndex"/> 条 Leg 构造 <see cref="Crosswalk"/>。
        /// 若 Leg 的左 / 右侧缺 CornerArc（非相邻 / 几何失败）则退化使用 Leg 侧向锚点
        /// （<c>ApproachPoint ± Perpendicular(uA) · HalfWidth</c>）。
        /// </summary>
        public static Crosswalk BuildCrosswalkForLeg(
            Intersection intersection,
            int legIndex,
            double gapWidth = Crosswalk.DefaultGapWidth,
            double width = Crosswalk.DefaultWidth,
            double stopLineDistance = Crosswalk.DefaultStopLineDistance,
            double stripeSpacing = Crosswalk.DefaultStripeSpacing,
            double stripeWidth = Crosswalk.DefaultStripeWidth)
        {
            if (intersection == null) throw new ArgumentNullException(nameof(intersection));
            if (legIndex < 0 || legIndex >= intersection.Legs.Count)
                throw new ArgumentOutOfRangeException(nameof(legIndex));

            var leg = intersection.Legs[legIndex];
            var perp = leg.InwardDirection.Perpendicular();

            // 默认锚点（退化值）：沿 +perp 和 -perp 偏 HalfWidth
            Point2D baseLeft = leg.ApproachPoint.Add(perp * leg.HalfWidth);
            Point2D baseRight = leg.ApproachPoint.Add(perp * -leg.HalfWidth);

            // 尝试取 CornerArc 切点替换：
            //   Left  = CornerArc(LegIndexA == legIndex).StartPoint
            //   Right = CornerArc(LegIndexB == legIndex).EndPoint
            foreach (var ca in intersection.CornerArcs)
            {
                if (ca.LegIndexA == legIndex) baseLeft = ca.StartPoint;
                if (ca.LegIndexB == legIndex) baseRight = ca.EndPoint;
            }

            return new Crosswalk(
                legIndex,
                baseLeft,
                baseRight,
                leg.InwardDirection,
                gapWidth,
                width,
                stopLineDistance,
                stripeSpacing,
                stripeWidth);
        }

        /// <summary>
        /// 对 <paramref name="intersection"/> 的每条 Leg 生成一个 <see cref="Crosswalk"/>。
        /// </summary>
        public static IReadOnlyList<Crosswalk> LayoutForAllLegs(
            Intersection intersection,
            double gapWidth = Crosswalk.DefaultGapWidth,
            double width = Crosswalk.DefaultWidth,
            double stopLineDistance = Crosswalk.DefaultStopLineDistance,
            double stripeSpacing = Crosswalk.DefaultStripeSpacing,
            double stripeWidth = Crosswalk.DefaultStripeWidth)
        {
            if (intersection == null) throw new ArgumentNullException(nameof(intersection));
            var list = new List<Crosswalk>(intersection.Legs.Count);
            for (int i = 0; i < intersection.Legs.Count; i++)
            {
                list.Add(BuildCrosswalkForLeg(
                    intersection, i, gapWidth, width, stopLineDistance, stripeSpacing, stripeWidth));
            }
            return list;
        }

        /// <summary>
        /// 条纹布局：沿 roadDir（= BaseLeft → BaseRight）以 <see cref="Crosswalk.StripeSpacing"/> 为中心距分布，
        /// 首条从 i=0（BaseLeft 侧）开始，末条不越过 BaseRight。
        /// </summary>
        public static IReadOnlyList<CrosswalkStripe> ComputeStripes(Crosswalk cw)
        {
            double roadWidth = cw.RoadWidth;
            if (roadWidth <= 1e-6 || cw.StripeSpacing <= 1e-6) return Array.Empty<CrosswalkStripe>();

            var widthVec = new Vector2D(cw.BaseRight.X - cw.BaseLeft.X, cw.BaseRight.Y - cw.BaseLeft.Y);
            if (!widthVec.TryNormalize(out var roadDir, 1e-9)) return Array.Empty<CrosswalkStripe>();

            var innerLeft = cw.InnerLeft;
            var outerLeft = cw.OuterLeft;

            int count = (int)Math.Floor(roadWidth / cw.StripeSpacing) + 1;
            var stripes = new List<CrosswalkStripe>(count);
            for (int i = 0; i < count; i++)
            {
                double t = i * cw.StripeSpacing;
                if (t > roadWidth + 1e-9) break;
                var inner = innerLeft.Add(roadDir * t);
                var outer = outerLeft.Add(roadDir * t);
                stripes.Add(new CrosswalkStripe(i, inner, outer));
            }
            return stripes;
        }

        /// <summary>停止线两端点（L4 左 / 右）。</summary>
        public static (Point2D Left, Point2D Right) ComputeStopLine(Crosswalk cw)
            => (cw.StopLineLeft, cw.StopLineRight);
    }
}
