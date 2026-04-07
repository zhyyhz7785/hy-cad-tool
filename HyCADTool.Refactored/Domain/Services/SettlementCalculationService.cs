using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Refactored.Domain.Models.Settlement;

namespace HyCADTool.Refactored.Domain.Services
{
    /// <summary>
    /// 基础沉降计算引擎（纯数学，平台无关）
    /// 支持天然基础 / 复合地基 / 桩基础三类沉降计算
    /// 规范依据：GB 50007-2011 §5.3、JGJ 79-2012 §7.1、JGJ 94-2008 §5.5
    /// </summary>
    public static class SettlementCalculationService
    {
        #region 附加应力系数 α（Newmark 公式）

        /// <summary>
        /// 矩形面积均布荷载角点附加应力系数 α（Newmark 1935）
        /// I = (1/4π)[ 2mn√(m²+n²+1)·(m²+n²+2) / ((m²+n²+1)·(1+m²)(1+n²))
        ///          + arctan(2mn√(m²+n²+1) / (m²+n²+1-m²n²)) ]
        /// 其中 m = l/z, n = b/z
        /// 接口参数使用表参数 l/b 和 z/b，内部转换
        /// </summary>
        /// <param name="lbRatio">l/b（长宽比，表参数）</param>
        /// <param name="zbRatio">z/b（深宽比，表参数）</param>
        public static double CalculateAlpha(double lbRatio, double zbRatio)
        {
            if (lbRatio <= 0 || zbRatio < 0) return 0;
            if (zbRatio < 1e-10) return 0.25;

            double m = lbRatio / zbRatio; // l/z
            double n = 1.0 / zbRatio;     // b/z

            return CalculateAlphaByNewmark(m, n);
        }

        /// <summary>
        /// Newmark 公式核心：m = l/z, n = b/z
        /// </summary>
        private static double CalculateAlphaByNewmark(double m, double n)
        {
            double m2 = m * m;
            double n2 = n * n;
            double mn2 = m2 * n2;
            double s = m2 + n2 + 1;
            double sqrtS = Math.Sqrt(s);

            // 第一项：2mn√s·(s+1) / (s·(1+m²)(1+n²))
            // 其中 s+1 = m²+n²+2, (1+m²)(1+n²) = m²+n²+m²n²+1 = s+mn2
            double A = 2 * m * n * sqrtS / (s + mn2);
            double B = (m2 + n2 + 2) / s;

            // arctan 项：需要处理分支切割（当 m²n² > m²+n²+1 时加 π）
            double arctanDenom = s - mn2;
            double arctanNumer = 2 * m * n * sqrtS;

            double C;
            if (Math.Abs(arctanDenom) < 1e-15)
                C = Math.PI / 2;
            else
            {
                C = Math.Atan2(arctanNumer, arctanDenom);
                if (C < 0) C += Math.PI;
            }

            return (A * B + C) / (4 * Math.PI);
        }

        /// <summary>
        /// 矩形中心点附加应力系数 α_center
        /// 四块角点叠加：子矩形 l'=L/2, b'=B/2
        ///   m' = l'/b' = L/B（不变）, z/b' = z/(B/2) = 2z/B
        ///   α_center = 4 × α_corner(l/b, 2z/b)
        /// </summary>
        /// <param name="lbRatio">l/b（全宽）</param>
        /// <param name="zbRatio">z/b（全宽）</param>
        public static double CalculateAlphaCenter(double lbRatio, double zbRatio)
        {
            return 4.0 * CalculateAlpha(lbRatio, 2.0 * zbRatio);
        }

        #endregion

        #region 平均附加应力系数 ᾱ（解析公式 3）

        /// <summary>
        /// 矩形面积均布荷载角点的平均附加应力系数 ᾱ
        /// ᾱ = (1/(2πz))·[ l·ln(…) + b·ln(…) + z·arctan(lb/(zR)) ]
        /// 直接使用 l, b, z 三个物理量
        /// </summary>
        /// <param name="lbRatio">l/b（长宽比，表参数）</param>
        /// <param name="zbRatio">z/b（深宽比，表参数）</param>
        public static double CalculateAlphaBar(double lbRatio, double zbRatio)
        {
            if (lbRatio <= 0) return 0;
            if (zbRatio < 1e-10) return lbRatio > 0 ? 0.25 : 0;

            // 用 b=1 归一化，则 l=lbRatio, z=zbRatio
            double b = 1.0;
            double l = lbRatio;
            double z = zbRatio;

            return CalculateAlphaBarDirect(l, b, z);
        }

