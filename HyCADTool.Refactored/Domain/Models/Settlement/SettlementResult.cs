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

        /// <summary>附加应力压缩沉降 (mm) = ψs × ψe × s'（不含回弹效应）</summary>
        public double CompressionSettlement { get; set; }

        /// <summary>覆土自重压力 pc = γm·d (kPa)</summary>
        public double OverburdenPressure { get; set; }

        /// <summary>再加荷总压力 p = p₀ + γm·d (kPa)</summary>
        public double TotalReloadPressure { get; set; }

        /// <summary>再加荷比 R' = p / pc</summary>
        public double ReloadRatio { get; set; }

        /// <summary>回弹量 sc (mm)，§5.3.10（正值，物理方向向上）</summary>
        public double ReboundSettlement { get; set; }

        /// <summary>再压缩量 (mm)，方向向下</summary>
        public double RecompressionSettlement { get; set; }

        /// <summary>实际回弹量 η·sc (mm)（施工期间实际发生的回弹）</summary>
        public double ActualRebound { get; set; }

        /// <summary>回弹再压缩净沉降 (mm)（正=向下，负=向上）</summary>
        public double NetReboundSettlement { get; set; }

        /// <summary>是否包含回弹再压缩计算</summary>
        public bool HasRebound { get; set; }

        /// <summary>计算是否成功</summary>
        public bool Success { get; set; }

        /// <summary>错误/警告信息</summary>
        public string Message { get; set; } = "";
    }
}
