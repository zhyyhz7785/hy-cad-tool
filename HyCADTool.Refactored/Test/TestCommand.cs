using Autodesk.AutoCAD.ApplicationServices;
using Autofac;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation.ViewModels;
using HyCADTool.Refactored.Presentation.Views;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// C1 入口：
    ///   1. 有命令（新命令或重复上次） → 执行（带计时）
    ///   2. 无命令 → 打开独立 FilterPanel 窗口（测试用）
    /// </summary>
    public static class TestCommand
    {
        public static void Run()
        {
            // 获取命令（新命令或重复上次）
            var command = SettingsPanelViewModel.ConsumePendingCommand();
            if (command != null)
            {
                // 执行命令（面板按钮或重复上次）
                SimpleLogger.LogElapsedTime("命令执行", () =>
                {
                    try
                    {
                        command();
                    }
                    catch (System.Exception ex)
                    {
                        Application.DocumentManager.MdiActiveDocument?.Editor
                            ?.WriteMessage($"\n错误：{ex.Message}");
                    }
                });
            }
            else
            {
                // 无命令 → 打开独立 FilterPanel 窗口（测试用）
                ShowFilterPanel();
            }
        }

        /// <summary>
        /// 显示独立的 FilterPanel 窗口（测试用）
        /// </summary>
        private static void ShowFilterPanel()
        {
            var panel = new FilterPanel();
            
            var window = new System.Windows.Window
            {
                Title = "过滤器面板（独立测试）",
                Content = panel,
                SizeToContent = System.Windows.SizeToContent.WidthAndHeight,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
            };
            
            AcApp.ShowModelessWindow(window);
        }
    }
}
