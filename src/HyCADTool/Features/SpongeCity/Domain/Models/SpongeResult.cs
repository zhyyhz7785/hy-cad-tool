using System.Collections.Generic;

namespace HyCADTool.Features.SpongeCity.Domain.Models
{
    /// <summary>单条校核结果。</summary>
    public class CheckRow
    {
        /// <summary>稳定代码，见 SpongeCheckCodes。</summary>
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string ActualText { get; set; } = "";
        public string TargetText { get; set; } = "";
        public bool Passed { get; set; }
        /// <summary>false 的行不参与总评（α、工业用地绿地率、外排 ψ）。</summary>
        public bool CountsTowardVerdict { get; set; } = true;
        /// <summary>达标 / 不达标 / 满足 / —参考 / —。</summary>
        public string VerdictText { get; set; } = "";
        public string Reference { get; set; } = "";
    }

    /// <summary>
    /// 海绵城市计算结果（Domain 输出，平台无关）。
    /// </summary>
    public class SpongeResult
    {
        // ── ψ 与容积 ──
        /// <summary>下垫面面积合计 (m²)。</summary>
        public double TotalSurfaceArea { get; set; }

        /// <summary>本次计算使用的规范 Id。</summary>
        public string StandardId { get; set; } = "";

        /// <summary>综合雨量径流系数。规范要求舍入时，这里已是舍入后的值。</summary>
        public double PsiZ { get; set; }

        /// <summary>总用地 hm²。规范要求舍入时，这里已是舍入后的值。</summary>
        public double AreaHm2 { get; set; }

        /// <summary>设计调蓄容积 W = 10·ψ·H·F(hm²)  (m³)（DB13 §4.2.4）。</summary>
        public double DesignVolumeW { get; set; }

        /// <summary>§5.2.2 配建容积 V_配 (m³)。</summary>
        public double RequiredVolume522 { get; set; }

        /// <summary>取严格 W_设计 = MAX(W, V_配) (m³)。</summary>
        public double FinalRequiredVolume { get; set; }

        /// <summary>设施实际提供调蓄量 V_实 (m³)。</summary>
        public double ProvidedVolume { get; set; }

        /// <summary>富余率 = (V_实 - W_设计)/W_设计。</summary>
        public double VolumeSurplus { get; set; }

        /// <summary>硬化面积 F_硬化 (m²)（用于 §5.2.2 校核）。</summary>
        public double HardeningArea { get; set; }

        // ── 污染削减 ──
        /// <summary>综合 SS 削减率 η_综合（设施加权）。</summary>
        public double EtaCombined { get; set; }

        /// <summary>年 SS 削减率 η_年 = α·η_综合。DB42 校核不使用这个数。</summary>
        public double EtaAnnual { get; set; }

        /// <summary>SS 校核实际拿去和目标比的数：京津冀为 η_年，DB42 为 η综合。</summary>
        public double EtaCompared { get; set; }

        /// <summary>绿地率校核使用的实际值（手填或计绿公式）。</summary>
        public double GreenRatioActual { get; set; }

        // ── 关键比率 ──
        /// <summary>下沉式绿地率 = F_下沉绿地 / F_绿地总。</summary>
        public double SinkenGreenRatio { get; set; }

        /// <summary>透水铺装率 = F_透水 / (F_透水+F_广场+F_停车场)。</summary>
        public double PerviousPavementRatio { get; set; }

        // ── 外排 ──
        /// <summary>流量径流系数 Ψm = 1.1·ψ_z。</summary>
        public double PsiM { get; set; }

        /// <summary>外排设计流量 Q = Ψm′·q·F (L/s)。</summary>
        public double DesignFlowQ { get; set; }

        /// <summary>年径流总量 W₀ = F(m²)·H/1000·ψc (m³)。</summary>
        public double AnnualRunoffVolume { get; set; }

        // ── 校核 ──
        /// <summary>7+ 项强制性指标判定。</summary>
        public List<CheckRow> Checks { get; set; } = new List<CheckRow>();

        /// <summary>计入总评的校核是否全部通过。</summary>
        public bool AllPassed { get; set; }

        /// <summary>总评原文，与 Excel、Markdown 同一句。</summary>
        public string VerdictText { get; set; } = "";

        // ── 造价（简单估算）──
        /// <summary>海绵投资估算 (万元)。</summary>
        public double EstimatedCostWanyuan { get; set; }
    }
}