        /// <summary>
        /// 公式 (3)：直接用 l, b, z 计算 ᾱ
        /// </summary>
        private static double CalculateAlphaBarDirect(double l, double b, double z)
        {
            double R = Math.Sqrt(b * b + l * l + z * z);
            double R0 = Math.Sqrt(b * b + l * l);

            // 第一项 l·ln[(√(b²+l²+z²)-b)(√(b²+l²)+b) / ((√(b²+l²+z²)+b)(√(b²+l²)-b))]
            double ln1Num = (R - b) * (R0 + b);
            double ln1Den = (R + b) * (R0 - b);
            double term1 = 0;
            if (ln1Den > 1e-15 && ln1Num > 1e-15)
                term1 = l * Math.Log(ln1Num / ln1Den);

            // 第二项 b·ln[(√(b²+l²+z²)-l)(√(b²+l²)+l) / ((√(b²+l²+z²)+l)(√(b²+l²)-l))]
            double ln2Num = (R - l) * (R0 + l);
            double ln2Den = (R + l) * (R0 - l);
            double term2 = 0;
            if (ln2Den > 1e-15 && ln2Num > 1e-15)
                term2 = b * Math.Log(ln2Num / ln2Den);

            // 第三项 z·arctan(l·b / (z·√(b²+l²+z²)))
            double arcDen = z * R;
            double term3 = 0;
            if (arcDen > 1e-15)
                term3 = z * Math.Atan(l * b / arcDen);

            return (term1 + term2 + term3) / (2 * Math.PI * z);
        }

        /// <summary>
        /// 矩形中心点的平均附加应力系数 ᾱ_center
        /// 四块角点叠加：子矩形 l'=L/2, b'=B/2
        ///   m' = l'/b' = L/B（不变）, z/b' = z/(B/2) = 2z/B
        ///   ᾱ_center = 4 × ᾱ_corner(l/b, 2z/b)
        /// </summary>
        /// <param name="lbRatio">l/b（全宽）</param>
        /// <param name="zbRatio">z/b（全宽）</param>
        public static double CalculateAlphaBarCenter(double lbRatio, double zbRatio)
        {
            if (zbRatio < 1e-10) return lbRatio > 0 ? 1.0 : 0;
            return 4.0 * CalculateAlphaBar(lbRatio, 2.0 * zbRatio);
        }

        #endregion

        #region 沉降计算主入口

        /// <summary>
        /// 执行沉降计算
        /// </summary>
        public static SettlementResult Calculate(SettlementInput input)
        {
            var result = new SettlementResult { Type = input.Type };

            if (!Validate(input, result))
                return result;

            // 裁剪土层：用户输入从孔口开始，计算从基底开始
            // 孔口到基底的距离 = 相对高程 + 基础埋深
            double skipDepth = input.RelativeElevation + input.FoundationDepth;
            var effectiveLayers = GetSoilLayersBelowDepth(input.SoilLayers, skipDepth);
            if (!effectiveLayers.Any())
            {
                result.Message = $"基底以下无有效土层（孔口到基底距离 {skipDepth:F2}m 超过总土层厚度）";
                return result;
            }

            var effectiveInput = CloneInputWithLayers(input, effectiveLayers);

            if (input.AdditionalPressure > 0)
            {
                switch (input.Type)
                {
                    case FoundationType.Natural:
                        CalculateNatural(effectiveInput, result);
                        break;
                    case FoundationType.Composite:
                        CalculateComposite(effectiveInput, result);
                        break;
                    case FoundationType.Pile:
                        CalculatePile(effectiveInput, result);
                        break;
                }
            }
            else
            {
                result.Success = true;
            }

            if (result.Success && input.EnableRebound)
                CalculateRebound(effectiveInput, result);

            return result;
        }

