using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Diagnostics;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// 测试运行器：RunAllTests() 委托给 TestCommand.Run()。
    /// 要切换测试命令请改 TestCommand.cs 里的一行。计时请用 SimpleLogger.LogElapsedTime。
    /// </summary>
    public class TestRunner
    {
        private readonly Editor _editor;

        public TestRunner()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            _editor = doc?.Editor;
        }

        /// <summary>
        /// 运行测试（实际执行 TestCommand.Run，单命令由 TestCommand.cs 配置）
        /// </summary>
        public void RunAllTests()
        {
            TestCommand.Run();
        }

        /// <summary>
        /// 记录代码块执行时间（与 SimpleLogger 风格一致，供需要 Editor 实例的测试复用）
        /// </summary>
        public void LogElapsedTime(string operation, Action action)
        {
            var stopwatch = Stopwatch.StartNew();
            action?.Invoke();
            stopwatch.Stop();
            _editor?.WriteMessage("\nINFO: " + operation + " 耗时 " + stopwatch.ElapsedMilliseconds + " 毫秒");
        }

        /// <summary>
        /// 记录代码块执行时间并返回结果
        /// </summary>
        public T LogElapsedTime<T>(string operation, Func<T> func)
        {
            var stopwatch = Stopwatch.StartNew();
            T result = func != null ? func.Invoke() : default;
            stopwatch.Stop();
            _editor?.WriteMessage("\nINFO: " + operation + " 耗时 " + stopwatch.ElapsedMilliseconds + " 毫秒");
            return result;
        }
    }
}
