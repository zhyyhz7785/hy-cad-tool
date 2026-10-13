using System;
using System.Collections.Generic;
using System.IO;

namespace HyCADTool.App.Bootstrap
{
    /// <summary>
    /// 用户可写的 config.json 在 %APPDATA%\HyCADTool。首次启动把安装目录旧文件迁过去。
    /// </summary>
    public static class ConfigFileLocator
    {
        public static string UserConfigJson()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HyCADTool",
                "config.json");
        }

        public static string FindConfigJson()
        {
            return FindConfigJson(
                new[]
                {
                    Path.GetDirectoryName(typeof(ConfigFileLocator).Assembly.Location),
                    Environment.CurrentDirectory,
                    AppDomain.CurrentDomain.BaseDirectory,
                },
                UserConfigJson());
        }

        /// <summary>测试入口：legacy 目录里的 config.json 复制到 userPath（若 user 还不存在）。</summary>
        public static string FindConfigJson(IEnumerable<string> legacyDirectories, string userPath)
        {
            if (string.IsNullOrWhiteSpace(userPath))
                throw new ArgumentException("userPath is required", nameof(userPath));

            if (File.Exists(userPath))
                return userPath;

            if (legacyDirectories != null)
            {
                foreach (var location in legacyDirectories)
                {
                    if (string.IsNullOrEmpty(location))
                        continue;
                    var legacy = Path.Combine(location, "config.json");
                    if (!File.Exists(legacy))
                        continue;
                    if (string.Equals(Path.GetFullPath(legacy), Path.GetFullPath(userPath), StringComparison.OrdinalIgnoreCase))
                        return userPath;

                    var destDir = Path.GetDirectoryName(userPath);
                    if (!string.IsNullOrEmpty(destDir))
                        Directory.CreateDirectory(destDir);
                    File.Copy(legacy, userPath, overwrite: false);
                    return userPath;
                }
            }

            return userPath;
        }
    }
}
