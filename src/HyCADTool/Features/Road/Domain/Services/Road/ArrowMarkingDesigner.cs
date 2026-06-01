using System;
using System.Collections.Generic;
using HyCAD.Geometry;
using HyCADTool.Domain.ValueObjects.Road;

namespace HyCADTool.Domain.Services.Road
{
    /// <summary>
    /// 导流箭头几何服务（纯函数）。GB 5768-2009 §5.3。
    ///
    /// <para><b>参数化</b></para>
    /// 以 <see cref="ArrowMarking.Anchor"/> 为 local 原点、<see cref="ArrowMarking.Direction"/>
    /// 为 local +X；+Y = <see cref="Vector2D.Perpendicular"/>（主方向逆时针 90°，车辆的"左侧"）。
    /// local 坐标下生成顶点后，再用 <see cref="ToWorld"/> 变换到世界系。
    ///
    /// <para><b>形状</b></para>
    /// <list type="table">
    /// <listheader><term>Kind</term><description>输出多边形数</description></listheader>
    /// <item><term><see cref="ArrowMarkingKind.Straight"/></term><description>1 个闭合多边形（杆 + 尖）</description></item>
    /// <item><term><see cref="ArrowMarkingKind.Left"/> / <see cref="ArrowMarkingKind.Right"/></term><description>1 个闭合多边形（L 形 + 尖）</description></item>
    /// <item><term><see cref="ArrowMarkingKind.StraightLeft"/> / <see cref="ArrowMarkingKind.StraightRight"/></term><description>2 个闭合多边形（直行 + 分支）</description></item>
    /// <item><term><see cref="ArrowMarkingKind.LeftRight"/></term><description>2 个闭合多边形（左分支 + 右分支）</description></item>
    /// </list>
    ///
    /// <para><b>默认参数</b></para>
    /// 所有默认值参照 GB 5768-2009 §5.3 "60 km/h 档"（主长 6 m）通用化：箭杆宽 0.30 m，
    /// 箭头长 = 0.25L（= 1.5 m），箭头半宽 = 0.45 m；转弯分支长 = 0.40L（= 2.4 m），其余尺寸按比例派生。
    /// </summary>
    public static class ArrowMarkingDesigner
    {
        public const double DefaultShaftWidth = 0.30;
        public const double DefaultArrowHeadLengthRatio = 0.25;
        public const double DefaultArrowHeadHalfWidth = 0.45;
        public const double DefaultTurnBranchRatio = 0.40;

        /// <summary>构造 <see cref="ArrowMarking"/>（Id 自动生成）。</summary>
        public static ArrowMarking Create(
            Point2D anchor,
            Vector2D direction,
            ArrowMarkingKind kind,
            double length = ArrowMarking.DefaultLength)
        {
            return new ArrowMarking(Guid.NewGuid(), anchor, direction, kind, length);
        }

        /// <summary>
        /// 构造 local→world 变换所需的 perp 向量（+Y 方向的世界向量），
        /// 并用 <paramref name="anchor"/> 作为原点返回 world 点。
        /// </summary>
        public static Point2D ToWorld(Point2D anchor, Vector2D direction, double localX, double localY)
        {
            var perp = direction.Perpendicular();
            return new Point2D(
                anchor.X + direction.X * localX + perp.X * localY,
                anchor.Y + direction.Y * localX + perp.Y * localY);
        }

