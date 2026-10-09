using System;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace HyCADTool.Features.DesignSpec.Services
{
    /// <summary>
    /// HyMd 可执行文件路径等独立小配置（%APPDATA%\HyCADTool\hymd-cad.json）。
    /// </summary>
    public sealed class HyMdCadSettingsData
    {
        public string ExePath { get; set; }
        public double Scale { get; set; } = 1.0;
    }

    public static class HyMdCadSettings
    {
        public static string FilePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HyCADTool",
                "hymd-cad.json");

        public static HyMdCadSettingsData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var data = JsonConvert.DeserializeObject<HyMdCadSettingsData>(File.ReadAllText(FilePath));
                    if (data != null)
                    {
                        if (data.Scale <= 0) data.Scale = 1.0;
                        return data;
                    }
                }
            }
            catch
            {
            }

            return new HyMdCadSettingsData();
        }

        public static void Save(HyMdCadSettingsData data)
        {
            if (data == null) return;
            string dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(data, Formatting.Indented));
        }

        public static string ResolveExePath()
        {
            var data = Load();
            if (!string.IsNullOrWhiteSpace(data.ExePath) && File.Exists(data.ExePath))
                return data.ExePath;

            foreach (var candidate in ProbeCandidates())
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return data.ExePath;
        }

        public static string PromptForExe()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "选择 HyMd 可执行文件";
                dlg.Filter = "HyMd|HyMd.exe;hymd-tauri2.exe|可执行文件|*.exe";
                dlg.CheckFileExists = true;
                if (dlg.ShowDialog() != DialogResult.OK)
                    return null;

                var data = Load();
                data.ExePath = dlg.FileName;
                Save(data);
                return dlg.FileName;
            }
        }

        private static string[] ProbeCandidates()
        {
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new[]
            {
                Path.Combine(pf, "HyMd", "HyMd.exe"),
                Path.Combine(local, "HyMd", "HyMd.exe"),
                @"E:\Code\HyMdTauri2\src-tauri\target\debug\hymd-tauri2.exe",
                @"E:\Code\HyMdTauri2\src-tauri\target\release\hymd.exe",
                @"E:\Code\HyMdTauri2\src-tauri\target\release\hymd-tauri2.exe",
            };
        }
    }
}
