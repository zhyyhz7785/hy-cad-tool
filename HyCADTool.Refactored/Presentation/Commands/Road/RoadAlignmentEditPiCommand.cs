using HyCADTool.Refactored.Infrastructure.Configuration;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// <c>hyRoadAlnEditPi</c>：路线 PI 编辑入口。
    ///
    /// 交互模型（本版起统一）：
    ///   1) 在图上拾取一条 HY_ROAD Alignment Polyline（<see cref="RoadAlignmentPiPipeline.PickAlignment"/>）；
    ///   2) 打开独立的「路线工作台」WPF 非模态窗口（<see cref="PanelManager.ShowAlignmentWorkbench"/>），
    ///      并把拾取到的 Alignment 预选上；
    ///   3) 后续所有 PI 参数/插删/反转/偏移/桩号/几何点 等都走面板完成。
    ///
    /// 原先的 [窗口(W)/命令行(C)] 分支、<c>PiThreeUnitWindow</c> 模态窗、命令行 Prompt 链路
    /// 全部下线；模态窗 xaml 与相关辅助方法被面板内复用的 <see cref="ViewModels.Road.PiThreeUnitViewModel"/>
    /// 取代（<c>Rebind</c> 非模态语义）。
    /// </summary>
    public sealed class RoadAlignmentEditPiCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            if (!RoadAlignmentPiPipeline.PickAlignment(doc, out var alignment, out var elements, out _))
                return;

            var panels = ServiceLocator.Resolve<PanelManager>();
            // 默认定位到第一个内部 PI（端点不可编辑）；若只有 2 点则让工作台自行处理。
            int? initialPi = elements != null && elements.Count >= 3 ? 1 : (int?)null;
            panels.ShowAlignmentWorkbench(alignment.Id, initialPi);

            doc.Editor.WriteMessage(
                $"\n[道路] 已打开路线工作台，线位：{(string.IsNullOrEmpty(alignment.Name) ? alignment.Id.ToString("N") : alignment.Name)}");
        }
    }
}
