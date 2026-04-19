using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Domain.Services.Road
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

            // 两条"路缘外边线"（沿 Leg Inward 前进的外侧）：
            // - Leg A 的 "右侧"（沿 uA 前进右手侧）= perpCCW(uA) 旋转 -90° = uA.Perpendicular() 的反方向；
            //   若 Perpendicular() 为逆时针 90°（= (−Y, X)），右手侧 = −Perpendicular()。
            // - Leg B 的 "左侧"（沿 uB 前进左手侧）= +Perpendicular()。
            // 这样相邻 (A→B, CCW) 两条外边线之间的"外角口袋"就是我们要切圆的 corner 所在。
            var PA = legA.ApproachPoint.Add(uA.Perpendicular() * (-legA.HalfWidth));
            var PB = legB.ApproachPoint.Add(uB.Perpendicular() * legB.HalfWidth);

            if (!TryLineIntersection(PA, uA, PB, uB, out var X)) return false;

            double cross = uA.Cross(uB);
            if (Math.Abs(cross) < 1e-9) return false;

            // theta = 两 Inward 方向的夹角（AngleTo 返回 (-π, π]）。
            // CCW 相邻两臂 theta ∈ (0, π)；theta ≤ 0 表示排序反向或臂几乎共线 / 反向。
            double theta = uA.AngleTo(uB);
            if (theta <= 1e-6 || theta >= Math.PI - 1e-6) return false;

            // 两条路缘外边线（同样方向 uA / uB）在 X 处相交。
            // 对"外凸 corner 圆"，圆心位于 X 沿 −(uA + uB) 方向的外角平分线上；
            // 两切点均位于 X 的 −uA / −uB 方向（朝路缘源端回退 T）。
            //
            // 几何推导（内切于两直线 + 外凸 corner）：
            //   设两条外边线内夹角 = theta（= uA 与 uB 的夹角），
            //   外角 β = π − theta，
            //   切线长 T = R / tan(β / 2) = R / tan((π − theta)/2) = R · tan(theta / 2),
            //   圆心距 D = R / sin(β / 2) = R / sin((π − theta)/2) = R / cos(theta / 2).
            double T = radius * Math.Tan(theta / 2.0);
            double D = radius / Math.Cos(theta / 2.0);

            var bis = -uA + -uB;
            if (!bis.TryNormalize(out var bisUnit, 1e-12)) return false;

            var start = X.Add(uA * (-T));
            var end = X.Add(uB * (-T));
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

        private static double NormalizeToPositive(double a)
        {
            while (a < 0) a += 2 * Math.PI;
            while (a > 2 * Math.PI) a -= 2 * Math.PI;
            return a;
        }
    }
}
