#if DEBUG
using Newtonsoft.Json;
using System;
using System.IO;

namespace HyCADTool.Shared.AutoCAD.Utilities
{
    internal static class AgentDebugLogger
    {
        private static readonly string LogPath = Path.Combine(
            Path.GetTempPath(),
            "HyCADTool-agent-debug.log");

        public static void Log(string runId, string hypothesisId, string location, string message, object data)
        {
            try
            {
                var payload = new
                {
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
#endif
