using System;
using System.IO;
using Newtonsoft.Json;

namespace HyCADTool.Refactored.Presentation
{
    /// <summary>
    /// 调试会话专用：把一条 NDJSON 追加到工作区根目录的 debug-eef710.log。
    /// 线程安全（锁住单例 lock），全部异常吃掉不影响业务。
    /// 所有调用点用 #region agent log 包裹，验证通过后一次性摘掉。
    /// </summary>
    internal static class DebugLogger
    {
        private const string LogPath = @"E:\BaiduSyncdisk\Code\CSharp\CursorProjects\hy-cad-tool\debug-eef710.log";
        private const string SessionId = "eef710";
        private static readonly object _lock = new object();

        public static void Log(string location, string message, object data, string hypothesisId)
        {
            try
            {
                var payload = new
                {
                    sessionId = SessionId,
                    id = "log_" + DateTime.UtcNow.Ticks.ToString(),
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    location,
                    message,
                    data,
                    hypothesisId,
                };
                string line = JsonConvert.SerializeObject(payload) + "\n";
                lock (_lock)
                {
                    File.AppendAllText(LogPath, line);
                }
            }
            catch
            {
                // 静默：日志失败不影响业务
            }
        }
    }
}
