using Autodesk.AutoCAD.ApplicationServices;
using Autofac;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using System.Diagnostics;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// 测试入口：C1 调用 Run()。
    /// </summary>
    public static class TestCommand
    {
        public static void Run()
        {
            SimpleLogger.LogElapsedTime("测试执行", () =>
            {
                try
                {
                    // ========== 只改下面一行即可切换测试命令 ==========
                    // 面板测试（含详细耗时分解）
                    TestShowSettingsPanel();
                    // 纯样式测试（无 WPF）
                    // TestApplyStyle();
                }
                catch (System.Exception ex)
                {
                    Application.DocumentManager.MdiActiveDocument?.Editor?.WriteMessage("\n错误：" + ex.Message);
                }
            });
        }

        /// <summary>
        /// 带详细耗时分解的面板测试，定位 WPF 慢在哪一步
        /// </summary>
        private static void TestShowSettingsPanel()
        {
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            var swTotal = Stopwatch.StartNew();

            // 1. 解析 PanelManager
            var sw1 = Stopwatch.StartNew();
            var panelManager = ServiceLocator.Container.Resolve<Presentation.PanelManager>();
            sw1.Stop();

            // 2. 解析 SettingsPanel（DI 创建 ViewModel + View）
            var sw2 = Stopwatch.StartNew();
            var panel = ServiceLocator.Container.Resolve<Presentation.Views.SettingsPanel>();
            sw2.Stop();

            // 3. 创建 PaletteSet + ElementHost
            var sw3 = Stopwatch.StartNew();
            panelManager.TogglePanel<Presentation.Views.SettingsPanel>(
                "HY 设置",
                new Guid("F6A7B8C9-D0E1-2345-FA67-890ABCDEF123"));
            sw3.Stop();

            swTotal.Stop();
            ed?.WriteMessage($"\n面板耗时 {swTotal.ElapsedMilliseconds}ms " +
                $"(PanelMgr={sw1.ElapsedMilliseconds} + 创建面板={sw2.ElapsedMilliseconds} + 显示={sw3.ElapsedMilliseconds})");
        }

        /// <summary>
        /// 快速测试样式应用（不开 WPF 面板）
        /// </summary>
        private static void TestApplyStyle()
        {
            var styleService = ServiceLocator.Container.Resolve<IStyleService>();
            double scale = 40;
            string textStyleName = $"0_Hy_{scale}";
            string dimStyleName = $"0_Hy_{scale}_Dim";
            string mleaderStyleName = $"0_Hy_{scale}_Mleader";

            styleService.CreateTextStyle(textStyleName, "tssdeng.shx", "hztxt.shx", 2.5 * scale, 0.7);
            styleService.SetCurrentTextStyle(textStyleName);

            styleService.CreateDimensionStyle(dimStyleName, textStyleName, scale);
            styleService.SetCurrentDimensionStyle(dimStyleName);

            styleService.CreateMLeaderStyle(mleaderStyleName, textStyleName, scale);
            styleService.SetCurrentMLeaderStyle(mleaderStyleName);

            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage($"\n样式测试完成: {textStyleName}");
        }
    }
}
