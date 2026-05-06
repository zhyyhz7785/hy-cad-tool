using System.Collections.Generic;
using HyCADTool.Features.AcadDimension.Domain.Config;
using HyCADTool.Features.AcadDimension.Domain.Results;

namespace HyCADTool.Features.AcadDimension.Domain.PostProcessing
{
    /// <summary>
    /// 后处理器接口（去零长 / 去重 / 去近距平行 / 等长界线）。
    /// Phase 4 默认实现：DefaultDimensionPostProcessor；
    /// 通用化后未来可与 ClusterPanel 共享（05 §5 通用优化引擎）。
    /// </summary>
    public interface IDimensionPostProcessor
    {
        IReadOnlyList<DerivedDimension> Process(IReadOnlyList<DerivedDimension> input, NewDdsConfig config);
    }
}
