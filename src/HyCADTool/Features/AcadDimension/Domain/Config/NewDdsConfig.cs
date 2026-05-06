namespace HyCADTool.Features.AcadDimension.Domain.Config
{
    /// <summary>
    /// NewDDS 配置（已乘 Scale 后的实际 mm 值；Domain 不感知 ViewModel）。
    /// 由 NewDdsConfigAdapter 从 SettingsPanelViewModel 转换而来。
    /// 钢筋首选项面板上的"内/外/带尺寸距离/距离容差"四项同时为老 dds 与 NewDDS 服务（07 §1.2）。
    /// </summary>
    public sealed class NewDdsConfig
    {
        /// <summary>外部尺寸偏移（实际 mm）。对应钢筋面板"外部尺寸距离 × Scale"。</summary>
        public double DimensionDistanceOutside { get; set; }

        /// <summary>内部尺寸偏移（实际 mm）。</summary>
        public double DimensionDistanceInside { get; set; }

        /// <summary>外部总尺寸再加距离（实际 mm）。</summary>
        public double DimensionDistanceWithDim { get; set; }

        /// <summary>近距平行标注合并阈值（实际 mm）。</summary>
        public double DimDistanceTolerance { get; set; }

        /// <summary>是否生成外部总尺寸（左右总长 / 上下总长）。</summary>
        public bool GenerateOutsideTotalDimension { get; set; } = true;

        /// <summary>步长策略（修 06 §7 #1）。Adaptive=自适应；Fixed=固定（老 dds 兼容）。</summary>
        public StepStrategy StepStrategy { get; set; } = StepStrategy.Adaptive;

        /// <summary>StepStrategy=Fixed 时的步长值。老 dds 行为 = 20。</summary>
        public double FixedStepValue { get; set; } = 20.0;

        /// <summary>Bulge 弧段细分精度（实际 mm，默认 = 步长/2）。修 06 §7 #8。</summary>
        public double BulgeTessellatePrecision { get; set; } = 10.0;

        /// <summary>诊断早退阈值（替代老 dds safetyCounter=1000 死循环兜底）。修 06 §7 #2。</summary>
        public int SafetyIterationLimit { get; set; } = 1000;
    }

    public enum StepStrategy
    {
        /// <summary>自适应：步长 = clamp(短边 × 5%, 1mm, 100mm)。NewDDS 默认。</summary>
        Adaptive,
        /// <summary>固定：与老 dds 行为一致（默认 20mm）。</summary>
        Fixed
    }
}
