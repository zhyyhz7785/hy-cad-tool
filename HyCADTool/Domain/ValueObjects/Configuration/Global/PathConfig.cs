using System;
using System.IO;

namespace HyCADTool.Domain.ValueObjects.Configuration.Global
{
    /// <summary>
    /// 路径配置值对象
    /// </summary>
    public class PathConfig
    {
        /// <summary>
        /// 默认导出路径
        /// </summary>
        public string DefaultExportPath { get; set; }

        /// <summary>
        /// 默认导入路径
        /// </summary>
        public string DefaultImportPath { get; set; }

        /// <summary>
        /// 临时文件路径
        /// </summary>
        public string TempFilesPath { get; set; }

        /// <summary>
        /// 配置文件目录
        /// </summary>
        public string ConfigDirectory { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public PathConfig()
        {
            DefaultExportPath = string.Empty;
            DefaultImportPath = string.Empty;
            TempFilesPath = Path.Combine(Path.GetTempPath(), "HyCADTool");
            ConfigDirectory = string.Empty;
        }

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public bool IsValid(out string error)
        {
            // 路径可以为空，由用户后续设置
            error = null;
            return true;
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static PathConfig CreateDefault()
        {
            return new PathConfig
            {
                DefaultExportPath = @"E:\BaiduSyncdisk\Code\testResult",
                DefaultImportPath = string.Empty,
                TempFilesPath = Path.Combine(Path.GetTempPath(), "HyCADTool"),
                ConfigDirectory = string.Empty
            };
        }

        /// <summary>
        /// 确保目录存在（如果路径已设置）
        /// </summary>
        public void EnsureDirectoriesExist()
        {
            TryCreateDirectory(DefaultExportPath);
            TryCreateDirectory(DefaultImportPath);
            TryCreateDirectory(TempFilesPath);
            TryCreateDirectory(ConfigDirectory);
        }

        private void TryCreateDirectory(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch
                {
                    // 忽略创建失败
                }
            }
        }

        public override string ToString()
        {
            return $"PathConfig[Export={DefaultExportPath}, Import={DefaultImportPath}]";
        }
    }
}

