using System;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;

namespace HyCADTool.Features.Road.CrossSection.Domain
{
    /// <summary>
    /// 从 AutoCAD 闭合 Polyline 反解一条横断面条带参数（M8.1）。
    ///
    /// <para><b>适用场景</b></para>
    /// 用户在 DWG 平面上圈出一块"已有绿化带 / 人行道 / 板块轮廓"，
    /// 执行 <c>hyRoadExtract</c> 命令 → 本服务反求 <see cref="CrossSectionBand"/>（宽度、横坡、板块号），
    /// 供 <c>hyRoadExtract</c> 命令写入 Domain 或作"黄线 diff 预览"对比。
    ///
    /// <para><b>算法</b></para>
    /// <list type="number">
    ///   <item>用 polyline 的轴向包围盒推断宽度与厚度（水平跨度 = 宽度）。</item>
    ///   <item>水平跨度 &gt; 垂直跨度：视为典型板块（宽扁形），否则拒绝。</item>
    ///   <item>横坡 = (maxY - minY) / width × 100%（符号按左右对称性近似为 0 — 命令层若需要精确可由用户确认）。</item>
    /// </list>
    ///
    /// <para><b>限制</b></para>
    /// v1 仅支持<b>无 bulge</b>的闭合 Polyline（纯折线板块）；含弧段时返回 null，命令层提示用户先 EXPLODE/简化。
    /// </summary>
    public static class PolylineToBandExtractor
    {
        public sealed class ExtractedBandGeometry
        {
            public CrossSectionBand Band { get; set; }
            public double ThicknessCm { get; set; }
            public double ElevationDiff { get; set; }
        }

        /// <summary>
        /// 尝试从 <paramref name="polyline"/> 反解一条条带。失败返回 false，同时填入 <paramref name="errorReason"/>。
        /// </summary>
        /// <param name="polyline">闭合 polyline；如果非闭合 / 顶点过少 / 含弧段将失败。</param>
        /// <param name="kind">用户指定的条带类型（机动车道 / 绿化带 / 人行道 / ...）；<see cref="TemplateComponentKind"/>。</param>
        /// <param name="side">左半 / 右半。</param>
        /// <param name="name">用户给条带的名字。</param>
        /// <param name="band">返回的条带；失败时为默认值。</param>
        /// <param name="errorReason">失败原因（中文），成功时为 null。</param>
        public static bool TryExtract(
            Polyline3D polyline,
            TemplateComponentKind kind,
            BandSide side,
            string name,
            out CrossSectionBand band,
            out string errorReason)
        {
            band = default;
            errorReason = null;

            if (polyline == null)
            {
                errorReason = "polyline 为 null。";
                return false;
            }
            if (polyline.VertexCount < 3)
            {
                errorReason = $"顶点数过少（当前 {polyline.VertexCount}），至少需要 3 个顶点。";
                return false;
            }
            if (!polyline.IsClosed)
            {
                errorReason = "polyline 必须是闭合的。";
                return false;
            }
            if (polyline.HasArcs)
            {
                errorReason = "暂不支持含弧段的 polyline；请先 EXPLODE 或直接用直线段近似。";
                return false;
            }

            double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
            double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
            for (int i = 0; i < polyline.VertexCount; i++)
            {
                var v = polyline.GetPointAt(i);
                if (v.X < minX) minX = v.X;
                if (v.X > maxX) maxX = v.X;
                if (v.Y < minY) minY = v.Y;
                if (v.Y > maxY) maxY = v.Y;
            }

            double width = maxX - minX;
            double height = maxY - minY;

            if (width < 1e-6)
            {
                errorReason = "水平跨度为 0，无法作为条带宽度。";
                return false;
            }
            if (width <= height * 0.5)
            {
                errorReason = $"polyline 的水平跨度（{width:F3} m）不足垂直跨度（{height:F3} m）的一半，不像是横向板块。";
                return false;
            }

            double crossSlopePct = 0;
            if (width > 1e-6 && height > 1e-6)
            {
                crossSlopePct = (height / width) * 100.0;
                if (crossSlopePct > 20) crossSlopePct = 20;  // Band 上限
            }

            try
            {
                band = new CrossSectionBand(
                    name: string.IsNullOrWhiteSpace(name) ? kind.ToString() : name,
                    kind: kind,
                    width: width,
                    crossSlopePct: Math.Round(crossSlopePct, 2),
                    side: side);
                return true;
            }
            catch (Exception ex)
            {
                errorReason = $"构造 CrossSectionBand 失败：{ex.Message}";
                return false;
            }
        }

        public static bool TryExtractWithThickness(
            Polyline3D polyline,
            TemplateComponentKind kind,
            BandSide side,
            string name,
            out ExtractedBandGeometry extracted,
            out string errorReason)
        {
            extracted = null;
            if (!TryExtract(polyline, kind, side, name, out var band, out errorReason))
            {
                return false;
            }

            double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
            for (int i = 0; i < polyline.VertexCount; i++)
            {
                var v = polyline.GetPointAt(i);
                if (v.Y < minY) minY = v.Y;
                if (v.Y > maxY) maxY = v.Y;
            }

            double thicknessCm = (maxY - minY) * 100.0;
            extracted = new ExtractedBandGeometry
            {
                Band = band,
                ThicknessCm = thicknessCm < 0 ? 0 : Math.Round(thicknessCm, 2),
                ElevationDiff = Math.Round(maxY - minY, 3),
            };
            return true;
        }

        /// <summary>
        /// 把反解结果写入 <paramref name="design"/> 的第一个 Template。
        /// 若没有 Template 则新建一个空壳，把条带作为唯一 band 放进去。
        /// 返回写入后的 Template Id。
        /// </summary>
        public static Guid ApplyToDesign(RoadDesign design, CrossSectionBand band)
        {
            if (design == null) throw new ArgumentNullException(nameof(design));

            // MVP：简单把条带信息作为一个 TemplatePoint 追加到第一个 Template。
            // 完整的"按 TemplateComponentKind 合并到正确位置"留给 M10 整合。
            Template tpl;
            if (design.Templates.Count > 0)
            {
                tpl = design.Templates[0];
            }
            else
            {
                tpl = new Template { Name = "提取的模板" };
                design.Templates.Add(tpl);
            }

            int idx = tpl.Points.Count;
            tpl.Points.Add(new TemplatePoint
            {
                Name = band.Name,
                HorizontalOffset = (band.Side == BandSide.Right ? 1 : -1) * (idx + 1) * band.Width,
                VerticalOffset = 0,
                MaterialKey = null,
            });

            return tpl.Id;
        }
    }
}
