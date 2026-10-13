using System;
using System.IO;
using System.Text;

namespace HyCADTool.Shared.Core
{
    /// <summary>
    /// 先写临时文件再替换，避免写到一半断电/崩溃留下半截配置。
    /// </summary>
    public static class AtomicFile
    {
        public static void WriteAllText(string path, string contents)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("path is required", nameof(path));

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";
            File.WriteAllText(tmp, contents ?? string.Empty, new UTF8Encoding(false));

            if (File.Exists(path))
            {
                var bak = path + ".bak";
                File.Replace(tmp, path, bak, true);
            }
            else
            {
                File.Move(tmp, path);
            }
        }
    }
}
