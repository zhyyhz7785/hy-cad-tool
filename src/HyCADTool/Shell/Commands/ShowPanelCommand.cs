using Autofac;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Shell;
using HyCADTool.Shell.ViewModels;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Shell.Commands
{
    /// <summary>
    /// 面板显示方法（无 [CommandMethod]，避免热重载 eDuplicateKey）。
    ///
    /// 唯一 PaletteSet：<see cref="HyCADTool.Shell.Views.HyBlenderPanel"/>。所有入口都转发到它的对应 Tab：
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
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                ServiceLocator.Container.Resolve<PanelManager>().ToggleHyBlenderPanel();
            }
            catch (System.Exception ex)
            {
                doc.Editor?.WriteMessage($"\n显示 HyBlender 面板失败: {ex.Message}");
            }
        }

        /// <summary>显示设置 Tab。</summary>
        public static void ShowSettingsPanel()
            => OpenTab(HyBlenderPanelViewModel.PreferencesTabKey, "显示设置面板失败");

        /// <summary>显示过滤 Tab。</summary>
        public static void ShowFilterPanel()
            => OpenTab(HyBlenderPanelViewModel.FilterTabKey, "显示过滤面板失败");

        /// <summary>HYSpongeCity 入口：显示海绵城市伪分类 Tab（参数+CAD+输出 一体面板）。</summary>
        public static void ShowSpongeCityPanel()
            => OpenTab(HyBlenderPanelViewModel.SpongeCityTabKey, "显示海绵城市面板失败");

        /// <summary>显示桩基分类 Tab。</summary>
        public static void ShowPilePanel()      => OpenTab("桩基", "显示桩基面板失败");

        /// <summary>显示底板配筋分类 Tab。</summary>
        public static void ShowBaseReinPanel()  => OpenTab(HyBlenderPanelViewModel.BaseReinTabKey, "显示基础配筋面板失败");

        /// <summary>显示螺栓聚类与基础标注 Tab。</summary>
        public static void ShowClusterPanel()   => OpenTab(HyBlenderPanelViewModel.ClusterTabKey, "显示聚类面板失败");

        /// <summary>hyobP 入口：拉起 hyob 历史 PaletteSet（独立面板，不在 HyBlenderPanel 内）。</summary>
        public static void ShowHyobHistoryPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            try { ServiceLocator.Container.Resolve<PanelManager>().ShowHyobHistoryPanel(); }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n显示 hyob 历史面板失败: {ex.Message}");
            }
        }

        /// <summary>HYFEA 梁元 MVP（N7 / 独立 PaletteSet）。</summary>
        public static void ShowHyfeaBeamMvpPanel(ObjectId? sourceEntityId = null)
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            try
            {
                ServiceLocator.Container.Resolve<PanelManager>().ShowHyfeaBeamMvpPalette(sourceEntityId);
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n显示 HYFEA 面板失败: {ex.Message}");
            }
        }

        private static void OpenTab(string tabKey, string errorPrefix)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                ServiceLocator.Container.Resolve<PanelManager>().OpenHyBlenderPanelAndSelectTab(tabKey);
            }
            catch (System.Exception ex)
            {
                doc.Editor?.WriteMessage($"\n{errorPrefix}: {ex.Message}");
            }
        }
    }
}
