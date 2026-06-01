using System;
using System.Collections.Generic;
using HyCADTool.Domain.Models.Road;
using HyCAD.Geometry;
using HyCADTool.Domain.ValueObjects.Road;

namespace HyCADTool.Domain.Services.Road
{
    /// <summary>
    /// v1.1 人行横道设计器（纯 C#，替代旧 <c>HyCADTool.Shared.AutoCAD.Services.CrosswalkService</c>
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
    /// <para><b>弧线裁切（v1.2）</b></para>
    /// 当 Crosswalk 的 L2/L3（条纹两端）落入 <see cref="CornerArc"/> 凸出的范围内时，条纹会与弧相交。
    /// 旧 <c>CrosswalkService.DrawCrosswalkForArm</c> 的 <c>RayHitArc</c> 逻辑已迁移到本类的
    /// <see cref="ClipStripeByCornerArcs"/> 与 <see cref="ComputeStripesClipped"/>：
    /// <list type="bullet">
    /// <item>对每条条纹 <c>From → To</c> 方向做射线测试；最近的弧交点把条纹的对应端点替换（裁短）；</item>
    /// <item>裁短后如果剩余长度 &lt; <see cref="DefaultMinStripeLength"/>（默认 0.05 m）视为被完全吃掉，丢弃；</item>
    /// <item>算法兼容 v1.2 后的"凸向外侧"几何与 v1.1 的"镜像"几何 —— 只依赖 <see cref="CornerArc"/> 的
    /// <see cref="CornerArc.Center"/> / <see cref="CornerArc.Radius"/> / <see cref="CornerArc.StartAngle"/> / <see cref="CornerArc.EndAngle"/>。</item>
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

            // 退化锚点：沿 ±Perpendicular(InwardDirection) 偏 HalfWidth（InwardDirection = "ApproachPoint → 交叉口中心"）。
            // 若相邻 CornerArc 存在，下方会用弧切点覆盖本默认值。
            Point2D baseLeft = leg.ApproachPoint.Add(perp * leg.HalfWidth);
            Point2D baseRight = leg.ApproachPoint.Add(perp * -leg.HalfWidth);

