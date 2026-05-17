using System.Linq;
using HyCADTool.Features.AcadDimension.Domain.Boundary;
using HyCADTool.Features.AcadDimension.Domain.Results;

namespace HyCADTool.Features.AcadDimension.Validation
{
    /// <summary>
    /// Phase 6 M1：覆盖率 Quality 函数（仅 DirectionalCoverage + 简单 Overall）。
    ///
    /// 评分规则（每项 0.25，全有则 1.0）：
    ///   - OutsideLeft  ≥ 1 条
    ///   - OutsideRight ≥ 1 条
    ///   - OutsideUp    ≥ 1 条
    ///   - OutsideDown  ≥ 1 条
    /// 任一方向缺失 → 写入 Issues，便于命令行诊断。
    ///
    /// 不参与生成决策（范式 C 铁律：Q 只校核不选择，06 §5.2 / 07 §11 第 5 条）。
    /// 后续 Phase 6 完整版补齐：Leak / Overlap / OutOfBounds / Consistency 4 条规则。
    /// </summary>
    public sealed class CoverageQualityFunction : IQualityFunction
    {
        public QualityScoreCard Evaluate(NewDdsResult result, BoundaryFeatures features)
        {
            var card = new QualityScoreCard();
            if (result?.Dimensions == null || result.Dimensions.Count == 0)
            {
                card.Issues.Add("无标注产出（派生链路返回空）");
                return card;
            }

            int hasLeft = result.Dimensions.Any(d => d.Source == DimensionSource.OutsideLeft) ? 1 : 0;
            int hasRight = result.Dimensions.Any(d => d.Source == DimensionSource.OutsideRight) ? 1 : 0;
            int hasUp = result.Dimensions.Any(d => d.Source == DimensionSource.OutsideUp) ? 1 : 0;
            int hasDown = result.Dimensions.Any(d => d.Source == DimensionSource.OutsideDown) ? 1 : 0;

            card.DirectionalCoverage = (hasLeft + hasRight + hasUp + hasDown) / 4.0;
            card.Overall = card.DirectionalCoverage;

            if (hasLeft == 0) card.Issues.Add("外左方向无标注");
            if (hasRight == 0) card.Issues.Add("外右方向无标注");
            if (hasUp == 0) card.Issues.Add("外上方向无标注");
            if (hasDown == 0) card.Issues.Add("外下方向无标注");

            return card;
        }
    }
}
