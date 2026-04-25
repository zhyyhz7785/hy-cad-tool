using System.Collections.Generic;

namespace HyCADTool.Features.Settlement
{
    /// <summary>
    /// 承载力验算输入参数
    /// 复合地基：JGJ 79-2012 §7.1.5
    /// 桩基：JGJ 94-2008 §5.2~5.4
    /// </summary>
    public class BearingCapacityInput
    {
        #region 通用参数

        /// <summary>荷载标准值 Fk (kN)</summary>
        public double Fk { get; set; }

        /// <summary>荷载设计值 Nk (kN)（桩基承载力验算用）</summary>
        public double Nk { get; set; }

        #endregion

        #region 复合地基参数 (JGJ 79 §7.1.5)

        /// <summary>单桩承载力发挥系数 λ（有粘结强度增强体，默认 1.0）</summary>
        public double Lambda { get; set; } = 1.0;

        /// <summary>桩间土承载力发挥系数 β（默认 0.5）</summary>
        public double Beta { get; set; } = 0.5;

        /// <summary>处理后桩间土承载力特征值 fsk (kPa)</summary>
        public double Fsk { get; set; }

        /// <summary>桩体试块28d抗压强度平均值 fcu (kPa)</summary>
        public double Fcu { get; set; }

        /// <summary>桩端端阻力发挥系数 αp</summary>
        public double AlphaP { get; set; } = 1.0;

        /// <summary>桩端端阻力特征值 qp (kPa)</summary>
        public double CompQp { get; set; }

        /// <summary>复合地基增强体桩径 (m)</summary>
        public double CompPileDiameter { get; set; } = 0.5;

        /// <summary>复合地基增强体桩间距 (m)</summary>
        public double CompPileSpacing { get; set; } = 1.5;

        /// <summary>布桩方式：0=等边三角形, 1=正方形, 2=矩形</summary>
        public int ArrangementType { get; set; } = 1;

        /// <summary>各层侧阻力特征值 (kPa) 和对应厚度 (m)</summary>
        public List<SideResistanceLayer> CompSideResistance { get; set; } = new List<SideResistanceLayer>();

        #endregion

        #region 桩基参数 (JGJ 94 §5.2~5.4)

        /// <summary>桩端极限端阻力标准值 qp (kPa)</summary>
        public double PileQp { get; set; }

        /// <summary>各层极限侧阻力标准值和厚度</summary>
        public List<SideResistanceLayer> PileSideResistance { get; set; } = new List<SideResistanceLayer>();

        /// <summary>安全系数 K（默认 2.0）</summary>
        public double SafetyFactor { get; set; } = 2.0;

        /// <summary>承台底土分担荷载系数 ηc</summary>
        public double EtaC { get; set; }

        /// <summary>是否考虑承台底土分担荷载</summary>
        public bool HasCapSoilBearing { get; set; }

        #endregion
    }

    /// <summary>
    /// 侧阻力/摩阻力层参数
    /// </summary>
    public class SideResistanceLayer
    {
        /// <summary>土层名称</summary>
        public string LayerName { get; set; } = "";

        /// <summary>土类描述</summary>
        public string SoilType { get; set; } = "";

        /// <summary>该层厚度 (m)</summary>
        public double Thickness { get; set; }

        /// <summary>侧阻力/摩阻力特征值或标准值 (kPa)</summary>
        public double Qsi { get; set; }
    }
}
