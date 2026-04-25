using System;
using System.Collections.Generic;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;

namespace HyCADTool.Domain.Services.Road
{
    /// <summary>
    /// v1.1 —— 连接相邻 <see cref="CornerArc"/> 的路缘外边线直段计算服务。
    ///
    /// <para><b>作用</b></para>
    /// 仅有 <see cref="CornerArc"/> 时，交叉口的每条 Leg 两侧路缘是"断开"的（<c>CornerArc</c> 只覆盖 corner 附近）。
    /// 本 Designer 为每条 Leg 的两侧 <b>各</b> 计算 1 段从 <see cref="IntersectionLeg.ApproachPoint"/> 的侧向锚点
    /// 延伸到相邻 CornerArc 切点的直线，把路缘外边线补全 → N 条 Leg 产生 2N 段（理论上限）。
    ///
    /// <para><b>切点对照（与 <see cref="IntersectionDesigner.TryBuildCornerArc"/> 修复后语义对齐）</b></para>
    /// <list type="bullet">
    /// <item><see cref="CornerArc.StartPoint"/> 位于 <see cref="CornerArc.LegIndexA"/> 的 <b>右</b>侧路缘外边线上
    ///   （Designer v1.2 起 <c>PA = ApproachPoint + Perpendicular(uA) · −HalfWidth</c>）；</item>
    /// <item><see cref="CornerArc.EndPoint"/> 位于 <see cref="CornerArc.LegIndexB"/> 的 <b>左</b>侧路缘外边线上
    ///   （Designer v1.2 起 <c>PB = ApproachPoint + Perpendicular(uB) · +HalfWidth</c>）；</item>
    /// <item>故 Leg i 的 <b>Left</b>（+perp · HW）kerb 段终点 = Arc 中 <c>LegIndexB == i</c> 的 <c>EndPoint</c>；</item>
    /// <item>Leg i 的 <b>Right</b>（−perp · HW）kerb 段终点 = Arc 中 <c>LegIndexA == i</c> 的 <c>StartPoint</c>。</item>
    /// </list>
    /// <para>v1.1 初版代码把 Left 和 StartPoint 绑定（与 Designer 修复前的镜像几何自洽），
    /// v1.2 随 <see cref="IntersectionDesigner.TryBuildCornerArc"/> 的符号修复同步对换。</para>
    ///
    /// <para><b>Inward 方向约定（与 <see cref="IntersectionLeg.InwardDirection"/> XMLdoc 一致）</b></para>
    /// <see cref="IntersectionLeg.InwardDirection"/> 定义为 <b>从 <see cref="IntersectionLeg.ApproachPoint"/>
    /// 指向交叉口中心</b> 的单位向量，由 <see cref="IntersectionDesigner.BuildLegFromAlignment"/> 在
    /// 非退化场景（<c>ApproachPoint ≠ aroundPoint</c>）下生成；在退化测试场景（<c>ApproachPoint = Center</c>）
    /// 下该方向退化为"沿 Alignment 切线延伸"方向，任何带 InwardDirection 的断言都应按 <b>实际几何</b> 理解。
    ///
    /// <para><b>异常 / 剔除规则</b></para>
    /// <list type="bullet">
    /// <item>当 (<c>To − From</c>) · <c>InwardDir</c> ≤ 0 → 切点在锚点的背面（几何退化）→ 剔除；</item>
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

                // Left：+perp · HalfWidth，终点 = CornerArc[LegIndexB==i].EndPoint（EndPoint 位于 LegIndexB 左侧外边线）
                if (asLegB[i] >= 0 && TryBuildSegment(
                        i, KerbSide.Left,
                        leg.ApproachPoint.Add(perp * leg.HalfWidth),
                        intersection.CornerArcs[asLegB[i]].EndPoint,
                        u, out var left))
                {
                    result.Add(left);
                }

                // Right：−perp · HalfWidth，终点 = CornerArc[LegIndexA==i].StartPoint（StartPoint 位于 LegIndexA 右侧外边线）
                if (asLegA[i] >= 0 && TryBuildSegment(
                        i, KerbSide.Right,
                        leg.ApproachPoint.Add(perp * -leg.HalfWidth),
                        intersection.CornerArcs[asLegA[i]].StartPoint,
                        u, out var right))
                {
                    result.Add(right);
                }
            }
            return result;
        }

        /// <summary>
        /// 仅当切点在锚点的 <b>+InwardDirection</b> 侧（即 Leg 朝交叉口中心一侧）才生成段。
        /// 等价于 <c>(To − From) · InwardDir &gt; 1e-6</c>。
        /// </summary>
        internal static bool TryBuildSegment(
            int legIndex, KerbSide side, Point2D from, Point2D to, Vector2D inward, out KerbSegment seg)
        {
            seg = default;
            var d = new Vector2D(to.X - from.X, to.Y - from.Y);
            if (d.IsZero(1e-9)) return false;
            double projInward = d.X * inward.X + d.Y * inward.Y;
            if (projInward <= 1e-6) return false;
            seg = new KerbSegment(legIndex, side, from, to);
            return true;
        }
    }
}
