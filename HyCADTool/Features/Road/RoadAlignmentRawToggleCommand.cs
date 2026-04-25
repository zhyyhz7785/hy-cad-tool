using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.Shared.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadAlnRawShow</c> — 在图层「05_hy_道路_原线」（<see cref="HyRoadLayers.RawPolylineLayer"/>）
    /// 以 <see cref="HyRoadXdata.KindAlignmentRawPick"/> KIND 批量绘制所有线位的原线快照（ACI 252 ByLayer 本色）。
    ///
    /// <para><b>⚠ 工作台 UI 不再使用</b>（2026-04-21 工作流简化）：
    /// 拾取后会自动调用 <see cref="RoadAlignmentRawPolylineService.DrawForAlignment"/> 写入一次永久快照，
    /// 无需用户再开 / 关。本命令保留仅为命令行兼容、批量补绘场景（例如 JSON 迁移后一次性回写 DWG）。</para>
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
    /// <c>hyRoadAlnRawHide</c> — 擦除图层「05_hy_道路_原线」上所有 <see cref="HyRoadXdata.KindAlignmentRawPick"/>
    /// 原线实体（<i>不</i>擦除 <see cref="HyRoadXdata.KindAlignmentDesignPreview"/> 分段彩色预览）。
    ///
    /// <para><b>⚠ 工作台 UI 不再使用</b>（2026-04-21 工作流简化）：
    /// 拾取写入的 RawPick 记录与 Alignment 生命周期绑定，不再由开关切换；
    /// 本命令保留仅用于批量清理 / 旧 DWG 迁移场景。</para>
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
