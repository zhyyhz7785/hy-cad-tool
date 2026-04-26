using System;

using HyCADTool.Features.Road.PlanAlignment.Services;
namespace HyCADTool.Features.Road.PlanAlignment.Commands
{
    /// <summary>
    /// 路线工作台「绘出预览」经 <see cref="Commands.CommandDispatcher.Send"/> 投递到 AutoCAD 命令线程时，
    /// 携带当前选中的 AlignmentId + 颜色模式（非模态 WPF 不可直接 LockDocument）。
    /// </summary>
    internal static class RoadAlignmentUserPickPreviewSession
    {
        private static Guid? _pendingWorkbenchAlignmentId;
        private static HyCADTool.Features.Road.PlanAlignment.Services.RoadAlignmentUserPickPreviewService.ColorMode _pendingColorMode
            = HyCADTool.Features.Road.PlanAlignment.Services.RoadAlignmentUserPickPreviewService.ColorMode.BySegmentKind;

        internal static void RequestWorkbenchDraw(
            Guid alignmentId,
            HyCADTool.Features.Road.PlanAlignment.Services.RoadAlignmentUserPickPreviewService.ColorMode colorMode
                = HyCADTool.Features.Road.PlanAlignment.Services.RoadAlignmentUserPickPreviewService.ColorMode.BySegmentKind)
        {
            _pendingWorkbenchAlignmentId = alignmentId;
            _pendingColorMode = colorMode;
        }

        /// <summary>读取并清空，避免重复消费。</summary>
        internal static (Guid? alignmentId, HyCADTool.Features.Road.PlanAlignment.Services.RoadAlignmentUserPickPreviewService.ColorMode colorMode) ConsumePendingWorkbench()
        {
            var v = _pendingWorkbenchAlignmentId;
            var m = _pendingColorMode;
            _pendingWorkbenchAlignmentId = null;
            _pendingColorMode = HyCADTool.Features.Road.PlanAlignment.Services.RoadAlignmentUserPickPreviewService.ColorMode.BySegmentKind;
            return (v, m);
        }
    }
}
