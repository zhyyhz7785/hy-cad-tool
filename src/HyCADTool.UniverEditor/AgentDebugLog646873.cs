using System;
using System.IO;
using Newtonsoft.Json;

namespace HyCADTool.UniverEditor
{
    internal static class AgentDebugLog646873
    {
        private const string LogPath = @"e:\BaiduSyncdisk\Code\CSharp\CursorProjects\hy-cad-tool\debug-646873.log";

        internal static void Write(string hypothesisId, string location, string message, object data)
        {
            try
            {
                var json = JsonConvert.SerializeObject(new
                {
                    sessionId = "646873",
                    hypothesisId,
                    location,
                    message,
                    data,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                });
                File.AppendAllText(LogPath, json + Environment.NewLine);
            }
            catch
            {
                // ignore debug log failures
            }
        }
    }
}
