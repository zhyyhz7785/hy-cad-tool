namespace HyCADTool.Features.Settlement
{
    /// <summary>
    /// 单层沉降计算结果（对应表 5-7 各列）
    /// </summary>
    public class SettlementLayerResult
    {
        /// <summary>点号（从 0 开始，0 为基底面）</summary>
        public int Index { get; set; }

        /// <summary>地层编号</summary>
        public string LayerId { get; set; } = "";

        /// <summary>地层名称</summary>
        public string LayerName { get; set; } = "";

        /// <summary>z_i (m) 本层底面距基底距离</summary>
        public double Zi { get; set; }

        /// <summary>l/b（全宽比）= L/B，子矩形 l'/b' 相同</summary>
        public double M { get; set; }

        /// <summary>z/(0.5b) = 2z/B（子矩形 z/b'，查表参数）</summary>
        public double NHalf { get; set; }

        /// <summary>ᾱ_i（中心点）= 4 × 角点值</summary>
        public double AlphaBar { get; set; }

        /// <summary>角点 ᾱ 值（= AlphaBar / 4，用于显示 "4×0.xxxx"）</summary>
        public double AlphaBarCorner { get; set; }

        /// <summary>ᾱ_i · z_i (mm)</summary>
        public double AlphaBarZi { get; set; }

        /// <summary>ᾱ_i·z_i − ᾱ_{i-1}·z_{i-1} (mm)</summary>
        public double ZAlphaBarDiff { get; set; }

        /// <summary>p₀ / E_si（无量纲）</summary>
        public double P0overEs { get; set; }

        /// <summary>Δs'_i (mm)</summary>
        public double DeltaS { get; set; }

        /// <summary>Σ Δs'_i (mm) 累计到本层</summary>
        public double CumulativeDeltaS { get; set; }

        /// <summary>Δs'_n / Σ Δs'_i（最后有效层才有意义）</summary>
        public double DepthCheckRatio { get; set; }

        /// <summary>压缩模量 Es (MPa)，复合地基已乘 ξ</summary>
        public double Es { get; set; }

        /// <summary>是否在计算深度范围内</summary>
        public bool WithinDepth { get; set; } = true;
    }
}
