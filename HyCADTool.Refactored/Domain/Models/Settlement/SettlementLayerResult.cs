namespace HyCADTool.Refactored.Domain.Models.Settlement
{
    /// <summary>
    /// 单层沉降计算结果
    /// </summary>
    public class SettlementLayerResult
    {
        /// <summary>层序号（从 1 开始）</summary>
        public int Index { get; set; }

        /// <summary>地层编号</summary>
        public string LayerId { get; set; } = "";

        /// <summary>地层名称</summary>
        public string LayerName { get; set; } = "";

        /// <summary>本层底面距基底距离 z_i (m)</summary>
        public double Zi { get; set; }

        /// <summary>l/b 比值</summary>
        public double M { get; set; }

        /// <summary>z_i/b 比值</summary>
        public double N { get; set; }

        public double LbRatio => M;
        public double ZbRatio => N;

        /// <summary>附加应力系数 α</summary>
        public double Alpha { get; set; }

        /// <summary>平均附加应力系数 ᾱ</summary>
        public double AlphaBar { get; set; }

        /// <summary>z_i * ᾱ_i - z_{i-1} * ᾱ_{i-1}</summary>
        public double ZAlphaBarDiff { get; set; }

        /// <summary>压缩模量 Es (MPa)，复合地基已乘 ξ</summary>
        public double Es { get; set; }

        /// <summary>本层变形增量 Δs' (mm)</summary>
        public double DeltaS { get; set; }

        /// <summary>是否在计算深度范围内</summary>
        public bool WithinDepth { get; set; } = true;
    }
}
