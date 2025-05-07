using System;
using System.Collections.Generic;
using System.IO;
namespace HyCADTool.Log
{
    /// <summary>
    /// 一个简单的日志类，用于记录日志信息和操作耗时
    /// </summary>
    public static class SimpleLogger
    {
        // 日志文件的路径
        private static readonly string logFilePath = "E:\\BaiduSyncdisk\\Code\\testResult\\SimpleLogger.txt";
        private static readonly object lockObj = new object();
        private static Dictionary<string, System.Diagnostics.Stopwatch> timers = new Dictionary<string, System.Diagnostics.Stopwatch>();
        /// <summary>
        /// 记录一条日志消息到日志文件
        /// </summary>
        /// <param name="message">要记录的日志消息</param>
        public static void Log(string message)
        {
            try
            {
                lock (lockObj)
                {
                    // 以追加模式打开日志文件，并写入日志消息
                    using (StreamWriter writer = new StreamWriter(logFilePath, true))
                    {
                        writer.WriteLine($"{DateTime.Now}: {message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果写日志失败，在控制台输出错误信息
                Console.WriteLine($"写日志失败: {ex.Message}");
            }
        }
        /// <summary>
        /// 记录信息级别的日志消息
        /// </summary>
        /// <param name="message">要记录的日志消息</param>
        public static void LogInfo(string message)
        {
            Log($"INFO: {message}");
        }
        /// <summary>
        /// 记录警告级别的日志消息
        /// </summary>
        /// <param name="message">要记录的日志消息</param>
        public static void LogWarning(string message)
        {
            Log($"WARNING: {message}");
        }
        /// <summary>
        /// 记录错误级别的日志消息
        /// </summary>
        /// <param name="message">要记录的日志消息</param>
        /// <param name="ex">异常对象</param>
        public static void LogError(string message, Exception ex)
        {
            Log($"ERROR: {message} Exception: {ex}");
        }
        /// <summary>
        /// 记录严重错误级别的日志消息
        /// </summary>
        /// <param name="message">要记录的日志消息</param>
        /// <param name="ex">异常对象</param>
        public static void LogCritical(string message, Exception ex)
        {
            Log($"CRITICAL: {message} Exception: {ex}");
        }
        /// <summary>
        /// 记录一个操作的耗时到日志文件
        /// </summary>
        /// <param name="operation">操作的名称</param>
        /// <param name="action">要执行的操作</param>
        public static void LogElapsedTime(string operation, Action action)
        {
            // 开始计时
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            // 执行操作
            action();
            // 停止计时
            stopwatch.Stop();
            // 记录操作耗时到日志文件
            Log($"INFO: {operation} 耗时 {stopwatch.ElapsedMilliseconds} 毫秒");
        }
        /// <summary>
        /// 开始记录一个操作的时间
        /// </summary>
        /// <param name="operation">操作的名称</param>
        public static void StartTiming(string operation)
        {
            lock (lockObj)
            {
                if (!timers.ContainsKey(operation))
                {
                    timers[operation] = System.Diagnostics.Stopwatch.StartNew();
                    LogInfo($"{operation} 开始计时。");
                }
                else
                {
                    timers[operation].Restart();
                    LogInfo($"{operation} 重新开始计时。");
                }
            }
        }
        /// <summary>
        /// 停止记录一个操作的时间，并将耗时记录到日志文件
        /// </summary>
        /// <param name="operation">操作的名称</param>
        public static void StopTiming(string operation)
        {
            lock (lockObj)
            {
                if (timers.ContainsKey(operation))
                {
                    timers[operation].Stop();
                    LogInfo($"{operation} 耗时 {timers[operation].ElapsedMilliseconds} 毫秒");
                }
                else
                {
                    LogWarning($"{operation} 尚未开始计时。");
                }
            }
        }
    }
}
