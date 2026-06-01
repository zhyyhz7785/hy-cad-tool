using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Features.SpongeCity.Domain.Models;

namespace HyCADTool.Features.SpongeCity.Domain.Services
{
    /// <summary>
    /// 海绵城市计算引擎（纯算法，平台无关）。
    /// 依据：DB13(J) 8457-2022 §4 / §5；建城函[2014]275号；GB 50180-2018。
    /// 对齐 gen_sponge_housing_calc.py 的全部 7 个 Sheet 算子。
    /// </summary>
    public static class SpongeCalculationService
    {
        /// <summary>
        /// 综合径流系数 ψ_z = Σ(F_i·ψ_i) / ΣF_i （DB13 §4.1.4 式 4.1.4）。
        /// </summary>
        public static double CalculatePsiZ(IEnumerable<SurfaceItem> surfaces)
        {
            if (surfaces == null) return 0;
            double sumFi = 0, sumFiPsi = 0;
            foreach (var s in surfaces)
            {
                sumFi += s.Area;
                sumFiPsi += s.Area * s.Psi;
            }
            return sumFi > 0 ? sumFiPsi / sumFi : 0;
        }

        /// <summary>
        /// 设计调蓄容积 W = 10·ψ_z·h_y·F (hm²)  → 单位 m³ （DB13 §4.2.4 式 4.2.4）。
        /// </summary>
        public static double CalculateW(double psiZ, double hyMm, double fM2)
        {
            double fHm2 = fM2 / 10000.0;
            return 10.0 * psiZ * hyMm * fHm2;
        }

        /// <summary>
        /// 硬化面积 F_硬化（DB13 §5.2.2 第 3 款）：
        /// - 居住区项目 = 屋面投影；
        /// - 非居住区项目 = F_用地 - F_绿地（含绿屋顶）- F_透水铺装。
        /// </summary>
        public static double CalculateHardeningArea(SpongeProjectInput input)
        {
            if (input == null) return 0;
            bool isResidential =
                input.ProjectType == ProjectType.NewResidential
                || input.ProjectType == ProjectType.RebuildOldResidential
                || input.ProjectType == ProjectType.RebuildOtherResidential;
            if (isResidential) return Math.Max(0, input.BuildingFootprintM2);

            double greenAndRoof = AreaByCodes(input, "S2", "S6", "S7", "S8");
            double pervious = AreaByCodes(input, "S5");
            return Math.Max(0, input.TotalAreaM2 - greenAndRoof - pervious);
        }

        /// <summary>
        /// DB13 §5.2.2 配建容积 V_配（按地区与硬化面积分档）：
        /// - 任意地区 F_硬化 &gt; 10000m² → 50 m³/千 m²；
        /// - 北京 F_硬化 &gt; 2000m² 且 ≤ 10000m² → 30 m³/千 m²；
        /// - 天津 F_硬化 &gt; 5000m² 且 ≤ 10000m² → 30 m³/千 m²；
        /// 其余取 0（最终再与 W 取严格 MAX）。
        /// </summary>
        public static double CalculateRequiredVolume522(SpongeRegion region, double hardeningM2)
        {
            if (hardeningM2 <= 0) return 0;
            if (hardeningM2 > 10000) return hardeningM2 / 1000.0 * 50.0;
            if (region == SpongeRegion.Beijing && hardeningM2 > 2000) return hardeningM2 / 1000.0 * 30.0;
            if (region == SpongeRegion.Tianjin && hardeningM2 > 5000) return hardeningM2 / 1000.0 * 30.0;
            return 0;
        }

        /// <summary>
        /// 设施提供调蓄量 ΣV_i；同步把每个设施的 ProvidedVolume 写回去。
        /// </summary>
        public static double CalculateProvidedVolume(IEnumerable<FacilityItem> facilities)
        {
            if (facilities == null) return 0;
            double sum = 0;
            foreach (var f in facilities)
            {
                f.ProvidedVolume = f.Quantity * f.UnitVolumeFactor;
                sum += f.ProvidedVolume;
            }
            return sum;
        }

        /// <summary>
        /// 综合 SS 削减率 η_综合 = Σ(V_i·η_i) / ΣV_i。
        /// </summary>
        public static double CalculateEtaCombined(IEnumerable<FacilityItem> facilities)
        {
            if (facilities == null) return 0;
            double sumV = 0, sumVeta = 0;
            foreach (var f in facilities)
            {
                sumV += f.ProvidedVolume;
                sumVeta += f.ProvidedVolume * f.EtaSs;
            }
            return sumV > 0 ? sumVeta / sumV : 0;
        }