        /// <summary>
        /// 从孔口表面向下跳过 skipDepth 米，返回基底以下的有效土层
        /// </summary>
        private static List<SoilLayer> GetSoilLayersBelowDepth(List<SoilLayer> layers, double skipDepth)
        {
            if (skipDepth <= 0) return layers.ToList();

            var result = new List<SoilLayer>();
            double depthAccum = 0;

            foreach (var layer in layers)
            {
                double layerTop = depthAccum;
                double layerBottom = depthAccum + layer.Thickness;
                depthAccum = layerBottom;

                if (layerBottom <= skipDepth) continue;

                double remainThickness = layerBottom - Math.Max(layerTop, skipDepth);
                if (remainThickness <= 0) continue;

                result.Add(new SoilLayer
                {
                    Id = layer.Id,
                    Name = layer.Name,
                    Thickness = remainThickness,
                    Es = layer.Es,
                    Eci = layer.Eci,
                    Description = layer.Description
                });
            }

            return result;
        }

        /// <summary>
        /// 复制 input 但替换土层列表（避免修改原始输入）
        /// </summary>
        private static SettlementInput CloneInputWithLayers(SettlementInput src, List<SoilLayer> layers)
        {
            return new SettlementInput
            {
                BoreholeElevation = src.BoreholeElevation,
                StructureZeroElevation = src.StructureZeroElevation,
                FoundationDepth = src.FoundationDepth,
                Type = src.Type,
                FoundationLength = src.FoundationLength,
                FoundationWidth = src.FoundationWidth,
                AdditionalPressure = src.AdditionalPressure,
                BearingCapacity = src.BearingCapacity,
                CompositeBearingCapacity = src.CompositeBearingCapacity,
                TreatedDepth = src.TreatedDepth,
                PileLength = src.PileLength,
                PileDiameter = src.PileDiameter,
                PileSpacing = src.PileSpacing,
                CapLength = src.CapLength,
                CapWidth = src.CapWidth,
                TotalPileCount = src.TotalPileCount,
                IsBoxFoundation = src.IsBoxFoundation,
                BoxConcreteThickness = src.BoxConcreteThickness,
                GammaM = src.GammaM,
                EnableRebound = src.EnableRebound,
                EciEsiRatio = src.EciEsiRatio,
                PsiC = src.PsiC,
                Kappa = src.Kappa,
                Eta1 = src.Eta1,
                Eta2 = src.Eta2,
                R0Prime = src.R0Prime,
                r0Prime = src.r0Prime,
                ReboundDepthRatio = src.ReboundDepthRatio,
                SoilLayers = layers
            };
        }

        #endregion

        #region 天然基础沉降（GB 50007 §5.3.5）

        private static void CalculateNatural(SettlementInput input, SettlementResult result)
        {
            double b = input.FoundationWidth;
            double l = input.FoundationLength;
            double m = l / b;
            double p0 = input.AdditionalPressure;

            var layerResults = CalculateLayerDeformations(input.SoilLayers, m, b, p0, null, 0);
            result.LayerResults = layerResults;

            ApplyDepthCheck(layerResults, b);

            var withinDepth = layerResults.Where(r => r.WithinDepth).ToList();
            result.TheoreticalSettlement = withinDepth.Sum(r => r.DeltaS);
            result.CalculationDepth = withinDepth.Any() ? withinDepth.Last().Zi : 0;
            result.SimplifiedDepth = b > 0 ? b * (2.5 - 0.4 * Math.Log(b)) : 0;

            result.EquivalentEs = CalculateEquivalentEs(withinDepth, m, b);
            result.PressureRatio = input.BearingCapacity > 0 ? p0 / input.BearingCapacity : 0;
            result.PsiS = InterpolatePsiS_Natural(result.EquivalentEs, result.PressureRatio > 0.75);
            result.CompressionSettlement = result.PsiS * result.TheoreticalSettlement;
            result.FinalSettlement = result.CompressionSettlement;
            result.Success = true;
        }

        #endregion

        #region 复合地基沉降（JGJ 79 §7.1.7）

