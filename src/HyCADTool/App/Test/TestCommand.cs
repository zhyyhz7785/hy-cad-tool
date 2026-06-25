using HyCADTool.App.Bootstrap;
using HyCADTool.Features.Tables.Commands;

namespace HyCADTool.App.Test
{
    /// <summary>
    /// C1 临时测试入口（带耗时输出）。
    ///
    /// 日常业务请直接在 AutoCAD 命令行输对应命令名 —— 这些命令都由
    /// <c>ReCall.CommandFacade</c> 注册，经 <c>commands.json</c> 映射后反射调用。
    /// 本方法只为"我想临时写一段代码跑一下"的调试场景保留。
    ///
    /// 改下方 <c>Execute()</c> 那一行即可切换测试目标，然后 C2 → C1。
    /// </summary>
    public static class TestCommand
    {
        public static void Run()
        {
            var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor;
            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var ts = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            ed?.WriteMessage($"\n[C1] v{ver} @ {ts}");

            SimpleLogger.LogElapsedTime("HyTable 019 Excel化表格编辑器", () =>
            {
                new OpenTableEditorCommand().Execute();
            });
        }
    }
}
