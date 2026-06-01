using System;
using System.Collections.Generic;
using HyCADTool.Domain.Models.Road;
using HyCAD.Geometry;

namespace HyCADTool.Features.Road.PlanProfile.Domain
{
    /// <summary>
    /// 现状地面线（EG, Existing Grade）采样器。
    ///
    /// 沿 Alignment 中心线（XY 平面）按桩号步进，在用户拾取的 3D 地面多段线上
    /// 取最近 XY 投影点的 Z，组装成可直接喂给
    /// <c>RoadProfileService.CreateOrReplaceEgProfile</c> 的 <see cref="ProfileVertex"/> 序列。
    ///
    /// 设计原则：
    /// - 纯 C#，零 AutoCAD / WPF 依赖（命令层负责将 AutoCAD <c>Polyline</c> / <c>Polyline3d</c>
    ///   通过 <c>RoadGeometryBridge</c> 转成 <see cref="Polyline3D"/>）；
    /// - 不写状态、不抛异常（仅在参数级别抛 <see cref="ArgumentException"/>），
    ///   "超界 / 重合" 等情况通过 <see cref="Result"/> 字段告知；
    /// - 桩号语义：基于 centerline 的 XY 平面累计弧长（与 <see cref="Polyline3D.SamplePlanarStations"/> 一致）；
    /// - groundLine 的弧段（bulge != 0）按弦近似处理 —— 现实地形采样几乎都是直线段连接的
    ///   <c>Polyline3d</c>，弧段近似带来的偏差远小于地形本身的不确定性。
    /// </summary>
    public static class EgProfileSampler
    {
        /// <summary>采样配置。</summary>
        public sealed class Options
        {
            /// <summary>桩号步长（m）。默认 5；必须 &gt; 0。</summary>
            public double IntervalM { get; set; } = 5.0;

            /// <summary>是否在末尾追加一个"恰好等于平面长度"的样点。默认 true。</summary>
            public bool IncludeEnd { get; set; } = true;

            /// <summary>
            /// 中心线某桩号的 XY 投影点到 groundLine 的最大允许横向距离（m）。
            /// 超过此距离视为"地面线未覆盖该桩号"，跳过该样点。
            /// 默认 1000 m（≈不限制）；设为 ≤ 0 表示完全不限制。
            /// </summary>
            public double MaxLateralOffsetM { get; set; } = 1000.0;
        }

        /// <summary>采样结果。</summary>
        public sealed class Result
        {
            /// <summary>采样得到的 PVI 序列（Station 升序，CurveRadius = 0）。</summary>
            public IReadOnlyList<ProfileVertex> Vertices { get; }

            /// <summary>因 <see cref="Options.MaxLateralOffsetM"/> 截断而跳过的桩号数。</summary>
            public int SkippedOutOfRange { get; }

            /// <summary>已采样桩号中遇到的最大横向偏移（m），便于命令层回显诊断。</summary>
            public double MaxLateralOffsetSeen { get; }

            public Result(IReadOnlyList<ProfileVertex> vertices, int skipped, double maxOffset)
            {
                Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
                SkippedOutOfRange = skipped;
                MaxLateralOffsetSeen = maxOffset;
            }
        }

        /// <summary>
        /// 沿 <paramref name="centerline"/> 采样 <paramref name="groundLine"/> 的高程。
        ///
        /// 算法：
        /// 1. 调 <see cref="Polyline3D.SamplePlanarStations"/> 按 <paramref name="options"/> 步进生成桩号；
        /// 2. 每个桩号取 centerline 的 XY 投影点；
        /// 3. 在 groundLine 的每个分段（XY 平面）上找最近投影，取该投影处的 Z（按段内 t 线性插值 Z）；
        /// 4. 距离 &gt; <see cref="Options.MaxLateralOffsetM"/> → 跳过；
        ///    否则记 <see cref="ProfileVertex"/>(Station, Elevation, CurveRadius=0)。
        /// </summary>
        public static Result Sample(
            Polyline3D centerline,
            Polyline3D groundLine,
            Options options = null)
        {
            if (centerline == null) throw new ArgumentNullException(nameof(centerline));
            if (groundLine == null) throw new ArgumentNullException(nameof(groundLine));
            if (centerline.VertexCount < 2) throw new ArgumentException("centerline 顶点不足 2 个。", nameof(centerline));
            if (groundLine.VertexCount < 2) throw new ArgumentException("groundLine 顶点不足 2 个。", nameof(groundLine));

            var opts = options ?? new Options();
            if (opts.IntervalM <= 0)
                throw new ArgumentOutOfRangeException(nameof(options), $"IntervalM 必须 > 0，当前 {opts.IntervalM}。");

            var vertices = new List<ProfileVertex>();
            int skipped = 0;
            double maxOffsetSeen = 0;

            foreach (var sample in centerline.SamplePlanarStations(opts.IntervalM, 0, opts.IncludeEnd))
            {
                ProjectToGroundLine(sample.Point, groundLine, out double elevation, out double lateralOffset);
                if (lateralOffset > maxOffsetSeen) maxOffsetSeen = lateralOffset;

                if (opts.MaxLateralOffsetM > 0 && lateralOffset > opts.MaxLateralOffsetM)
                {
                    skipped++;
                    continue;
                }

                vertices.Add(new ProfileVertex
                {
                    Station = sample.Station,
                    Elevation = elevation,
                    CurveRadius = 0,
                });
            }

            return new Result(vertices.AsReadOnly(), skipped, maxOffsetSeen);
        }

        // ============================== 内部 ==============================

        /// <summary>
        /// 把空间点投影到 groundLine 的 XY 平面，返回最近段内的 Z 与到 groundLine 的水平距离。
        /// 调用方保证 groundLine.VertexCount ≥ 2。
        /// </summary>
        private static void ProjectToGroundLine(Point3D pt, Polyline3D groundLine, out double elevation, out double lateralOffset)
        {
            double bestDistSq = double.MaxValue;
            double bestZ = pt.Z;

            int segCount = groundLine.SegmentCount;
            for (int i = 0; i < segCount; i++)
            {
                var seg = groundLine.GetSegmentAt(i);
                ProjectPointToSegmentXY(pt.X, pt.Y, seg.Start, seg.End,
                    out double tClamped, out double distSq);

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestZ = seg.Start.Z + (seg.End.Z - seg.Start.Z) * tClamped;
                }
            }

            elevation = bestZ;
            lateralOffset = Math.Sqrt(bestDistSq);
        }

        /// <summary>
        /// 把 (px, py) 投影到 (a, b) 的 XY 平面段，返回 clamp 到 [0, 1] 的参数 t 与到投影点的距离平方。
        /// </summary>
        private static void ProjectPointToSegmentXY(
            double px, double py, Point3D a, Point3D b,
            out double tClamped, out double distSq)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double len2 = dx * dx + dy * dy;

            double t;
            if (len2 < 1e-12)
            {
                // 退化段（端点重合）：当点处理
                t = 0.0;
            }
            else
            {
                t = ((px - a.X) * dx + (py - a.Y) * dy) / len2;
            }

            tClamped = Math.Max(0.0, Math.Min(1.0, t));

            double projX = a.X + tClamped * dx;
            double projY = a.Y + tClamped * dy;
            double diffX = px - projX;
            double diffY = py - projY;
            distSq = diffX * diffX + diffY * diffY;
        }
    }
}
