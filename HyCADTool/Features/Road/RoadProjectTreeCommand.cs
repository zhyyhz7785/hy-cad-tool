using HyCADTool.Presentation;
using HyCADTool.Shared.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadTree</c>（短别名 <c>rTree</c>）：打开"项目树"独立 PaletteSet（045 / M6）。
    ///
    /// 设计：
    /// <list type="bullet">
    ///   <item>独立 PaletteSet（不挤进 <see cref="Views.HyBlenderPanel"/> 道路 Tab，树需要纵向空间）；</item>
    ///   <item>每次打开会从 <c>RoadProjectRegistry</c> 取当前 DWG 的 <c>RoadProject</c>（v1.x 文件自动包装），
    ///         向 VM 喂数据触发重建；</item>
    ///   <item>双击 / 右键菜单 / 选中高亮 见 <see cref="ViewModels.Road.RoadProjectTreeViewModel"/>。</item>
    /// </list>
    /// </summary>
    public sealed class RoadProjectTreeCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var panels = ServiceLocator.Resolve<PanelManager>();
            if (panels == null)
            {
                doc.Editor.WriteMessage("\n[道路] 面板服务未注册，无法打开项目树。");
                return;
            }

            panels.ShowRoadProjectTree();
            doc.Editor.WriteMessage("\n[道路] 已打开项目树。");
        }
    }
}
