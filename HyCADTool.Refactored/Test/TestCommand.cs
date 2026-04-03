using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Utilities;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation;
using HyCADTool.Refactored.Presentation.Commands;
using HyCADTool.Refactored.Presentation.ViewModels;
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
            var asmPath = Assembly.GetExecutingAssembly().Location;
            ed?.WriteMessage($"\n[C1] v{ver} @ {ts}");

            #region agent log
            AgentDebugLogger.Log("heartbeat", "H0", "TestCommand.Run", "c1 entry heartbeat",
                new
                {
                    version = ver?.ToString(),
                    timestamp = ts,
                    assemblyPath = asmPath
                });
            #endregion

            SimpleLogger.LogElapsedTime("命令执行", () =>
            {
                var pendingCommand = SettingsPanelViewModel.ConsumePendingCommand();
                if (pendingCommand != null)
                {
                    #region agent log
                    AgentDebugLogger.Log("post-fix", "H7", "TestCommand.Run", "c1 consumed pending command",
                        new { });
                    #endregion

                    pendingCommand();
                    return;
                }

                if (CommandRelayStore.TryConsume(out var commandKey))
                {
                    #region agent log
                    AgentDebugLogger.Log("post-fix", "H14", "TestCommand.Run", "c1 consumed relay command",
                        new { commandKey });
                    #endregion

                    SettingsPanelViewModel.CommitFocusedTextBoxValue();
                    var vm = SettingsPanelViewModel.Current;
                    vm?.LoadSettings();
                    vm?.EnsureStylesApplied();

                    switch (commandKey)
                    {
                        case "gg":
                            new DrawOffsetPolylineCommand().Execute();
                            return;
                        case "g1":
                            new ReinAddAnchorCommand(isVertical: false).Execute();
                            return;
                        case "g2":
                            new ReinAddAnchorCommand(isVertical: true).Execute();
                            return;
                        case "ge":
                            new ReinExtendCommand().Execute();
                            return;
                    }
                }

                #region agent log
                AgentDebugLogger.Log("post-fix", "H7", "TestCommand.Run", "c1 fallback open settings panel",
                    new { pendingCommandWasNull = true });
                #endregion

                // 只改下面这一行即可切换 C1 测试命令
                ShowPanelCommand.ShowHyToolPanel();
                // 例如：ShowPanelCommand.ShowSettingsPanel();
            });
        }
    }
}