        /// <summary>
        /// 按 <see cref="ArrowMarking.Kind"/> 构造闭合多边形列表（每个 Polyline2D 都 IsClosed=true）。
        /// </summary>
        public static IReadOnlyList<Polyline2D> BuildFootprint(
            ArrowMarking arrow,
            double shaftWidth = DefaultShaftWidth,
            double arrowHeadHalfWidth = DefaultArrowHeadHalfWidth,
            double arrowHeadLengthRatio = DefaultArrowHeadLengthRatio,
            double turnBranchRatio = DefaultTurnBranchRatio)
        {
            if (shaftWidth <= 0) throw new ArgumentOutOfRangeException(nameof(shaftWidth));
            if (arrowHeadHalfWidth <= 0) throw new ArgumentOutOfRangeException(nameof(arrowHeadHalfWidth));
            if (arrowHeadLengthRatio <= 0 || arrowHeadLengthRatio >= 1)
                throw new ArgumentOutOfRangeException(nameof(arrowHeadLengthRatio), "必须 ∈ (0, 1)");
            if (turnBranchRatio <= 0 || turnBranchRatio >= 1)
                throw new ArgumentOutOfRangeException(nameof(turnBranchRatio), "必须 ∈ (0, 1)");
            if (arrowHeadHalfWidth <= shaftWidth / 2)
                throw new ArgumentException("arrowHeadHalfWidth 必须大于 shaftWidth/2（否则翼宽为零）");

            double L = arrow.Length;
            double w = shaftWidth;
            double hL = L * arrowHeadLengthRatio;
            double hW = arrowHeadHalfWidth;
            double branch = L * turnBranchRatio;

            switch (arrow.Kind)
            {
                case ArrowMarkingKind.Straight:
                    return new[] { BuildStraightLocal(arrow, L, w, hL, hW) };

                case ArrowMarkingKind.Left:
                    return new[] { BuildTurnLocal(arrow, L, branch, w, hL, hW, sign: +1) };

                case ArrowMarkingKind.Right:
                    return new[] { BuildTurnLocal(arrow, L, branch, w, hL, hW, sign: -1) };

                case ArrowMarkingKind.StraightLeft:
                    return new[]
                    {
                        BuildStraightLocal(arrow, L, w, hL, hW),
                        BuildBranchLocal(arrow, L, branch, w, hL, hW, sign: +1),
                    };

                case ArrowMarkingKind.StraightRight:
                    return new[]
                    {
                        BuildStraightLocal(arrow, L, w, hL, hW),
                        BuildBranchLocal(arrow, L, branch, w, hL, hW, sign: -1),
                    };

                case ArrowMarkingKind.LeftRight:
                    return new[]
                    {
                        BuildTurnLocal(arrow, L, branch, w, hL, hW, sign: +1),
                        BuildTurnLocal(arrow, L, branch, w, hL, hW, sign: -1),
                    };

                default:
                    return new[] { BuildStraightLocal(arrow, L, w, hL, hW) };
            }
        }

        // ---------- 基元：直行 ----------
        //
        // local 坐标（+X = 前进方向，+Y = 左侧）：
        //   V0 (0,       -w/2)    箭尾右下
        //   V1 (L-hL,    -w/2)    杆 ↔ 头 交界（右）
        //   V2 (L-hL,    -hW)     三角形右翼外侧
        //   V3 (L,        0 )     箭尖
        //   V4 (L-hL,    +hW)     三角形左翼外侧
        //   V5 (L-hL,    +w/2)    杆 ↔ 头 交界（左）
        //   V6 (0,       +w/2)    箭尾左上
        private static Polyline2D BuildStraightLocal(
            ArrowMarking arrow, double L, double w, double hL, double hW)
        {
            var verts = new[]
            {
                ToWorld(arrow.Anchor, arrow.Direction, 0,       -w / 2),
                ToWorld(arrow.Anchor, arrow.Direction, L - hL,  -w / 2),
                ToWorld(arrow.Anchor, arrow.Direction, L - hL,  -hW),
                ToWorld(arrow.Anchor, arrow.Direction, L,        0),
                ToWorld(arrow.Anchor, arrow.Direction, L - hL,   hW),
                ToWorld(arrow.Anchor, arrow.Direction, L - hL,   w / 2),
                ToWorld(arrow.Anchor, arrow.Direction, 0,        w / 2),
            };
            return new Polyline2D(verts, isClosed: true);
        }

