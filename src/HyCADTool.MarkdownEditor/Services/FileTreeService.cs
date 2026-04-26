using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using HyCADTool.MarkdownEditor.Models;

namespace HyCADTool.MarkdownEditor.Services
{
    internal class FileTreeService
    {
        public TreeViewItem BuildTreeNode(string dirPath, string currentFile, Func<string, Brush> getBrush, FileSortMode sortMode)
        {
            string dirName = Path.GetFileName(dirPath);
            if (string.IsNullOrWhiteSpace(dirName)) dirName = dirPath;
            var node = new TreeViewItem
            {
                Header = "\U0001F4C1 " + dirName,
                Tag = dirPath,
                IsExpanded = false,
                Foreground = getBrush("ThemeTextPrimaryBrush"),
            };

            try
            {
                foreach (var sub in SortPaths(Directory.GetDirectories(dirPath), sortMode, isDirectory: true))
                {
                    if (Path.GetFileName(sub).StartsWith(".")) continue;
                    node.Items.Add(BuildTreeNode(sub, currentFile, getBrush, sortMode));
                }
            }
            catch { }

            try
            {
                foreach (var file in SortPaths(Directory.GetFiles(dirPath, "*.md", SearchOption.TopDirectoryOnly), sortMode, isDirectory: false))
                {
                    bool isCurrent = string.Equals(file, currentFile, StringComparison.OrdinalIgnoreCase);
                    node.Items.Add(new TreeViewItem
                    {
                        Header = "\U0001F4C4 " + Path.GetFileName(file),
                        Tag = file,
                        ToolTip = file,
                        Foreground = isCurrent ? getBrush("ThemeAccentBrush") : getBrush("ThemeTextPrimaryBrush"),
                        IsSelected = isCurrent,
                    });
                }
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(currentFile) &&
                currentFile.StartsWith(dirPath, StringComparison.OrdinalIgnoreCase))
                node.IsExpanded = true;

            return node;
        }

        private static IOrderedEnumerable<string> SortPaths(string[] paths, FileSortMode sortMode, bool isDirectory)
        {
            switch (sortMode)
            {
                case FileSortMode.NameDesc:
                    return paths.OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
                case FileSortMode.ModifiedDesc:
                    return paths
                        .OrderByDescending(path => GetWriteTimeUtc(path, isDirectory))
                        .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
                case FileSortMode.ModifiedAsc:
                    return paths
                        .OrderBy(path => GetWriteTimeUtc(path, isDirectory))
                        .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
                default:
                    return paths.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
            }
        }

        private static DateTime GetWriteTimeUtc(string path, bool isDirectory)
        {
            try
            {
                return isDirectory ? Directory.GetLastWriteTimeUtc(path) : File.GetLastWriteTimeUtc(path);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        public string ResolveDir(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (Directory.Exists(path)) return path;
            if (File.Exists(path)) return Path.GetDirectoryName(path);
            return null;
        }

        public string CreateFile(string directory, string fileNameWithoutExtOrWithExt)
        {
            string name = fileNameWithoutExtOrWithExt ?? "";
            if (!name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) name += ".md";
            string full = Path.Combine(directory, name);
            if (File.Exists(full)) throw new IOException("文件已存在。");
            File.WriteAllText(full, $"# {Path.GetFileNameWithoutExtension(name)}\n", Encoding.UTF8);
            return full;
        }

        public string CreateFolder(string directory, string folderName)
        {
            string full = Path.Combine(directory, folderName ?? "");
            if (Directory.Exists(full)) throw new IOException("文件夹已存在。");
            Directory.CreateDirectory(full);
            return full;
        }

        public string RenameItem(string path, string newName)
        {
            string newPath = Path.Combine(Path.GetDirectoryName(path) ?? "", newName ?? "");
            if (File.Exists(path))
            {
                File.Move(path, newPath);
                return newPath;
            }

            if (Directory.Exists(path))
            {
                Directory.Move(path, newPath);
                return newPath;
            }

            throw new FileNotFoundException("目标不存在。", path);
        }

        public string DuplicateFile(string path)
        {
            string dir = Path.GetDirectoryName(path) ?? "";
            string nameNoExt = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            string copyPath = Path.Combine(dir, nameNoExt + " - 副本" + ext);
            int n = 2;
            while (File.Exists(copyPath))
            {
                copyPath = Path.Combine(dir, $"{nameNoExt} - 副本{n}{ext}");
                n++;
            }

            File.Copy(path, copyPath);
            return copyPath;
        }

        public void DeletePath(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return;
            }

            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
                return;
            }

            throw new FileNotFoundException("目标不存在。", path);
        }
    }
}
