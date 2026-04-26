using System.IO;

namespace HyCADTool.Shell.Configuration.Global
{
    public class PathConfig
    {
        public string DefaultExportPath { get; set; } = string.Empty;
        public string DefaultImportPath { get; set; } = string.Empty;
        public string TempFilesPath { get; set; } = Path.Combine(Path.GetTempPath(), "HyCADTool");
        public string ConfigDirectory { get; set; } = string.Empty;

        public static PathConfig CreateDefault()
        {
            return new PathConfig
            {
                DefaultExportPath = string.Empty,
                DefaultImportPath = string.Empty,
                TempFilesPath = Path.Combine(Path.GetTempPath(), "HyCADTool"),
                ConfigDirectory = string.Empty
            };
        }
    }
}