        // ---------- 基元：90° 转弯（sign=+1 左转，sign=-1 右转）----------
        //
        // local 坐标：主杆沿 +X 走 L_main = L - branch，然后向 sign*+Y 转弯
        // 走 branch 到箭身顶端；箭头长 hL 沿 sign*+Y 方向，箭尖在
        // (L_main, sign * branch)。
        //
        // CCW 顶点（sign=+1 左转）：
        //   V0 (0,           -w/2)
        //   V1 (L_main+w/2,  -w/2)                    右下外角
        //   V2 (L_main+w/2,  branch-hL)               右侧沿 +Y 到箭根
        //   V3 (L_main+hW,   branch-hL)               右翼
        //   V4 (L_main,      branch)                  箭尖（朝 +Y）
        //   V5 (L_main-hW,   branch-hL)               左翼
        //   V6 (L_main-w/2,  branch-hL)               左侧回箭根
        //   V7 (L_main-w/2,  +w/2)                    内角
        //   V8 (0,           +w/2)                    回起点上
        //
        // sign=-1 右转时所有 y 坐标取反，并反转顶点顺序以保持 CCW。
        private static Polyline2D BuildTurnLocal(
            ArrowMarking arrow, double L, double branch, double w, double hL, double hW, int sign)
        {
            double lMain = L - branch;
            if (lMain <= w / 2)
                throw new ArgumentException("L - branch 必须 > shaftWidth/2，否则几何退化");
            if (branch <= hL)
                throw new ArgumentException("branch 必须 > arrowHeadLength，否则箭头根部越过转弯内角");

            var local = new (double X, double Y)[]
            {
                (0,            -w / 2),
                (lMain + w / 2, -w / 2),
                (lMain + w / 2, branch - hL),
                (lMain + hW,    branch - hL),
                (lMain,         branch),
                (lMain - hW,    branch - hL),
                (lMain - w / 2, branch - hL),
                (lMain - w / 2, w / 2),
                (0,             w / 2),
            };

            var verts = new Point2D[local.Length];
            for (int i = 0; i < local.Length; i++)
            {
                double ySigned = sign * local[i].Y;
                verts[i] = ToWorld(arrow.Anchor, arrow.Direction, local[i].X, ySigned);
            }

            // sign=-1 时反转顺序以维持 CCW
            if (sign < 0) Array.Reverse(verts);

            return new Polyline2D(verts, isClosed: true);
        }

        // ---------- 基元：分支（仅尾部转弯段 + 箭头，不含主干）----------
        //
        // 用于 StraightLeft / StraightRight 的"附加分支"：从主杆的某点分叉出去，
        // 向 sign*+Y 方向走，末端带箭头。
        //
        // 分支起点为主杆中段（local X = L_main = L - branch），宽度仍为 w。
        // CCW（sign=+1，即左分支）：
        //   V0 (L_main-w/2,  +w/2)           分支根部内侧（= 主干左侧）
        //   V1 (L_main+w/2,  +w/2)           分支根部外侧
        //   V2 (L_main+w/2,  branch-hL)      外侧沿 +Y 到箭根
        //   V3 (L_main+hW,   branch-hL)      右翼
        //   V4 (L_main,      branch)         箭尖
        //   V5 (L_main-hW,   branch-hL)      左翼
        //   V6 (L_main-w/2,  branch-hL)      内侧到箭根
        private static Polyline2D BuildBranchLocal(
            ArrowMarking arrow, double L, double branch, double w, double hL, double hW, int sign)
        {
            double lMain = L - branch;
            if (lMain <= w / 2)
                throw new ArgumentException("L - branch 必须 > shaftWidth/2");
            if (branch <= hL)
                throw new ArgumentException("branch 必须 > arrowHeadLength");

            var local = new (double X, double Y)[]
            {
                (lMain - w / 2, w / 2),
                (lMain + w / 2, w / 2),
                (lMain + w / 2, branch - hL),
                (lMain + hW,    branch - hL),
                (lMain,         branch),
                (lMain - hW,    branch - hL),
                (lMain - w / 2, branch - hL),
            };

            var verts = new Point2D[local.Length];
            for (int i = 0; i < local.Length; i++)
            {
                double ySigned = sign * local[i].Y;
                verts[i] = ToWorld(arrow.Anchor, arrow.Direction, local[i].X, ySigned);
            }
            if (sign < 0) Array.Reverse(verts);

            return new Polyline2D(verts, isClosed: true);
        }
    }
}
