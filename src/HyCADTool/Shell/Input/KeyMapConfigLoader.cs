using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using Newtonsoft.Json;

namespace HyCADTool.Shell.Input
{
    /// <summary>
    /// hy-keymap.json 的读写封装（%APPDATA%\HyCADTool\hy-keymap.json）。
    /// 单进程进程内缓存一份 <see cref="KeyMapData"/>：
    ///   - UI 键位映射（key → Operator Id）；
    ///   - 读取失败时返回 null，由调用方走默认键位（空配置不触发覆盖语义）；
    ///   - 写入前保证目录存在，异常吞掉返回 false。
    /// </summary>
    public static class KeyMapConfigLoader
    {
        /// <summary>UI 键位条目（对应一条 KeyMapItem 的可序列化形式）。</summary>
        public sealed class KeyMapEntry
        {
            /// <summary>绑定按键，WPF <see cref="Key"/> 枚举名（如 "F" / "Escape" / "E"）。</summary>
            public string Key { get; set; }

            /// <summary>修饰键，<see cref="ModifierKeys"/> 枚举名 + "|" 拼接（如 "Control" / "Control|Shift" / "None"）。</summary>
            public string Modifiers { get; set; }

            /// <summary>要触发的 Operator Id（如 "hy.ui.focus_search" 或 "hy.cmd.gj"）。</summary>
            public string OperatorId { get; set; }
        }

        /// <summary>完整 hy-keymap.json 内容。</summary>
        public sealed class KeyMapData
        {
            /// <summary>配置名（用于调试/多配置切换，不影响运行）。</summary>
            public string Name { get; set; } = "HyUI";

            /// <summary>键位条目列表。为空时调用方走内置默认。</summary>
            public List<KeyMapEntry> Items { get; set; } = new List<KeyMapEntry>();
        }

        private const string FileName = "hy-keymap.json";

        /// <summary>读取 hy-keymap.json；文件不存在/解析失败时返回 null。</summary>
        public static KeyMapData Load()
        {
            try
            {
                var path = GetConfigFilePath();
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonConvert.DeserializeObject<KeyMapData>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>把 KeyMapData 写到 hy-keymap.json；失败返回 false（不抛，避免阻断 UI）。</summary>
        public static bool Save(KeyMapData data)
        {
            if (data == null) return false;
            try
            {
                var path = GetConfigFilePath();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(path, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>配置文件完整路径（%APPDATA%\HyCADTool\hy-keymap.json）。</summary>
        public static string GetConfigFilePath()
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HyCADTool");
            return Path.Combine(appDataDir, FileName);
        }

        /// <summary>字符串 → Key；解析失败返回 <see cref="Key.None"/>。</summary>
        public static Key ParseKey(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Key.None;
            return Enum.TryParse<Key>(s, true, out var k) ? k : Key.None;
        }

        /// <summary>"Control|Shift" → ModifierKeys.Control|ModifierKeys.Shift。空 / "None" → <see cref="ModifierKeys.None"/>。</summary>
        public static ModifierKeys ParseModifiers(string s)
        {
            if (string.IsNullOrWhiteSpace(s) || string.Equals(s, "None", StringComparison.OrdinalIgnoreCase))
                return ModifierKeys.None;
            var result = ModifierKeys.None;
            foreach (var token in s.Split('|', '+', ','))
            {
                var t = token.Trim();
                if (t.Length == 0) continue;
                if (Enum.TryParse<ModifierKeys>(t, true, out var m)) result |= m;
            }
            return result;
        }

        /// <summary>ModifierKeys → "Control|Shift" 格式字符串（None → "None"）。</summary>
        public static string FormatModifiers(ModifierKeys m)
        {
            if (m == ModifierKeys.None) return "None";
            var parts = new List<string>(3);
            if (m.HasFlag(ModifierKeys.Control)) parts.Add("Control");
            if (m.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (m.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (m.HasFlag(ModifierKeys.Windows)) parts.Add("Windows");
            return string.Join("|", parts);
        }
    }
}
