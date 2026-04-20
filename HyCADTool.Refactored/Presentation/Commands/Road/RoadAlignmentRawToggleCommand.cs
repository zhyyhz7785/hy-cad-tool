using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;
using HyCADTool.Refactored.Infrastructure.Configuration;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// <c>hyRoadAlnRawShow</c> — 路线工作台「原线」开关打开：在图层「05_hy_道路_原线」绘制所有线位的原线快照。
    /// </summary>
    public sealed class RoadAlignmentRawShowCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            if (!registry.TryGet(doc.Name, out var design))
            {
                ed.WriteMessage("\n[道路] 当前 DWG 无道路设计数据。");
                return;
            }

            try
            {
                var (drawn, upgraded) = RoadAlignmentRawPolylineService.DrawAll(doc, design);
                if (upgraded > 0)
                {
                    var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
                    exporter.SaveForDocument(design, doc.Name);
                }
                ed.WriteMessage(
                    drawn > 0
                        ? $"\n[道路] 已在图层「{HyRoadLayers.RawPolylineLayer}」绘制 {drawn} 条原线 Polyline（ByLayer 颜色 ACI {HyRoadLayers.RawPolylineColor}）。"
                        : "\n[道路] 无可绘制的原线（无有效中心线）。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[道路] 原线显示失败：{ex.Message}");
            }
        }
    }

    /// <summary>
    /// <c>hyRoadAlnRawHide</c> — 路线工作台「原线」开关关闭：擦除原线图层上的 HY_ROAD 原线实体。
    /// </summary>
    public sealed class RoadAlignmentRawHideCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            try
            {
                int n = RoadAlignmentRawPolylineService.EraseAll(doc);
                ed.WriteMessage(n > 0
                    ? $"\n[道路] 已擦除 {n} 条原线 Polyline。"
                    : "\n[道路] 无可擦除的原线。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[道路] 原线隐藏失败：{ex.Message}");
            }
        }
    }
}
