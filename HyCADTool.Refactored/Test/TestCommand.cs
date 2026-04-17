using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Utilities;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation;
using HyCADTool.Refactored.Presentation.Commands;
using HyCADTool.Refactored.Presentation.Commands.Road;
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

                    if (TryDispatchCommandKey(commandKey))
                        return;
                    ed?.WriteMessage($"\n[C1] 未识别的命令 key（来自 CommandRelayStore）: {commandKey}");
                    return;
                }

                // ================================================================
                //  C2 热重载"新命令"救急通道（P1.c 新增）
                //
                //  背景：AutoCAD 只在**首次 NETLOAD** 时扫描 [CommandMethod]，C2 的
                //  Assembly.Load(byte[]) 无法让命令表感知新命令。如果本轮热重载新增了
                //  hyRoadAlnStation 这种新命令，在命令行输入命令名会提示"未知命令"。
                //
                //  解决：在走 fallback 之前给用户一次交互式输入命令 key 的机会 —— 由于
                //  C2 会把 C1 重新绑定到**最新** TestCommand.Run，TryDispatchCommandKey
                //  这段 switch 就是最新的，任何新命令都能在这里分派，无需重启 AutoCAD。
                //
                //  使用：C2 → C1 → 命令行提示"输入命令 key" → 输入 hyRoadAlnStation 回车。
                //  回车跳过则走最下面的内置 fallback（调试测试用）。
                // ================================================================
                if (ed != null)
                {
                    var pso = new PromptStringOptions(
                        "\n[C1] 输入要执行的命令 key（回车跳过走内置 fallback）: ")
                    {
                        AllowSpaces = false
                    };
                    var pr = ed.GetString(pso);
                    if (pr.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(pr.StringResult))
                    {
                        var key = pr.StringResult.Trim();
                        SettingsPanelViewModel.CommitFocusedTextBoxValue();
                        var vm2 = SettingsPanelViewModel.Current;
                        vm2?.LoadSettings();
                        vm2?.EnsureStylesApplied();

                        if (TryDispatchCommandKey(key))
                            return;
                        ed.WriteMessage($"\n[C1] 未识别的命令 key: {key}");
                        return;
                    }
                }

                #region agent log
                AgentDebugLogger.Log("post-fix", "H7", "TestCommand.Run", "c1 fallback open settings panel",
                    new { pendingCommandWasNull = true });
                #endregion

                // 只改下面这一行即可切换 C1 测试命令
                new DimensionAlignCommand().Execute();
                // 例如：new SettlementCalculationCommand().Execute();
            });
        }

        /// <summary>
        /// 统一的命令 key → 实现分派表。两个调用点共享：
        /// - <see cref="CommandRelayStore"/> 的 key 消费（老流程）
        /// - C2 热重载"新命令"救急通道（用户在 C1 交互式输入的 key）
        ///
        /// 未识别 key 返回 <c>false</c>，由调用方决定 fallback。
        /// </summary>
        private static bool TryDispatchCommandKey(string key)
        {
            switch (key)
            {
                case "gg":
                    new DrawOffsetPolylineCommand().Execute();
                    return true;
                case "g1":
                    new ReinAddAnchorCommand(isVertical: false).Execute();
                    return true;
                case "g2":
                    new ReinAddAnchorCommand(isVertical: true).Execute();
                    return true;
                case "ge":
                    new ReinExtendCommand().Execute();
                    return true;
                case "hyjc":
                    new SettlementCalculationCommand().Execute();
                    return true;

                // --- P0/P1 市政道路设计命令（C2 后只能走 C1 转发，见 CommandRegistry 注释）---
                case "hyRoadA":
                    new RoadAlignmentCommand().Execute();
                    return true;
                case "hyRoadAlnStation":
                    new RoadAlignmentStationCommand().Execute();
                    return true;
                case "hyRoadP":
                    new RoadProfileCommand().Execute();
                    return true;
                case "hyRoadT":
                    new RoadTemplateCommand().Execute();
                    return true;
                case "hyRoadC":
                    new RoadCorridorCommand().Execute();
                    return true;
                case "hyRoadSave":
                    new RoadOpenJsonCommand().Execute();
                    return true;
                case "hyRoadLoad":
                    new RoadImportJsonCommand().Execute();
                    return true;
                case "hyRoad3dExportGltf":
                    new Road3dExportGltfCommand().Execute();
                    return true;
                default:
                    return false;
            }
        }
    }
}
