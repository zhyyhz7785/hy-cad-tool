using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// 缘石坡道设计器（<b>纯函数 / 无状态</b>）—— 基于已构造的 <see cref="Intersection"/>（含 <see cref="CornerArc"/>）
    /// 自动在每个转角圆弧上布置一个 <see cref="CurbRamp"/>。
    ///
    /// <para><b>布置策略（默认：单面坡）</b></para>
    /// <list type="number">
    /// <item>定位：取 CornerArc 的<b>角度平分点</b>（= Center + R·(单位角平分向量)）作为坡道前沿 <see cref="CurbRamp.FrontCenter"/>；</item>
    /// <item>法线：<b>Center → FrontCenter</b>（指向人行道侧）—— 修复后的 <see cref="IntersectionDesigner.TryBuildCornerArc"/>（v1.2+）
    /// 构造出的 CornerArc <b>凸向人行道外侧</b>，圆心落在交叉口内部；车道在弧内（圆心侧），人行道在弧外（背圆心侧），
    /// 故"从车道踩上人行道"= 沿 Center→FrontCenter 继续外推；</item>
    /// <item>切线：<see cref="Vector2D.Perpendicular"/>(OutwardNormal) —— 沿 CornerArc 切向，坡道宽度沿此展开；</item>
    /// <item>尺寸：<see cref="CurbRamp.DefaultWidth"/> × <see cref="CurbRamp.DefaultDepth"/>，坡度 <see cref="CurbRamp.DefaultSlope"/>。</item>
    /// </list>
    /// <para><b>v1.2 方向修正</b>：v1.1 代码里 <c>OutwardNormal = FrontCenter → Center</c>，与当时 <see cref="IntersectionDesigner"/>
    /// 的"镜像"几何下圆心恰好处在人行道远端的巧合一致；修复镜像 bug 后圆心移到了交叉口内部，必须反号。
    /// 详见 <see cref="CurbRampDesignerDirectionTests"/> 与 <c>doc/RoadDesign/08Intersection.md §9.2</c>。</para>
    ///
    /// <para><b>Domain 纯净</b></para>
    /// 只依赖 <see cref="Point2D"/> / <see cref="Vector2D"/> / Domain 值对象；不引用 AutoCAD 任何类型。
    /// </summary>
    public static class CurbRampDesigner
    {
        /// <summary>几何容差（米）。</summary>
        public const double DefaultTolerance = 1e-9;

        /// <summary>
        /// 给定 <see cref="Intersection"/>，对每个 <see cref="CornerArc"/> 布置一个 <see cref="CurbRamp"/>，
        /// 并把结果写回 <see cref="Intersection.CurbRamps"/>（覆盖）。
        /// </summary>
        /// <param name="intersection">已 Designer 构造完 Legs + CornerArcs 的交叉口。</param>
        /// <param name="kind">坡道类型，默认单面坡。</param>
        /// <param name="width">宽度（m），默认 <see cref="CurbRamp.DefaultWidth"/>。</param>
        /// <param name="depth">深度（m），默认 <see cref="CurbRamp.DefaultDepth"/>。</param>
        /// <param name="slope">坡度（rise/run），默认 <see cref="CurbRamp.DefaultSlope"/>。</param>
        /// <returns>生成的 CurbRamp 列表（同时写入 intersection.CurbRamps）。</returns>
        public static IReadOnlyList<CurbRamp> LayoutRampsOnCornerArcs(
            Intersection intersection,
            CurbRampKind kind = CurbRampKind.SingleFace,
            double width = CurbRamp.DefaultWidth,
            double depth = CurbRamp.DefaultDepth,
            double slope = CurbRamp.DefaultSlope)
        {
            if (intersection == null) throw new ArgumentNullException(nameof(intersection));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, "width 必须 > 0");
            if (depth <= 0) throw new ArgumentOutOfRangeException(nameof(depth), depth, "depth 必须 > 0");
            if (slope <= 0) throw new ArgumentOutOfRangeException(nameof(slope), slope, "slope 必须 > 0");

            var ramps = new List<CurbRamp>(intersection.CornerArcs.Count);
            for (int i = 0; i < intersection.CornerArcs.Count; i++)
            {
                if (TryBuildRampOnArc(intersection.CornerArcs[i], i, kind, width, depth, slope, out var ramp))
                {
                    ramps.Add(ramp);
                }
            }

            intersection.CurbRamps = ramps;
            intersection.LastModifiedUtc = DateTime.UtcNow;
            return ramps;
        }

        /// <summary>
        /// 对单个 <see cref="CornerArc"/> 构造 1 个 <see cref="CurbRamp"/>（布置在弧中点）。
        /// </summary>
        internal static bool TryBuildRampOnArc(
            CornerArc arc,
            int arcIndex,
            CurbRampKind kind,
            double width,
            double depth,
            double slope,
            out CurbRamp ramp)
        {
            ramp = default;

            if (arc.Radius <= DefaultTolerance) return false;

            // 弧中点方向 = 起点方向 + 终点方向（单位化），长度 ~ 2·cos(sweep/2)；零向量退化时拿起点方向兜底。
            var uStart = new Vector2D(
                arc.StartPoint.X - arc.Center.X,
                arc.StartPoint.Y - arc.Center.Y);
            var uEnd = new Vector2D(
                arc.EndPoint.X - arc.Center.X,
                arc.EndPoint.Y - arc.Center.Y);

            Vector2D midDir;
            var sumDir = uStart + uEnd;
            if (!sumDir.TryNormalize(out midDir, DefaultTolerance))
            {
                // 半圆（180°）情形：退化用 (uStart 顺 sweep 方向旋 90°)
                if (!uStart.TryNormalize(out var uStartUnit, DefaultTolerance)) return false;
                double rotBy = arc.SweepAngle >= 0 ? Math.PI / 2 : -Math.PI / 2;
                midDir = uStartUnit.Rotate(rotBy);
            }

            var frontCenter = new Point2D(
                arc.Center.X + midDir.X * arc.Radius,
                arc.Center.Y + midDir.Y * arc.Radius);

            // v1.2 修正后：CornerArc 凸向 <b>人行道外侧</b>（圆心在交叉口内）。
            // OutwardNormal 从前沿（弧中点，车道侧）指向人行道（远离圆心方向）= Center → FrontCenter 方向。
            var outwardNormal = new Vector2D(
                frontCenter.X - arc.Center.X,
                frontCenter.Y - arc.Center.Y);
            if (!outwardNormal.TryNormalize(out var outwardUnit, DefaultTolerance)) return false;

            // 切线 = OutwardNormal 逆时针旋 90°（让 Width 沿"前沿方向"铺开）。
            var tangentUnit = outwardUnit.Perpendicular();

            ramp = new CurbRamp(
                cornerArcIndex: arcIndex,
                kind: kind,
                frontCenter: frontCenter,
                tangent: tangentUnit,
                outwardNormal: outwardUnit,
                width: width,
                depth: depth,
                slope: slope);
            return true;
        }

        // =====================================================================
        //  v1.1 分类几何（BuildFootprint）—— 纯函数，返回闭合 2D 多段线列表
        // =====================================================================

        /// <summary>Fan 弧镶嵌默认段数（按弧长不长时 16 段足够平滑）。</summary>
        public const int DefaultFanTesselationSegments = 16;

        /// <summary>ThreeFace 侧面坡默认与主坡深度相等（45° 侧坡）。</summary>
        public const double DefaultThreeFaceSideLength = CurbRamp.DefaultDepth;

        /// <summary>
        /// 按 <see cref="CurbRamp.Kind"/> 输出坡道的<b>闭合平面轮廓</b>（供 Infrastructure 画 AutoCAD Polyline）。
        ///
        /// <para><b>几何规则</b></para>
        /// <list type="bullet">
        /// <item><see cref="CurbRampKind.SingleFace"/>：1 条 4 顶点矩形（FrontLeft → FrontRight → BackRight → BackLeft → 闭合）；</item>
        /// <item><see cref="CurbRampKind.ThreeFace"/>：3 条闭合多边形：
        ///   <list type="number">
        ///   <item>主坡矩形（同 SingleFace）；</item>
        ///   <item>左侧三角坡：<c>FrontLeft</c> → <c>FrontLeft − Tangent·<paramref name="sideLength"/></c> → <c>BackLeft</c>；</item>
        ///   <item>右侧三角坡：<c>FrontRight</c> → <c>FrontRight + Tangent·<paramref name="sideLength"/></c> → <c>BackRight</c>；</item>
        ///   </list></item>
        /// <item><see cref="CurbRampKind.Fan"/>（需提供 <paramref name="arc"/>）：1 条扇环闭合多段线 —
        ///   外弧（= CornerArc 本身）用 <paramref name="tesselationSegments"/> 段直线近似，
        ///   内弧（同心，R_inner = R − Depth）反向闭合；若 Depth ≥ R 则退化为 SingleFace 矩形。</item>
        /// </list>
        ///
        /// <para><b>Domain 纯净</b></para>
        /// 只返回 <see cref="Polyline2D"/>（IsClosed = true）列表；不依赖 AutoCAD；
        /// Infrastructure 层可直接把每条 Polyline2D 映射为 LWPolyline（闭合、顶点序列一致）。
        /// </summary>
        /// <param name="ramp">已布置好的 CurbRamp（提供 FrontCenter / Tangent / OutwardNormal / Width / Depth / Kind）。</param>
        /// <param name="arc">所属 CornerArc；Fan 必需，Single / ThreeFace 可传 default。</param>
        /// <param name="sideLength">ThreeFace 侧面坡长度（米），默认 = <see cref="DefaultThreeFaceSideLength"/>。</param>
        /// <param name="tesselationSegments">Fan 弧镶嵌段数（&gt;= 2），默认 <see cref="DefaultFanTesselationSegments"/>。</param>
        /// <returns>1 或 3 条闭合 <see cref="Polyline2D"/>（按上述顺序）。</returns>
        public static IReadOnlyList<Polyline2D> BuildFootprint(
            CurbRamp ramp,
            CornerArc? arc = null,
            double sideLength = DefaultThreeFaceSideLength,
            int tesselationSegments = DefaultFanTesselationSegments)
        {
            if (sideLength <= 0) throw new ArgumentOutOfRangeException(nameof(sideLength), sideLength, "sideLength 必须 > 0");
            if (tesselationSegments < 2) throw new ArgumentOutOfRangeException(nameof(tesselationSegments), tesselationSegments, "tesselationSegments 必须 >= 2");

            switch (ramp.Kind)
            {
                case CurbRampKind.SingleFace:
                    return new[] { BuildRectangle(ramp) };

                case CurbRampKind.ThreeFace:
                    return new[]
                    {
                        BuildRectangle(ramp),
                        BuildSideTriangle(ramp, leftSide: true, sideLength),
                        BuildSideTriangle(ramp, leftSide: false, sideLength),
                    };

                case CurbRampKind.Fan:
                    if (arc.HasValue && arc.Value.Radius > ramp.Depth + DefaultTolerance)
                    {
                        return new[] { BuildFanRing(arc.Value, ramp.Depth, tesselationSegments) };
                    }
                    // R ≤ Depth：不足以展开扇环，退化为矩形（同 SingleFace 几何，保留 Kind 元信息）
                    return new[] { BuildRectangle(ramp) };

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(ramp.Kind), ramp.Kind, $"未支持的 CurbRampKind：{ramp.Kind}");
            }
        }

        /// <summary>FrontLeft → FrontRight → BackRight → BackLeft → 闭合（4 顶点矩形）。</summary>
        private static Polyline2D BuildRectangle(CurbRamp ramp)
        {
            return new Polyline2D(
                new[] { ramp.FrontLeft, ramp.FrontRight, ramp.BackRight, ramp.BackLeft },
                isClosed: true);
        }

        /// <summary>
        /// 侧面三角坡（3 顶点）。
        /// 左侧：<c>FrontLeft → FrontLeft − Tangent·sideLength → BackLeft → 闭合</c>；右侧对称。
        /// </summary>
        private static Polyline2D BuildSideTriangle(CurbRamp ramp, bool leftSide, double sideLength)
        {
            Point2D apex;       // 前沿外侧端点（沿路缘延伸一段，到车道面）
            Point2D nearFront;  // 主坡前沿对应端点
            Point2D nearBack;   // 主坡上口对应端点
            double signedSide = leftSide ? -sideLength : sideLength;

            nearFront = leftSide ? ramp.FrontLeft : ramp.FrontRight;
            nearBack = leftSide ? ramp.BackLeft : ramp.BackRight;
            apex = nearFront.Add(ramp.Tangent * signedSide);

            return new Polyline2D(
                new[] { nearFront, apex, nearBack },
                isClosed: true);
        }

        /// <summary>
        /// 扇环闭合轮廓：外弧（CornerArc 本身，从 StartPoint → EndPoint，CW 走）+ 内弧（同心，R−Depth，反向回到起点）。
        /// 弧部分用 <paramref name="segments"/> 段直线近似。
        /// </summary>
        internal static Polyline2D BuildFanRing(CornerArc arc, double depth, int segments)
        {
            double rOuter = arc.Radius;
            double rInner = rOuter - depth;

            // 外弧从 StartAngle 扫到 StartAngle + SweepAngle（保留 Designer 的 CW / CCW 方向）。
            var vertices = new List<Point2D>(2 * (segments + 1));
            for (int i = 0; i <= segments; i++)
            {
                double t = i / (double)segments;
                double a = arc.StartAngle + arc.SweepAngle * t;
                vertices.Add(new Point2D(
                    arc.Center.X + rOuter * Math.Cos(a),
                    arc.Center.Y + rOuter * Math.Sin(a)));
            }
            // 内弧反向
            for (int i = segments; i >= 0; i--)
            {
                double t = i / (double)segments;
                double a = arc.StartAngle + arc.SweepAngle * t;
                vertices.Add(new Point2D(
                    arc.Center.X + rInner * Math.Cos(a),
                    arc.Center.Y + rInner * Math.Sin(a)));
            }
            return new Polyline2D(vertices, isClosed: true);
        }
    }
}
