using HyCADTool.Features.AcadDimension.Domain.Boundary;
using HyCADTool.Features.AcadDimension.Domain.Results;

namespace HyCADTool.Features.AcadDimension.Validation
{
    /// <summary>
    /// Quality 函数接口（Phase 6 落地）。
    /// 输入：派生结果 + 拓扑特征；输出：评分卡（不影响生成结果——仅校核与诊断）。
    /// 范式 C 的关键约束：Q 仅校核不选择（06 §5.2、07 §11 第 5 条）。
    /// </summary>
    public interface IQualityFunction
    {
        QualityScoreCard Evaluate(NewDdsResult result, BoundaryFeatures features);
    }
}
