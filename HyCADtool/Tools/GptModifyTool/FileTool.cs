using Microsoft.Win32;
using System;
using System.IO;
namespace HyCADTool.Tools
{
    public static partial class Tools
    {
        // 获取保存文件路径
        public static string GetSaveFilePath(string defaultFileName = null)
        {
            if (string.IsNullOrEmpty(defaultFileName))
            {
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                defaultFileName = $"LayerTableRecords_{timestamp}.xlsx";
            }
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                Title = "另存为",
                FileName = defaultFileName
            };
            bool? result = saveFileDialog.ShowDialog();
            return result == true ? saveFileDialog.FileName : null;
        }
        // 获取打开文件路径
        public static string GetOpenFilePath()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                Title = "打开Excel文件"
            };
            bool? result = openFileDialog.ShowDialog();
            return result == true ? openFileDialog.FileName : null;
        }
        // 读取文件内容
        public static string ReadFileContent(string filePath)
        {
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }
            else
            {
                throw new FileNotFoundException("文件未找到", filePath);
            }
        }
        // 写入内容到文件
        public static void WriteFileContent(string filePath, string content)
        {
            File.WriteAllText(filePath, content);
        }
        // 追加内容到文件
        public static void AppendFileContent(string filePath, string content)
        {
            File.AppendAllText(filePath, content);
        }
        // 复制文件
        public static void CopyFile(string sourceFilePath, string destinationFilePath, bool overwrite = false)
        {
            if (File.Exists(sourceFilePath))
            {
                File.Copy(sourceFilePath, destinationFilePath, overwrite);
            }
            else
            {
                throw new FileNotFoundException("源文件未找到", sourceFilePath);
            }
        }
        // 删除文件
        public static void DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            else
            {
                throw new FileNotFoundException("文件未找到", filePath);
            }
        }
    }
}
