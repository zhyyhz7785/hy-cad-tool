using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;

namespace HyCADTool.Domain.Services.Road
{
    /// <summary>
    /// 平面交叉口设计器（<b>纯函数 / 无状态</b>）。
    ///
    /// <para><b>职责</b></para>
    /// <list type="number">
    /// <item>给定一组 <see cref="Alignment"/> 与一个"用户指的交叉口中心"点，
    /// 自动找每条 Alignment 距该点 <b>最近的端点</b> 作为 Leg 端点；</item>
    /// <item>按入向方位角 <b>CCW</b> 排序 Legs；</item>
    /// <item>相邻两 Leg 之间生成一个 <see cref="CornerArc"/>（两条路缘外边线的内切圆弧）。</item>
    /// </list>
    ///
    /// <para><b>CornerArc 几何推导</b>（关键数学）</para>
    /// <para>
    /// 给定相邻两 Leg A、B（按 CCW 排序；A 在前，B 在后），记：
    /// </para>
    /// <list type="bullet">
    /// <item>Leg A 入向 <c>u_A</c>（单位向量，从 A 的端点指向交叉口中心）；</item>
    /// <item>Leg B 入向 <c>u_B</c>。</item>
    /// </list>
    /// <para>
    /// Leg A 的"右侧路缘外边线"（沿 <c>u_A</c> 前进时右手侧）经过 <c>P_A = A.ApproachPoint + (-perpCCW(u_A)) · halfWidth_A</c>，
    /// 方向 <c>u_A</c>；其中 <c>perpCCW</c> = 逆时针旋转 90°（即 <see cref="Vector2D.Perpendicular"/>）。
    /// Leg B 的"左侧路缘外边线"经过 <c>P_B = B.ApproachPoint + perpCCW(u_B) · halfWidth_B</c>，方向 <c>u_B</c>。
    /// </para>
    /// <para>
    /// 两条射线 / 直线的 <b>外侧夹角 β</b>（= π - 内角）决定圆角圆心位置。令两方向的夹角（按 Vector2D.AngleTo）为 <c>θ</c>，
    /// 则 β = π - θ。切线长 T = R / tan(β/2)；圆心偏距 D = R / sin(β/2)。
    /// 沿两条边线从 <b>其交点 X</b> 回退 T 得到切点；圆心 = X + (角平分线单位向量) · D（向交叉口内侧）。
    /// </para>
    ///
    /// <para><b>Domain 纯净</b></para>
    /// 只依赖 <see cref="Polyline3D"/> / <see cref="Point2D"/> / <see cref="Vector2D"/>；不引用 AutoCAD 任何类型。
    /// </summary>
    public static class IntersectionDesigner
    {
        /// <summary>Designer 内部常用的几何容差（米）。</summary>
        public const double DefaultTolerance = 1e-6;

        /// <summary>
        /// 给定 Alignment 列表与"交叉口大致中心"提示点，构造出 <see cref="Intersection"/>。
        /// </summary>
        /// <param name="alignments">参与交叉口的 Alignment 列表（≥ 2 条）。</param>
        /// <param name="aroundPoint">用户点击的"交叉口大致中心"；Designer 用它来决定每条 Alignment 取起端还是末端。</param>
        /// <param name="defaultRadius">缺省转角半径（米）；每个 CornerArc 都会用此 R（单轮次统一）。</param>
        /// <param name="defaultHalfWidth">缺省 Leg 半宽（米）。</param>
        /// <param name="name">Intersection 显示名（可 null）。</param>
        /// <param name="designSpeed">设计速度（km/h），供 <see cref="IntersectionCodeChecker"/> 使用；默认 30。</param>
        /// <returns>已填充 Legs（CCW 排序）+ CornerArcs（逐对相邻）的 Intersection 对象。</returns>
        public static Intersection ComputeFromAlignments(
            IReadOnlyList<Alignment> alignments,
            Point2D aroundPoint,
            double defaultRadius = Intersection.DefaultCornerRadiusValue,
            double defaultHalfWidth = IntersectionLeg.DefaultHalfWidth,
            string name = null,
            double designSpeed = 30)
        {
            if (alignments == null || alignments.Count < 2)
                throw new ArgumentException("至少需要 2 条 Alignment 才能构造交叉口。", nameof(alignments));
            if (defaultRadius <= 0)
                throw new ArgumentOutOfRangeException(nameof(defaultRadius), defaultRadius, "defaultRadius 必须 > 0");
            if (defaultHalfWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(defaultHalfWidth), defaultHalfWidth, "defaultHalfWidth 必须 > 0");

            var legs = new List<IntersectionLeg>(alignments.Count);
            foreach (var aln in alignments)
            {
                if (aln == null || aln.Centerline == null || aln.Centerline.VertexCount < 2) continue;
                var leg = BuildLegFromAlignment(aln, aroundPoint, defaultHalfWidth);
                if (leg.HasValue) legs.Add(leg.Value);
            }

            if (legs.Count < 2)
                throw new InvalidOperationException(
                    $"有效臂数不足（当前 {legs.Count}），至少需要 2 条 Alignment 能提供端点。");

            legs = legs
                .OrderBy(l => NormalizeAngle(l.InwardAngle))
                .ToList();

            var center = ComputeCenterFromLegs(legs, aroundPoint);

            var arcs = new List<CornerArc>(legs.Count);
            for (int i = 0; i < legs.Count; i++)
            {
                int next = (i + 1) % legs.Count;
                if (TryBuildCornerArc(legs[i], legs[next], i, next, defaultRadius, out var arc))
                {
                    arcs.Add(arc);
                }
            }

            return new Intersection
            {
                Name = name,
                Legs = legs,
                CornerArcs = arcs,
                Center = center,
                DefaultCornerRadius = defaultRadius,
                DesignSpeed = designSpeed,
            };
        }

