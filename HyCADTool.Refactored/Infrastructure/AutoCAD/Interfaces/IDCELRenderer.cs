using HyCADTool.Refactored.Domain.DataStructures.DCEL;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces
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
    }
}






