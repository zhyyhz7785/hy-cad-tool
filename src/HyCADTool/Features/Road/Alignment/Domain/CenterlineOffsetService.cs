using System;
using System.Collections.Generic;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Features.Road.PlanAlignment.Domain
{
    /// <summary>
    /// 把 <see cref="Polyline3D"/>（平面中心线）整体向左 / 右平移一个距离，生成新的辅助 Polyline3D。
    ///
    /// 算法：「逐段平移 + 交点连接」。
    /// · 对原 Polyline 每一段 (V[i], V[i+1])：计算单位切向 t，左侧法向 n = <c>t.Perpendicular()</c>（逆时针 90°）；
    ///   平移后的段为两端 V[i] + offset·n 和 V[i+1] + offset·n。
    /// · 相邻平移段的连接点：求两条平移段所在直线的交点；若两段几乎共线（叉积 ≈ 0），
    ///   退化为直接取后一段的起点，避免数值爆炸。
    /// · 首段平移起点 / 末段平移终点按端点法向平移（不涉及交点）。
    ///
    /// <b>输入约定</b>：<paramref name="source"/> 只读 XY 顶点；Z 继承原值，本服务不做 3D 曲线处理。
    /// <b>偏移方向</b>：<c>offset &gt; 0</c> 为"沿前进方向的左侧"（与 <c>Vector2D.Perpendicular()</c> 一致）；
    /// <c>offset &lt; 0</c> 为右侧；0 返回原顶点副本。
    ///
    /// <b>已知局限</b>：v1 只做平面折线级的"刚性偏移"，在曲率极大 / 偏移量大于最小曲率半径时可能
    /// 产生自交或回环。生产中应限制 |offset| &lt; min(R)，或由调用方做后处理。
    /// </summary>
    public static class CenterlineOffsetService
    {
        /// <summary>
        /// 计算 <see cref="Polyline3D"/> 所有圆弧段的最小曲率半径（单位：米）。
        /// 直线段（|bulge| &lt; 1e-12）跳过；全为直线时返回 <see cref="double.PositiveInfinity"/>。
        ///
        /// <b>用途</b>：作为偏移距离的硬上限 —— <c>|offset| &lt; MinCurvatureRadius</c> 时偏移不会自交；
        /// 否则几何上无解（内曲段的偏移半径变为负，退化为"内翻"折线）。
        /// </summary>
        public static double ComputeMinCurvatureRadius(Polyline3D source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            double min = double.PositiveInfinity;
            for (int i = 0; i < source.SegmentCount; i++)
            {
                double b = source.GetBulgeAt(i);
                if (Math.Abs(b) < 1e-12) continue;
                var seg = source.GetSegmentAt(i);
                double dx = seg.End.X - seg.Start.X;
                double dy = seg.End.Y - seg.Start.Y;
                double chord = Math.Sqrt(dx * dx + dy * dy);
                if (chord < 1e-12) continue;
                double theta = 2.0 * Math.Atan(Math.Abs(b));
                double sinHalfIncluded = Math.Sin(theta);
                if (sinHalfIncluded < 1e-12) continue;
                double r = chord / (2.0 * sinHalfIncluded);
                if (r < min) min = r;
            }
            return min;
        }

        /// <summary>
        /// 对一条 XY 顶点序列做平行偏移，返回新序列（长度与输入一致；重合顶点会被跳过）。
        /// </summary>
        /// <param name="source">原顶点（至少 2 个，重合顶点会在内部剔除）。</param>
        /// <param name="offset">偏移距离（米，正=左侧、负=右侧、0=原样返回）。</param>
        /// <param name="tolerance">相邻顶点重合判定 / 平行段判定容差。</param>
        public static List<Point2D> Offset(
            IReadOnlyList<Point2D> source,
            double offset,
            double tolerance = 1e-9)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Count < 2) throw new ArgumentException("至少 2 个顶点。", nameof(source));

            if (Math.Abs(offset) < tolerance)
            {
                var copy = new List<Point2D>(source.Count);
                foreach (var p in source) copy.Add(p);
                return copy;
            }

            // 1) 过滤重合顶点
            var clean = new List<Point2D>(source.Count) { source[0] };
            for (int i = 1; i < source.Count; i++)
            {
                if (source[i].DistanceTo(clean[clean.Count - 1]) > tolerance)
                    clean.Add(source[i]);
            }
            if (clean.Count < 2)
                throw new ArgumentException("过滤重合后顶点不足 2 个，无法偏移。", nameof(source));

            // 2) 逐段计算单位切向 / 左法向 / 平移端点
            int segCount = clean.Count - 1;
            var tangents = new Vector2D[segCount];
            var normals = new Vector2D[segCount];
            var aPoints = new Point2D[segCount]; // 平移后段起点
            var bPoints = new Point2D[segCount]; // 平移后段终点
            for (int i = 0; i < segCount; i++)
            {
                var d = new Vector2D(clean[i + 1].X - clean[i].X, clean[i + 1].Y - clean[i].Y);
                if (!d.TryNormalize(out var t, tolerance))
                    throw new InvalidOperationException($"段[{i}] 方向退化，无法偏移。");
                var n = t.Perpendicular();
                tangents[i] = t;
                normals[i] = n;
                aPoints[i] = new Point2D(clean[i].X + offset * n.X, clean[i].Y + offset * n.Y);
                bPoints[i] = new Point2D(clean[i + 1].X + offset * n.X, clean[i + 1].Y + offset * n.Y);
            }

            // 3) 拼输出顶点
            var result = new List<Point2D>(clean.Count) { aPoints[0] };
            for (int i = 1; i < segCount; i++)
            {
                if (!TryIntersect(aPoints[i - 1], tangents[i - 1], aPoints[i], tangents[i], tolerance, out var p))
                {
                    // 共线时两个平移段实际上是同一条线——任取 aPoints[i]
                    result.Add(aPoints[i]);
                }
                else
                {
                    result.Add(p);
                }
            }
            result.Add(bPoints[segCount - 1]);
            return result;
        }

        /// <summary>
        /// 对 <see cref="Polyline3D"/> 的 XY 顶点做偏移，Z 直接取原顶点 Z。
        /// 输入的顶点数可能与输出不同（重合顶点被过滤）。
        /// </summary>
        public static Polyline3D Offset(Polyline3D source, double offset, double tolerance = 1e-9)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.VertexCount < 2) throw new ArgumentException("至少 2 个顶点。", nameof(source));

            // 最小曲率半径自检：|offset| ≥ Rmin − 1mm 视为必然自交。
            double rMin = ComputeMinCurvatureRadius(source);
            if (!double.IsInfinity(rMin) && Math.Abs(offset) >= rMin - 1e-3)
            {
                throw new InvalidOperationException(
                    $"偏移距离 |{offset:F3}| m 已逼近/超过最小曲率半径 {rMin:F3} m，"
                    + "偏移结果将产生自交或回环；请减小偏移量。");
            }

            var pts2d = new List<Point2D>(source.VertexCount);
            var zValues = new List<double>(source.VertexCount);
            for (int i = 0; i < source.VertexCount; i++)
            {
                var p = source.GetPointAt(i);
                pts2d.Add(new Point2D(p.X, p.Y));
                zValues.Add(p.Z);
            }

            var offs = Offset(pts2d, offset, tolerance);
            // offs 长度可能 ≤ pts2d（重合过滤），按比例最近取 Z
            var result = new List<Point3D>(offs.Count);
            for (int i = 0; i < offs.Count; i++)
            {
                double z = zValues.Count == pts2d.Count && offs.Count == pts2d.Count
                    ? zValues[i]
                    : zValues[0];
                result.Add(new Point3D(offs[i].X, offs[i].Y, z));
            }
            return new Polyline3D(result);
        }

        /// <summary>
        /// 直线求交：P0 + s·d0 与 P1 + u·d1 的交点。
        /// 平行（叉积 &lt; <paramref name="tolerance"/>）时返回 <c>false</c>。
        /// </summary>
        private static bool TryIntersect(
            Point2D p0, Vector2D d0,
            Point2D p1, Vector2D d1,
            double tolerance,
            out Point2D intersection)
        {
            double denom = d0.X * d1.Y - d0.Y * d1.X;
            if (Math.Abs(denom) < tolerance)
            {
                intersection = default(Point2D);
                return false;
            }
            double dx = p1.X - p0.X;
            double dy = p1.Y - p0.Y;
            double s = (dx * d1.Y - dy * d1.X) / denom;
            intersection = new Point2D(p0.X + s * d0.X, p0.Y + s * d0.Y);
            return true;
        }
    }
}
