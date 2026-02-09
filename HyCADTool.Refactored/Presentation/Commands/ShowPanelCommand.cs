using Autofac;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 面板显示方法（无 [CommandMethod]，避免热重载 eDuplicateKey）。
    /// 命令注册在 ReCall 项目中通过反射路由。
    /// </summary>
    public static class ShowPanelCommand
    {
        /// <summary>
        /// 显示/隐藏 HY 设置面板
        /// </summary>
        public static void ShowSettingsPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
                panelManager.TogglePanel<HyCADTool.Refactored.Presentation.Views.SettingsPanel>(
                    "HY 设置",
                    new Guid("F6A7B8C9-D0E1-2345-FA67-890ABCDEF123"));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n显示设置面板失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示/隐藏过滤器面板
        /// </summary>
        public static void ShowFilterPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
                panelManager.TogglePanel<HyCADTool.Refactored.Presentation.Views.FilterPanel>(
                    "图形过滤器",
                    new Guid("B2C3D4E5-F6A7-8901-BCDE-F23456789012"));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n显示面板失败: {ex.Message}");
            }
        }
    }
}
