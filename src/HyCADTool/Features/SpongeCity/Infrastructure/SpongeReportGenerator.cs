using System;
using System.Globalization;
using System.Text;
using HyCADTool.Features.SpongeCity.Domain.Models;
using HyCADTool.Features.SpongeCity.Domain.Standards;

namespace HyCADTool.Features.SpongeCity.Infrastructure
{
    /// <summary>
    /// 海绵城市 md 计算书生成器。
    /// 结构：① 项目信息 / ② 下垫面与综合径流系数 / ③ 调蓄容积 / ④ 设施配置与污染削减
    /// / ⑤ 强制性指标校核 / ⑥ 综合结论 / ⑦ 规范引用。
    /// 风格参考 SettlementReportGenerator。
    /// </summary>
    public static class SpongeReportGenerator
    {
        public static string Generate(SpongeProjectInput input, SpongeResult result)
        {
            if (input == null || result == null) return string.Empty;

            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            var standard = SpongeStandardRegistry.GetOrDefault(input.StandardId);
            var rounding = standard.Rounding ?? new SpongeRounding();

            sb.AppendLine($"# 海绵城市雨水控制与利用计算书 — {input.ProjectName}");
            sb.AppendLine();
            sb.AppendLine($"> 主依据：{standard.Citation}。");
            sb.AppendLine($"> 规范 Id：{standard.Id}（{standard.DisplayName}）。数值与面板、Excel 同源，来自同一次计算。");
            sb.AppendLine($"> 生成日期：{DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine();

            // ── 一、项目信息 ──
            sb.AppendLine("## 一、项目信息");
            sb.AppendLine();
            sb.AppendLine("| 项目要素 | 数值 / 取值 |");
            sb.AppendLine("|---|---|");
            sb.AppendLine($"| 项目名称 | {input.ProjectName} |");
            sb.AppendLine($"| 建设地点 | {input.Location} |");
            sb.AppendLine($"| 适用规范 | {standard.DisplayName} |");
            sb.AppendLine($"| 适用地区 | {RegionText(input.Region)} |");
            sb.AppendLine($"| 用地性质 | {ProjectTypeText(input.ProjectType)} |");
            sb.AppendLine($"| 总用地面积 F | {input.TotalAreaM2.ToString("F0", ci)} m² ({(input.TotalAreaM2 / 10000).ToString("F4", ci)} hm²) |");
            sb.AppendLine($"| 建筑屋面投影 | {input.BuildingFootprintM2.ToString("F0", ci)} m² |");
            sb.AppendLine($"| 绿地率 | {input.GreenRatio:P1} |");
            sb.AppendLine($"| 设计单位 | {(string.IsNullOrWhiteSpace(input.Designer) ? "—" : input.Designer)} |");
            sb.AppendLine($"| 设计日期 | {(string.IsNullOrWhiteSpace(input.Date) ? DateTime.Now.ToString("yyyy-MM") : input.Date)} |");
            sb.AppendLine();

            sb.AppendLine("### 设计控制目标");
            sb.AppendLine();
            sb.AppendLine("| 控制指标 | 数值 | 依据 |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine($"| 年径流总量控制率 α | {input.AlphaTarget.ToString("P1", ci)} | {standard.Id} |");
            sb.AppendLine($"| 对应设计降雨厚度 h_y | {input.DesignRainfallMm.ToString("F1", ci)} mm | {standard.Id} |");
            sb.AppendLine($"| SS 削减率目标 | {input.EtaSsTarget.ToString("P1", ci)} | {standard.Id} |");
            sb.AppendLine($"| 初期弃流厚度 | {input.InitialDiscardMm.ToString("F1", ci)} mm | 不从 V需 中扣除 |");
            sb.AppendLine($"| 外排雨水重现期 | {input.ReturnPeriodYear} 年 | {standard.Id} |");
            sb.AppendLine();

            // ── 二、下垫面 ──
            sb.AppendLine("## 二、下垫面与综合径流系数 ψ_z");
            sb.AppendLine();
            sb.AppendLine("> 公式：ψc = Σ(F_i·ψ_i) / ΣF_i。下表 ψ 与面积来自本次输入；ψc 为引擎舍入后的值。");
            sb.AppendLine();
            sb.AppendLine("| 序号 | 下垫面类型 | 图层名 | 面积 (m²) | 占比 | ψ_i | F_i·ψ_i |");
            sb.AppendLine("|---|---|---|---:|---:|---:|---:|");
            double total = result.TotalSurfaceArea;
            foreach (var s in input.Surfaces)
            {
                double frac = total > 0 ? s.Area / total : 0;
                sb.AppendLine($"| {s.Code} | {s.Name} | {s.LayerName} | {s.Area.ToString("F0", ci)} | {frac:P1} | {s.Psi.ToString("F2", ci)} | {(s.Area * s.Psi).ToString("F1", ci)} |");
            }
            double psiSumFiPsi = 0;
            foreach (var s in input.Surfaces) psiSumFiPsi += s.Area * s.Psi;
            sb.AppendLine($"| — | **合计** | — | **{total.ToString("F0", ci)}** | 100% | — | **{psiSumFiPsi.ToString("F1", ci)}** |");
            sb.AppendLine();
            sb.AppendLine($"**ψc = {result.PsiZ.ToString("F3", ci)}**（{InterpretPsi(result.PsiZ)}）");
            sb.AppendLine();
            sb.AppendLine($"流量径流系数（GB 50014 §4.2.2 系数 1.1，上限 0.95）：Ψm = **{result.PsiM.ToString("F3", ci)}**");
            sb.AppendLine();

            // ── 三、调蓄容积 ──
            sb.AppendLine("## 三、调蓄容积");
            sb.AppendLine();
            sb.AppendLine("> 公式：V需 = 10·ψc·h_y·F(hm²)。ψc、F、V需 的小数位由当前规范决定，与 Excel 同一数字。");
            sb.AppendLine();
            sb.AppendLine($"- 设计调蓄容积 W = 10 × {result.PsiZ.ToString("F3", ci)} × {input.DesignRainfallMm.ToString("F1", ci)} × {result.AreaHm2.ToString("F4", ci)} = **{Fmt(result.DesignVolumeW, rounding.RequiredVolumeDecimals, ci)} m³**");
            sb.AppendLine($"- 硬化面积 F_硬化 = {result.HardeningArea.ToString("F0", ci)} m²");
            sb.AppendLine($"- 配建容积 V_配 = **{Fmt(result.RequiredVolume522, rounding.RequiredVolumeDecimals, ci)} m³**");
            sb.AppendLine($"- V需 = MAX(W, V_配) = **{Fmt(result.FinalRequiredVolume, rounding.RequiredVolumeDecimals, ci)} m³**");
            sb.AppendLine($"- 单位汇水面积控制水量 = {(result.FinalRequiredVolume / Math.Max(0.0001, input.TotalAreaM2 / 10000.0)).ToString("F2", ci)} m³/hm²");
            sb.AppendLine();

            // ── 四、设施配置与污染削减 ──
            sb.AppendLine("## 四、设施配置与污染削减");
            sb.AppendLine();
            sb.AppendLine("| 序号 | 设施 | 单位 | 规模 | 单位调蓄 | 调蓄量 V_i (m³) | η_i | V_i·η_i |");
            sb.AppendLine("|---|---|---|---:|---:|---:|---:|---:|");
            double provVeta = 0;
            foreach (var f in input.Facilities)
            {
                provVeta += f.ProvidedVolume * f.EtaSs;
                sb.AppendLine($"| {f.Code} | {f.Name} | {f.Unit} | {f.Quantity.ToString("F0", ci)} | {f.UnitVolumeFactor.ToString("F2", ci)} | {f.ProvidedVolume.ToString("F2", ci)} | {f.EtaSs:P1} | {(f.ProvidedVolume * f.EtaSs).ToString("F2", ci)} |");
            }
            sb.AppendLine($"| — | **合计 V_实** | — | — | — | **{result.ProvidedVolume.ToString("F2", ci)}** | — | **{provVeta.ToString("F2", ci)}** |");
            sb.AppendLine();
            sb.AppendLine($"- 综合 SS 削减率 η_综合 = **{result.EtaCombined.ToString("F3", ci)}**");
            sb.AppendLine($"- 年 SS 削减率 η_年 = α·η_综合 = **{result.EtaAnnual.ToString("F3", ci)}**");
            sb.AppendLine(standard.MultiplySsByAlpha
                ? "- 本规范 SS 校核比较的是 η_年。"
                : "- 本规范 SS 校核比较的是 η_综合，不乘 α。");
            sb.AppendLine($"- V_实 / W_设计 富余率 = **{result.VolumeSurplus:P1}**");
            sb.AppendLine();

            // ── 五、强制性指标校核 ──
            sb.AppendLine("## 五、强制性指标校核");
            sb.AppendLine();
            sb.AppendLine("| 序号 | 校核项 | 实际值 | 目标/阈值 | 判定 | 依据 |");
            sb.AppendLine("|---|---|---|---|---|---|");
            int idx = 1;
            foreach (var c in result.Checks)
            {
                string mark = string.IsNullOrEmpty(c.VerdictText) ? (c.Passed ? "达标" : "不达标") : c.VerdictText;
                sb.AppendLine($"| {idx} | {c.Name} | {c.ActualText} | {c.TargetText} | {mark} | {c.Reference} |");
                idx++;
            }
            sb.AppendLine();

            // ── 六、综合结论 ──
            sb.AppendLine("## 六、综合结论");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(standard.PerviousPavement?.Documentation))
            {
                sb.AppendLine(standard.PerviousPavement.Documentation);
                sb.AppendLine();
            }
            sb.AppendLine(string.IsNullOrEmpty(result.VerdictText)
                ? (result.AllPassed ? standard.PassVerdict : standard.FailVerdict)
                : "**" + result.VerdictText + "**");
            sb.AppendLine();
            sb.AppendLine("### 关键技术指标汇总");
            sb.AppendLine();
            sb.AppendLine($"- 项目用地面积 F = {input.TotalAreaM2.ToString("F0", ci)} m²");
            sb.AppendLine($"- 综合径流系数 ψc = {result.PsiZ.ToString("F3", ci)}");
            sb.AppendLine($"- 年径流总量控制率 α = {input.AlphaTarget.ToString("P1", ci)}");
            sb.AppendLine($"- 设计降雨厚度 h_y = {input.DesignRainfallMm.ToString("F1", ci)} mm");
            sb.AppendLine($"- 设计调蓄容积 V需 = {Fmt(result.FinalRequiredVolume, rounding.RequiredVolumeDecimals, ci)} m³");
            sb.AppendLine($"- 设施提供 V实 = {result.ProvidedVolume.ToString("F2", ci)} m³");
            sb.AppendLine($"- 综合 SS 削减率 = {result.EtaCombined.ToString("F3", ci)}");
            sb.AppendLine($"- SS 校核取值 = {result.EtaCompared.ToString("F3", ci)}");
            sb.AppendLine($"- 外排设计流量 Q = {result.DesignFlowQ.ToString("F1", ci)} L/s");
            sb.AppendLine($"- 海绵投资估算 = {result.EstimatedCostWanyuan.ToString("F2", ci)} 万元");
            sb.AppendLine();

            // ── 七、规范引用 ──
            sb.AppendLine("## 七、规范引用");
            sb.AppendLine();
            sb.AppendLine("- " + standard.Citation);
            sb.AppendLine("- GB 50014 《室外排水设计标准》§4.2（流量径流系数 1.1，上限 0.95）");
            sb.AppendLine();

            sb.AppendLine($"---");
            sb.AppendLine($"*本文档由 HyCAD HYSpongeCity 自动生成（{DateTime.Now:yyyy-MM-dd HH:mm:ss}）。*");

            return sb.ToString();
        }

        private static string RegionText(SpongeRegion r)
        {
            switch (r)
            {
                case SpongeRegion.Beijing: return "北京市";
                case SpongeRegion.Tianjin: return "天津市";
                case SpongeRegion.Hubei: return "湖北省";
                default: return "河北省";
            }
        }

        private static string ProjectTypeText(ProjectType t)
        {
            switch (t)
            {
                case ProjectType.NewResidential: return "新建住宅小区";
                case ProjectType.NewCommercialHighGreen: return "新建商业小区（绿地率 ≥25%）";
                case ProjectType.NewCommercialLowGreen: return "新建商业小区（绿地率 <25%）";
                case ProjectType.RebuildOldResidential: return "改扩建老旧小区";
                case ProjectType.RebuildOtherResidential: return "改扩建其他小区";
                case ProjectType.RebuildPublic: return "改扩建公共建筑";
                case ProjectType.Industrial: return "工业用地";
                default: return "—";
            }
        }

        private static string Fmt(double value, int decimals, CultureInfo ci)
        {
            if (decimals < 0) return value.ToString("F2", ci);
            return value.ToString("F" + decimals.ToString(ci), ci);
        }

        private static string InterpretPsi(double psi)
        {
            if (psi <= 0.30) return "硬化少，源头削峰条件好";
            if (psi <= 0.50) return "中等硬化，需配套设施削峰";
            if (psi <= 0.70) return "硬化较重，需较大调蓄";
            return "硬化重，需调蓄+调节联合";
        }
    }
}
