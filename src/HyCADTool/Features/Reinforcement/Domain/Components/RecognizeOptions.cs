using System.Collections.Generic;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>竖边成对识别出的墙柱矩形（mm）。</summary>
    public sealed class WallColumnRect
    {
        public double X0 { get; set; }
        public double X1 { get; set; }
        public double YBot { get; set; }
        public double YTop { get; set; }

        public double WidthMm => X1 - X0;
        public double HeightMm => YTop - YBot;

        /// <summary>边对重叠高 / 混凝土柱全高（平行占比诊断）。</summary>
        public double OverlapRatio { get; set; }
    }

    /// <summary>N12 等扩展识别选项；默认与 N8 行为一致。</summary>
    public sealed class RecognizeOptions
    {
        public static RecognizeOptions Default { get; } = new RecognizeOptions();

        /// <summary>土气割线 Y（mm）；有值时作 grounded 门控与底板 cut 上限。</summary>
        public double? SoilCutY { get; set; }

        /// <summary>出口裁剪：每个分区多边形 ∩ (Outer − Holes)。</summary>
        public bool ClipToRegion { get; set; }

        /// <summary>阶段2 用竖直轮廓边成对识别墙（N12）；false 时走 N8 逐带量宽。</summary>
        public bool UseVerticalEdgeWalls { get; set; }

        /// <summary>竖边墙最小重叠高度（mm）；默认 200。</summary>
        public double WallMinHeightMm { get; set; } = 200.0;

        /// <summary>竖边墙检测输出（UseVerticalEdgeWalls 时填充）。</summary>
        public List<WallColumnRect> DetectedWallColumns { get; } = new List<WallColumnRect>();
    }
}
