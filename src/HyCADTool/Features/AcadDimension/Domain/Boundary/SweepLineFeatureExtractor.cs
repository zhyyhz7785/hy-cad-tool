using System;
using System.Collections.Generic;
using HyCADTool.Features.AcadDimension.Domain.Config;
using HyCADTool.Features.AcadDimension.Domain.Context;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Features.AcadDimension.Domain.Boundary
{
    /// <summary>
    /// 拓扑特征提取器（割线扫描法）。范式：S1a 解析式直接派生（06 §5）。
    ///
    /// 算法骨架与老 dds GetAllSecantLines* 一致——但 Domain 化、纯几何，
    /// 修 06 §7 三处稳健性问题：
    ///   #1 步长硬编码 → 自适应（短边 5%，clamp[1,100]）；
    ///   #5 双 API（IntersectWith 数量 + NTS 坐标）→ 单路径纯几何；
    ///   #8 Bulge 段被忽略 → Service 层入口已经 tessellate 进 Polyline2D，本类只面对顶点序列。
    ///
    /// 输出：HorizontalSecantColumns / VerticalSecantColumns。每个"列"对应老 dds 一条
    /// 割线在 polyline 上切出的所有边段（顶点 i → 顶点 i+1 的直线段）。
    /// </summary>
    public sealed class SweepLineFeatureExtractor : IBoundaryFeatureExtractor
    {
        private const double Tolerance = 1e-6;

        public BoundaryFeatures Extract(Polyline2D boundary, NewDdsConfig config, NewDdsContext context)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var features = new BoundaryFeatures
            {
                Vertices = boundary.Vertices
            };

            if (boundary.VertexCount < 3)
            {
                features.Diagnostics.Add("[Sweep] 顶点数 < 3，无法形成闭合边界");
                return features;
            }

            features.Bounds = ComputeBounds(boundary);
            context.Bounds = features.Bounds;

            double step = ResolveEffectiveStep(features.Bounds, config);
            context.EffectiveStep = step;

            features.HorizontalSecantColumns = ScanHorizontal(boundary, features.Bounds, step, config, features);
            features.VerticalSecantColumns = ScanVertical(boundary, features.Bounds, step, config, features);

            return features;
        }

        // === 自适应步长（修 06 §7 #1）==============================================
        private static double ResolveEffectiveStep(BoundingBox bounds, NewDdsConfig config)
        {
            if (config.StepStrategy == StepStrategy.Fixed)
                return config.FixedStepValue > 1e-3 ? config.FixedStepValue : 20.0;

            double width = bounds.MaxPoint.X - bounds.MinPoint.X;
            double height = bounds.MaxPoint.Y - bounds.MinPoint.Y;
            double shorter = Math.Min(width, height);
            double s = shorter * 0.05;
            if (s < 1.0) s = 1.0;
            if (s > 100.0) s = 100.0;
            return s;
        }

        private static BoundingBox ComputeBounds(Polyline2D boundary)
        {
            double xmin = double.MaxValue, ymin = double.MaxValue;
            double xmax = double.MinValue, ymax = double.MinValue;
            foreach (var v in boundary.Vertices)
            {
                if (v.X < xmin) xmin = v.X;
                if (v.X > xmax) xmax = v.X;
                if (v.Y < ymin) ymin = v.Y;
                if (v.Y > ymax) ymax = v.Y;
            }
            return new BoundingBox(new Point2D(xmin, ymin), new Point2D(xmax, ymax));
        }

        // === 水平扫描（y = 常数）===================================================
        // 推进策略与老 dds 一致：若拓扑稳定（最短边 EndPoint.Y 决定区间）则跳过。
        private static IReadOnlyList<IReadOnlyList<Line2D>> ScanHorizontal(
            Polyline2D boundary, BoundingBox bounds, double step,
            NewDdsConfig config, BoundaryFeatures features)
        {
            var columns = new List<IReadOnlyList<Line2D>>();
            double yLo = bounds.MinPoint.Y;
            double yHi = bounds.MaxPoint.Y;
            double y = yLo + step / 2.0;
            int safety = 0;

            while (y < yHi - Tolerance && safety < config.SafetyIterationLimit)
            {
                var crossings = HorizontalCrossings(boundary, y);
                if (crossings.Count >= 2)
                {
                    var lines = BuildEdgeLinesAt(boundary, crossings);
                    SortByMidX(lines);
                    NormalizeDirectionY(lines);
                    columns.Add(lines);

                    double topMin = double.MaxValue;
                    foreach (var l in lines)
                        if (l.EndPoint.Y < topMin) topMin = l.EndPoint.Y;

                    y = Math.Max(y + step, topMin + step / 2.0);
                }
                else
                {
                    y += step / 5.0;
                }
                safety++;
            }

            if (safety >= config.SafetyIterationLimit)
                features.Diagnostics.Add($"[Sweep-H] safetyCounter 命中 {safety}（请检查步长配置）");
            features.Diagnostics.Add($"[Sweep-H] 列数={columns.Count} step={step:F2}");
            return columns;
        }

        // === 垂直扫描（x = 常数）===================================================
        private static IReadOnlyList<IReadOnlyList<Line2D>> ScanVertical(
            Polyline2D boundary, BoundingBox bounds, double step,
            NewDdsConfig config, BoundaryFeatures features)
        {
            var columns = new List<IReadOnlyList<Line2D>>();
            double xLo = bounds.MinPoint.X;
            double xHi = bounds.MaxPoint.X;
            double x = xLo + step / 2.0;
            int safety = 0;

            while (x < xHi - Tolerance && safety < config.SafetyIterationLimit)
            {
                var crossings = VerticalCrossings(boundary, x);
                if (crossings.Count >= 2)
                {
                    var lines = BuildEdgeLinesAt(boundary, crossings);
                    SortByMidY(lines);
                    NormalizeDirectionX(lines);
                    columns.Add(lines);

                    double rightMin = double.MaxValue;
                    foreach (var l in lines)
                        if (l.EndPoint.X < rightMin) rightMin = l.EndPoint.X;

                    x = Math.Max(x + step, rightMin + step / 2.0);
                }
                else
                {
                    x += step / 5.0;
                }
                safety++;
            }

            if (safety >= config.SafetyIterationLimit)
                features.Diagnostics.Add($"[Sweep-V] safetyCounter 命中 {safety}（请检查步长配置）");
            features.Diagnostics.Add($"[Sweep-V] 列数={columns.Count} step={step:F2}");
            return columns;
        }

        // === 水平交点：y=Y 与 polyline 各边的交点 ==================================
        // 顶点穿越规则：(yA <= y && yB > y) || (yB <= y && yA > y)，避免在顶点处重复计交。
        private static List<Crossing> HorizontalCrossings(Polyline2D pl, double y)
        {
            var crossings = new List<Crossing>();
            var verts = pl.Vertices;
            int n = verts.Count;
            int last = pl.IsClosed ? n : n - 1;

            for (int i = 0; i < last; i++)
            {
                var a = verts[i];
                var b = verts[(i + 1) % n];
                double ya = a.Y, yb = b.Y;
                if (Math.Abs(ya - yb) < Tolerance) continue;
                bool crosses = (ya <= y && yb > y) || (yb <= y && ya > y);
                if (!crosses) continue;
                double t = (y - ya) / (yb - ya);
                double x = a.X + t * (b.X - a.X);
                crossings.Add(new Crossing(i, x));
            }
            return crossings;
        }

        // === 垂直交点：x=X 与 polyline 各边的交点 ==================================
        private static List<Crossing> VerticalCrossings(Polyline2D pl, double x)
        {
            var crossings = new List<Crossing>();
            var verts = pl.Vertices;
            int n = verts.Count;
            int last = pl.IsClosed ? n : n - 1;

            for (int i = 0; i < last; i++)
            {
                var a = verts[i];
                var b = verts[(i + 1) % n];
                double xa = a.X, xb = b.X;
                if (Math.Abs(xa - xb) < Tolerance) continue;
                bool crosses = (xa <= x && xb > x) || (xb <= x && xa > x);
                if (!crosses) continue;
                double t = (x - xa) / (xb - xa);
                double yc = a.Y + t * (b.Y - a.Y);
                crossings.Add(new Crossing(i, yc));
            }
            return crossings;
        }

        // === 把交点列表对应的 polyline 边段还原成 Line2D（与老 dds 语义一致）======
        private static List<Line2D> BuildEdgeLinesAt(Polyline2D pl, List<Crossing> crossings)
        {
            var verts = pl.Vertices;
            int n = verts.Count;
            var result = new List<Line2D>(crossings.Count);
            foreach (var c in crossings)
                result.Add(new Line2D(verts[c.SegIndex], verts[(c.SegIndex + 1) % n]));
            return result;
        }

        // === 排序与方向归一 ========================================================
        private static void SortByMidX(List<Line2D> lines)
        {
            lines.Sort((a, b) => ((a.StartPoint.X + a.EndPoint.X) * 0.5)
                .CompareTo((b.StartPoint.X + b.EndPoint.X) * 0.5));
        }

        private static void SortByMidY(List<Line2D> lines)
        {
            lines.Sort((a, b) => ((a.StartPoint.Y + a.EndPoint.Y) * 0.5)
                .CompareTo((b.StartPoint.Y + b.EndPoint.Y) * 0.5));
        }

        private static void NormalizeDirectionY(List<Line2D> lines)
        {
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].StartPoint.Y > lines[i].EndPoint.Y)
                    lines[i] = new Line2D(lines[i].EndPoint, lines[i].StartPoint);
        }

        private static void NormalizeDirectionX(List<Line2D> lines)
        {
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].StartPoint.X > lines[i].EndPoint.X)
                    lines[i] = new Line2D(lines[i].EndPoint, lines[i].StartPoint);
        }

        private readonly struct Crossing
        {
            public int SegIndex { get; }
            public double Coord { get; }
            public Crossing(int segIndex, double coord) { SegIndex = segIndex; Coord = coord; }
        }
    }
}
