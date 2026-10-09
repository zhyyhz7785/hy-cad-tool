using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HyCADTool.Features.SpongeCity.Domain.Models;
using HyCADTool.Features.SpongeCity.Domain.Standards;

namespace HyCADTool.Features.SpongeCity.Domain.Services
{
    /// <summary>
    /// 海绵城市计算引擎。只认 <see cref="ISpongeStandard"/> 上的规则，
    /// 面板、Markdown、Excel 都读这一次 <see cref="Compute"/> 的结果。
    /// </summary>
    public static class SpongeCalculationService
    {
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

        /// <summary>W = 10·ψ·h·F(hm²)，单位 m³。ψ 与 F 由调用方决定是否已舍入。</summary>
        public static double CalculateW(double psiZ, double hyMm, double fHm2)
        {
            return 10.0 * psiZ * hyMm * fHm2;
        }

        /// <summary>
        /// 硬化面积（DB13 §5.2.2）：居住区 = 屋面投影；其余 = 总用地 − 绿地代码 − S5。
        /// </summary>
        public static double CalculateHardeningArea(SpongeProjectInput input)
        {
            if (input == null) return 0;
            bool isResidential =
                input.ProjectType == ProjectType.NewResidential
                || input.ProjectType == ProjectType.RebuildOldResidential
                || input.ProjectType == ProjectType.RebuildOtherResidential;
            if (isResidential) return Math.Max(0, input.BuildingFootprintM2);

            double greenAndRoof = SumArea(input.Surfaces, "S2", "S6", "S7", "S8");
            double pervious = SumArea(input.Surfaces, "S5");
            return Math.Max(0, input.TotalAreaM2 - greenAndRoof - pervious);
        }

        /// <summary>
        /// DB13 §5.2.2 配建容积：硬化 &gt;10000 → 50 m³/千 m²；
        /// 北京 &gt;2000 且 ≤10000 → 30；天津 &gt;5000 且 ≤10000 → 30；其余 0。
        /// </summary>
        public static double CalculateRequiredVolume522(SpongeRegion region, double hardeningM2)
        {
            if (hardeningM2 <= 0) return 0;
            if (hardeningM2 > 10000) return hardeningM2 / 1000.0 * 50.0;
            if (region == SpongeRegion.Beijing && hardeningM2 > 2000) return hardeningM2 / 1000.0 * 30.0;
            if (region == SpongeRegion.Tianjin && hardeningM2 > 5000) return hardeningM2 / 1000.0 * 30.0;
            return 0;
        }

        public static double SumArea(IEnumerable<SurfaceItem> surfaces, params string[] codes)
        {
            if (surfaces == null || codes == null || codes.Length == 0) return 0;
            var set = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
            double sum = 0;
            foreach (var s in surfaces)
            {
                if (s != null && set.Contains(s.Code)) sum += s.Area;
            }
            return sum;
        }

        /// <summary>分子面积 / 分母面积。分母为 0 时返回 0。</summary>
        public static double AreaRatio(IEnumerable<SurfaceItem> surfaces, IEnumerable<string> numeratorCodes, IEnumerable<string> denominatorCodes)
        {
            double den = SumArea(surfaces, denominatorCodes == null ? new string[0] : denominatorCodes.ToArray());
            if (den <= 0) return 0;
            double num = SumArea(surfaces, numeratorCodes == null ? new string[0] : numeratorCodes.ToArray());
            return num / den;
        }

