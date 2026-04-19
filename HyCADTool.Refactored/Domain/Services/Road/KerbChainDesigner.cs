using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// v1.1 —— 连接相邻 <see cref="CornerArc"/> 的路缘外边线直段计算服务。
    ///
    /// <para><b>作用</b></para>
    /// 仅有 <see cref="CornerArc"/> 时，交叉口的每条 Leg 两侧路缘是"断开"的（<c>CornerArc</c> 只覆盖 corner 附近）。
    /// 本 Designer 为每条 Leg 的两侧 <b>各</b> 计算 1 段从 <see cref="IntersectionLeg.ApproachPoint"/> 的侧向锚点
    /// 延伸到相邻 CornerArc 切点的直线，把路缘外边线补全 → N 条 Leg 产生 2N 段（理论上限）。
    ///
    /// <para><b>切点对照（与 <see cref="IntersectionDesigner.TryBuildCornerArc"/> 对齐）</b></para>
    /// <list type="bullet">
    /// <item>Leg i 的 <b>左</b>侧（+Perpendicular(uA) · HalfWidth）→ <c>CornerArc[i].StartPoint</c>；</item>
    /// <item>Leg i 的 <b>右</b>侧（−Perpendicular(uA) · HalfWidth）→ <c>CornerArc[(i−1+N)%N].EndPoint</c>；</item>
    /// <item>其中 Arc 的 LegIndexA == i 时 StartPoint 就在 Leg i 左外边线上；LegIndexB == i 时 EndPoint 在 Leg i 右外边线上。</item>
    /// </list>
    ///
    /// <para><b>Inward 方向约定（与 <see cref="IntersectionDesigner.BuildLegFromAlignment"/> 对齐）</b></para>
    /// <see cref="IntersectionLeg.InwardDirection"/> 在实现中是
    /// <b>"从 ApproachPoint 指向 Alignment 另一端"</b>（即远离交叉口的方向，
    /// 与 <c>IntersectionLeg</c> XML 注释的字面意思相反；以 Designer 实现为准）。
    /// 因此合法切点应在 ApproachPoint 的 <b>+InwardDirection</b> 侧。
    ///
    /// <para><b>异常 / 剔除规则</b></para>
    /// <list type="bullet">
    /// <item>当 (<c>To − From</c>) · <c>InwardDir</c> ≤ 0 → 切点不在 Leg 外延方向上（几何退化）→ 剔除；</item>
    /// <item>对应 CornerArc 不存在（索引越界 / LegCount 为 0）→ 剔除；</item>
    /// <item>本 Designer <b>不</b> 抛异常，以容错保证整个交叉口至少能画出"能画的那几段"。</item>
    /// </list>
    ///
    /// <para><b>纯 Domain</b></para>
    /// 不引用 Autodesk.AutoCAD.*，完全可单测。
    /// </summary>
    public static class KerbChainDesigner
    {
        /// <summary>
        /// 计算 <paramref name="intersection"/> 所有合法的路缘外边线直段。
        /// </summary>
        /// <returns>0 ~ 2 × Legs.Count 条 <see cref="KerbSegment"/>。</returns>
        public static IReadOnlyList<KerbSegment> ComputeKerbSegments(Intersection intersection)
        {
            if (intersection == null) throw new ArgumentNullException(nameof(intersection));
            int n = intersection.Legs?.Count ?? 0;
            if (n == 0 || intersection.CornerArcs == null || intersection.CornerArcs.Count == 0)
                return Array.Empty<KerbSegment>();

            // LegIndex → 该 Leg 作为 LegIndexA / LegIndexB 时的 CornerArc 索引
            var asLegA = new int[n];
            var asLegB = new int[n];
            for (int i = 0; i < n; i++) { asLegA[i] = -1; asLegB[i] = -1; }
            for (int k = 0; k < intersection.CornerArcs.Count; k++)
            {
                var ca = intersection.CornerArcs[k];
                if (ca.LegIndexA >= 0 && ca.LegIndexA < n) asLegA[ca.LegIndexA] = k;
                if (ca.LegIndexB >= 0 && ca.LegIndexB < n) asLegB[ca.LegIndexB] = k;
            }

            var result = new List<KerbSegment>(2 * n);
            for (int i = 0; i < n; i++)
            {
                var leg = intersection.Legs[i];
                var u = leg.InwardDirection;
                var perp = u.Perpendicular();

                // Left：+perp · HalfWidth，终点 = CornerArc[LegIndexA==i].StartPoint
                if (asLegA[i] >= 0 && TryBuildSegment(
                        i, KerbSide.Left,
                        leg.ApproachPoint.Add(perp * leg.HalfWidth),
                        intersection.CornerArcs[asLegA[i]].StartPoint,
                        u, out var left))
                {
                    result.Add(left);
                }

                // Right：−perp · HalfWidth，终点 = CornerArc[LegIndexB==i].EndPoint
                if (asLegB[i] >= 0 && TryBuildSegment(
                        i, KerbSide.Right,
                        leg.ApproachPoint.Add(perp * -leg.HalfWidth),
                        intersection.CornerArcs[asLegB[i]].EndPoint,
                        u, out var right))
                {
                    result.Add(right);
                }
            }
            return result;
        }

        /// <summary>
        /// 仅当切点在锚点的 Leg 外延方向上（沿 +InwardDir，即 Designer 实现下的"朝外"方向）才生成段。
        /// 等价于 <c>(To − From) · InwardDir &gt; 1e-6</c>。
        /// </summary>
        internal static bool TryBuildSegment(
            int legIndex, KerbSide side, Point2D from, Point2D to, Vector2D inward, out KerbSegment seg)
        {
            seg = default;
            var d = new Vector2D(to.X - from.X, to.Y - from.Y);
            if (d.IsZero(1e-9)) return false;
            double projOutward = d.X * inward.X + d.Y * inward.Y;
            if (projOutward <= 1e-6) return false;
            seg = new KerbSegment(legIndex, side, from, to);
            return true;
        }
    }
}
