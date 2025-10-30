using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using HyCADTool.Refactored.Presentation.Views;
using HyCADTool.Refactored.Presentation.ViewModels;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using System.Diagnostics;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;


[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.Commands.TestPanelCommand))]

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 测试面板命令 - 用于性能对比
    /// 模仿原有代码的简洁风格，直接创建面板，无复杂依赖注入
    /// </summary>
    public class TestPanelCommand
    {
        private static PaletteSet _testPalette; // 静态面板实例，确保单例

        /// <summary>
        /// 显示测试面板
        /// 命令: HYTEST
        /// </summary>
        [CommandMethod("HYTEST")]
        public static void ShowTestPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            var totalWatch = Stopwatch.StartNew();

            try
            {
                if (_testPalette == null)
                {
                    ed.WriteMessage("\n=== HYTEST 性能测试 ===");
                    
                    // Step 1: 创建 PaletteSet
                    var step1 = Stopwatch.StartNew();
                    _testPalette = new PaletteSet("HY 测试面板")
                    {
                        Style = PaletteSetStyles.ShowAutoHideButton 
                              | PaletteSetStyles.ShowCloseButton 
                              | PaletteSetStyles.Snappable
                    };
                    ed.WriteMessage($"\n[1-创建 PaletteSet] {step1.ElapsedMilliseconds} 毫秒");

                
                }

                // 显示面板
                var step4 = Stopwatch.StartNew();
                _testPalette.Visible = true;
                ed.WriteMessage($"\n[4-设置 Visible] {step4.ElapsedMilliseconds} 毫秒");

                totalWatch.Stop();
                ed.WriteMessage($"\n[总时间] {totalWatch.ElapsedMilliseconds} 毫秒");
                ed.WriteMessage("\n✅ 测试面板已显示");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
                ed.WriteMessage($"\n堆栈：{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 隐藏测试面板
        /// 命令: HYTESTHIDE
        /// </summary>
        [CommandMethod("HYTESTHIDE")]
        public static void HideTestPanel()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                if (_testPalette != null)
                {
                    _testPalette.Visible = false;
                    ed.WriteMessage("\n✅ 测试面板已隐藏");
                }
                else
                {
                    ed.WriteMessage("\n⚠️ 测试面板尚未创建");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
            }
        }
    }
}

