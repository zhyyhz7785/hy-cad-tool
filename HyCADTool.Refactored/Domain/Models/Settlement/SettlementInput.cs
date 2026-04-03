using System.Collections.Generic;

namespace HyCADTool.Refactored.Domain.Models.Settlement
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
    }
}
