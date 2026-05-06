using System.Collections.Generic;

namespace HyCADTool.Features.AcadDimension.Domain.Results
{
    /// <summary>
    /// Quality 评分卡（5 项 + 综合分 + 问题清单）。
    /// 关键约束：Q 仅校核不选择（06 §5.2、07 §11 第 5 条）。
    /// 范式 C（解析式直接派生）下，Q 不参与生成决策；用于回归门槛与诊断面板可视化。
    /// </summary>
    public sealed class QualityScoreCard
    {
        /// <summary>覆盖率：所有特征点是否都被某条标注覆盖。0=全漏 / 1=全标。</summary>
        public double Coverage { get; set; }

        /// <summary>无重叠率：标注间是否互不重叠。0=全重叠 / 1=无重叠。</summary>
        public double NoOverlap { get; set; }

        /// <summary>不超界率：标注是否全部落在合理区域。0=全超 / 1=全不超。</summary>
        public double WithinBounds { get; set; }

        /// <summary>样式一致性：长度 / 偏移 / 文字方向是否一致。0=全乱 / 1=完全一致。</summary>
        public double Consistency { get; set; }

        /// <summary>4 方向覆盖率：上下左右是否都有外部标注。0=全缺 / 1=全有。</summary>
        public double DirectionalCoverage { get; set; }

        /// <summary>综合分（5 项加权，权重均等时即均值）。</summary>
        public double Overall { get; set; }

        /// <summary>问题清单（"漏标点 #5"、"标注 0xAB 与 0xCD 重叠"等）。</summary>
        public IList<string> Issues { get; } = new List<string>();
    }
}