        /// <summary>
        /// 对单条 Alignment，取"离 aroundPoint 最近的那一端"作为 Leg 端点：
        /// - 比较 Alignment 起点（Centerline[0]）与终点（Centerline[VertexCount-1]）到 aroundPoint 的距离；
        /// - 取距离更近的那端作为 Leg 的 ApproachPoint；
        /// - InwardDirection = 从 ApproachPoint 指向另一端（= Alignment 切线在该端）。
        /// </summary>
        internal static IntersectionLeg? BuildLegFromAlignment(
            Alignment alignment,
            Point2D aroundPoint,
            double halfWidth)
        {
            var cl = alignment.Centerline;
            int lastIdx = cl.VertexCount - 1;
            if (lastIdx < 1) return null;

            var p0 = new Point2D(cl.GetPointAt(0).X, cl.GetPointAt(0).Y);
            var pN = new Point2D(cl.GetPointAt(lastIdx).X, cl.GetPointAt(lastIdx).Y);

            double d0 = p0.DistanceTo(aroundPoint);
            double dN = pN.DistanceTo(aroundPoint);

            bool useStart = d0 <= dN;
            Point2D approachPoint = useStart ? p0 : pN;

            double totalLen = cl.GetTotalLength();
            double approachRaw = useStart ? 0.0 : totalLen;

            Vector2D tangent = cl.TangentAtPlanarStation(approachRaw);
            if (tangent.IsZero(DefaultTolerance))
            {
                var other = useStart ? new Point2D(cl.GetPointAt(1).X, cl.GetPointAt(1).Y)
                                     : new Point2D(cl.GetPointAt(lastIdx - 1).X, cl.GetPointAt(lastIdx - 1).Y);
                tangent = useStart ? approachPoint.VectorTo(other) : other.VectorTo(approachPoint);
            }

            Vector2D inward = useStart ? -tangent : tangent;
            if (!inward.TryNormalize(out var inwardUnit, DefaultTolerance)) return null;

            return new IntersectionLeg(
                alignment.Id,
                approachRaw,
                approachPoint,
                inwardUnit,
                halfWidth,
                alignment.Name);
        }

        /// <summary>
        /// 从已排序的 Legs 估算交叉口中心。
        /// 策略：取所有 Leg 端点的算术平均；若离 aroundPoint 偏差过大（&gt; 2× 最大半宽），直接用 aroundPoint 兜底。
        /// </summary>
        internal static Point2D ComputeCenterFromLegs(IReadOnlyList<IntersectionLeg> legs, Point2D aroundPoint)
        {
            if (legs == null || legs.Count == 0) return aroundPoint;
            double sx = 0, sy = 0, maxW = 0;
            foreach (var l in legs)
            {
                sx += l.ApproachPoint.X;
                sy += l.ApproachPoint.Y;
                if (l.HalfWidth > maxW) maxW = l.HalfWidth;
            }
            var avg = new Point2D(sx / legs.Count, sy / legs.Count);
            if (avg.DistanceTo(aroundPoint) > 2 * maxW + 1) return aroundPoint;
            return avg;
        }

