using System.Collections.Generic;
using HyCADTool.Features.AcadDimension.Domain.Boundary;
using HyCADTool.Features.AcadDimension.Domain.Config;
using HyCADTool.Features.AcadDimension.Domain.Results;

namespace HyCADTool.Features.AcadDimension.Domain.Derivation
{
    /// <summary>
    /// 标注派生器接口。
    /// 输入：拓扑特征 + 配置；输出：DerivedDimension[]（Domain 层标注 DTO，未做后处理）。
    /// Phase 3 实现：OutsideDimensionDeriver / InsideDimensionDeriver / 总尺寸 Deriver；
    /// 多个 Deriver 由 Service 编排串联（按 06 §6 拆分）。
    /// </summary>
    public interface IDimensionDeriver
    {
        IReadOnlyList<DerivedDimension> Derive(BoundaryFeatures features, NewDdsConfig config);
    }
}
