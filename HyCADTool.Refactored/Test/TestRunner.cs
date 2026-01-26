using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Diagnostics;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// 通用测试运行器 - 简化版
    /// 测试当前正在开发的命令
    /// 
    /// 使用说明：
    /// - RunAllTests(): 执行测试命令（自动计时）
    /// - LogElapsedTime(): 辅助方法，完全按照原项目 SimpleLogger.LogElapsedTime 的简洁风格
    /// 
    /// 测试方式：
    /// - 一次只测试一个命令（已测试通过的命令注释掉）
    /// - 使用 C1 命令调用此方法
    /// - 可通过 C11-C19 直接调用单个命令
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
        /// 运行测试 - 一次只测试一个命令
        /// </summary>
        public void RunAllTests()
        {
            if (_editor == null)
            {
                return;
            }

            // ========== 当前测试命令 ==========
            // 一次只启用一个命令的测试
            
            // ✅ HYOV - 已测试通过
            // LogElapsedTime("HYOV 命令执行", () =>
            // {
            //     var cmd = new HyCADTool.Refactored.Presentation.Commands.OverKillCommand();
            //     cmd.Execute();
            // });

            // ✅ HYBC - 已测试通过
            // LogElapsedTime("HYBC 命令执行", () =>
            // {
            //     var cmd = new HyCADTool.Refactored.Presentation.Commands.BreakCurvesCommand();
            //     cmd.Execute();
            // });

            // 🔄 HYDCEL - 当前测试
            LogElapsedTime("HYDCEL 命令执行", () =>
            {
                try
                {
                    var cmd = new HyCADTool.Refactored.Presentation.Commands.DCELCommand();
                    cmd.Execute();
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