using HyCADTool.Features.AcadDimension.Domain.Config;
using HyCADTool.Features.AcadDimension.Domain.Context;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Features.AcadDimension.Domain.Boundary
{
    /// <summary>
    /// 拓扑特征提取器接口。
    /// 范式：S1a 解析式直接派生（06 §5）。
    /// 当前唯一实现：SweepLineFeatureExtractor（Phase 2 落地）。
    /// 入参：边界 Polyline2D、配置、上下文；出参：BoundaryFeatures。
    /// </summary>
    public interface IBoundaryFeatureExtractor
    {
        BoundaryFeatures Extract(Polyline2D boundary, NewDdsConfig config, NewDdsContext context);
    }
}
