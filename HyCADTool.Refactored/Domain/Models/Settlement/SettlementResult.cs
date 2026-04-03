using System.Collections.Generic;

namespace HyCADTool.Refactored.Domain.Models.Settlement
{
    /// <summary>
    /// 沉降计算整体结果
    /// </summary>
    public class SettlementResult
    {
        /// <summary>计算类型</summary>
        public FoundationType Type { get; set; }

        /// <summary>逐层计算结果</summary>
        public List<SettlementLayerResult> LayerResults { get; set; } = new List<SettlementLayerResult>();

        /// <summary>理论总沉降 s' (mm)（分层总和法原始值）</summary>
        public double TheoreticalSettlement { get; set; }

        /// <summary>沉降计算经验系数 ψ_s</summary>
        public double PsiS { get; set; }

        /// <summary>桩基等效沉降系数 ψ_e（仅桩基础有值，天然/复合为 1.0）</summary>
        public double PsiE { get; set; } = 1.0;

        /// <summary>最终沉降 s (mm) = ψ_s * ψ_e * s'</summary>
        public double FinalSettlement { get; set; }

        /// <summary>计算深度 z_n (m)</summary>
        public double CalculationDepth { get; set; }

        /// <summary>压缩模量当量值 Ē_s (MPa)</summary>
        public double EquivalentEs { get; set; }

        /// <summary>简化公式估算深度 z_n = b(2.5 - 0.4·ln b) (m)，仅参考</summary>
        public double SimplifiedDepth { get; set; }

        /// <summary>附加压力与承载力之比 p₀ / f_ak</summary>
        public double PressureRatio { get; set; }

        /// <summary>计算是否成功</summary>
        public bool Success { get; set; }

        /// <summary>错误/警告信息</summary>
        public string Message { get; set; } = "";
    }
}
