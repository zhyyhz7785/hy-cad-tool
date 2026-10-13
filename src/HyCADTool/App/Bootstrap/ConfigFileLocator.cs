using System;
using System.IO;

namespace HyCADTool.App.Bootstrap
{
    /// <summary>
    /// 查找出厂 config.json。不探测 bin\Debug；找不到时落到用户目录以便可写。
    /// </summary>
    public static class ConfigFileLocator
    {
        public static string FindConfigJson()
        {
            var locations = new[]
            {
                Path.GetDirectoryName(typeof(ConfigFileLocator).Assembly.Location),
                Environment.CurrentDirectory,
                AppDomain.CurrentDomain.BaseDirectory,
            };

            foreach (var location in locations)
            {
                if (string.IsNullOrEmpty(location)) continue;
                var configPath = Path.Combine(location, "config.json");
                if (File.Exists(configPath))
                    return configPath;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HyCADTool",
                "config.json");
        }
    }
}
