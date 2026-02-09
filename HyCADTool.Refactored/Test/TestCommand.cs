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
    /// 面板用 TestShowSettingsPanel 打开（首次慢，后续秒开）。
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
                    TestMleaderRein();               // gb: 多引线标注+点钢筋
                    //TestMleaderReinOne();          // gb1: 单引线标注
                   // TestMleaderReinSix();          // gb2: 六点引线标注+点钢筋
                    // TestDrawReinforcement();       // gj: 绘制钢筋（完整配筋流程）
                    // TestReinExtend();              // ge: 延伸钢筋+15d弯折
                    // TestReinQuickExtend();         // ge1: 快速延伸至边界
                    // TestReinCut();                 // gd: 截断钢筋
                    // TestReinAddAnchor1();          // g1: 单侧弯钩
                    // TestReinAddAnchor2();          // g2: 竖向弯钩
                    // TestDrawOffsetPolyline();      // gg: 偏移多段线+弯钩
                    // TestShowSettingsPanel();       // 调出设置面板
                }
                catch (System.Exception ex)
                {
                    Application.DocumentManager.MdiActiveDocument?.Editor?.WriteMessage("\n错误：" + ex.Message);
                }
            });
        }

        /// <summary>
        /// 测试绘制钢筋（使用默认参数，不开面板）
        /// </summary>
        private static void TestDrawReinforcement()
        {
            // 先确保样式存在
            TestApplyStyle();
            // 执行绘制命令（会让用户选择多段线）
            new Presentation.Commands.DrawReinforcementCommand().Execute();
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
        }

        /// <summary>
        /// 打开设置面板（首次慢，后续秒开）
        /// </summary>
        private static void TestShowSettingsPanel()
        {
            var sw = Stopwatch.StartNew();
            Presentation.Commands.ShowPanelCommand.ShowSettingsPanel();
            sw.Stop();
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage($"\n面板耗时 {sw.ElapsedMilliseconds}ms");
        }

        /// <summary>g1: 单侧弯钩（isVertical=false）</summary>
        private static void TestReinAddAnchor1()
        {
            new Presentation.Commands.ReinAddAnchorCommand(isVertical: false).Execute();
        }

        /// <summary>g2: 竖向弯钩（isVertical=true）</summary>
        private static void TestReinAddAnchor2()
        {
            new Presentation.Commands.ReinAddAnchorCommand(isVertical: true).Execute();
        }

        /// <summary>gg: 偏移多段线绘制+弯钩</summary>
        private static void TestDrawOffsetPolyline()
        {
            new Presentation.Commands.DrawOffsetPolylineCommand().Execute();
        }

        /// <summary>gd: 截断钢筋</summary>
        private static void TestReinCut()
        {
            new Presentation.Commands.ReinCutCommand().Execute();
        }

        /// <summary>gb: 多引线标注+点钢筋（命令内部自动同步面板样式）</summary>
        private static void TestMleaderRein()
        {
            EnsureDefaultViewModel();
            new Presentation.Commands.MleaderReinCommand(
                Presentation.Commands.MleaderReinCommand.Mode.Standard).Execute();
        }

        /// <summary>gb1: 单引线标注（命令内部自动同步面板样式）</summary>
        private static void TestMleaderReinOne()
        {
            EnsureDefaultViewModel();
            new Presentation.Commands.MleaderReinCommand(
                Presentation.Commands.MleaderReinCommand.Mode.Single).Execute();
        }

        /// <summary>gb2: 六点引线标注+点钢筋（命令内部自动同步面板样式）</summary>
        private static void TestMleaderReinSix()
        {
            EnsureDefaultViewModel();
            new Presentation.Commands.MleaderReinCommand(
                Presentation.Commands.MleaderReinCommand.Mode.Six).Execute();
        }

        /// <summary>
        /// 确保 SettingsPanelViewModel.Current 存在（面板未打开时用默认值初始化）
        /// 面板已打开则不做任何事，命令会读取面板最新参数
        /// </summary>
        private static void EnsureDefaultViewModel()
        {
            if (Presentation.ViewModels.SettingsPanelViewModel.Current == null)
            {
                var styleService = ServiceLocator.Container.Resolve<IStyleService>();
                new Presentation.ViewModels.SettingsPanelViewModel(styleService);
                // 构造函数自动设置 Current
            }
        }

        /// <summary>ge: 延伸钢筋（锚固长度）</summary>
        private static void TestReinExtend()
        {
            new Presentation.Commands.ReinExtendCommand().Execute();
        }

        /// <summary>ge1: 快速延伸至边界</summary>
        private static void TestReinQuickExtend()
        {
            new Presentation.Commands.ReinQuickExtendCommand().Execute();
        }
    }
}
