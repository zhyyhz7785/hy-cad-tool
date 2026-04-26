using System.Collections.Generic;

namespace HyCADTool.Features.Settlement
{
    /// <summary>
    /// 沉降计算输入参数集
    /// </summary>
    public class SettlementInput
    {
        #region 高程参数

        /// <summary>孔点高程 (m)</summary>
        public double BoreholeElevation { get; set; }

        /// <summary>结构正负零高程 (m)</summary>
        public double StructureZeroElevation { get; set; }

        /// <summary>基础埋置深度 (m)，相对于结构正负零</summary>
        public double FoundationDepth { get; set; }

        /// <summary>相对高程 = 孔点高程 - 结构正负零高程 (m)</summary>
        public double RelativeElevation => BoreholeElevation - StructureZeroElevation;

        #endregion

        #region 基础参数

        /// <summary>计算类型</summary>
        public FoundationType Type { get; set; } = FoundationType.Natural;

        /// <summary>基础长度 l (m)</summary>
        public double FoundationLength { get; set; }

        /// <summary>基础宽度 b (m)</summary>
        public double FoundationWidth { get; set; }

        /// <summary>基底附加压力 p₀ (kPa)</summary>
        public double AdditionalPressure { get; set; }

        /// <summary>地基承载力特征值 fak (kPa)</summary>
        public double BearingCapacity { get; set; }

        #endregion

        #region 复合地基参数

        /// <summary>复合地基承载力特征值 fspk (kPa)</summary>
        public double CompositeBearingCapacity { get; set; }

        /// <summary>加固层深度 (m)，从基底算起</summary>
        public double TreatedDepth { get; set; }

        /// <summary>压缩模量提高系数 ξ = fspk / fak (JGJ 79 §7.1.7)</summary>
        public double Xi => BearingCapacity > 0 ? CompositeBearingCapacity / BearingCapacity : 1.0;

        #endregion

        #region 桩基参数

        /// <summary>桩长 (m)</summary>
        public double PileLength { get; set; }

        /// <summary>桩径 (m)</summary>
        public double PileDiameter { get; set; }

        /// <summary>桩距 (m)</summary>
        public double PileSpacing { get; set; }

        /// <summary>承台长度 Lc (m)</summary>
        public double CapLength { get; set; }

        /// <summary>承台宽度 Bc (m)</summary>
        public double CapWidth { get; set; }

        /// <summary>总桩数</summary>
        public int TotalPileCount { get; set; }

        #endregion

        #region 土层数据

        /// <summary>从基底起算的各土层</summary>
        public List<SoilLayer> SoilLayers { get; set; } = new List<SoilLayer>();

        #endregion

        #region 基础子类型

        /// <summary>是否为箱型基础（影响 G_k 和 p₀ 自动计算）</summary>
        public bool IsBoxFoundation { get; set; }

        /// <summary>箱型基础混凝土折算厚度 (m)，用于计算基础自重</summary>
        public double BoxConcreteThickness { get; set; }

        /// <summary>基础底面以上土的加权平均重度 γ_m (kN/m³)</summary>
        public double GammaM { get; set; } = 20.0;

        /// <summary>室内外高差 Δh (m)，用于计算覆土平均厚度 d_eff = d − Δh/2</summary>
        public double IndoorOutdoorDiff { get; set; }

        #endregion

        #region 承载力验算参数

        /// <summary>承载力验算输入（Phase 2），为 null 时不计算承载力</summary>
        public BearingCapacityInput BearingInput { get; set; }

        /// <summary>桩身结构验算输入（Phase 3），为 null 时不验算</summary>
        public PileStructuralInput StructuralInput { get; set; }

        #endregion

        #region 回弹再压缩参数

        /// <summary>是否考虑回弹再压缩（深基础时启用）</summary>
        public bool EnableRebound { get; set; }

        /// <summary>Eci/Esi 默认倍率（当 SoilLayer.Eci=0 时使用）</summary>
        public double EciEsiRatio { get; set; } = 5.0;

        /// <summary>回弹经验系数 ψ_c（无经验时取 1.0）</summary>
        public double PsiC { get; set; } = 1.0;

        /// <summary>再压缩增大系数 κ = r'(R'=1.0)（黏性土 1.19，砂土 1.10）</summary>
        public double Kappa { get; set; } = 1.19;

        /// <summary>η₁ 回弹完成比（0~1）：开挖到加载期间理论回弹完成比例</summary>
        public double Eta1 { get; set; } = 0.5;

        /// <summary>η₂ 回弹清除比（0~1）：已完成回弹中因基底整平被移除的比例</summary>
        public double Eta2 { get; set; } = 0.8;

        /// <summary>R'₀ 临界再加荷比（§5.3.11），默认 0.3</summary>
        public double R0Prime { get; set; } = 0.3;

        /// <summary>r'₀ 临界再压缩比率（§5.3.11），默认 0.4</summary>
        public double r0Prime { get; set; } = 0.4;

        /// <summary>回弹计算深度终止比值（Δsc_i / Σsc ≤ 此值时终止），默认 0.025</summary>
        public double ReboundDepthRatio { get; set; } = 0.025;

        #endregion
    }
}
