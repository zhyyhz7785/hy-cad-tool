using HyCADTool.Domain.DataStructures.DCEL;
using HyCADTool.Shared.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Domain.Services.GeometryAlgorithms
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

        /// <summary>
        /// 分类面为外轮廓或内部（Classify Faces）
        /// </summary>
        /// <param name="graph">DCEL 图</param>
        void ClassifyFaces(DCELGraph graph);

        /// <summary>
        /// 计算面的有向面积（Calculate Signed Area）
        /// 正值表示逆时针（外轮廓），负值表示顺时针（内部）
        /// </summary>
        /// <param name="face">面</param>
        /// <returns>有向面积</returns>
        double CalculateSignedArea(Face face);
    }
}

