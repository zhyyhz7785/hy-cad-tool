using System;
using System.IO;
using System.Text;

namespace HyCADTool.Features.DataExchange.Hyob.Infrastructure.AutoCadMirror
{
    // #region agent log
    internal static class HyobDebugLog
    {
        private const string LogPath = @"e:\BaiduSyncdisk\Code\CSharp\CursorProjects\hy-cad-tool\debug-49c9eb.log";

        public static void Write(string hypothesisId, string location, string message, string step = null, string error = null)
        {
            try
            {
                var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var sb = new StringBuilder();
                sb.Append("{\"sessionId\":\"49c9eb\",\"timestamp\":").Append(ts)
                  .Append(",\"hypothesisId\":\"").Append(Esc(hypothesisId))
                  .Append("\",\"location\":\"").Append(Esc(location))
                  .Append("\",\"message\":\"").Append(Esc(message)).Append('"');
                if (step != null) sb.Append(",\"data\":{\"step\":\"").Append(Esc(step)).Append('"');
                if (error != null)
                {
                    if (step == null) sb.Append(",\"data\":{");
                    else sb.Append(',');
                    sb.Append("\"error\":\"").Append(Esc(error)).Append('"');
                    sb.Append('}');
                }
                else if (step != null) sb.Append('}');
                sb.Append("}\n");
                File.AppendAllText(LogPath, sb.ToString());
            }
            catch { }
        }

        private static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
    // #endregion
}