        private static void CalculateComposite(SettlementInput input, SettlementResult result)
        {
            double b = input.FoundationWidth;
            double l = input.FoundationLength;
            double m = l / b;
            double p0 = input.AdditionalPressure;
            double xi = input.Xi;

            var layerResults = CalculateLayerDeformations(input.SoilLayers, m, b, p0, xi, input.TreatedDepth);
            result.LayerResults = layerResults;

            ApplyDepthCheck(layerResults, b);

            var withinDepth = layerResults.Where(r => r.WithinDepth).ToList();
            result.TheoreticalSettlement = withinDepth.Sum(r => r.DeltaS);
            result.CalculationDepth = withinDepth.Any() ? withinDepth.Last().Zi : 0;
            result.SimplifiedDepth = b > 0 ? b * (2.5 - 0.4 * Math.Log(b)) : 0;

            result.EquivalentEs = CalculateEquivalentEs(withinDepth, m, b);
            result.PressureRatio = input.BearingCapacity > 0 ? p0 / input.BearingCapacity : 0;
            result.PsiS = InterpolatePsiS_Composite(result.EquivalentEs);
            result.CompressionSettlement = result.PsiS * result.TheoreticalSettlement;
            result.FinalSettlement = result.CompressionSettlement;
            result.Success = true;
        }

        #endregion

        #region 桩基础沉降（JGJ 94 §5.5.6~5.5.9）

        private static void CalculatePile(SettlementInput input, SettlementResult result)
        {
            double b = input.CapWidth;
            double l = input.CapLength;
            if (b <= 0) b = input.FoundationWidth;
            if (l <= 0) l = input.FoundationLength;

            double m = b > 0 ? l / b : 1;
            double p0 = input.AdditionalPressure;

            // 桩端以下的土层（跳过桩长范围内的土层）
            var belowPileTip = GetSoilLayersBelowPileTip(input.SoilLayers, input.PileLength);
            var layerResults = CalculateLayerDeformations(belowPileTip, m, b, p0, null, 0);
            result.LayerResults = layerResults;

            ApplyDepthCheck_Pile(layerResults, p0, input);

            var withinDepth = layerResults.Where(r => r.WithinDepth).ToList();
            result.TheoreticalSettlement = withinDepth.Sum(r => r.DeltaS);
            result.CalculationDepth = withinDepth.Any() ? withinDepth.Last().Zi : 0;

            result.EquivalentEs = CalculateEquivalentEs(withinDepth, m, b);
            result.PsiS = InterpolatePsiS_Pile(result.EquivalentEs);
            result.PsiE = CalculatePsiE(input);
            result.CompressionSettlement = result.PsiS * result.PsiE * result.TheoreticalSettlement;
            result.FinalSettlement = result.CompressionSettlement;
            result.Success = true;
        }

        /// <summary>
        /// 获取桩端以下的土层（将桩身范围内的土层厚度扣除）
        /// </summary>
        private static List<SoilLayer> GetSoilLayersBelowPileTip(List<SoilLayer> layers, double pileLength)
        {
            var result = new List<SoilLayer>();
            double depthAccum = 0;

            foreach (var layer in layers)
            {
                double layerTop = depthAccum;
                double layerBottom = depthAccum + layer.Thickness;
                depthAccum = layerBottom;

                if (layerBottom <= pileLength) continue;

                double remainThickness = layerBottom - Math.Max(layerTop, pileLength);
                if (remainThickness <= 0) continue;

                result.Add(new SoilLayer
                {
                    Id = layer.Id,
                    Name = layer.Name,
                    Thickness = remainThickness,
                    Es = layer.Es,
                    Description = layer.Description
                });
            }

            return result;
        }

        /// <summary>
        /// 桩基等效沉降系数 ψ_e (JGJ 94 式5.5.9)
        /// ψ_e = C₀ + (nb-1)/[C₁(nb-1)+C₂]
        /// 简化取 C₀=0.04, C₁=2.0, C₂=0.4（中等桩距桩径比）
        /// </summary>
        private static double CalculatePsiE(SettlementInput input)
        {
            double bc = input.CapWidth > 0 ? input.CapWidth : input.FoundationWidth;
            double lc = input.CapLength > 0 ? input.CapLength : input.FoundationLength;
            int n = input.TotalPileCount;

            if (n <= 1 || bc <= 0 || lc <= 0) return 1.0;

            double nb = Math.Sqrt(n * bc / lc);
            if (nb <= 1) return 1.0;

            // C₀、C₁、C₂ 的精确值需查附录 E（取决于 sa/d, l/d, Lc/Bc）
            // 此处取典型中间值作为简化
            double c0 = 0.04, c1 = 2.0, c2 = 0.4;
            return c0 + (nb - 1) / (c1 * (nb - 1) + c2);
        }

