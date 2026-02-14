using Newtonsoft.Json;
using System;
using System.IO;

namespace HyCADTool.Refactored.Diagnostics
{
    internal static class AgentDebugLogger
    {
        private const string LogPath = @"e:\BaiduSyncdisk\Code\CSharp\CursorProjects\hy-cad-tool\.cursor\debug.log";

        public static void Log(string runId, string hypothesisId, string location, string message, object data = null)
        {
            try
            {
                long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string id = "log_" + ts + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                var payload = new
                {
                    id,
                    timestamp = ts,
                    location,
                    message,
                    data = data ?? new { },
                    runId,
                    hypothesisId
                };
                File.AppendAllText(LogPath, JsonConvert.SerializeObject(payload) + Environment.NewLine);
            }
            catch
            {
                // 调试日志不能影响主流程
            }
        }
    }
}