            // 尝试取 CornerArc 切点替换（v1.2 起与修复后的 IntersectionDesigner 语义对齐）：
            //   Left  = CornerArc(LegIndexB == legIndex).EndPoint   （EndPoint 位于 LegIndexB 的 +perp 左侧外边线）
            //   Right = CornerArc(LegIndexA == legIndex).StartPoint （StartPoint 位于 LegIndexA 的 −perp 右侧外边线）
            foreach (var ca in intersection.CornerArcs)
            {
                if (ca.LegIndexB == legIndex) baseLeft = ca.EndPoint;
                if (ca.LegIndexA == legIndex) baseRight = ca.StartPoint;
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

        // =====================================================================
        //  v1.2 弧线裁切（迁自旧 CrosswalkService.RayHitArc）
        // =====================================================================

        /// <summary>条纹裁短后若剩余长度小于此值，则视为被弧线完全遮挡（米）。</summary>
        public const double DefaultMinStripeLength = 0.05;

        /// <summary>
        /// 对单条 <see cref="CrosswalkStripe"/> 做两端弧线裁切：
        /// <list type="number">
        /// <item>从 <see cref="CrosswalkStripe.From"/> 沿 <c>From→To</c> 方向射线测试，若有 <see cref="CornerArc"/>
        /// 的命中点落在当前段内，则把 <c>To</c> 推到最近交点；</item>
        /// <item>从（裁切后的）<c>To</c> 沿反向射线测试，若命中弧交点，则把 <c>From</c> 推到最近交点。</item>
        /// </list>
        /// 任何一端裁切后如果剩余长度 &lt; <paramref name="minLength"/> 返回 <c>null</c>（条纹被完全吃掉）。
        /// </summary>
        /// <param name="stripe">原条纹。</param>
        /// <param name="arcs">用于裁切的弧列表（典型 = <see cref="Models.Road.Intersection.CornerArcs"/>）。</param>
        /// <param name="minLength">最小保留长度（米）。</param>
        /// <returns>裁切后的 <see cref="CrosswalkStripe"/>，或 <c>null</c>。</returns>
        public static CrosswalkStripe? ClipStripeByCornerArcs(
            CrosswalkStripe stripe,
            IEnumerable<CornerArc> arcs,
            double minLength = DefaultMinStripeLength)
        {
            if (arcs == null) throw new ArgumentNullException(nameof(arcs));

            var from = stripe.From;
            var to = stripe.To;
            var dirVec = new Vector2D(to.X - from.X, to.Y - from.Y);
            double fullLen = dirVec.Length;
            if (fullLen < minLength) return null;
            if (!dirVec.TryNormalize(out var dir, 1e-9)) return null;

            // 1) 裁切 To 端：从 From 沿 +dir 发射，找最近命中 < fullLen
            double bestTo = fullLen;
            foreach (var arc in arcs)
            {
                if (TryRayArcIntersect(from, dir, arc, out var hit))
                {
                    double t = from.DistanceTo(hit);
                    if (t > 1e-4 && t < bestTo) bestTo = t;
                }
            }
            var newTo = from.Add(dir * bestTo);
            double afterToLen = from.DistanceTo(newTo);
            if (afterToLen < minLength) return null;

            // 2) 裁切 From 端：从 newTo 沿 −dir 发射，找最近命中 < afterToLen
            var negDir = new Vector2D(-dir.X, -dir.Y);
            double bestFrom = afterToLen;
            foreach (var arc in arcs)
            {
                if (TryRayArcIntersect(newTo, negDir, arc, out var hit))
                {
                    double t = newTo.DistanceTo(hit);
                    if (t > 1e-4 && t < bestFrom) bestFrom = t;
                }
            }
            var newFrom = newTo.Add(negDir * bestFrom);
            if (newFrom.DistanceTo(newTo) < minLength) return null;

            return new CrosswalkStripe(stripe.Index, newFrom, newTo);
        }

        /// <summary>
        /// 先 <see cref="ComputeStripes"/> 生成基础条纹，再对每条调 <see cref="ClipStripeByCornerArcs"/> 裁短，
        /// 返回裁切后仍有效的条纹。
        /// </summary>
        public static IReadOnlyList<CrosswalkStripe> ComputeStripesClipped(
            Crosswalk cw,
            IEnumerable<CornerArc> arcs,
            double minLength = DefaultMinStripeLength)
        {
            if (arcs == null) throw new ArgumentNullException(nameof(arcs));
            var basic = ComputeStripes(cw);
            var arcList = arcs as IReadOnlyList<CornerArc> ?? new List<CornerArc>(arcs);
            if (arcList.Count == 0) return basic;

            var clipped = new List<CrosswalkStripe>(basic.Count);
            foreach (var s in basic)
            {
                var r = ClipStripeByCornerArcs(s, arcList, minLength);
                if (r.HasValue) clipped.Add(r.Value);
            }
            return clipped;
        }

        /// <summary>
        /// 射线 P0 + t·dir（t ≥ 0）与弧线 <paramref name="arc"/> 求最近交点（最小正 t 且角度在弧上）。
        /// 迁自旧 <c>CrosswalkService.RayHitArc</c>，改用 <see cref="Point2D"/> / <see cref="Vector2D"/>。
        /// </summary>
        internal static bool TryRayArcIntersect(Point2D origin, Vector2D dir, CornerArc arc, out Point2D hit)
        {
            hit = default;

            double ox = origin.X - arc.Center.X;
            double oy = origin.Y - arc.Center.Y;
            double dx = dir.X;
            double dy = dir.Y;
            double r = arc.Radius;

            double a = dx * dx + dy * dy;
            if (a < 1e-20) return false;
            double b = 2.0 * (ox * dx + oy * dy);
            double c = ox * ox + oy * oy - r * r;
            double disc = b * b - 4.0 * a * c;
            if (disc < 0) return false;

            double sqrtDisc = Math.Sqrt(disc);
            double t1 = (-b - sqrtDisc) / (2.0 * a);
            double t2 = (-b + sqrtDisc) / (2.0 * a);

            bool found = false;
            double bestT = double.MaxValue;

            foreach (double t in new[] { t1, t2 })
            {
                if (t < -1e-6) continue;
                double px = origin.X + dx * t;
                double py = origin.Y + dy * t;
                double angle = Math.Atan2(py - arc.Center.Y, px - arc.Center.X);
                if (IsAngleOnArc(angle, arc) && t < bestT)
                {
                    bestT = t;
                    hit = new Point2D(px, py);
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// 判断角度 <paramref name="testAngle"/>（弧度）是否落在弧
        /// <paramref name="arc"/> 的扫描区间 [<c>StartAngle</c>, <c>StartAngle + SweepAngle</c>] 内，
        /// 方向由 <see cref="CornerArc.SweepAngle"/> 符号决定（正 = CCW，负 = CW），跨越 ±2π 自动归一。
        /// </summary>
        private static bool IsAngleOnArc(double testAngle, CornerArc arc)
        {
            const double twoPi = Math.PI * 2.0;
            const double eps = 0.002;

            double delta = testAngle - arc.StartAngle;
            while (delta > twoPi) delta -= twoPi;
            while (delta < -twoPi) delta += twoPi;

            double sweep = arc.SweepAngle;
            if (sweep >= 0)
            {
                if (delta < -eps) delta += twoPi;
                return delta >= -eps && delta <= sweep + eps;
            }
            else
            {
                if (delta > eps) delta -= twoPi;
                return delta <= eps && delta >= sweep - eps;
            }
        }
    }
}