        public static SpongeResult Compute(SpongeProjectInput input)
        {
            var r = new SpongeResult();
            if (input == null) return r;

            var standard = SpongeStandardRegistry.GetOrDefault(input.StandardId);
            r.StandardId = standard.Id;
            var rounding = standard.Rounding ?? new SpongeRounding();

            ApplyLinkedFacilities(standard, input);

            r.TotalSurfaceArea = input.Surfaces?.Sum(s => s.Area) ?? 0;
            double rawPsi = CalculatePsiZ(input.Surfaces);
            r.PsiZ = Round(rawPsi, rounding.PsiDecimals);
            r.AreaHm2 = Round(input.TotalAreaM2 / 10000.0, rounding.AreaHm2Decimals);

            r.DesignVolumeW = Round(
                CalculateW(r.PsiZ, input.DesignRainfallMm, r.AreaHm2),
                rounding.RequiredVolumeDecimals);
            r.HardeningArea = standard.UseHardeningAllocation ? CalculateHardeningArea(input) : 0;
            r.RequiredVolume522 = standard.AllocationVolume(input, r.HardeningArea);
            r.FinalRequiredVolume = Math.Max(r.DesignVolumeW, r.RequiredVolume522);

            r.ProvidedVolume = SumFacilityVolume(input.Facilities, rounding);
            r.VolumeSurplus = r.FinalRequiredVolume > 0
                ? (r.ProvidedVolume - r.FinalRequiredVolume) / r.FinalRequiredVolume
                : 0;

            r.EtaCombined = EtaCombined(input.Facilities, rounding);
            r.EtaAnnual = input.AlphaTarget * r.EtaCombined;
            r.EtaCompared = standard.MultiplySsByAlpha ? r.EtaAnnual : r.EtaCombined;

            r.SinkenGreenRatio = AreaRatio(input.Surfaces, standard.SunkenGreen?.NumeratorCodes, standard.SunkenGreen?.DenominatorCodes);
            r.PerviousPavementRatio = AreaRatio(input.Surfaces, standard.PerviousPavement?.NumeratorCodes, standard.PerviousPavement?.DenominatorCodes);
            r.GreenRatioActual = GreenActual(standard, input);

            double psiM = Math.Min(0.95, r.PsiZ * 1.1);
            if (rounding.PsiDecimals >= 0)
                psiM = Round(psiM, rounding.PsiDecimals);
            r.PsiM = psiM;
            double flow = r.PsiM * input.StormIntensity * r.AreaHm2;
            r.DesignFlowQ = rounding.RequiredVolumeDecimals >= 0 ? Round(flow, 1) : flow;

            double annual = input.TotalAreaM2 * input.DesignRainfallMm / 1000.0 * r.PsiZ;
            r.AnnualRunoffVolume = rounding.RequiredVolumeDecimals >= 0 ? Round(annual, 1) : annual;
            r.EstimatedCostWanyuan = input.UnitSpongeCost * input.TotalAreaM2 / 10000.0;

            r.Checks = standard.BuildChecks(input, r) ?? new List<CheckRow>();
            bool controlling = true;
            foreach (var c in r.Checks)
            {
                if (c.CountsTowardVerdict && !c.Passed) controlling = false;
            }
            r.AllPassed = controlling && r.Checks.Count > 0;
            r.VerdictText = r.AllPassed ? standard.PassVerdict : standard.FailVerdict;
            return r;
        }

        /// <summary>面积类设施从下垫面回写规模；η 不动。</summary>
        public static void ApplyLinkedFacilities(ISpongeStandard standard, SpongeProjectInput input)
        {
            if (standard?.Facilities == null || input?.Facilities == null) return;
            foreach (var spec in standard.Facilities)
            {
                FacilityItem item = null;
                foreach (var f in input.Facilities)
                {
                    if (string.Equals(f.Code, spec.Code, StringComparison.OrdinalIgnoreCase))
                    {
                        item = f;
                        break;
                    }
                }
                if (item == null) continue;
                if (!string.IsNullOrEmpty(spec.AreaSourceCode))
                    item.Quantity = SumArea(input.Surfaces, spec.AreaSourceCode);
                if (spec.FactorFromSinkDepth)
                    item.UnitVolumeFactor = input.SinkenDepthMm / 1000.0;
                else if (spec.ResetFactorEachCompute)
                    item.UnitVolumeFactor = spec.UnitVolumeFactor;
            }
        }

