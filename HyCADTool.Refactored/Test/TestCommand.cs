using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.Refactored.Presentation.Commands;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// C1 入口：
    ///   1. 有命令（新命令或重复上次） → 执行（带计时）
    ///   2. 无命令 → 打开 HY 统一工具面板
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
                // ★ 测试入口：Markdown 设计说明（WebView2 编辑器）
                new DesignSpecCommand().Execute();
            }
        }
    }
}