        /// <summary>
        /// 桩基深度判定：应力比法 σ_z ≤ 0.2·σ_c (JGJ 94 §5.5.8)
        /// </summary>
        private static void ApplyDepthCheck_Pile(List<SettlementLayerResult> layers, double p0, SettlementInput input)
        {
            // 简化：桩端以下的自重应力需要累加桩长范围内的土重
            // 假设平均容重 18 kN/m³，地下水位以下取有效重度 8 kN/m³
            double gamma = 10.0; // kN/m³，简化有效重度
            double sigmaC_base = gamma * input.PileLength + gamma * input.FoundationDepth;

            double depthAccum = 0;
            foreach (var layer in layers)
            {
                depthAccum = layer.Zi;
                double zbFull = layer.NHalf * 0.5;
                double sigmaZ = p0 * CalculateAlphaCenter(layer.M, zbFull);
                double sigmaC = sigmaC_base + gamma * depthAccum;

                if (sigmaZ <= 0.2 * sigmaC && depthAccum > 0)
                {
                    // 从此层开始标记为超出计算深度
                    int idx = layers.IndexOf(layer);
                    for (int j = idx + 1; j < layers.Count; j++)
                        layers[j].WithinDepth = false;
                    break;
                }
            }
        }

        #endregion

        #region 分层变形计算核心

        /// <summary>
        /// 计算各层变形增量
        /// </summary>
        /// <param name="layers">土层列表</param>
        /// <param name="m">l/b</param>
        /// <param name="b">基础宽度 (m)</param>
        /// <param name="p0">基底附加压力 (kPa)</param>
        /// <param name="xi">复合地基压缩模量提高系数（null 表示不使用）</param>
        /// <param name="treatedDepth">加固层深度 (m)，从基底起算</param>
        private static List<SettlementLayerResult> CalculateLayerDeformations(
            List<SoilLayer> layers, double m, double b, double p0,
            double? xi, double treatedDepth)
        {
            var results = new List<SettlementLayerResult>();
            double halfB = b * 0.5;
            double zPrev = 0;
            double alphaBarPrev = CalculateAlphaBarCenter(m, 0);
            double alphaBarZiPrev = 0;
            double cumDeltaS = 0;
            int index = 1;

            foreach (var layer in layers)
            {
                if (layer.Thickness <= 0 || layer.Es <= 0) continue;

                double zi = zPrev + layer.Thickness;
                double ni = b > 0 ? zi / b : 0;
                double nHalf = halfB > 0 ? zi / halfB : 0;

                double alphaBar_i = CalculateAlphaBarCenter(m, ni);
                double alphaBarCorner = alphaBar_i / 4.0;
                double alphaBarZi = alphaBar_i * zi * 1000;
                double zAlphaDiffMm = alphaBarZi - alphaBarZiPrev;

                double es = layer.Es;
                if (xi.HasValue && zPrev < treatedDepth)
                {
                    double treatedPortion = Math.Min(layer.Thickness, treatedDepth - zPrev);
                    double naturalPortion = layer.Thickness - treatedPortion;
                    if (naturalPortion <= 0)
                        es = layer.Es * xi.Value;
                    else
                        es = layer.Es * (treatedPortion * xi.Value + naturalPortion) / layer.Thickness;
                }

                double p0overEs = es > 0 ? p0 / (es * 1000) : 0;
                double deltaS = p0overEs * zAlphaDiffMm;
                cumDeltaS += deltaS;

                results.Add(new SettlementLayerResult
                {
                    Index = index++,
                    LayerId = layer.Id,
                    LayerName = layer.Name,
                    Zi = zi,
                    M = m,
                    NHalf = nHalf,
                    AlphaBar = alphaBar_i,
                    AlphaBarCorner = alphaBarCorner,
                    AlphaBarZi = alphaBarZi,
                    ZAlphaBarDiff = zAlphaDiffMm,
                    P0overEs = p0overEs,
                    Es = es,
                    DeltaS = deltaS,
                    CumulativeDeltaS = cumDeltaS
                });

                zPrev = zi;
                alphaBarPrev = alphaBar_i;
                alphaBarZiPrev = alphaBarZi;
            }

            return results;
        }