        /// <summary>
        /// 一键全套：填充并返回 SpongeResult。
        /// </summary>
        public static SpongeResult Compute(SpongeProjectInput input)
        {
            var r = new SpongeResult();
            if (input == null) return r;

            r.TotalSurfaceArea = input.Surfaces?.Sum(s => s.Area) ?? 0;
            r.PsiZ = CalculatePsiZ(input.Surfaces);
            r.PsiM = Math.Min(0.95, r.PsiZ * 1.1);

            r.DesignVolumeW = CalculateW(r.PsiZ, input.DesignRainfallMm, input.TotalAreaM2);
            r.HardeningArea = CalculateHardeningArea(input);
            r.RequiredVolume522 = CalculateRequiredVolume522(input.Region, r.HardeningArea);
            r.FinalRequiredVolume = Math.Max(r.DesignVolumeW, r.RequiredVolume522);

            r.ProvidedVolume = CalculateProvidedVolume(input.Facilities);
            r.VolumeSurplus = r.FinalRequiredVolume > 0
                ? (r.ProvidedVolume - r.FinalRequiredVolume) / r.FinalRequiredVolume
                : 0;

            r.EtaCombined = CalculateEtaCombined(input.Facilities);
            r.EtaAnnual = input.AlphaTarget * r.EtaCombined;

            // 下沉绿地率 = 下沉绿地面积 / 全部绿地（S2 种植屋面 + S6 下沉 + S7 普通 + S8 顶板）
            double aGreenTotal = AreaByCodes(input, "S2", "S6", "S7", "S8");
            double aSinken = AreaByCodes(input, "S6");
            r.SinkenGreenRatio = aGreenTotal > 0 ? aSinken / aGreenTotal : 0;

            // 透水铺装率 = 透水砖 / (混凝土路+块石广场+透水砖)
            double aPervious = AreaByCodes(input, "S5");
            double aPaveTotal = AreaByCodes(input, "S3", "S4", "S5");
            r.PerviousPavementRatio = aPaveTotal > 0 ? aPervious / aPaveTotal : 0;

            // 外排流量
            r.DesignFlowQ = r.PsiM * input.StormIntensity * (input.TotalAreaM2 / 10000.0);

            // 年径流总量近似
            r.AnnualRunoffVolume = input.TotalAreaM2 * input.DesignRainfallMm / 1000.0 * r.PsiZ;

            // 海绵投资估算 (万元)
            r.EstimatedCostWanyuan = input.UnitSpongeCost * input.TotalAreaM2 / 10000.0;

            // ── 校核 7 项 + 综合 ──
            r.Checks = BuildChecks(input, r);
            r.AllPassed = r.Checks.All(c => c.Passed);

            return r;
        }

        private static double AreaByCodes(SpongeProjectInput input, params string[] codes)
        {
            if (input?.Surfaces == null) return 0;
            var set = new HashSet<string>(codes, StringComparer.Ordinal);
            return input.Surfaces.Where(s => set.Contains(s.Code)).Sum(s => s.Area);
        }

        private static List<CheckRow> BuildChecks(SpongeProjectInput input, SpongeResult r)
        {
            var list = new List<CheckRow>();

            // 1. 年径流总量控制率 α（设定值，无对错，仅展示）
            list.Add(new CheckRow
            {
                Name = "年径流总量控制率 α",
                ActualText = $"{r.PsiZ * 0 + input.AlphaTarget:P1}",
                TargetText = "—",
                Passed = true,
                Reference = "DB13 §5.2.4-1 目标自设"
            });

            // 2. 调蓄容积 V实 ≥ W_设计
            list.Add(new CheckRow
            {
                Name = "调蓄容积 V实 ≥ W_设计",
                ActualText = $"{r.ProvidedVolume:F1} m³",
                TargetText = $"{r.FinalRequiredVolume:F1} m³",
                Passed = r.ProvidedVolume >= r.FinalRequiredVolume - 0.5,
                Reference = "DB13 §4.2.4 + §5.2.2"
            });

            // 3. 年 SS 削减率 ≥ 目标
            list.Add(new CheckRow
            {
                Name = "年 SS 削减率 η_年",
                ActualText = $"{r.EtaAnnual:P1}",
                TargetText = $"{input.EtaSsTarget:P1}",
                Passed = r.EtaAnnual >= input.EtaSsTarget - 1e-6,
                Reference = "DB13 §5.2.5"
            });

            // 4. 外排峰值径流系数 ψ_z ≤ 0.4（新建）/ 0.5（改建）
            bool isNew = input.ProjectType == ProjectType.NewResidential
                      || input.ProjectType == ProjectType.NewCommercialHighGreen
                      || input.ProjectType == ProjectType.NewCommercialLowGreen;
            double psiLimit = isNew ? 0.4 : 0.5;
            list.Add(new CheckRow
            {
                Name = "外排径流系数 ψ_z",
                ActualText = $"{r.PsiZ:F3}",
                TargetText = $"≤ {psiLimit:F2}",
                Passed = r.PsiZ <= psiLimit + 1e-6,
                Reference = "DB13 §5.2.1"
            });

            // 5. 绿地率 ≥ 30%（R2 居住区）
            list.Add(new CheckRow
            {
                Name = "绿地率 (R2)",
                ActualText = $"{input.GreenRatio:P1}",
                TargetText = "≥ 30%",
                Passed = input.GreenRatio >= 0.30 - 1e-6,
                Reference = "GB 50180 §4.0.4"
            });

            // 6. 下沉式绿地率 ≥ 50%（绿地）
            list.Add(new CheckRow
            {
                Name = "下沉式绿地率",
                ActualText = $"{r.SinkenGreenRatio:P1}",
                TargetText = "≥ 50%",
                Passed = r.SinkenGreenRatio >= 0.50 - 1e-6,
                Reference = "DB13 §5.2.2 第 5 款"
            });

            // 7. 透水铺装率 ≥ 70%
            list.Add(new CheckRow
            {
                Name = "透水铺装率",
                ActualText = $"{r.PerviousPavementRatio:P1}",
                TargetText = "≥ 70%",
                Passed = r.PerviousPavementRatio >= 0.70 - 1e-6,
                Reference = "DB13 §5.2.2 第 6 款"
            });

            return list;
        }
    }
}
