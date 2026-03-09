using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation;
using HyCADTool.Refactored.Presentation.Commands;
using System;
using System.Reflection;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// C1 入口：只执行下方配置的一条测试命令（带计时）。
    /// 默认打开 Refactored 统一面板，便于直接开始联调测试。
    /// </summary>
    public static class TestCommand
    {
        public static void Run()
        {
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            ed?.WriteMessage($"\n[C1] v{ver} @ {ts}");
            SimpleLogger.LogElapsedTime("命令执行", () =>
            {
                // 只改下面这一行即可切换 C1 测试命令
                ShowPanelCommand.ShowSettingsPanel();
                // 例如：new Presentation.Commands.DesignSpecCommand().Execute();
            });
        }
    }
}
