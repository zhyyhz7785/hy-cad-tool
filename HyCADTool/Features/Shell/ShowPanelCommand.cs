using Autofac;
using HyCADTool.Presentation;
using HyCADTool.Presentation.ViewModels;
using HyCADTool.Shared.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Shell
{
    /// <summary>
    /// 面板显示方法（无 [CommandMethod]，避免热重载 eDuplicateKey）。
    ///
    /// 唯一 PaletteSet：<see cref="Views.HyBlenderPanel"/>。所有入口都转发到它的对应 Tab：
    /// - Hy / ShowSettingsPanel → 「设置」Tab
    /// - HyB                    → toggle 显示
    /// - ShowFilterPanel        → 「过滤」Tab
    /// - ShowPilePanel 等       → commands.json 里的 Category Tab
    /// </summary>
    public static class ShowPanelCommand
    {
        /// <summary>Hy 命令入口：打开 HyBlenderPanel 并跳到「设置」Tab。</summary>
        public static void ShowHyToolPanel()
            => OpenTab(HyBlenderPanelViewModel.PreferencesTabKey, "显示面板失败");

        /// <summary>HyB 命令入口：显示/隐藏 HyBlenderPanel。</summary>
        public static void ShowHyBlenderPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                ServiceLocator.Container.Resolve<PanelManager>().ToggleHyBlenderPanel();
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n显示 HyBlender 面板失败: {ex.Message}");
            }
        }

        /// <summary>显示设置 Tab。</summary>
        public static void ShowSettingsPanel()
            => OpenTab(HyBlenderPanelViewModel.PreferencesTabKey, "显示设置面板失败");

        /// <summary>显示过滤 Tab。</summary>
        public static void ShowFilterPanel()
            => OpenTab(HyBlenderPanelViewModel.FilterTabKey, "显示过滤面板失败");

        /// <summary>显示桩基分类 Tab。</summary>
        public static void ShowPilePanel()      => OpenTab("桩基", "显示桩基面板失败");

        /// <summary>显示底板配筋分类 Tab。</summary>
        public static void ShowBaseReinPanel()  => OpenTab("底板配筋", "显示基础配筋面板失败");

        /// <summary>显示块引线分类 Tab（原聚类面板入口）。</summary>
        public static void ShowClusterPanel()   => OpenTab("块引线", "显示聚类面板失败");

        private static void OpenTab(string tabKey, string errorPrefix)
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                ServiceLocator.Container.Resolve<PanelManager>().OpenHyBlenderPanelAndSelectTab(tabKey);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n{errorPrefix}: {ex.Message}");
            }
        }
    }
}
