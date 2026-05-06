using System.Collections.Generic;

namespace HyCADTool.Features.AcadDimension.Domain.Results
{
    /// <summary>
    /// NewDDS 一次执行的最终产物。
    /// 由 NewDdsService 编排链路逐阶段填充；最终交给 Renderer 写入 AutoCAD。
    /// </summary>
    public sealed class NewDdsResult
    {
        /// <summary>所有派生标注（已经过后处理，可直接写入）。</summary>
        public IReadOnlyList<DerivedDimension> Dimensions { get; set; }
            = new List<DerivedDimension>();

        /// <summary>Quality 评分卡（Phase 6 填充）。Q 仅校核不选择（06 §5.2）。</summary>
        public QualityScoreCard ScoreCard { get; set; } = new QualityScoreCard();

        /// <summary>跨阶段诊断信息合并（Context + Features + 链路日志）。</summary>
        public IList<string> Diagnostics { get; set; } = new List<string>();
    }
}
