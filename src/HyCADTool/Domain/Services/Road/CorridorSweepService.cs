using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Features.Road.CrossSection.Domain;

namespace HyCADTool.Domain.Services.Road
{
    /// <summary>
    /// 平面分段扫掠服务（M10.1）：沿 <see cref="Alignment"/> 按 <see cref="CrossSectionLayout"/> 展开红线 / 板块分界线 /
    /// 立缘石位置，产出 <see cref="CorridorPlanPolylines"/>。
    ///
    /// <para><b>算法</b></para>
    /// <list type="number">
    ///   <item>对 Alignment 的 Centerline 逐段采样（<see cref="SampleAlignmentUniformly"/>）。</item>
    ///   <item>每个采样点 P_i 计算局部切线方向 t，取法向 n（t 旋转 +90°）。</item>
    ///   <item>按 <paramref name="layout"/> 的左半条带累计 -offset_left × n，得到"左红线点"。</item>
    ///   <item>同理累计右半得到"右红线点"。</item>
    ///   <item>各条带边界也一并积累，得到"板块分界线"集合。</item>
    /// </list>
    ///
    /// <para>纯 Domain，不依赖 AutoCAD。</para>
    /// </summary>
    public sealed class CorridorSweepService
    {
        /// <summary>
        /// 扫掠。<paramref name="samplingStepM"/> 默认 5 米。
        /// <paramref name="alignment.Centerline"/> 顶点数 &lt; 2 时抛出。
        /// </summary>
        public CorridorPlanPolylines Sweep(Alignment alignment, CrossSectionLayout layout, double samplingStepM = 5.0)
        {
            if (alignment == null) throw new ArgumentNullException(nameof(alignment));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (alignment.Centerline == null || alignment.Centerline.VertexCount < 2)
                throw new ArgumentException("Alignment.Centerline 顶点数不足 2，无法扫掠。", nameof(alignment));
            if (samplingStepM <= 0) throw new ArgumentOutOfRangeException(nameof(samplingStepM));

            var samples = SampleAlignmentUniformly(alignment.Centerline, samplingStepM);

            var centerline = new List<Point3D>(samples.Count);
            var leftEdge = new List<Point3D>(samples.Count);
            var rightEdge = new List<Point3D>(samples.Count);

            // 板块分界线：左半条带数 + 右半条带数（不含红线本身）
            int leftLanes = Math.Max(0, layout.LeftBands.Count - 1);
            int rightLanes = Math.Max(0, layout.RightBands.Count - 1);
            var leftDividers = new List<Point3D>[leftLanes];
            for (int i = 0; i < leftLanes; i++) leftDividers[i] = new List<Point3D>(samples.Count);
            var rightDividers = new List<Point3D>[rightLanes];
            for (int i = 0; i < rightLanes; i++) rightDividers[i] = new List<Point3D>(samples.Count);

            double halfMedian = layout.CenterMedianWidth / 2.0;

            for (int s = 0; s < samples.Count; s++)
            {
                var sample = samples[s];
                // 法向 = 切向顺时针旋转 90°（+y = 左边；-y = 右边）
                double nx = -sample.Tangent.Y;
                double ny = sample.Tangent.X;

                centerline.Add(sample.Point);

                // 左边：从中心线向左（-n 方向）累加
                double leftOffset = halfMedian;
                for (int i = 0; i < layout.LeftBands.Count; i++)
                {
                    leftOffset += layout.LeftBands[i].Width;
                    var pt = new Point3D(
                        sample.Point.X - nx * leftOffset,
                        sample.Point.Y - ny * leftOffset,
                        sample.Point.Z);
                    if (i == layout.LeftBands.Count - 1)
                        leftEdge.Add(pt);
                    else
                        leftDividers[i].Add(pt);
                }

                // 右边：从中心线向右（+n 方向）累加
                double rightOffset = halfMedian;
                for (int i = 0; i < layout.RightBands.Count; i++)
                {
                    rightOffset += layout.RightBands[i].Width;
                    var pt = new Point3D(
                        sample.Point.X + nx * rightOffset,
                        sample.Point.Y + ny * rightOffset,
                        sample.Point.Z);
                    if (i == layout.RightBands.Count - 1)
                        rightEdge.Add(pt);
                    else
                        rightDividers[i].Add(pt);
                }
            }

            return new CorridorPlanPolylines
            {
                AlignmentId = alignment.Id,
                CenterLine = MakePoly(centerline),
                LeftRedLine = MakePoly(leftEdge),
                RightRedLine = MakePoly(rightEdge),
                LeftBandDividers = MakePolyList(leftDividers),
                RightBandDividers = MakePolyList(rightDividers),
            };
        }

