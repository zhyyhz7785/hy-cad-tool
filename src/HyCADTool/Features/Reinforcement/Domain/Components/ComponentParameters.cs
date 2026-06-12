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
        public double BottomSlabMaxThicknessMm { get; set; } = 700.0;

        /// <summary>忽略底板顶面小台阶：run 内 max(t)-min(t) ≤ 阈值时统一取 min(t)。</summary>
        public bool IgnoreBottomSlabHeightDiff { get; set; } = true;

        /// <summary>底板高差阈值（mm），默认 150。</summary>
        public double BottomSlabHeightToleranceMm { get; set; } = 150.0;

        public double MassConcreteMinSizeMm { get; set; } = 1000.0;
        public double BeamMaxWidthMm { get; set; } = 800.0;
        public double BeamMaxHeightMm { get; set; } = 1500.0;
        public bool BeamSkipReinforcement { get; set; }

        /// <summary>锚固长度（mm）：构件带深入相邻混凝土的延伸限度。</summary>
        public double AnchorageLengthMm { get; set; } = 500.0;

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
