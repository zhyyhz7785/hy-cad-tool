using System;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>
    /// 构件识别与分区配筋参数。
    /// </summary>
    public sealed class ComponentParameters
    {
        public double ParallelAngleThresholdDeg { get; set; } = 15.0;
        public double MaxAngleDeg { get; set; } = 90.0;

        public double SlabMaxThicknessMm { get; set; } = 300.0;
        public double WallMaxThicknessMm { get; set; } = 500.0;
        /// <summary>条带最小高宽比 k：竖条 h≥k·w，横条 w≥k·h。默认 2。</summary>
        public double StripMinAspectRatio { get; set; } = 2.0;
        /// <summary>基础底板高度上限（mm）：超出部分归大体积混凝土。</summary>
        public double BottomSlabMaxThicknessMm { get; set; } = 1500.0;

        /// <summary>沿 S_g 竖直割线步进间距（mm）。</summary>
        public double BottomSlabMarchStepMm { get; set; } = 10.0;

        /// <summary>true = 矮凸起并入底板（一体蓝色）；false = 拆分出局部混凝土（底板取最低台阶面）。</summary>
        public bool MergeBumpsIntoBottomSlab { get; set; } = true;

        public double MassConcreteMinSizeMm { get; set; } = 1000.0;

        /// <summary>局部混凝土高差上限（mm）：基础上凸低于该值 → 局部混凝土，否则进墙/大体积判型。</summary>
        public double LocalConcreteMaxHeightMm { get; set; } = 1000.0;

        /// <summary>N16 网格划分：局部混凝土最大高度（mm），上方空且下方为底板/楼板时生效。</summary>
        public double LocalBumpMaxHeightMm { get; set; } = 200.0;
        public double BeamMaxWidthMm { get; set; } = 800.0;
        public double BeamMaxHeightMm { get; set; } = 1500.0;
        public bool BeamSkipReinforcement { get; set; }

        /// <summary>锚固长度（mm）：构件带深入相邻混凝土的延伸限度。</summary>
        public double AnchorageLengthMm { get; set; } = 500.0;

        /// <summary>区域分组聚类距离（mm）：外轮廓 bbox 间隙 ≤ 此值归同一计算组（同 hymbr）。</summary>
        public double RegionGroupDistanceMm { get; set; } = 1500.0;

        /// <summary>基础底面标高（m，模型 Y）：用户输入，默认 -8.1。</summary>
        public double FoundationBottomElevationMm { get; set; } = -8.1;

        /// <summary>埋置深度（m）：从独立图形 ymin 向上，割线 Y = ymin + 本值。</summary>
        public double EmbedmentDepthMm { get; set; } = 3.0;

        /// <summary>平行直线占比下限（0~1）：边对支撑高度 / 混凝土柱全高低于该值时不判墙/梁。</summary>
        public double ParallelLineRatioMin { get; set; } = 0.6;

        public double SlabRebarDiameter { get; set; } = 14.0;
        public double SlabRebarSpacing { get; set; } = 200.0;
        public double WallRebarDiameter { get; set; } = 14.0;
        public double WallRebarSpacing { get; set; } = 200.0;
        public double BottomSlabRebarDiameter { get; set; } = 14.0;
        public double BottomSlabRebarSpacing { get; set; } = 200.0;
        public double MassRebarDiameter { get; set; } = 14.0;
        public double MassRebarSpacing { get; set; } = 200.0;
        public double BeamRebarDiameter { get; set; } = 14.0;
        public double BeamRebarSpacing { get; set; } = 200.0;

        public double MassConstructRebarDiameter { get; set; } = 12.0;
        public double MassConstructRebarMaxSpacing { get; set; } = 500.0;

        public double BumpMaxHeightMm { get; set; } = 150.0;
        public double BumpRebarDiameter { get; set; } = 8.0;
        public double BumpRebarSpacing { get; set; } = 200.0;

        public double Scale { get; set; } = 40.0;

        public double GetRebarDiameter(ComponentType type)
        {
            switch (type)
            {
                case ComponentType.Slab: return SlabRebarDiameter;
                case ComponentType.Wall: return WallRebarDiameter;
                case ComponentType.BottomSlab: return BottomSlabRebarDiameter;
                case ComponentType.MassConcrete: return MassRebarDiameter;
                case ComponentType.LocalConcrete: return MassRebarDiameter;
                case ComponentType.Beam: return BeamRebarDiameter;
                default: return SlabRebarDiameter;
            }
        }

        public double GetRebarSpacing(ComponentType type)
        {
            switch (type)
            {
                case ComponentType.Slab: return SlabRebarSpacing;
                case ComponentType.Wall: return WallRebarSpacing;
                case ComponentType.BottomSlab: return BottomSlabRebarSpacing;
                case ComponentType.MassConcrete: return MassRebarSpacing;
                case ComponentType.LocalConcrete: return MassRebarSpacing;
                case ComponentType.Beam: return BeamRebarSpacing;
                default: return SlabRebarSpacing;
            }
        }

        public string BuildLabelContent(ComponentType type)
        {
            return $"\\U+E532{GetRebarDiameter(type):0}@{GetRebarSpacing(type):0}";
        }
    }
}
