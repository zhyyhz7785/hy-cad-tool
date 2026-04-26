using System;
using System.Linq;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadAlnUserPickDrawWB</c> — 在 AutoCAD 命令线程上把
    /// <see cref="RoadAlignmentUserPickPreviewSession"/> 中登记的线位绘到「用户拾取」层。
    ///
    /// <para><b>⚠ 工作台 UI 不再使用</b>（2026-04-21 工作流简化）：
    /// 路线工作台的「预览」按钮已改为直接在 WPF 线程调用
    /// <see cref="RoadAlignmentLivePreviewService.DrawForAlignment(Autodesk.AutoCAD.ApplicationServices.Document, Domain.Models.Road.Alignment)"/>
    /// 向 <see cref="HyRoadLayers.LivePreviewLayer"/>（05_hy_道路_预览）追加分段彩色快照，无需跨线程 session 传参。
    /// 本命令保留仅为命令行兼容（脚本 / 旧面板）。</para>
    /// </summary>
    public sealed class RoadAlignmentWorkbenchUserPickDrawCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            var pending = RoadAlignmentUserPickPreviewSession.ConsumePendingWorkbench();
            Guid? id = pending.alignmentId;
            var colorMode = pending.colorMode;
            if (!id.HasValue || id.Value == Guid.Empty)
            {
                ed.WriteMessage("\n[道路] 未收到工作台绘出请求。请在工作台点击「绘出预览」。");
                return;
            }

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            if (!registry.TryGet(doc.Name, out var design))
            {
                ed.WriteMessage("\n[道路] 当前 DWG 无道路设计数据。");
                return;
            }

            var aln = design.Alignments.FirstOrDefault(a => a.Id == id.Value);
            if (aln == null)
            {
                ed.WriteMessage($"\n[道路] 找不到 AlignmentId={id.Value:N}，请先在工作台点「刷新」。");
                return;
            }

            try
            {
                int n = RoadAlignmentUserPickPreviewService.DrawPreviewPolylines(doc, aln, colorMode);
                string palette = colorMode == RoadAlignmentUserPickPreviewService.ColorMode.ByAlignmentId
                    ? "（按 Alignment Id 统一取色）"
                    : "（直=黄 / 缓入=青 / 缓出=橙 / 圆=绿）";
                ed.WriteMessage(
                    n > 0
                        ? $"\n[道路] 已在图层「{HyRoadLayers.UserPickPreviewLayer}」绘制 {n} 条 Polyline{palette}。"
                        : "\n[道路] 当前线位无可用中心线，未绘制。");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 绘出失败：{ex.Message}");
            }
        }
    }
}
