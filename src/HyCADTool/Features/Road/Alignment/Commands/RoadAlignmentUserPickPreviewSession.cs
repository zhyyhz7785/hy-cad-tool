using System;
using HyCADTool.Shared.AutoCAD.Session;

using HyCADTool.Features.Road.PlanAlignment.Services;
namespace HyCADTool.Features.Road.PlanAlignment.Commands
{
    /// <summary>
    /// 路线工作台「绘出预览」经 <see cref="Commands.CommandDispatcher.Send"/> 投递到 AutoCAD 命令线程时，
    /// 携带当前选中的 AlignmentId + 颜色模式（非模态 WPF 不可直接 LockDocument）。
    /// </summary>
    internal static class RoadAlignmentUserPickPreviewSession
    {
        internal static void RequestWorkbenchDraw(
            Guid alignmentId,
            HyCADTool.Features.Road.PlanAlignment.Services.RoadAlignmentUserPickPreviewService.ColorMode colorMode
                = HyCADTool.Features.Road.PlanAlignment.Services.RoadAlignmentUserPickPreviewService.ColorMode.BySegmentKind)
        {
            var slot = ActiveDocumentSession.GetOrNull<RoadAlignmentPreviewPending>();
            if (slot == null)
                return;
            slot.AlignmentId = alignmentId;
            slot.ColorMode = colorMode;
        }

        /// <summary>读取并清空，避免重复消费。</summary>
        internal static (Guid? alignmentId, HyCADTool.Features.Road.PlanAlignment.Services.RoadAlignmentUserPickPreviewService.ColorMode colorMode) ConsumePendingWorkbench()
        {
            var slot = ActiveDocumentSession.GetOrNull<RoadAlignmentPreviewPending>();
            var fallback = HyCADTool.Features.Road.PlanAlignment.Services.RoadAlignmentUserPickPreviewService.ColorMode.BySegmentKind;
            if (slot == null)
                return (null, fallback);
            var v = slot.AlignmentId;
            var m = slot.ColorMode;
            slot.AlignmentId = null;
            slot.ColorMode = fallback;
            return (v, m);
        }
    }

    /// <summary>单张图上的路线预览待办，由 <see cref="DocumentSessionStore"/> 按 Database 持有。</summary>
    public sealed class RoadAlignmentPreviewPending
    {
        public Guid? AlignmentId;
        public HyCADTool.Features.Road.PlanAlignment.Services.RoadAlignmentUserPickPreviewService.ColorMode ColorMode
            = HyCADTool.Features.Road.PlanAlignment.Services.RoadAlignmentUserPickPreviewService.ColorMode.BySegmentKind;
    }
}