        /// <summary>
        /// <b>v1.1 局部编辑</b>：仅重算 <paramref name="intersection"/> 的第 <paramref name="cornerArcIndex"/>
        /// 条 <see cref="CornerArc"/>，半径改为 <paramref name="newRadius"/>。其他 CornerArc 不动。
        /// </summary>
        /// <returns>成功写回 true；几何不可解（共线 / 反向）返回 false 并保留旧弧。</returns>
        public static bool TryRebuildCornerArc(Intersection intersection, int cornerArcIndex, double newRadius)
        {
            if (intersection == null) throw new ArgumentNullException(nameof(intersection));
            if (newRadius <= 0) throw new ArgumentOutOfRangeException(nameof(newRadius), newRadius, "newRadius 必须 > 0");
            if (cornerArcIndex < 0 || cornerArcIndex >= intersection.CornerArcs.Count)
                throw new ArgumentOutOfRangeException(nameof(cornerArcIndex));

            var old = intersection.CornerArcs[cornerArcIndex];
            int idxA = old.LegIndexA;
            int idxB = old.LegIndexB;
            if (idxA < 0 || idxA >= intersection.Legs.Count || idxB < 0 || idxB >= intersection.Legs.Count) return false;

            if (!TryBuildCornerArc(intersection.Legs[idxA], intersection.Legs[idxB], idxA, idxB, newRadius, out var fresh))
                return false;

            intersection.CornerArcs[cornerArcIndex] = fresh;
            intersection.LastModifiedUtc = DateTime.UtcNow;
            return true;
        }

        /// <summary>
        /// <b>v1.1 局部编辑</b>：修改第 <paramref name="legIndex"/> 条 Leg 的半宽 <paramref name="newHalfWidth"/>，
        /// 并重算该 Leg 相邻的 <b>两条 CornerArc</b>（LegIndexA == legIndex 或 LegIndexB == legIndex 的那两条）。
        /// 其他 CornerArc 保持不变。
        /// </summary>
        /// <returns>返回受影响弧的索引（0~2 条）。</returns>
        public static IReadOnlyList<int> UpdateLegHalfWidth(Intersection intersection, int legIndex, double newHalfWidth)
        {
            if (intersection == null) throw new ArgumentNullException(nameof(intersection));
            if (newHalfWidth <= 0) throw new ArgumentOutOfRangeException(nameof(newHalfWidth));
            if (legIndex < 0 || legIndex >= intersection.Legs.Count)
                throw new ArgumentOutOfRangeException(nameof(legIndex));

            intersection.Legs[legIndex] = intersection.Legs[legIndex].WithHalfWidth(newHalfWidth);

            var touched = new List<int>(2);
            for (int i = 0; i < intersection.CornerArcs.Count; i++)
            {
                var arc = intersection.CornerArcs[i];
                if (arc.LegIndexA != legIndex && arc.LegIndexB != legIndex) continue;
                if (TryBuildCornerArc(
                        intersection.Legs[arc.LegIndexA],
                        intersection.Legs[arc.LegIndexB],
                        arc.LegIndexA,
                        arc.LegIndexB,
                        arc.Radius,
                        out var fresh))
                {
                    intersection.CornerArcs[i] = fresh;
                    touched.Add(i);
                }
            }
            intersection.LastModifiedUtc = DateTime.UtcNow;
            return touched;
        }

