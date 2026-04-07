namespace HyCADTool.Refactored.Domain.Models.Settlement
{
    /// <summary>
    /// 桩身结构验算输入参数
    /// JGJ 94-2008 §5.8~5.9
    /// </summary>
    public class PileStructuralInput
    {
        #region 桩身受压 (§5.8.2)

        /// <summary>桩顶轴力设计值 N (kN)</summary>
        public double AxialForceDesign { get; set; }

        /// <summary>成桩工艺系数 ψc（灌注桩 0.7~0.8，预制桩 1.0，先张法管桩 0.85）</summary>
        public double PsiC { get; set; } = 0.7;

        /// <summary>稳定系数 φ（一般取 1.0）</summary>
        public double Phi { get; set; } = 1.0;

        /// <summary>混凝土轴心抗压强度设计值 fc (kPa)</summary>
        public double Fc { get; set; }

        /// <summary>混凝土等级（如 "C30"、"C35"）</summary>
        public string ConcreteGrade { get; set; } = "C30";

        #endregion

        #region 桩身配筋 (§5.8.3~5.8.4)

        /// <summary>纵向钢筋截面积 As (mm²)</summary>
        public double SteelArea { get; set; }

        /// <summary>桩长 (m)，用于判定最小配筋率</summary>
        public double PileLength { get; set; }

        #endregion

        #region 预应力管桩抗裂 (§5.9.2)

        /// <summary>是否为预应力桩</summary>
        public bool IsPrestressed { get; set; }

        /// <summary>混凝土有效预压应力 σpc (kPa)</summary>
        public double SigmaPc { get; set; }

        /// <summary>由弯矩产生的混凝土拉应力 σtp (kPa)</summary>
        public double SigmaTp { get; set; }

        /// <summary>混凝土抗拉强度标准值 ftk (kPa)</summary>
        public double Ftk { get; set; }

        #endregion
    }
}
