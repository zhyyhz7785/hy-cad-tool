using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.Features.Shell;
using HyCADTool.Shared.Bootstrap;
using System;
using System.Reflection;

namespace HyCADTool.Test
{
    /// <summary>
    /// C1 临时测试入口（带耗时输出）。
    ///
    /// 日常业务请直接在 AutoCAD 命令行输对应命令名 —— 这些命令都由
    /// <c>ReCall.CommandFacade</c> 注册，经 <c>commands.json</c> 映射后反射调用。
    /// 本方法只为"我想临时写一段代码跑一下"的调试场景保留。aa
    ///
    /// 改下方 <c>Execute()</c> 那一行即可切换测试目标，然后 C2 → C1。
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
                ShowPanelCommand.ShowHyBlenderPanel();
            });
        }
    }
}