        /// <summary>
        /// 按桩号区间为每条采样点选择不同 <see cref="CrossSectionLayout"/> 扫掠，硬切于区段边界。
        /// 若各区段条带数不一致，仅输出中心线 + 左右红线，板块分界线置空（避免多模板条带数不一致难以拼接）。
        /// </summary>
        /// <returns>扫掠结果 + 是否输出了板块分界线（多模板条带数不一致时仅有红线+中心线）。</returns>
        public (CorridorPlanPolylines Plan, bool UsedBandDividers) SweepWithAssignments(
            Alignment alignment,
            IReadOnlyList<Template> allTemplates,
            IReadOnlyList<CrossSectionAssignment> assignments,
            double samplingStepM = 5.0)
        {
            bool usedBandDividers = false;
            if (alignment == null) throw new ArgumentNullException(nameof(alignment));
            if (allTemplates == null) throw new ArgumentNullException(nameof(allTemplates));
            if (assignments == null || assignments.Count == 0)
                throw new ArgumentException("assignments 不能为空。", nameof(assignments));
            if (alignment.Centerline == null || alignment.Centerline.VertexCount < 2)
                throw new ArgumentException("Alignment.Centerline 顶点数不足 2，无法扫掠。", nameof(alignment));
            if (samplingStepM <= 0) throw new ArgumentOutOfRangeException(nameof(samplingStepM));

            var totalLen = alignment.Centerline.GetPlanarLength();
            var segs = new List<(double lo, double hi, CrossSectionLayout L)>();
            foreach (var a in assignments
                         .OrderBy(x => System.Math.Min(x.StartStation, x.EndStation))
                         .ToList())
            {
                if (a == null) continue;
                var (ok, err) = a.Validate(totalLen);
                if (!ok) throw new InvalidOperationException("CrossSectionAssignment 非法：" + err);
                var tpl = allTemplates.FirstOrDefault(t => t != null && t.Id == a.TemplateId);
                if (tpl == null) throw new InvalidOperationException("找不到 TemplateId=" + a.TemplateId);
                var lo = System.Math.Min(a.StartStation, a.EndStation);
                var hi = System.Math.Max(a.StartStation, a.EndStation);
                var layout = CrossSectionLayoutBuilder.FromTemplate(tpl);
                if (layout == null) throw new InvalidOperationException("Template 无法反解 CrossSectionLayout。");
                segs.Add((lo, hi, layout));
            }

            if (segs.Count == 0) throw new InvalidOperationException("无有效区段。");

            bool sameBands = segs.All(s =>
                s.L.LeftBands.Count == segs[0].L.LeftBands.Count
                && s.L.RightBands.Count == segs[0].L.RightBands.Count);
            if (!sameBands) { /* 仅红 + 中 */ }
            else usedBandDividers = true;

            var defaultL = segs[0].L;
            var samples = SampleAlignmentUniformly(alignment.Centerline, samplingStepM);
            if (samples.Count < 2)
                throw new InvalidOperationException("采样点不足 2。");

            int leftLanes = sameBands ? System.Math.Max(0, segs[0].L.LeftBands.Count - 1) : 0;
            int rightLanes = sameBands ? System.Math.Max(0, segs[0].L.RightBands.Count - 1) : 0;

            var centerline = new List<Point3D>(samples.Count);
            var leftEdge = new List<Point3D>(samples.Count);
            var rightEdge = new List<Point3D>(samples.Count);
            var leftDividers = new List<Point3D>[leftLanes];
            for (int i = 0; i < leftLanes; i++) leftDividers[i] = new List<Point3D>(samples.Count);
            var rightDividers = new List<Point3D>[rightLanes];
            for (int i = 0; i < rightLanes; i++) rightDividers[i] = new List<Point3D>(samples.Count);

            for (int s = 0; s < samples.Count; s++)
            {
                var sample = samples[s];
                var layout = PickLayoutForStation(sample.StationM, segs, defaultL, totalLen);
                double nx = -sample.Tangent.Y;
                double ny = sample.Tangent.X;
                centerline.Add(sample.Point);

                double halfMedian = layout.CenterMedianWidth / 2.0;

                double leftOffset = halfMedian;
                for (int i = 0; i < layout.LeftBands.Count; i++)
                {
                    leftOffset += layout.LeftBands[i].Width;
                    var pt = new Point3D(
                        sample.Point.X - nx * leftOffset,
                        sample.Point.Y - ny * leftOffset,
                        sample.Point.Z);
                    if (i == layout.LeftBands.Count - 1)
                        leftEdge.Add(pt);
                    else if (sameBands)
                        leftDividers[i].Add(pt);
                }

                double rightOffset = halfMedian;
                for (int i = 0; i < layout.RightBands.Count; i++)
                {
                    rightOffset += layout.RightBands[i].Width;
                    var pt = new Point3D(
                        sample.Point.X + nx * rightOffset,
                        sample.Point.Y + ny * rightOffset,
                        sample.Point.Z);
                    if (i == layout.RightBands.Count - 1)
                        rightEdge.Add(pt);
                    else if (sameBands)
                        rightDividers[i].Add(pt);
                }
            }

            return (new CorridorPlanPolylines
            {
                AlignmentId = alignment.Id,
                CenterLine = MakePoly(centerline),
                LeftRedLine = MakePoly(leftEdge),
                RightRedLine = MakePoly(rightEdge),
                LeftBandDividers = sameBands ? MakePolyList(leftDividers) : new List<Polyline3D>(),
                RightBandDividers = sameBands ? MakePolyList(rightDividers) : new List<Polyline3D>(),
            }, usedBandDividers);
        }