        #endregion

        #region 计算深度判定（GB 50007 §5.3.7）

        /// <summary>
        /// 天然基础/复合地基深度判定：Δs'_n ≤ 0.025·Σ(Δs'_i)
        /// 使用 Δz 按 b 确定（表5.3.7）
        /// </summary>
        private static void ApplyDepthCheck(List<SettlementLayerResult> layers, double b)
        {
            if (!layers.Any()) return;

            double totalSum = layers.Sum(r => r.DeltaS);
            if (totalSum <= 0) return;

            double threshold = 0.025 * totalSum;

            // 从最后一层向上检查，找到满足条件的层
            bool foundDepth = false;
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                if (layers[i].DeltaS > threshold)
                {
                    foundDepth = true;
                    break;
                }
                // 如果连续多层都很小，不标记（保持 WithinDepth=true，让结果自然截断）
            }

            // 如果没找到需要截断的位置，全部层都在计算深度内
            if (!foundDepth) return;

            for (int i = layers.Count - 1; i >= 0; i--)
            {
                if (layers[i].DeltaS > threshold) break;
                layers[i].WithinDepth = false;
            }

            var withinLayers = layers.Where(r => r.WithinDepth).ToList();
            if (withinLayers.Any())
            {
                double cumSum = withinLayers.Sum(r => r.DeltaS);
                var last = withinLayers.Last();
                last.DepthCheckRatio = cumSum > 0 ? last.DeltaS / cumSum : 0;
            }
        }

        #endregion

        #region 压缩模量当量值（GB 50007 §5.3.6）

        /// <summary>
        /// Ē_s = ΣA_i / Σ(A_i/E_si)
        /// A_i = z_i·ᾱ_i - z_{i-1}·ᾱ_{i-1}（附加应力积分值）
        /// </summary>
        private static double CalculateEquivalentEs(List<SettlementLayerResult> layers, double m, double b)
        {
            double sumA = 0, sumAoverE = 0;

            foreach (var r in layers)
            {
                double ai = r.ZAlphaBarDiff;
                sumA += ai;
                if (r.Es > 0)
                    sumAoverE += ai / r.Es;
            }

            return sumAoverE > 1e-15 ? sumA / sumAoverE : 0;
        }

        #endregion

        #region 经验系数 ψ_s 插值

        /// <summary>
        /// 天然基础经验系数（GB 50007 表5.3.5）
        /// </summary>
        private static double InterpolatePsiS_Natural(double es, bool highPressure)
        {
            // 表5.3.5: Ē_s → ψ_s
            // p₀ > 0.75fak 行
            double[] esValues_high = { 2.5, 4.0, 7.0, 15.0, 20.0 };
            double[] psi_high = { 1.4, 1.3, 1.0, 0.4, 0.2 };
            // p₀ ≤ 0.75fak 行
            double[] esValues_low = { 2.5, 4.0, 7.0, 15.0, 20.0 };
            double[] psi_low = { 1.1, 1.0, 0.7, 0.4, 0.2 };

            return highPressure
                ? Interpolate(esValues_high, psi_high, es)
                : Interpolate(esValues_low, psi_low, es);
        }

        /// <summary>
        /// 复合地基经验系数（JGJ 79 表7.1.8）
        /// </summary>
        private static double InterpolatePsiS_Composite(double es)
        {
            double[] esValues = { 4.0, 7.0, 15.0, 20.0, 35.0 };
            double[] psiValues = { 1.0, 0.7, 0.4, 0.25, 0.2 };
            return Interpolate(esValues, psiValues, es);
        }

        /// <summary>
        /// 桩基经验系数（JGJ 94 表5.5.11）
        /// </summary>
        private static double InterpolatePsiS_Pile(double es)
        {
            double[] esValues = { 10.0, 15.0, 20.0, 35.0, 50.0 };
            double[] psiValues = { 2.0, 0.9, 0.65, 0.50, 0.40 };
            return Interpolate(esValues, psiValues, es);
        }

