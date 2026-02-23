using System;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.Refactored.Presentation.Commands;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// C1 入口：只执行下方配置的一条测试命令（带计时）。
    /// 切换测试对象时，仅修改 Run() 中“只改下面这一行”。
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
                new DesignSpecCommand().Execute();   // MText 样式设置页：C2→C1 打开编辑器，切 Tab "MText 样式" 验证
                // 例如：new OverKillCommand().Execute();
            });
        }
    }
}