        /// <summary>
        /// 对相邻两 Leg 构造 CornerArc。失败返回 false（例如两臂方向几乎反向 / 共线）。
        /// </summary>
        internal static bool TryBuildCornerArc(
            IntersectionLeg legA,
            IntersectionLeg legB,
            int idxA,
            int idxB,
            double radius,
            out CornerArc arc)
        {
            arc = default;

            var uA = legA.InwardDirection;
            var uB = legB.InwardDirection;

            // 两条"路缘外边线" —— 对 CCW 相邻两臂 (A, B)，物理相邻的 corner 位于
            //   Leg A 的 <b>右侧</b>路缘（沿 uA 前进右手侧 = −uA.Perpendicular()，约定 Perpendicular=(−Y,X)=CCW90°）
            // + Leg B 的 <b>左侧</b>路缘（沿 uB 前进左手侧 = +uB.Perpendicular()）
            // 两者外延相交于 X。X 位于 PA / PB 的 <b>−uA / −uB</b> 方向（即 ApproachPoint 的"交叉口外"一端）；
            // 切点 start / end 从 X 沿 <b>+uA / +uB</b> 方向（即 InwardDirection）偏 T 到达 —— 这是
            // CornerArc 切入路缘进入交叉口内部的位置，圆心沿内角平分线 +(uA+uB)/|..| 朝交叉口中心偏 D。
            //
            // v1.2 修正（2026-04）：此前 PA/PB 的 ± 符号写反（Leg A 用 +perp = 左侧，Leg B 用 -perp = 右侧），
            //   对 CCW 相邻对角度镜像到 Leg A-B <b>背对</b>一侧的对角象限，使 center / start / end 全数搬到
            //   交叉口对侧（非对称场景肉眼可见错位）。见 IntersectionDesignerCornerPositionTests。
            var PA = legA.ApproachPoint.Add(uA.Perpendicular() * (-legA.HalfWidth));
            var PB = legB.ApproachPoint.Add(uB.Perpendicular() * legB.HalfWidth);

            if (!TryLineIntersection(PA, uA, PB, uB, out var X)) return false;

            double cross = uA.Cross(uB);
            if (Math.Abs(cross) < 1e-9) return false;

            // theta = 两 Inward 的夹角（AngleTo 返回 (-π, π]）。
            // CCW 相邻两臂 theta ∈ (0, π)；theta ≤ 0 表示排序反向 / 臂几乎共线 / 反向。
            double theta = uA.AngleTo(uB);
            if (theta <= 1e-6 || theta >= Math.PI - 1e-6) return false;

            // 圆心沿 (uA + uB).Normalize() 方向（内角平分线 = 朝两路缘相会的更前方），距 X = D。
            // 两切点分别沿 +uA / +uB 方向偏离 X 距离 T。
            //
            // 几何推导（圆内切于两直线，位于内角口袋）：
            //   内夹角 = theta，内角平分线与任一路缘的夹角 = theta / 2。
            //   圆心到路缘距 = D · sin(theta / 2) = R  =>  D = R / sin(theta / 2).
            //   切线长 T = R / tan(theta / 2).
            double T = radius / Math.Tan(theta / 2.0);
            double D = radius / Math.Sin(theta / 2.0);

            var bis = uA + uB;
            if (!bis.TryNormalize(out var bisUnit, 1e-12)) return false;

            var start = X.Add(uA * T);
            var end = X.Add(uB * T);
            var center = X.Add(bisUnit * D);

            if (Math.Abs(center.DistanceTo(start) - radius) > 1e-3 ||
                Math.Abs(center.DistanceTo(end) - radius) > 1e-3)
            {
                return false;
            }

            double a0 = Math.Atan2(start.Y - center.Y, start.X - center.X);
            double a1 = Math.Atan2(end.Y - center.Y, end.X - center.X);

            // "最短路径"sweep：归一化 a1 − a0 到 [−π, π]。对 CCW 相邻臂 + 外凸 corner arc，sweep 必为负（CW）。
            double sweep = a1 - a0;
            while (sweep > Math.PI) sweep -= 2 * Math.PI;
            while (sweep < -Math.PI) sweep += 2 * Math.PI;

            arc = new CornerArc(idxA, idxB, center, radius, start, end, a0, a1, sweep);
            return true;
        }

        /// <summary>
        /// 两条由"点 + 方向向量"定义的直线的交点（若平行 / 重合，返回 false）。
        /// </summary>
        internal static bool TryLineIntersection(
            Point2D p1, Vector2D d1,
            Point2D p2, Vector2D d2,
            out Point2D intersection,
            double tolerance = 1e-9)
        {
            intersection = default;
            double denom = d1.X * d2.Y - d1.Y * d2.X;
            if (Math.Abs(denom) < tolerance) return false;

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double t = (dx * d2.Y - dy * d2.X) / denom;

            intersection = new Point2D(p1.X + t * d1.X, p1.Y + t * d1.Y);
            return true;
        }

        private static double NormalizeAngle(double a)
        {
            while (a < 0) a += 2 * Math.PI;
            while (a >= 2 * Math.PI) a -= 2 * Math.PI;
            return a;
        }
    }
}
