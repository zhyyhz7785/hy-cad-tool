using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace HyCADTool.UniverEditor.Services
{
    internal static class WebView2EnvironmentProvider
    {
        private static readonly object Sync = new object();
        private static Task<CoreWebView2Environment> _environmentTask;

        public static Task<CoreWebView2Environment> GetOrCreateAsync()
        {
            lock (Sync)
            {
                if (_environmentTask == null)
                {
                    string userDataFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "HyCADTool",
                        "WebView2");
                    Directory.CreateDirectory(userDataFolder);
                    _environmentTask = CoreWebView2Environment.CreateAsync(null, userDataFolder);
                }

                return _environmentTask;
            }
        }

        public static void Warmup()
        {
            string runtimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
            if (string.IsNullOrWhiteSpace(runtimeVersion))
                return;

            _ = GetOrCreateAsync();
        }
    }
}
