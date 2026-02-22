using System;
using System.IO;
using HyCADTool.MarkdownEditor.Models;
using Newtonsoft.Json;

namespace HyCADTool.MarkdownEditor.Services
{
    /// <summary>
    /// 编辑器配置持久化：保存到 %LocalAppData%/HyCADTool/MarkdownEditor/editor-config.json
    /// </summary>
    internal sealed class EditorConfigPersistenceService
    {
        private readonly string _configFilePath;

        public EditorConfigPersistenceService()
        {
            string baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HyCADTool",
                "MarkdownEditor");
            _configFilePath = Path.Combine(baseDir, "editor-config.json");
        }

        public EditorConfig Load()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                    return null;

                string json = File.ReadAllText(_configFilePath);
                var config = JsonConvert.DeserializeObject<EditorConfig>(json);
                return config;
            }
            catch
            {
                return null;
            }
        }

        public void Save(EditorConfig config)
        {
            if (config == null) return;

            try
            {
                string dir = Path.GetDirectoryName(_configFilePath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(_configFilePath, json);
            }
            catch
            {
                // 持久化失败不影响主流程
            }
        }

        /// <summary>将配置保存到指定文件路径</summary>
        public void SaveToFile(EditorConfig config, string filePath)
        {
            if (config == null || string.IsNullOrWhiteSpace(filePath)) return;

            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch
            {
                throw;
            }
        }

        /// <summary>从指定文件路径加载配置，失败返回 null</summary>
        public EditorConfig LoadFromFile(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return null;

                string json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<EditorConfig>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
