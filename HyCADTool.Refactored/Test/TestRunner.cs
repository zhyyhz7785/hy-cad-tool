using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Diagnostics;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// 通用测试运行器 - 简化版
    /// 只测试 HYOV 命令
    /// 
    /// 使用说明：
    /// - RunAllTests(): 执行 HYOV 测试（自动计时）
    /// - LogElapsedTime(): 辅助方法，完全按照原项目 SimpleLogger.LogElapsedTime 的简洁风格
    /// </summary>
    public class TestRunner
    {
        private readonly Editor _editor;

        public TestRunner()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            _editor = doc?.Editor;
        }

        /// <summary>
        /// 运行测试 - 直接执行 HYOV 命令
        /// </summary>
        public void RunAllTests()
        {
            if (_editor == null)
            {
                return;
            }

            LogElapsedTime("HYOV 命令执行", () =>
            {
                try
                {
                    // 直接执行 HYOV 命令
                    var hyovCommand = new HyCADTool.Refactored.Presentation.Commands.OverKillCommand();
                    hyovCommand.Execute();
                }
                catch (System.Exception ex)
                {
                    _editor.WriteMessage($"\n错误：{ex.Message}");
                    _editor.WriteMessage($"\n{ex.StackTrace}");
                }
            });
        }


        #region 辅助方法

        /// <summary>
        /// 记录代码块执行时间（完全按照原项目 SimpleLogger.LogElapsedTime 的简洁风格）
        /// </summary>
        /// <param name="operation">操作名称</param>
        /// <param name="action">要执行的操作</param>
        private void LogElapsedTime(string operation, Action action)
        {
            // 开始计时
            var stopwatch = Stopwatch.StartNew();
            // 执行操作
            action?.Invoke();
            // 停止计时
            stopwatch.Stop();
            // 输出耗时信息到命令行（简洁格式）
            _editor?.WriteMessage($"\nINFO: {operation} 耗时 {stopwatch.ElapsedMilliseconds} 毫秒");
        }

        /// <summary>
        /// 记录代码块执行时间并返回结果
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="operation">操作名称</param>
        /// <param name="func">要执行的函数</param>
        /// <returns>函数执行结果</returns>
        private T LogElapsedTime<T>(string operation, Func<T> func)
        {
            // 开始计时
            var stopwatch = Stopwatch.StartNew();
            // 执行操作
            T result = func != null ? func.Invoke() : default(T);
            // 停止计时
            stopwatch.Stop();
            // 输出耗时信息到命令行（简洁格式）
            _editor?.WriteMessage($"\nINFO: {operation} 耗时 {stopwatch.ElapsedMilliseconds} 毫秒");
            return result;
        }

        #endregion
    }
}