using Autodesk.AutoCAD.ApplicationServices;
using System;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// 测试入口：每次只改本文件里的一处即可切换要测的命令（沿用原 HyCADtool/00/TestCommand 习惯）。
    /// C1 命令会调用 Run()，带 SimpleLogger 耗时输出。
    /// </summary>
    public static class TestCommand
    {
        /// <summary>
        /// 运行当前配置的一个测试命令（C1 调用此方法）。要测别的命令时，只改下面 Lambda 里的一行即可。
        /// </summary>
        public static void Run()
        {
            SimpleLogger.LogElapsedTime("测试执行", () =>
            {
                try
                {
                    // ========== 只改下面一行即可切换测试命令 ==========
                    new Presentation.Commands.OverKillCommand().Execute();
                    // 示例：new Presentation.Commands.BreakCurvesCommand().Execute();
                    // 示例：new Presentation.Commands.DCELCommand().Execute();
                }
                catch (System.Exception ex)
                {
                    Application.DocumentManager.MdiActiveDocument?.Editor?.WriteMessage("\n错误：" + ex.Message);
                }
            });
        }
    }
}
