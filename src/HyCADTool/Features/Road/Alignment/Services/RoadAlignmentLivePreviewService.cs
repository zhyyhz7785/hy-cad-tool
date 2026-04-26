using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.Shared.AutoCAD.Services.Road;

namespace HyCADTool.Features.Road.PlanAlignment.Services
{
    /// <summary>
    /// 路线工作台「用户快照预览」服务：<b>手动触发</b>、<b>用户管理</b>的分段彩色 Polyline，
    /// 绘制到 <see cref="HyRoadLayers.LivePreviewLayer"/>（<c>05_hy_道路_预览</c> 图层）。
    ///
    /// <para>与前两层预览的分工（三层预览 + 一层存档架构）：</para>
    /// <list type="number">
    ///   <item>
    ///     <b>瞬态黄线</b>（<see cref="RoadAlignmentPreviewService"/>）— 拖 PI 参数过程中每帧刷新，
    ///     绘在 TransientManager，松手即清，不进实体。
    ///   </item>
    ///   <item>
    ///     <b>设计态彩色</b>（<see cref="RoadAlignmentDesignPreviewService"/>，KIND=<see cref="HyRoadXdata.KindAlignmentDesignPreview"/>）—
    ///     PI 调整 debounce 落地时自动更新，画在 <c>05_hy_道路_原线</c> 上。切面板 / 应用 / 切 Alignment 会擦掉。
    ///   </item>
    ///   <item>
    ///     <b>本服务（用户快照）</b>（KIND=<see cref="HyRoadXdata.KindAlignmentLivePreview"/>）—
    ///     用户点「预览」按钮时追加一批分段彩色 Polyline 到 <c>05_hy_道路_预览</c>。
    ///     <b>不自动清理</b> — 面板关闭、切 Alignment、应用定稿都不擦此层；用户需自行管理（ERASE / LAYOFF 等）。
    ///     目的是允许用户把「几个候选方案」并排画出来比对。
    ///   </item>
    /// </list>
    /// <para>
    /// 存档层：<c>05_hy_道路_平面线位</c>（KIND=<see cref="HyRoadXdata.KindAlignment"/>）— 唯一进 JSON 的实体，
    /// 只由「应用」按钮产出。
    /// </para>
    /// </summary>
    public static class RoadAlignmentLivePreviewService
    {
        /// <summary>
        /// 把 <paramref name="aln"/> 当前设计态按段彩色追加到 <c>05_hy_道路_预览</c>。
        /// 与 <see cref="RoadAlignmentDesignPreviewService.Refresh"/> 不同 — 本方法<b>不擦除</b>
        /// 同 KIND+ID 的旧快照，每次点击都叠加一批，让用户保留历史方案进行比对。
        /// </summary>
        /// <returns>新建的 Polyline 条数；无法绘制（中心线无效 / 文档关闭）返回 0。</returns>
        public static int DrawForAlignment(Document doc, Alignment aln)
        {
            return AlignmentSegmentRenderer.Draw(
                doc,
                aln,
                HyRoadLayers.LivePreviewLayer,
                HyRoadXdata.KindAlignmentLivePreview,
                eraseExistingSameId: false);
        }

        /// <summary>
        /// 手动清理 <c>05_hy_道路_预览</c> 图层上所有 KIND=<see cref="HyRoadXdata.KindAlignmentLivePreview"/> 的实体。
        /// <b>工作台 UI 生命周期不会自动调用</b> — 仅保留给用户的「清理预览层」命令 / 诊断工具使用。
        /// </summary>
        /// <returns>擦除条数；doc 为 null 或异常时返回 0。</returns>
        public static int EraseAll(Document doc)
        {
            return AlignmentSegmentRenderer.EraseByKind(doc, HyRoadXdata.KindAlignmentLivePreview);
        }

        /// <summary>仅擦除本 alignment 的用户快照（跨 alignment 多线位并存时的定点清理）。</summary>
        public static int EraseForAlignment(Document doc, System.Guid alignmentId)
        {
            return AlignmentSegmentRenderer.EraseByKindAndId(doc, HyRoadXdata.KindAlignmentLivePreview, alignmentId);
        }
    }
}
