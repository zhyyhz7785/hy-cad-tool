using HyCADTool.Refactored.Infrastructure.Configuration;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// <c>hyRoadAw</c>（短别名 <c>rlaw</c>）：直接打开「路线工作台」WPF 非模态窗口，不先要求命令行拾取。
    ///
    /// 与相近命令的差异：
    /// <list type="bullet">
    ///   <item><c>hyRoadA</c>（<c>rla</c>）：命令行拾取 Polyline → 校核转换；违规才提示打开工作台修复。</item>
    ///   <item><c>hyRoadAlnEditPi</c>（<c>rWk</c>）：先命令行拾取已登记 Alignment → 打开工作台并预选。</item>
    ///   <item><c>hyRoadAw</c>（<c>rlaw</c>）：不拾取任何实体，直接把工作台亮出；左列自动列出当前 DWG 已登记的 Alignment，
    ///         用户可在窗口内点「拾取」按钮按需补登记或切换线位。</item>
    /// </list>
    ///
    /// 设计意图：把"打开面板"与"拾取实体"解耦，常见场景：
    /// (1) 用户刚跑过 <c>hyRoadA</c> / <c>hyRoadAlnByPi</c>，想再次进入面板只查看 / 导出；
    /// (2) 面板被手动关闭后需要一键重开；
    /// (3) 空 DWG 先打开面板，再在面板内 pick 新 Polyline。
    /// </summary>
    public sealed class RoadAlignmentWorkbenchOpenCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var panels = ServiceLocator.Resolve<PanelManager>();
            if (panels == null)
            {
                doc.Editor.WriteMessage("\n[道路] 面板服务未注册，无法打开路线工作台。");
                return;
            }

            // 不传 alignmentId / piIndex：面板自行 RefreshAlignments 并保持当前 / 默认选择。
            panels.ShowAlignmentWorkbench();
            doc.Editor.WriteMessage("\n[道路] 已打开路线工作台。");
        }
    }
}
