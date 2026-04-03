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

            switch (input.Type)
            {
                case FoundationType.Natural:
                    CalculateNatural(input, result);
                    break;
                case FoundationType.Composite:
                    CalculateComposite(input, result);
                    break;
                case FoundationType.Pile:
                    CalculatePile(input, result);
                    break;
            }

            return result;
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
            result.FinalSettlement = result.PsiS * result.TheoreticalSettlement;
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
            result.FinalSettlement = result.PsiS * result.TheoreticalSettlement;
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
            result.FinalSettlement = result.PsiS * result.PsiE * result.TheoreticalSettlement;
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
                double sigmaZ = p0 * CalculateAlpha(layer.M, layer.N);
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
            double zPrev = 0;
            double alphaBarPrev = CalculateAlphaBar(m, 0); // z=0 时
            int index = 1;

            foreach (var layer in layers)
            {
                if (layer.Thickness <= 0 || layer.Es <= 0) continue;

                double zi = zPrev + layer.Thickness;
                double ni = b > 0 ? zi / b : 0;

                double alphaBar_i = CalculateAlphaBar(m, ni);
                double alpha_i = CalculateAlpha(m, ni);
                double zAlphaDiff = zi * alphaBar_i - zPrev * alphaBarPrev;

                // 复合地基：加固层内 Es 乘以 ξ
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

                // Δs'_i = (p₀ / E_si) * (z_i·ᾱ_i - z_{i-1}·ᾱ_{i-1})
                // 注意单位：p₀(kPa) / Es(MPa) = kPa / (1000 kPa) = 1/1000
                // 结果乘 1000 转 mm
                double deltaS = es > 0 ? (p0 / (es * 1000)) * zAlphaDiff * 1000 : 0;

                results.Add(new SettlementLayerResult
                {
                    Index = index++,
                    LayerId = layer.Id,
                    LayerName = layer.Name,
                    Zi = zi,
                    M = m,
                    N = ni,
                    Alpha = alpha_i,
                    AlphaBar = alphaBar_i,
                    ZAlphaBarDiff = zAlphaDiff,
                    Es = es,
                    DeltaS = deltaS
                });

                zPrev = zi;
                alphaBarPrev = alphaBar_i;
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

            // 重新从后向前标记：最后一个 DeltaS > threshold 的层之后都超出
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                if (layers[i].DeltaS > threshold) break;
                layers[i].WithinDepth = false;
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
            if (input.AdditionalPressure <= 0)
            {
                result.Message = "基底附加压力 p₀ 必须大于 0";
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
