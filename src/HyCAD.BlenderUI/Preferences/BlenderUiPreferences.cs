using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using HyCAD.BlenderUI.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HyCAD.BlenderUI.Preferences
{
    /// <summary>
    /// BlenderUI 壳层偏好。工具栏位置对应 HTML mock 的 localStorage 键
    /// <c>hy.blender.toolbarPos</c>（值 top|side），WPF 侧落到 %AppData%/HyCAD JSON。
    /// 默认 <see cref="ToolbarPosition.Top"/>。
    /// </summary>
    public sealed class BlenderUiPreferences : INotifyPropertyChanged
    {
        /// <summary>与批准模板一致的存储键：top = 上侧，side = 侧面。</summary>
        public const string ToolbarPosKey = "hy.blender.toolbarPos";

        private static readonly object FileGate = new object();
        private ToolbarPosition _toolbarPosition = ToolbarPosition.Top;

        public static BlenderUiPreferences Current { get; } = Load();

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>工具栏位置。写入后立即落盘；默认上侧。</summary>
        public ToolbarPosition ToolbarPosition
        {
            get => _toolbarPosition;
            set
            {
                if (_toolbarPosition == value) return;
                _toolbarPosition = value;
                OnPropertyChanged();
                Save();
            }
        }

        internal static string StorageFilePath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "HyCAD");
                return Path.Combine(dir, "blender-ui-prefs.json");
            }
        }

        public static string ToStorageValue(ToolbarPosition position)
            => position == ToolbarPosition.Side ? "side" : "top";

        public static ToolbarPosition ParseStorageValue(string raw)
        {
            if (string.Equals(raw, "side", StringComparison.OrdinalIgnoreCase))
                return ToolbarPosition.Side;
            return ToolbarPosition.Top;
        }

        private static BlenderUiPreferences Load()
        {
            var prefs = new BlenderUiPreferences();
            try
            {
                var path = StorageFilePath;
                if (!File.Exists(path)) return prefs;
                var json = File.ReadAllText(path);
                var obj = JObject.Parse(json);
                var raw = obj.Value<string>(ToolbarPosKey);
                if (!string.IsNullOrWhiteSpace(raw))
                    prefs._toolbarPosition = ParseStorageValue(raw);
            }
            catch (Exception)
            {
                // 偏好文件损坏或缺权限时回退默认上侧，不阻断 UI。
            }

            return prefs;
        }

        private void Save()
        {
            try
            {
                lock (FileGate)
                {
                    var path = StorageFilePath;
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    JObject obj;
                    if (File.Exists(path))
                    {
                        try { obj = JObject.Parse(File.ReadAllText(path)); }
                        catch (JsonException) { obj = new JObject(); }
                    }
                    else
                    {
                        obj = new JObject();
                    }

                    obj[ToolbarPosKey] = ToStorageValue(_toolbarPosition);
                    File.WriteAllText(path, obj.ToString(Formatting.Indented));
                }
            }
            catch (Exception)
            {
                // 写盘失败不影响当前会话内的切换。
            }
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