        public static List<CheckRow> BuildDefaultChecks(ISpongeStandard standard, SpongeProjectInput input, SpongeResult r)
        {
            var list = new List<CheckRow>();
            var ci = CultureInfo.InvariantCulture;

            list.Add(new CheckRow
            {
                Code = SpongeCheckCodes.Alpha,
                Name = "年径流总量控制率 α",
                ActualText = input.AlphaTarget.ToString("P1", ci),
                TargetText = "—",
                Passed = true,
                CountsTowardVerdict = false,
                VerdictText = "—",
                Reference = standard.Citation,
            });

            bool volumeOk = r.ProvidedVolume + 1e-6 >= r.FinalRequiredVolume;
            list.Add(new CheckRow
            {
                Code = SpongeCheckCodes.Volume,
                Name = "调蓄容积 V实 ≥ V需",
                ActualText = r.ProvidedVolume.ToString("F1", ci) + " m³",
                TargetText = r.FinalRequiredVolume.ToString("F1", ci) + " m³",
                Passed = volumeOk,
                CountsTowardVerdict = true,
                VerdictText = volumeOk ? "达标" : "不达标",
                Reference = standard.UseHardeningAllocation ? "容积法 + 配建容积取较大值" : "容积法 V=10·H·ψc·F",
            });

            string ssName = standard.MultiplySsByAlpha ? "年 SS 削减率 η_年" : "径流污染削减率 ηSS";
            string ssActual = standard.MultiplySsByAlpha
                ? r.EtaCompared.ToString("P1", ci)
                : r.EtaCompared.ToString("F3", ci);
            string ssTarget = standard.MultiplySsByAlpha
                ? input.EtaSsTarget.ToString("P1", ci)
                : input.EtaSsTarget.ToString("F3", ci);
            bool ssOk = r.EtaCompared + 1e-9 >= input.EtaSsTarget;
            list.Add(new CheckRow
            {
                Code = SpongeCheckCodes.Ss,
                Name = ssName,
                ActualText = ssActual,
                TargetText = ssTarget,
                Passed = ssOk,
                CountsTowardVerdict = true,
                VerdictText = ssOk ? "达标" : "不达标",
                Reference = standard.MultiplySsByAlpha ? "η_年 = α·η综合" : "η综合 = Σ(Vi·ηi)/ΣVi，直接与目标比",
            });

            var green = standard.GreenRatio ?? new GreenRatioRule();
            bool greenOk = r.GreenRatioActual + 1e-9 >= green.Threshold;
            list.Add(new CheckRow
            {
                Code = SpongeCheckCodes.Green,
                Name = string.IsNullOrEmpty(green.Name) ? "绿地率" : green.Name,
                ActualText = r.GreenRatioActual.ToString("P2", ci),
                TargetText = green.CountsTowardVerdict ? "≥ " + green.Threshold.ToString("P0", ci) : "—",
                Passed = green.CountsTowardVerdict ? greenOk : true,
                CountsTowardVerdict = green.CountsTowardVerdict,
                VerdictText = green.CountsTowardVerdict ? (greenOk ? "达标" : "不达标") : green.ReferenceVerdict,
                Reference = green.Reference,
            });

            list.Add(RatioCheck(SpongeCheckCodes.Sunken, standard.SunkenGreen, r.SinkenGreenRatio, input));
            list.Add(RatioCheck(SpongeCheckCodes.Pervious, standard.PerviousPavement, r.PerviousPavementRatio, input));

            var psiRule = standard.PsiOutflow ?? new PsiOutflowRule();
            bool isNew = input.ProjectType == ProjectType.NewResidential
                      || input.ProjectType == ProjectType.NewCommercialHighGreen
                      || input.ProjectType == ProjectType.NewCommercialLowGreen;
            double psiLimit = isNew ? psiRule.NewProjectLimit : psiRule.RebuildLimit;
            bool psiOk = r.PsiZ <= psiLimit + 1e-9;
            list.Add(new CheckRow
            {
                Code = SpongeCheckCodes.Psi,
                Name = string.IsNullOrEmpty(psiRule.Name) ? "外排径流系数 ψc" : psiRule.Name,
                ActualText = r.PsiZ.ToString("F3", ci),
                TargetText = "≤ " + psiLimit.ToString("F2", ci),
                Passed = psiRule.CountsTowardVerdict ? psiOk : true,
                CountsTowardVerdict = psiRule.CountsTowardVerdict,
                VerdictText = psiRule.CountsTowardVerdict ? (psiOk ? "达标" : "不达标") : psiRule.ReferenceVerdict,
                Reference = psiRule.Reference,
            });

            return list;
        }

