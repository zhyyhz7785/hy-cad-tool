using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// <c>hyRoadAlnUserPickDraw</c> — 拾取 HY_ROAD 平面线位后，将当前 Domain 几何按直/缓/圆分段绘制到图层 <see cref="HyRoadLayers.UserPickPreviewLayer"/>。
    /// </summary>
    public sealed class RoadAlignmentUserPickPreviewCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            if (!RoadAlignmentPiPipeline.PickAlignment(doc, out var alignment, out _, out _))
                return;

            int n = RoadAlignmentUserPickPreviewService.DrawPreviewPolylines(doc, alignment);
            ed.WriteMessage(
                $"\n[道路] 已在图层「{HyRoadLayers.UserPickPreviewLayer}」绘制 {n} 条 Polyline（直=黄 / 缓入=青 / 缓出=橙 / 圆=绿）。");
        }
    }
}
