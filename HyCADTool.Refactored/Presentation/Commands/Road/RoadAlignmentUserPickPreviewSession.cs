using System;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// 路线工作台「绘出预览」经 <see cref="Commands.CommandDispatcher.Send"/> 投递到 AutoCAD 命令线程时，
    /// 携带当前选中的 AlignmentId + 颜色模式（非模态 WPF 不可直接 LockDocument）。
    /// </summary>
    internal static class RoadAlignmentUserPickPreviewSession
    {
        private static Guid? _pendingWorkbenchAlignmentId;
        private static Infrastructure.AutoCAD.Services.Road.RoadAlignmentUserPickPreviewService.ColorMode _pendingColorMode
            = Infrastructure.AutoCAD.Services.Road.RoadAlignmentUserPickPreviewService.ColorMode.BySegmentKind;

        internal static void RequestWorkbenchDraw(
            Guid alignmentId,
            Infrastructure.AutoCAD.Services.Road.RoadAlignmentUserPickPreviewService.ColorMode colorMode
                = Infrastructure.AutoCAD.Services.Road.RoadAlignmentUserPickPreviewService.ColorMode.BySegmentKind)
        {
            _pendingWorkbenchAlignmentId = alignmentId;
            _pendingColorMode = colorMode;
        }

        /// <summary>读取并清空，避免重复消费。</summary>
        internal static (Guid? alignmentId, Infrastructure.AutoCAD.Services.Road.RoadAlignmentUserPickPreviewService.ColorMode colorMode) ConsumePendingWorkbench()
        {
            var v = _pendingWorkbenchAlignmentId;
            var m = _pendingColorMode;
            _pendingWorkbenchAlignmentId = null;
            _pendingColorMode = Infrastructure.AutoCAD.Services.Road.RoadAlignmentUserPickPreviewService.ColorMode.BySegmentKind;
            return (v, m);
        }
    }
}
