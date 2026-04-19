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
    /// <item>法线：<b>Center → FrontCenter 反向</b> = <b>FrontCenter → Center</b>（指向人行道侧）—— 因为 <see cref="CornerArc"/> 凸向交叉口内，
    /// 圆心在"人行道侧"的远端，沿 FrontCenter→Center 走即是从车道踩上人行道的方向；</item>
    /// <item>切线：<see cref="Vector2D.Perpendicular"/>(OutwardNormal) —— 沿 CornerArc 切向，坡道宽度沿此展开；</item>
    /// <item>尺寸：<see cref="CurbRamp.DefaultWidth"/> × <see cref="CurbRamp.DefaultDepth"/>，坡度 <see cref="CurbRamp.DefaultSlope"/>。</item>
    /// </list>
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

            // CornerArc 凸向交叉口内；圆心在人行道侧远端 —— OutwardNormal 从车道（弧中点）指向人行道（圆心）。
            var outwardNormal = new Vector2D(
                arc.Center.X - frontCenter.X,
                arc.Center.Y - frontCenter.Y);
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
    }
}
