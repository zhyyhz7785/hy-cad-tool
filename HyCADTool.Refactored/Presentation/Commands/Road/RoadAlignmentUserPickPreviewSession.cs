using System;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// 路线工作台「绘出预览」经 <see cref="Commands.CommandDispatcher.Send"/> 投递到 AutoCAD 命令线程时，
    /// 携带当前选中的 AlignmentId（非模态 WPF 不可直接 LockDocument）。
    /// </summary>
    internal static class RoadAlignmentUserPickPreviewSession
    {
        private static Guid? _pendingWorkbenchAlignmentId;

        internal static void RequestWorkbenchDraw(Guid alignmentId)
        {
            _pendingWorkbenchAlignmentId = alignmentId;
        }

        /// <summary>读取并清空，避免重复消费。</summary>
        internal static Guid? ConsumePendingWorkbenchAlignmentId()
        {
            var v = _pendingWorkbenchAlignmentId;
            _pendingWorkbenchAlignmentId = null;
            return v;
        }
    }
}
