using System;
using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.Shared.AutoCAD.Services.Road;

namespace HyCADTool.Features.Road.PlanAlignment.Services
{
    /// <summary>
    /// 路线工作台「设计态彩色预览」服务。
    ///
    /// <para><b>⚠ 工作流已下线</b>（v2 · 2026-04-21 简化）：自动 DesignPreview 机制（PI 调整稳定后在
    /// <c>05_hy_道路_原线</c> 刷分段彩色）已从工作台移除。原线层只保留 252 本色 RawPick 档案且启动即锁定，
    /// 分段彩色由用户点「预览」按钮手动追加到 <c>05_hy_道路_预览</c>（见 <see cref="RoadAlignmentLivePreviewService"/>）。</para>
    ///
    /// <para>本类保留原因：</para>
    /// <list type="bullet">
    ///   <item>历史 DWG 可能残留 <see cref="HyRoadXdata.KindAlignmentDesignPreview"/> 实体 —
    ///     由 <see cref="RoadAlignmentApplyService.Apply"/> 定稿时一并擦掉（按 (KIND, Id) 过滤）；</item>
    ///   <item>后续若需提供一次性迁移命令（如 <c>hyRoadAlnCleanDesignPreview</c>）可继续复用 <see cref="EraseAll"/>。</item>
    /// </list>
    ///
    /// <para><b>不要</b>从工作台 / 命令新链路再调 <see cref="Refresh"/>，否则会把锁定的原线层当作绘图层写入。</para>
    /// </summary>
    public static class RoadAlignmentDesignPreviewService
    {
        /// <summary>
        /// 刷新 <paramref name="aln"/> 在原线层的设计态彩色预览（幂等：先按 (KIND, aln.Id) 擦再画）。
        /// </summary>
        /// <returns>新建的分段 Polyline 条数；无法绘制（中心线无效 / 文档关闭）返回 0。</returns>
        public static int Refresh(Document doc, Alignment aln)
        {
            return AlignmentSegmentRenderer.Draw(
                doc,
                aln,
                HyRoadLayers.RawPolylineLayer,
                HyRoadXdata.KindAlignmentDesignPreview,
                eraseExistingSameId: true);
        }

        /// <summary>
        /// 擦除当前文档 ModelSpace 中所有 KIND=<see cref="HyRoadXdata.KindAlignmentDesignPreview"/> 的实体
        /// （不区分 alignment）。切面板 / 切文档 / Apply 定稿时使用。
        /// </summary>
        /// <returns>擦除条数；doc 为 null 或异常时返回 0。</returns>
        public static int EraseAll(Document doc)
        {
            return AlignmentSegmentRenderer.EraseByKind(doc, HyRoadXdata.KindAlignmentDesignPreview);
        }

        /// <summary>仅擦除指定 alignment 的设计态预览（跨 alignment 多线位并存时的定点清理）。</summary>
        public static int EraseForAlignment(Document doc, Guid alignmentId)
        {
            return AlignmentSegmentRenderer.EraseByKindAndId(doc, HyRoadXdata.KindAlignmentDesignPreview, alignmentId);
        }
    }
}
