using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HyCADTool.MarkdownEditor.Models;

namespace HyCADTool.MarkdownEditor.Services
{
    internal sealed class LeftPanelStateService
    {
        private const int MaxRecentDirectoryCount = 12;
        private readonly string _stateFilePath;

        public LeftPanelStateService()
        {
            string baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HyCADTool",
                "MarkdownEditor");
            _stateFilePath = Path.Combine(baseDir, "left-panel-state.json");
        }

        public LeftPanelState Load()
        {
            try
            {
                if (!File.Exists(_stateFilePath)) return LeftPanelState.CreateDefault();

                string json = File.ReadAllText(_stateFilePath);
                var state = JsonSerializer.Deserialize<LeftPanelState>(json) ?? LeftPanelState.CreateDefault();
                return Normalize(state);
            }
            catch
            {
                return LeftPanelState.CreateDefault();
            }
        }

        public void Save(FileSortMode sortMode, IEnumerable<string> recentDirectories)
        {
            try
            {
                var dir = Path.GetDirectoryName(_stateFilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var state = new LeftPanelState
                {
                    SortMode = sortMode,
                    RecentDirectories = (recentDirectories ?? Enumerable.Empty<string>())
                        .Where(path => !string.IsNullOrWhiteSpace(path))
                        .Select(path => path.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(MaxRecentDirectoryCount)
                        .ToList()
                };

                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_stateFilePath, json);
            }
            catch
            {
                // 本地状态写入失败不影响编辑器主流程
            }
        }

        private static LeftPanelState Normalize(LeftPanelState state)
        {
            state.RecentDirectories = (state.RecentDirectories ?? new List<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxRecentDirectoryCount)
                .ToList();

            return state;
        }
    }

    internal sealed class LeftPanelState
    {
        public FileSortMode SortMode { get; set; } = FileSortMode.NameAsc;
        public List<string> RecentDirectories { get; set; } = new List<string>();

        public static LeftPanelState CreateDefault() => new LeftPanelState();
    }
}
