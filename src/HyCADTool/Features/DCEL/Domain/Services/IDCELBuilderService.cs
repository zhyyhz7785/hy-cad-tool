using HyCADTool.Features.DCEL.Domain.DataStructures;
using HyCAD.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Features.DCEL.Domain.Services
{
    /// <summary>
    /// DCEL 构建服务接口（DCEL Builder Service Interface）
    /// 负责从几何数据构建双连接边表数据结构
    /// </summary>
    public interface IDCELBuilderService
    {
        /// <summary>
        /// 从线段列表构建 DCEL 图（Build DCEL from Line Segments）
        /// </summary>
        /// <param name="segments">线段列表</param>
        /// <param name="tolerance">容差</param>
        /// <returns>构建的 DCEL 图</returns>
        DCELGraph BuildFromSegments(List<Line2D> segments, Tolerance tolerance);
    }
}
