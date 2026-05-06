using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Features.AcadDimension.Services
{
    /// <summary>
    /// 把 AutoCAD Polyline 转换为 Domain 层平台无关的 Polyline2D（含 Bulge 段细分）。
    /// 修 06 §7 #8：老 dds 直接拿 polyline 顶点顺序送给割线扫描，弧段特征丢失；
    /// NewDDS 在 Service 入口把 Bulge 弧段 tessellate 成多顶点直线段链，Domain 内部
    /// 即可只面对"顶点序列"这一种几何，扫描法可纯几何无 AutoCAD API 实现。
    ///
    /// 设计要点：
    ///  - 仅本类引用 AutoCAD API；Domain 完全不感知 Polyline；
    ///  - precision 是"实际 mm"——表示弧段离散后每小段沿弧长不超过该值；
    ///  - 闭合 Polyline 保留 IsClosed=true；最后一段（vN-1 → v0）若有 Bulge 也参与离散。
    /// </summary>
    internal static class AcadPolylineConverter
    {
        private const double BulgeEpsilon = 1e-10;

        /// <summary>
        /// 转换为 Polyline2D。precision &lt; 1e-3 时按 1e-3 兜底，避免段数无穷大。
        /// </summary>
        public static Polyline2D ToPolyline2D(Polyline pline, double precision)
        {
            if (pline == null) throw new ArgumentNullException(nameof(pline));
            if (precision < 1e-3) precision = 1e-3;

            int n = pline.NumberOfVertices;
            bool closed = pline.Closed;
            var verts = new List<Point2D>(n * 2);

            for (int i = 0; i < n; i++)
            {
                var p = pline.GetPoint2dAt(i);
                verts.Add(new Point2D(p.X, p.Y));

                bool hasNextSegment = (i < n - 1) || closed;
                if (!hasNextSegment) continue;

                double bulge = pline.GetBulgeAt(i);
                if (Math.Abs(bulge) <= BulgeEpsilon) continue;

                int nextIdx = (i + 1) % n;
                var pNext = pline.GetPoint2dAt(nextIdx);
                var midPoints = TessellateBulgeMidpoints(
                    new Point2D(p.X, p.Y),
                    new Point2D(pNext.X, pNext.Y),
                    bulge,
                    precision);
                verts.AddRange(midPoints);
            }

            return new Polyline2D(verts, isClosed: closed);
        }

        /// <summary>
        /// 弧段细分（仅返回中间点；起点已由调用方加入，终点由下一次 i 加入）。
        /// 与 CurveSegmentExtractor.BulgeToArc2D 圆心几何保持一致。
        /// </summary>
        private static List<Point2D> TessellateBulgeMidpoints(
            Point2D start, Point2D end, double bulge, double precision)
        {
            double chord = Math.Sqrt(
                (end.X - start.X) * (end.X - start.X) +
                (end.Y - start.Y) * (end.Y - start.Y));
            if (chord < 1e-9) return new List<Point2D>();

            double sweep = 4.0 * Math.Atan(Math.Abs(bulge));
            double radius = chord / (2.0 * Math.Sin(sweep / 2.0));
            double arcLength = radius * sweep;
            int segments = Math.Max(2, (int)Math.Ceiling(arcLength / precision));

            double mx = (start.X + end.X) / 2.0;
            double my = (start.Y + end.Y) / 2.0;
            double chordDx = (end.X - start.X) / chord;
            double chordDy = (end.Y - start.Y) / chord;
            double perpX = -chordDy;
            double perpY = chordDx;
            double sagitta = Math.Abs(bulge) * chord / 2.0;
            double distToCenter = radius - sagitta;
            int sign = bulge > 0 ? 1 : -1;
            double cx = mx + sign * perpX * distToCenter;
            double cy = my + sign * perpY * distToCenter;

            double a0 = Math.Atan2(start.Y - cy, start.X - cx);
            double a1 = Math.Atan2(end.Y - cy, end.X - cx);
            if (bulge > 0 && a1 <= a0) a1 += 2.0 * Math.PI;
            if (bulge < 0 && a1 >= a0) a1 -= 2.0 * Math.PI;

            var mid = new List<Point2D>(segments - 1);
            for (int k = 1; k < segments; k++)
            {
                double t = (double)k / segments;
                double a = a0 + (a1 - a0) * t;
                mid.Add(new Point2D(cx + radius * Math.Cos(a), cy + radius * Math.Sin(a)));
            }
            return mid;
        }
    }
}
