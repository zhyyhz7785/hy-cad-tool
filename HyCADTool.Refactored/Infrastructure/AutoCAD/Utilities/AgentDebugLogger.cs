using Newtonsoft.Json;
using System;
using System.IO;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Utilities
{
    internal static class AgentDebugLogger
    {
        private const string LogPath = @"E:\BaiduSyncdisk\Code\CSharp\CursorProjects\hy-cad-tool\debug-24de30.log";
        private const string SessionId = "24de30";

        public static void Log(string runId, string hypothesisId, string location, string message, object data)
        {
            try
            {
                var payload = new
                {
                    sessionId = SessionId,
                    runId,
                    hypothesisId,
                    location,
                    message,
                    data,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                File.AppendAllText(LogPath, JsonConvert.SerializeObject(payload) + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
