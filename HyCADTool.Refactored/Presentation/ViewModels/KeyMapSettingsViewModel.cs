using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Refactored.Presentation.Input;

namespace HyCADTool.Refactored.Presentation.ViewModels
{
    /// <summary>
    /// 首选项「快捷键」分组的 VM：
    ///   - 载入 hy-keymap.json；为空或读取失败时用 HyKeyMap 的内置默认；
    ///   - 支持按 Key/Modifiers 录制（由 BlenderKeyEventField 回填字段，VM 只做校验与持久化）；
    ///   - 保存 / 恢复默认 都直接写 hy-keymap.json，重启下一次打开 hy 面板生效。
    /// 注意：当前 hy 面板是在 Loaded 时读一次 KeyMap 并 Attach Router，VM 保存后不会立刻热更新
    /// 正在运行的面板；这里只做"落盘"，等价 Blender Preferences 里保存 keymap.py，下次生效。
    /// </summary>
    public class KeyMapSettingsViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<KeyMapBindingVm> Items { get; } = new ObservableCollection<KeyMapBindingVm>();

        public ICommand SaveCommand { get; }
        public ICommand RestoreDefaultsCommand { get; }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public KeyMapSettingsViewModel()
        {
            SaveCommand = new RelayCommand(OnSave);
            RestoreDefaultsCommand = new RelayCommand(OnRestoreDefaults);
            LoadFromDisk();
        }

        /// <summary>从磁盘加载（失败时退回内置默认），并刷新 UI 列表。</summary>
        private void LoadFromDisk()
        {
            var data = KeyMapConfigLoader.Load() ?? HyKeyMap.BuildBuiltInDefaults();
            Items.Clear();
            if (data.Items != null)
            {
                foreach (var it in data.Items)
                    Items.Add(new KeyMapBindingVm(it));
            }
            StatusMessage = $"已加载 {Items.Count} 条键位";
        }

        private void OnSave()
        {
            var data = new KeyMapConfigLoader.KeyMapData { Name = "HyUI" };
            foreach (var vm in Items)
            {
                data.Items.Add(new KeyMapConfigLoader.KeyMapEntry
                {
                    Key = vm.Key,
                    Modifiers = vm.Modifiers,
                    OperatorId = vm.OperatorId,
                });
            }
            var ok = KeyMapConfigLoader.Save(data);
            StatusMessage = ok
                ? $"已保存 → {KeyMapConfigLoader.GetConfigFilePath()}"
                : "保存失败（见日志）";
        }

        private void OnRestoreDefaults()
        {
            var defaults = HyKeyMap.BuildBuiltInDefaults();
            Items.Clear();
            foreach (var it in defaults.Items)
                Items.Add(new KeyMapBindingVm(it));
            StatusMessage = "已恢复内置默认（尚未保存）";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>单条键位绑定 VM（行级）。</summary>
    public class KeyMapBindingVm : INotifyPropertyChanged
    {
        private string _key;
        public string Key
        {
            get => _key;
            set { if (_key == value) return; _key = value; OnPropertyChanged(); OnPropertyChanged(nameof(Display)); }
        }

        private string _modifiers;
        public string Modifiers
        {
            get => _modifiers;
            set { if (_modifiers == value) return; _modifiers = value; OnPropertyChanged(); OnPropertyChanged(nameof(Display)); }
        }

        public string OperatorId { get; set; }

        /// <summary>只读显示串："Ctrl+F" / "Esc"，供非录键模式下的文本展示。</summary>
        public string Display
        {
            get
            {
                if (string.IsNullOrEmpty(Key)) return string.Empty;
                if (string.IsNullOrEmpty(Modifiers) || Modifiers == "None") return Key;
                return Modifiers.Replace("|", "+") + "+" + Key;
            }
        }

        public KeyMapBindingVm() { }

        public KeyMapBindingVm(KeyMapConfigLoader.KeyMapEntry src)
        {
            _key = src?.Key;
            _modifiers = src?.Modifiers;
            OperatorId = src?.OperatorId;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
