using Autodesk.AutoCAD.ApplicationServices;
using Autofac;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// C1 入口：
    ///   1. 有命令（新命令或重复上次） → 执行（带计时）
    ///   2. 无命令 → 打开设置面板
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
                // 无命令 → 打开/切换设置面板
                Presentation.Commands.ShowPanelCommand.ShowSettingsPanel();
            }
        }
    }
}