        private static CrossSectionLayout PickLayoutForStation(
            double st,
            List<(double lo, double hi, CrossSectionLayout L)> segs,
            CrossSectionLayout defaultL,
            double totalLen)
        {
            const double eps = 1e-3;
            foreach (var seg in segs)
            {
                if (st + eps >= seg.lo && st - eps <= seg.hi) return seg.L;
            }

            if (st <= segs[0].lo + eps) return segs[0].L;
            if (st >= segs[segs.Count - 1].hi - eps) return segs[segs.Count - 1].L;
            for (int i = 0; i < segs.Count - 1; i++)
            {
                if (st > segs[i].hi && st < segs[i + 1].lo) return segs[i].L;
            }

            return defaultL ?? segs[0].L;
        }

        /// <summary>沿 <paramref name="polyline"/> 均匀采样，返回点 + 切向。</summary>
        public static IReadOnlyList<AlignmentSample> SampleAlignmentUniformly(Polyline3D polyline, double stepM)
        {
            var result = new List<AlignmentSample>();
            if (polyline == null || polyline.VertexCount < 2 || stepM <= 0) return result;

            double accumulated = 0;
            double nextStep = 0;

            // MVP：bulge 忽略（按直线段近似）。真实 bulge 采样留给后续里程碑。
            for (int i = 0; i < polyline.VertexCount - 1; i++)
            {
                var a = polyline.GetPointAt(i);
                var b = polyline.GetPointAt(i + 1);
                double dx = b.X - a.X;
                double dy = b.Y - a.Y;
                double dz = b.Z - a.Z;
                double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (len < 1e-9) continue;

                double tx = dx / len, ty = dy / len;

                // 当 nextStep 落在 [accumulated, accumulated+len) 内时采样
                while (nextStep <= accumulated + len + 1e-9)
                {
                    double u = (nextStep - accumulated) / len;
                    if (u > 1 + 1e-9) break;
                    if (u < 0) u = 0;
                    result.Add(new AlignmentSample(
                        new Point3D(a.X + dx * u, a.Y + dy * u, a.Z + dz * u),
                        new Vector2D(tx, ty),
                        nextStep));
                    nextStep += stepM;
                }

                accumulated += len;
            }

            // 补上末端
            var last = polyline.GetPointAt(polyline.VertexCount - 1);
            if (result.Count == 0 || Distance(result[result.Count - 1].Point, last) > 1e-6)
            {
                var prev = polyline.GetPointAt(polyline.VertexCount - 2);
                double dx = last.X - prev.X, dy = last.Y - prev.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 1e-9)
                {
                    result.Add(new AlignmentSample(last, new Vector2D(dx / len, dy / len), accumulated));
                }
            }

            return result;
        }

        private static double Distance(Point3D a, Point3D b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static Polyline3D MakePoly(List<Point3D> pts)
            => pts.Count >= 2 ? new Polyline3D(pts, isClosed: false) : null;

        private static List<Polyline3D> MakePolyList(List<Point3D>[] arr)
        {
            var result = new List<Polyline3D>(arr.Length);
            foreach (var list in arr)
            {
                var poly = MakePoly(list);
                if (poly != null) result.Add(poly);
            }
            return result;
        }
    }

    /// <summary>单个采样点信息：位置 + 切向 + 沿线里程（m）。</summary>
    public readonly struct AlignmentSample
    {
        public Point3D Point { get; }
        public Vector2D Tangent { get; }
        public double StationM { get; }

        public AlignmentSample(Point3D pt, Vector2D tangent, double station)
        {
            Point = pt;
            Tangent = tangent;
            StationM = station;
        }
    }

    /// <summary>
    /// 扫掠输出：Alignment 分段平面几何集合（M10.1）。
    /// </summary>
    public sealed class CorridorPlanPolylines
    {
        public Guid AlignmentId { get; set; }

        /// <summary>中心线（与 Alignment 原 Centerline 一致的采样序列）。</summary>
        public Polyline3D CenterLine { get; set; }

        /// <summary>左红线（最外条带外缘）。</summary>
        public Polyline3D LeftRedLine { get; set; }

        /// <summary>右红线（最外条带外缘）。</summary>
        public Polyline3D RightRedLine { get; set; }

        /// <summary>左半各条带分界线（不含分隔带中心、不含左红线）。</summary>
        public List<Polyline3D> LeftBandDividers { get; set; } = new List<Polyline3D>();

        /// <summary>右半各条带分界线。</summary>
        public List<Polyline3D> RightBandDividers { get; set; } = new List<Polyline3D>();
    }
}
