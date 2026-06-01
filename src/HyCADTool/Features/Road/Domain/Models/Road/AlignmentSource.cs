using System;
using System.Collections.Generic;
using HyCAD.Geometry;

namespace HyCADTool.Domain.Models.Road
{
    /// <summary>
    /// 平面线位的"输入来源"快照。用于按 PI 创建 / 编辑链路（hyRoadAlnByPi / hyRoadAlnEditPi / InsertPi 等）。
    /// <para><c>hyRoadAlnByPi</c> 写入完整 PI 参数表；<c>hyRoadA</c> 对无 bulge 的多段线按顶点生成折线 PI 表；
    /// 对含圆弧（bulge≠0）的开放多段线由 <see cref="TryCreatePiTableFromBulgeCenterline"/> 反求切线交点 PI 与半径（Ls=0）。</para>
    ///
    /// 持久化用途：
    /// - 让 hyRoadAlnEditPi 命令拿到原始 PI 表（含每点 R / Ls_in / Ls_out / Tag），无需从几何反解；
    /// - 后续 v2 Blender 联调可直接用此结构在 Blender 内重建参数化曲线。
    /// </summary>
    public sealed class AlignmentSource
    {
        /// <summary>
        /// 来源种类（v1 仅区分 PI 与未知；为后续 LandXML / 曲线法 留扩展位）。
        /// </summary>
        public AlignmentSourceKind Kind { get; set; } = AlignmentSourceKind.PiTable;

        /// <summary>PI 元素列表（含首尾点）。</summary>
        public List<AlignmentPiInput> PiElements { get; set; } = new List<AlignmentPiInput>();

        /// <summary>
        /// 从仅由直线段构成的中心线（全部 bulge≈0）生成 PI 表：每一顶点对应一个 PI，R/Ls 均为 0。
        /// 若含弧段（<see cref="Polyline3D.HasArcs"/>）则返回 <c>null</c>，因顶点不等于导线 PI，不能强行近似。
        /// </summary>
        public static AlignmentSource TryCreatePiTableFromStraightCenterline(Polyline3D centerline)
        {
            if (centerline == null || centerline.VertexCount < 2) return null;
            if (centerline.HasArcs) return null;

            var src = new AlignmentSource { Kind = AlignmentSourceKind.PiTable };
            for (int i = 0; i < centerline.VertexCount; i++)
            {
                var p = centerline.GetPointAt(i);
                src.PiElements.Add(new AlignmentPiInput
                {
                    P = new Point2D(p.X, p.Y),
                    Radius = 0,
                    SpiralIn = 0,
                    SpiralOut = 0,
                    Tag = null,
                });
            }

            return src;
        }

        /// <summary>
        /// 从含 bulge 圆弧段的<b>开放</b>多段线反推导线 PI 表：端点取顶点（R=0），
        /// 直线与直线夹角顶点为折线 PI（R=0），每段圆弧用弦两端切线交点作 PI 并填半径。
        /// 全直线时转调 <see cref="TryCreatePiTableFromStraightCenterline"/>；闭合线位或弧段切线无法求交时返回 <c>null</c>。
        /// </summary>
        public static AlignmentSource TryCreatePiTableFromBulgeCenterline(Polyline3D centerline)
        {
            if (centerline == null || centerline.VertexCount < 2) return null;
            if (!centerline.HasArcs) return TryCreatePiTableFromStraightCenterline(centerline);
            if (centerline.IsClosed) return null;

            const double bulgeEps = 1e-12;
            const double dedupeTol = 1e-4;
            int segCount = centerline.SegmentCount;

            var list = new List<AlignmentPiInput>();

            void TryAdd(Point2D p, double r)
            {
                if (list.Count > 0 && list[list.Count - 1].P.IsEqualTo(p, dedupeTol))
                    return;
                list.Add(new AlignmentPiInput
                {
                    P = p,
                    Radius = r,
                    SpiralIn = 0,
                    SpiralOut = 0,
                    Tag = null,
                });
            }

            var start = centerline.GetPointAt(0);
            TryAdd(new Point2D(start.X, start.Y), 0);

            for (int seg = 0; seg < segCount; seg++)
            {
                bool arc = Math.Abs(centerline.GetBulgeAt(seg)) > bulgeEps;
                if (arc)
                {
                    if (!centerline.TryGetArcTangentIntersectionPi(seg, out var pi, out var rAbs))
                        return null;
                    TryAdd(pi, rAbs);
                }
                else
                {
                    int bIndex = seg + 1;
                    if (bIndex >= centerline.VertexCount - 1)
                        continue;
                    bool nextArc = seg + 1 < segCount && Math.Abs(centerline.GetBulgeAt(seg + 1)) > bulgeEps;
                    if (nextArc)
                        continue;
                    var vb = centerline.GetPointAt(seg + 1);
                    TryAdd(new Point2D(vb.X, vb.Y), 0);
                }
            }

            var end = centerline.GetPointAt(centerline.VertexCount - 1);
            TryAdd(new Point2D(end.X, end.Y), 0);

            if (list.Count < 2) return null;
            return new AlignmentSource { Kind = AlignmentSourceKind.PiTable, PiElements = list };
        }
    }

    public enum AlignmentSourceKind
    {
        Unknown = 0,
        PiTable = 1,

        /// <summary>
        /// 用户从任意图层的 Polyline 拾取登记而来、尚未提交为正式平面线位。
        /// 提交到固定图层 <c>05_hy_道路_平面线位</c> 并写 HY_ROAD Xdata 后，
        /// 通常被后续 PI 编辑改写为 <see cref="PiTable"/>。
        /// </summary>
        UserPicked = 2,
    }

    /// <summary>
    /// 单个 PI 输入项的 JSON 持久化形式（与 <c>Domain.Services.Road.PiElement</c> 同构，但适合 Newtonsoft 序列化）。
    /// 命名故意区分以避免 Domain.Services 层的内部 readonly struct 暴露给 JSON。
    /// </summary>
    public sealed class AlignmentPiInput
    {
        public Point2D P { get; set; }
        public double Radius { get; set; }
        public double SpiralIn { get; set; }
        public double SpiralOut { get; set; }
        public string Tag { get; set; }
    }
}