        private static CheckRow RatioCheck(string code, AreaRatioRule rule, double ratio, SpongeProjectInput input)
        {
            if (rule == null) rule = new AreaRatioRule();
            double den = SumArea(input?.Surfaces, rule.DenominatorCodes ?? new string[0]);
            bool vacuous = den <= 0;
            bool numericPass = vacuous ? rule.PassWhenDenominatorZero : ratio + 1e-9 >= rule.Threshold;
            string verdict;
            if (!rule.CountsTowardVerdict)
                verdict = "—参考";
            else if (vacuous)
                verdict = rule.ZeroDenominatorVerdict ?? (numericPass ? "满足" : "不达标");
            else
                verdict = numericPass ? "达标" : "不达标";

            var ci = CultureInfo.InvariantCulture;
            string actual = vacuous && code == SpongeCheckCodes.Pervious
                ? "0（无铺装）"
                : ratio.ToString("P1", ci);
            return new CheckRow
            {
                Code = code,
                Name = string.IsNullOrEmpty(rule.Name) ? code : rule.Name,
                ActualText = actual,
                TargetText = "≥ " + rule.Threshold.ToString("P0", ci),
                Passed = rule.CountsTowardVerdict ? numericPass : true,
                CountsTowardVerdict = rule.CountsTowardVerdict,
                VerdictText = verdict,
                Reference = rule.Reference,
            };
        }

        private static double GreenActual(ISpongeStandard standard, SpongeProjectInput input)
        {
            var rule = standard.GreenRatio;
            if (rule == null || rule.UseManualInput) return input.GreenRatio;
            if (input.TotalAreaM2 <= 0) return 0;
            double full = SumArea(input.Surfaces, rule.FullCreditCodes ?? new string[0]);
            double partial = SumArea(input.Surfaces, rule.PartialCreditCodes ?? new string[0]);
            return (partial * rule.PartialCreditFactor + full) / input.TotalAreaM2;
        }

        private static double SumFacilityVolume(IEnumerable<FacilityItem> facilities, SpongeRounding rounding)
        {
            if (facilities == null) return 0;
            int digits = rounding == null ? -1 : rounding.FacilityVolumeDecimals;
            double sum = 0;
            foreach (var f in facilities)
            {
                f.ProvidedVolume = Round(f.Quantity * f.UnitVolumeFactor, digits);
                sum += f.ProvidedVolume;
            }
            return sum;
        }

        private static double EtaCombined(IEnumerable<FacilityItem> facilities, SpongeRounding rounding)
        {
            if (facilities == null) return 0;
            int volDigits = rounding == null ? -1 : rounding.FacilityVolumeDecimals;
            int etaDigits = rounding == null ? -1 : rounding.EtaDecimals;
            double sumV = 0, sumVeta = 0;
            foreach (var f in facilities)
            {
                double vi = f.ProvidedVolume;
                sumV += vi;
                double contrib = vi * f.EtaSs;
                if (etaDigits >= 0)
                    contrib = Round(contrib, volDigits >= 0 ? volDigits : etaDigits);
                sumVeta += contrib;
            }
            if (sumV <= 0) return 0;
            return Round(sumVeta / sumV, etaDigits);
        }

        public static double Round(double value, int decimals)
        {
            if (decimals < 0 || double.IsNaN(value) || double.IsInfinity(value)) return value;
            return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        }
    }
}
