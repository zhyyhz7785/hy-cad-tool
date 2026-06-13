using HyCADTool.Features.DCEL.Domain.DataStructures;
using HyCAD.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Features.DCEL.Services
{
    /// <summary>
    /// DCEL 渲染器接口（DCEL Renderer Interface）
    /// 负责将 DCEL 图渲染到 AutoCAD
    /// </summary>
    public interface IDCELRenderer
    {
        /// <summary>
        /// 渲染 DCEL 图
        /// 根据 Face.IsOuter 属性分别绘制到不同图层
        /// </summary>
        /// <param name="graph">DCEL 图</param>
        /// <param name="outerLayer">外轮廓面图层名称（默认: dcelOuter）</param>
        /// <param name="innerLayer">内部面图层名称（默认: dcelInner）</param>
        void Render(DCELGraph graph, string outerLayer = "dcelOuter", string innerLayer = "dcelInner");

        /// <summary>
        /// 渲染 DCEL 图（使用简化曲线映射）
        /// 根据映射字典恢复原始曲线或使用简化线段
        /// </summary>
        /// <param name="graph">DCEL 图</param>
        /// <param name="mappings">简化曲线映射列表</param>
        /// <param name="outerLayer">外轮廓面图层名称（默认: dcelOuter）</param>
        /// <param name="innerLayer">内部面图层名称（默认: dcelInner）</param>
        /// <param name="restoreOriginal">是否恢复原始曲线（true=恢复原曲线，false=使用简化线段）</param>
        /// <param name="tolerance">几何容差（与 DCEL 构建容差同源，用于曲线段匹配）</param>
        void RenderWithMappings(
            DCELGraph graph,
            List<SimplifiedCurveMapping> mappings,
            string outerLayer = "dcelOuter",
            string innerLayer = "dcelInner",
            bool restoreOriginal = true,
            double tolerance = 0.01);
    }
}