        /// <summary>
        /// 线性插值（超出范围取边界值）
        /// </summary>
        private static double Interpolate(double[] x, double[] y, double xVal)
        {
            if (x.Length == 0) return 1.0;
            if (xVal <= x[0]) return y[0];
            if (xVal >= x[x.Length - 1]) return y[y.Length - 1];

            for (int i = 0; i < x.Length - 1; i++)
            {
                if (xVal >= x[i] && xVal <= x[i + 1])
                {
                    double t = (xVal - x[i]) / (x[i + 1] - x[i]);
                    return y[i] + t * (y[i + 1] - y[i]);
                }
            }

            return y[y.Length - 1];
        }

        #endregion

        #region 回弹再压缩计算（GB 50007 §5.3.10~5.3.11）

        /// <summary>
        /// §5.3.11 分段线性再压缩公式：
        ///   p &lt; R'₀·pc:  s'c = r'₀·sc · p/(R'₀·pc)
        ///   R'₀·pc ≤ p ≤ pc:  s'c = sc·[r'₀ + (κ-r'₀)/(1-R'₀)·(p/pc - R'₀)]
        ///   p &gt; pc:  s'c = κ·sc（取 R'=1 上限）
        /// </summary>
        private static double CalculateRecompression531(
            double sc, double Rprime, double R0Prime, double r0Prime, double kappa)
        {
            if (sc <= 0 || Rprime <= 0) return 0;
            if (R0Prime <= 0) R0Prime = 0.3;
            if (r0Prime <= 0) r0Prime = 0.4;

            if (Rprime >= 1.0)
                return kappa * sc;

            if (Rprime < R0Prime)
                return r0Prime * sc * Rprime / R0Prime;

            double slope = (kappa - r0Prime) / (1.0 - R0Prime);
            return sc * (r0Prime + slope * (Rprime - R0Prime));
        }

        /// <summary>
        /// 回弹再压缩计算 — 严格遵循物理过程
        ///
        /// 物理过程：
        /// 1. 开挖卸荷 → 理论回弹 sc (§5.3.10)
        /// 2. 施工期实际完成回弹 η₁·sc，其中 η₂ 比例被基底整平清除
        /// 3. 再加荷 p = p₀ + pc
        ///
        /// 情况 A (p ≤ pc): 土体未超过原始应力，仅按 §5.3.11 分段公式
        ///   净变形 = s'c − η₂·η₁·sc
        ///
        /// 情况 B (p > pc): 两阶段
        ///   段 1 (0→pc): s'c(R'=1)=sc·κ，扣除剩余回弹后的净沉降
        ///   段 2 (p−pc→): 超出原始应力的部分 (p−pc)=p₀ 按 §5.3.5 计算沉降
        ///   最终沉降 = 段1净沉降 + 段2压缩沉降（保证 p=pc 边界连续）
        /// </summary>
        private static void CalculateRebound(SettlementInput input, SettlementResult result)
        {
            double b = input.FoundationWidth;
            double l = input.FoundationLength;
            double m = l / b;
            double gammaM = input.GammaM > 0 ? input.GammaM : 20.0;
            double pc = gammaM * input.FoundationDepth;
            double p0 = input.AdditionalPressure;
            double p = p0 + pc;
            double kappa = input.Kappa;
            double eta1 = input.Eta1;
            double eta2 = input.Eta2;

            if (pc <= 0) return;

            double sc = CalculateReboundSc(input.SoilLayers, m, b, pc, input);
            if (sc <= 0) return;

            double completedRebound = eta1 * sc;
            double clearedRebound = eta2 * completedRebound;
            double Rprime = p / pc;

            result.HasRebound = true;
            result.ReboundSettlement = Math.Round(sc, 3);
            result.CompletedRebound = Math.Round(completedRebound, 3);
            result.ClearedRebound = Math.Round(clearedRebound, 3);
            result.OverburdenPressure = Math.Round(pc, 1);
            result.TotalReloadPressure = Math.Round(p, 1);
            result.ReloadRatio = Math.Round(Rprime, 3);

            if (p <= pc)
            {
                // 情况 A：p ≤ pc（补偿/超补偿），全程 §5.3.11
                double sPrimeC = CalculateRecompression531(sc, Rprime,
                    input.R0Prime, input.r0Prime, kappa);

                // 土中剩余回弹 = sc − 清除量（理论回弹扣除永久损失）
                double remainingRebound = sc - clearedRebound;
                // s'c 先抵消剩余回弹，仅超出部分为实际沉降
                double netSettlement = sPrimeC - remainingRebound;

                result.RecompressionSettlement = Math.Round(sPrimeC, 3);
                result.NetReboundSettlement = Math.Round(netSettlement, 3);
                result.FinalSettlement = Math.Round(netSettlement, 2);
            }
            else
            {
                // 情况 B：p > pc（非补偿）
                // 段 1 (0→pc): 再压缩量 s'c = sc·κ，但清除的回弹不可恢复
                double sPrimeC = kappa * sc;
                double remainingRebound = sc - clearedRebound;
                double recompNet = sPrimeC - remainingRebound;
                if (recompNet < 0) recompNet = 0;

                result.RecompressionSettlement = Math.Round(sPrimeC, 3);
                result.NetReboundSettlement = Math.Round(recompNet, 3);

                // 段 2: p₀ 引起的附加应力沉降 (已在 §5.3.5 计算)
                // 最终 = 段1净沉降 + 段2压缩
                result.FinalSettlement = Math.Round(recompNet + result.CompressionSettlement, 2);
            }
        }

