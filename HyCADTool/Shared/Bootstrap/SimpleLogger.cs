using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Diagnostics;

namespace HyCADTool.Shared.Bootstrap
{
    /// <summary>
    /// 简单日志：记录操作耗时，输出到命令行（沿用原 HyCADtool/00/SimpleLogger 的 API 与格式）。
    /// 原版写文件，本版写 Editor，格式一致：INFO: {operation} 耗时 {ms} 毫秒。
    /// </summary>
    public static class SimpleLogger
    {
        private static Editor Editor => Application.DocumentManager.MdiActiveDocument?.Editor;

        /// <summary>
        /// 记录操作耗时（与原 SimpleLogger.LogElapsedTime 一致：先计时再输出一行）
        /// </summary>
        public static void LogElapsedTime(string operation, Action action)
        {
            var stopwatch = Stopwatch.StartNew();
            action?.Invoke();
            stopwatch.Stop();
            Editor?.WriteMessage("\nINFO: " + operation + " 耗时 " + stopwatch.ElapsedMilliseconds + " 毫秒");
        }

        /// <summary>
        /// 记录操作耗时并返回结果（便于需要返回值的测试）
        /// </summary>
        public static T LogElapsedTime<T>(string operation, Func<T> func)
        {
            var stopwatch = Stopwatch.StartNew();
            T result = func != null ? func.Invoke() : default;
            stopwatch.Stop();
            Editor?.WriteMessage("\nINFO: " + operation + " 耗时 " + stopwatch.ElapsedMilliseconds + " 毫秒");
            return result;
        }

        /// <summary>
        /// 输出一行 INFO（与原 LogInfo 一致，目标改为命令行）
        /// </summary>
        public static void LogInfo(string message)
        {
            Editor?.WriteMessage("\nINFO: " + message);
        }
    }
}
