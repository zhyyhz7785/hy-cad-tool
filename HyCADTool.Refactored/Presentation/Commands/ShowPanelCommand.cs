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

        // 以下是未来将要添加的命令占位符：

        /*
        [CommandMethod("HYREIN")]
        public static void ShowReinPanel()
        {
            var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
            panelManager.TogglePanel<ReinPanel>("钢筋配置", new Guid("A1B2C3D4-E5F6-..."));
        }

        [CommandMethod("HYFILTER")]
        public static void ShowFilterPanel()
        {
            var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
            panelManager.TogglePanel<FilterPanel>("图层过滤", new Guid("B2C3D4E5-F6G7-..."));
        }

        [CommandMethod("HYBASEREIN")]
        public static void ShowBaseReinPanel()
        {
            var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
            panelManager.TogglePanel<BaseReinPanel>("底板钢筋", new Guid("C3D4E5F6-G7H8-..."));
        }

        [CommandMethod("HYPILE")]
        public static void ShowPilePanel()
        {
            var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
            panelManager.TogglePanel<PilePanel>("桩配置", new Guid("D4E5F6G7-H8I9-..."));
        }

        [CommandMethod("HYCLUSTER")]
        public static void ShowClusterPanel()
        {
            var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
            panelManager.TogglePanel<ClusterPanel>("聚类分析", new Guid("E5F6G7H8-I9J0-..."));
        }
        */
    }
}


