using System.IO;

namespace HyCADTool.Licensing
{
    public static class LicensePaths
    {
        public static string ProgramDataHyCAD { get; } = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
            "HyCAD");

        public static string LicenseFile { get; } = Path.Combine(ProgramDataHyCAD, "license.lic");
        public static string StateFile { get; } = Path.Combine(ProgramDataHyCAD, "state.bin");
    }
}
