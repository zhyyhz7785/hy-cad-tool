using Autodesk.AutoCAD.Runtime;
using Autofac;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 面板显示命令基类
    /// 提供统一的面板显示逻辑
    /// </summary>
    public static class ShowPanelCommand
    {
        /// <summary>
        /// 显示测试面板（用于测试 PanelManager 功能）
        /// </summary>
        [CommandMethod("HYTEST_PANEL")]
        public static void ShowTestPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n========================================");
            ed.WriteMessage("\n开始测试 PanelManager...");
            ed.WriteMessage("\n========================================");
            
            try
            {
                // 检查容器是否初始化
                if (ServiceLocator.Container == null)
                {
                    ed.WriteMessage("\n❌ ServiceLocator.Container 为 null！");
                    ed.WriteMessage("\n提示：请确保插件已正确初始化（执行 C2 重新加载）");
                    return;
                }
                ed.WriteMessage("\n✅ ServiceLocator.Container 已初始化");

                // 解析 PanelManager
                ed.WriteMessage("\n正在解析 PanelManager...");
                var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
                
                if (panelManager == null)
                {
                    ed.WriteMessage("\n❌ PanelManager 解析失败（返回 null）");
                    return;
                }
                ed.WriteMessage("\n✅ PanelManager 已成功解析");

                // TODO: 在后续步骤中，当我们迁移实际面板时，会在这里添加显示逻辑
                // 示例:
                // panelManager.ShowPanel<ReinPanel>("钢筋配置", new Guid("..."));

                ed.WriteMessage("\n========================================");
                ed.WriteMessage("\n✅ 面板管理器测试成功！");
                ed.WriteMessage("\n提示：实际面板将在后续步骤中迁移。");
                ed.WriteMessage("\n========================================");
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\n========================================");
                ed.WriteMessage($"\n❌ 面板显示失败: {ex.Message}");
                ed.WriteMessage($"\n详细信息: {ex.StackTrace}");
                ed.WriteMessage("\n========================================");
            }
        }

        /// <summary>
        /// 显示/隐藏钢筋配置面板
        /// </summary>
        [CommandMethod("HYREIN")]
        public static void ShowReinPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
                panelManager.TogglePanel<HyCADTool.Refactored.Presentation.Views.ReinPanel>(
                    "钢筋配置", 
                    new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890"));
                ed.WriteMessage("\n钢筋配置面板已切换");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 显示面板失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示/隐藏过滤器面板
        /// </summary>
        [CommandMethod("HYFILTER")]
        public static void ShowFilterPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
                panelManager.TogglePanel<HyCADTool.Refactored.Presentation.Views.FilterPanel>(
                    "图形过滤器", 
                    new Guid("B2C3D4E5-F6A7-8901-BCDE-F23456789012"));
                ed.WriteMessage("\n图形过滤器面板已切换");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 显示面板失败: {ex.Message}");
            }
        }

        [CommandMethod("HYBASEREIN")]
        public static void ShowBaseReinPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
                panelManager.TogglePanel<HyCADTool.Refactored.Presentation.Views.BaseReinPanel>(
                    "底板钢筋配置", 
                    new Guid("C3D4E5F6-A7B8-9012-CDEF-34567890ABCD"));
                ed.WriteMessage("\n底板钢筋配置面板已切换");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 显示面板失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示/隐藏桩基布置面板
        /// </summary>
        [CommandMethod("HYPILE")]
        public static void ShowPilePanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
                panelManager.TogglePanel<HyCADTool.Refactored.Presentation.Views.PilePanel>(
                    "桩基布置面板", 
                    new Guid("D4E5F6A7-B8C9-0123-DEF4-567890ABCDEF"));
                ed.WriteMessage("\n桩基布置面板已切换");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 显示面板失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示/隐藏聚类分析面板
        /// </summary>
        [CommandMethod("HYCLUSTER")]
        public static void ShowClusterPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
                panelManager.TogglePanel<HyCADTool.Refactored.Presentation.Views.ClusterPanel>(
                    "聚类分析面板", 
                    new Guid("E5F6A7B8-C9D0-1234-EF56-7890ABCDEF12"));
                ed.WriteMessage("\n聚类分析面板已切换");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 显示面板失败: {ex.Message}");
            }
        }
    }
}