        /// <summary>
        /// 计算回弹量 sc（§5.3.10），含深度判定
        /// </summary>
        private static double CalculateReboundSc(
            List<SoilLayer> layers, double m, double b, double pc, SettlementInput input)
        {
            double depthRatio = input.ReboundDepthRatio > 0 ? input.ReboundDepthRatio : 0.025;

            var layerDeltas = new List<double>();
            double zPrev = 0;
            double alphaBarPrev = CalculateAlphaBarCenter(m, 0);
            double scTotal = 0;

            foreach (var layer in layers)
            {
                double zi = zPrev + layer.Thickness;
                double zbRatio = zi / b;
                double alphaBarI = CalculateAlphaBarCenter(m, zbRatio);
                double zAlphaDiff = zi * alphaBarI - zPrev * alphaBarPrev;

                double eci = layer.Eci > 0 ? layer.Eci : layer.Es * input.EciEsiRatio;
                if (eci <= 0) eci = layer.Es * 5.0;

                double deltaSc = (pc / (eci * 1000.0)) * zAlphaDiff * 1000.0;
                layerDeltas.Add(deltaSc);
                scTotal += deltaSc;

                zPrev = zi;
                alphaBarPrev = alphaBarI;
            }

            if (scTotal <= 0) return 0;

            double threshold = depthRatio * scTotal;
            int effectiveCount = layerDeltas.Count;
            for (int i = layerDeltas.Count - 1; i >= 0; i--)
            {
                if (layerDeltas[i] > threshold) break;
                effectiveCount = i;
            }
            if (effectiveCount <= 0) effectiveCount = 1;

            double sc = 0;
            for (int i = 0; i < effectiveCount; i++)
                sc += layerDeltas[i];

            return sc * input.PsiC;
        }

        #endregion

        #region 输入验证

        private static bool Validate(SettlementInput input, SettlementResult result)
        {
            if (input.FoundationWidth <= 0)
            {
                result.Message = "基础宽度 b 必须大于 0";
                return false;
            }
            if (input.FoundationLength <= 0)
            {
                result.Message = "基础长度 l 必须大于 0";
                return false;
            }
            if (input.AdditionalPressure <= 0 && !input.EnableRebound)
            {
                result.Message = "基底附加压力 p₀ 必须大于 0（或启用回弹再压缩计算）";
                return false;
            }
            if (input.SoilLayers == null || !input.SoilLayers.Any())
            {
                result.Message = "请输入至少一层土数据";
                return false;
            }
            if (input.SoilLayers.Any(s => s.Es <= 0))
            {
                result.Message = "所有土层的压缩模量 Es 必须大于 0";
                return false;
            }
            if (input.Type == FoundationType.Composite && input.CompositeBearingCapacity <= 0)
            {
                result.Message = "复合地基承载力 fspk 必须大于 0";
                return false;
            }
            if (input.Type == FoundationType.Pile && input.PileLength <= 0)
            {
                result.Message = "桩长必须大于 0";
                return false;
            }
            return true;
        }

        #endregion
    }
}
