using Autofac;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 面板显示方法（无 [CommandMethod]，避免热重载 eDuplicateKey）。
    /// 统一面板入口：ShowHyToolPanel()
    /// 旧方法保留向后兼容，内部转发到统一面板对应 Tab。
    /// </summary>
    public static class ShowPanelCommand
    {
        /// <summary>
        /// 命令 <c>Hy</c> 入口：改为打开 HyBlenderPanel 并跳到「设置」伪分类。
        /// 原 HyToolPanel 已退役（保留代码便于回退）。
        /// </summary>
        public static void ShowHyToolPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
                panelManager.OpenHyBlenderPanelAndSelectTab(
                    ViewModels.HyBlenderPanelViewModel.PreferencesTabKey);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n显示面板失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示/隐藏 HyCAD 命令检索面板（Blender 风格，命令 <c>HyB</c>）。
        /// 与 HyToolPanel 完全独立的 PaletteSet，可同时显示。
        /// </summary>
        public static void ShowHyBlenderPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
                panelManager.ToggleHyBlenderPanel();
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n显示 HyBlender 面板失败: {ex.Message}");
            }
        }

        /// <summary>
        /// [已弃用] 显示设置面板 → 打开统一面板样式 Tab
        /// </summary>
        [Obsolete("使用 ShowHyToolPanel() 替代")]
        public static void ShowSettingsPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
                #pragma warning disable CS0618
                panelManager.ShowSettingsPanel();
                #pragma warning restore CS0618
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n显示设置面板失败: {ex.Message}");
            }
        }

        /// <summary>
        /// [已弃用] 显示桩基面板 → 打开统一面板桩基 Tab
        /// </summary>
        [Obsolete("使用 ShowHyToolPanel() 替代")]
        public static void ShowPilePanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
                #pragma warning disable CS0618
                panelManager.ShowPilePanel();
                #pragma warning restore CS0618
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n显示桩基面板失败: {ex.Message}");
            }
        }

        /// <summary>
        /// [已弃用] 显示过滤器面板 → 打开统一面板过滤 Tab
        /// </summary>
        [Obsolete("使用 ShowHyToolPanel() 替代")]
        public static void ShowFilterPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
                #pragma warning disable CS0618
                panelManager.ShowFilterPanel();
                #pragma warning restore CS0618
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n显示面板失败: {ex.Message}");
            }
        }

        /// <summary>
        /// [已弃用] 显示基础配筋面板 → 打开统一面板底板 Tab
        /// </summary>
        [Obsolete("使用 ShowHyToolPanel() 替代")]
        public static void ShowBaseReinPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
                #pragma warning disable CS0618
                panelManager.ShowBaseReinPanel();
                #pragma warning restore CS0618
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n显示基础配筋面板失败: {ex.Message}");
            }
        }

        /// <summary>
        /// [已弃用] 显示聚类面板 → 打开统一面板聚类 Tab
        /// </summary>
        [Obsolete("使用 ShowHyToolPanel() 替代")]
        public static void ShowClusterPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
                #pragma warning disable CS0618
                panelManager.ShowClusterPanel();
                #pragma warning restore CS0618
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n显示聚类面板失败: {ex.Message}");
            }
        }
    }
}
