using System;
using System.Collections.Generic;

namespace HyCADTool.Features.Settlement
{
    /// <summary>
    /// GB 50007-2011 §5.3.7 压缩层深度：Δs′n ≤ ratio · ΣΔs′i。
    /// Δs′n 是计算深度处厚度 Δz 内的沉降，不是整层沉降；Δz 按基础宽度 b 查表 5.3.7。
    /// </summary>
    public static class Gb50007SettlementDepth
    {
        public const double DefaultRatio = 0.025;

        public readonly struct LayerSlice
        {
            public LayerSlice(double thickness, double deltaS)
            {
                Thickness = thickness;
                DeltaS = deltaS;
            }

            public double Thickness { get; }
            public double DeltaS { get; }
        }

        public readonly struct DepthDecision
        {
            public DepthDecision(int includedCount, double bandSettlement, double cumulativeSum, double ratio, bool satisfied)
            {
                IncludedCount = includedCount;
                BandSettlement = bandSettlement;
                CumulativeSum = cumulativeSum;
                Ratio = ratio;
                Satisfied = satisfied;
            }

            /// <summary>自地表向下计入的土层数。无一深度满足时等于全部层数。</summary>
            public int IncludedCount { get; }
            /// <summary>所选深度处 Δz 条带内的沉降 Δs′n。</summary>
            public double BandSettlement { get; }
            /// <summary>自第一层累计到所选深度的 ΣΔs′i。</summary>
            public double CumulativeSum { get; }
            /// <summary>Δs′n / ΣΔs′i。累计为 0 时为 0。</summary>
            public double Ratio { get; }
            public bool Satisfied { get; }
        }

        /// <summary>
        /// 表 5.3.7：b≤2m → 0.3m；2&lt;b≤4 → 0.6m；4&lt;b≤8 → 0.8m；b&gt;8 → 1.0m。b≤0 按 ≤2m 取 0.3m。
        /// </summary>
        public static double DeltaZ(double foundationWidthB)
        {
            if (foundationWidthB <= 2.0) return 0.3;
            if (foundationWidthB <= 4.0) return 0.6;
            if (foundationWidthB <= 8.0) return 0.8;
            return 1.0;
        }

        /// <summary>
        /// 自上而下找最浅的 n：层 n 底面以上 Δz 条带内的沉降 ≤ ratio × 累计到 n 的沉降。
        /// 条带切到某层时按厚度分摊该层 Δs。没有一层满足时保留全部土层。
        /// </summary>
        public static DepthDecision Resolve(IReadOnlyList<LayerSlice> layers, double foundationWidthB, double ratio = DefaultRatio)
        {
            if (layers == null || layers.Count == 0)
                return new DepthDecision(0, 0, 0, 0, false);

            if (ratio < 0) ratio = DefaultRatio;
            double deltaZ = DeltaZ(foundationWidthB);

            var thickness = new double[layers.Count];
            var delta = new double[layers.Count];
            var cumulative = new double[layers.Count];
            double sum = 0;
            for (int i = 0; i < layers.Count; i++)
            {
                thickness[i] = layers[i].Thickness;
                delta[i] = layers[i].DeltaS;
                sum += delta[i];
                cumulative[i] = sum;
            }

            for (int n = 1; n <= layers.Count; n++)
            {
                double band = BandSettlement(thickness, delta, n, deltaZ);
                double cum = cumulative[n - 1];
                if (cum > 0 && band <= ratio * cum + 1e-9)
                    return new DepthDecision(n, band, cum, band / cum, true);
            }

            int last = layers.Count;
            double lastCum = cumulative[last - 1];
            double lastBand = BandSettlement(thickness, delta, last, deltaZ);
            double lastRatio = lastCum > 0 ? lastBand / lastCum : 0;
            return new DepthDecision(last, lastBand, lastCum, lastRatio, false);
        }

        private static double BandSettlement(double[] thickness, double[] delta, int layerCount, double deltaZ)
        {
            double remaining = deltaZ;
            double band = 0;
            for (int i = layerCount - 1; i >= 0 && remaining > 1e-12; i--)
            {
                double t = thickness[i];
                if (t <= 1e-12)
                {
                    band += delta[i];
                    continue;
                }

                double take = t < remaining ? t : remaining;
                band += delta[i] * (take / t);
                remaining -= take;
            }
            return band;
        }
    }
}
