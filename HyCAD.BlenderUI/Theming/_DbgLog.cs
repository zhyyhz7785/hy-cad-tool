// !!! TEMP DEBUG ONLY — session b2db6c — DELETE ENTIRE FILE AFTER ROOT-CAUSE CONFIRMED !!!
using System;
using System.IO;
using System.Reflection;

namespace HyCAD.BlenderUI.Theming
{
    internal static class _DbgLog
    {
        private const string Path = @"e:\BaiduSyncdisk\Code\CSharp\CursorProjects\hy-cad-tool\debug-b2db6c.log";
        private static readonly object _lock = new object();
        private static readonly string _asmTag =
            Assembly.GetExecutingAssembly().FullName +
            "@" + Assembly.GetExecutingAssembly().GetHashCode();

        public static void W(string hyp, string loc, string msg, string dataJson = "{}")
        {
            try
            {
                var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var line =
                    "{\"sessionId\":\"b2db6c\"," +
                    "\"hypothesisId\":\"" + Esc(hyp) + "\"," +
                    "\"location\":\"" + Esc(loc) + "\"," +
                    "\"message\":\"" + Esc(msg) + "\"," +
                    "\"data\":" + dataJson + "," +
                    "\"asm\":\"" + Esc(_asmTag) + "\"," +
                    "\"timestamp\":" + ts + "}";

                lock (_lock)
                {
                    File.AppendAllText(Path, line + "\n");
                }
            }
            catch { /* never break the host on a debug log */ }
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
