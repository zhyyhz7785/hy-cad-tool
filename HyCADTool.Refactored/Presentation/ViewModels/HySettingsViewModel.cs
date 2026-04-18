using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HyCADTool.Refactored.Presentation.ViewModels
{
    /// <summary>
    /// HyB 面板「设置」伪分类使用的总 ViewModel（对应 Blender Preferences）。
    /// 框架阶段：仅搭建一级目录 + 二级分组骨架 + 底部按钮占位；
    /// 后续阶段逐项接入真实 Binding（见 plan hyb_设置_tab_框架搭建）。
    /// </summary>
    public class HySettingsViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<SettingsCategoryVm> Categories { get; } = new ObservableCollection<SettingsCategoryVm>();

        private SettingsCategoryVm _selectedCategory;
        public SettingsCategoryVm SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory == value) return;
                _selectedCategory = value;
                OnPropertyChanged();
            }
        }

        private bool _autoSaveEnabled = true;
        public bool AutoSaveEnabled
        {
            get => _autoSaveEnabled;
            set { if (_autoSaveEnabled == value) return; _autoSaveEnabled = value; OnPropertyChanged(); }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public ICommand ApplyCurrentCommand { get; }
        public ICommand SaveUserSettingsCommand { get; }
        public ICommand RestoreAutoSavedCommand { get; }
        public ICommand LoadDefaultsCommand { get; }

        public HySettingsViewModel()
        {
            ApplyCurrentCommand      = new RelayCommand(OnApplyCurrent);
            SaveUserSettingsCommand  = new RelayCommand(OnSaveUserSettings);
            RestoreAutoSavedCommand  = new RelayCommand(OnRestoreAutoSaved);
            LoadDefaultsCommand      = new RelayCommand(OnLoadDefaults);

            BuildSkeleton();
            SelectedCategory = Categories.Count > 0 ? Categories[0] : null;
        }

        private void BuildSkeleton()
        {
            Categories.Add(new SettingsCategoryVm("设置",       "⚙", new[]
            {
                new SettingsGroupVm("样式名称预览", "StylePreview"),
                new SettingsGroupVm("文字样式",     "TextStyle"),
                new SettingsGroupVm("标注样式",     "DimStyle"),
                new SettingsGroupVm("引线样式",     "MLeaderStyle"),
                new SettingsGroupVm("表格样式",     "TableStyle"),
            }));

            Categories.Add(new SettingsCategoryVm("钢筋",       "＃", new[]
            {
                new SettingsGroupVm("钢筋参数", "ReinParams"),
                new SettingsGroupVm("尺寸参数", "ReinDimParams"),
            }));

            Categories.Add(new SettingsCategoryVm("底板",       "▦", new[]
            {
                new SettingsGroupVm("配筋参数", "BasePlateRein"),
                new SettingsGroupVm("绘制参数", "BasePlateDraw"),
                new SettingsGroupVm("高级设置", "BasePlateAdvanced"),
            }));

            Categories.Add(new SettingsCategoryVm("桩基",       "○", new[]
            {
                new SettingsGroupVm("桩参数",   "PileParams"),
                new SettingsGroupVm("边距参数", "PileMargin"),
            }));

            Categories.Add(new SettingsCategoryVm("聚类",       "⌘", new[]
            {
                new SettingsGroupVm("聚类参数", "ClusterParams"),
                new SettingsGroupVm("绘图开关", "ClusterDrawSwitch"),
            }));

            Categories.Add(new SettingsCategoryVm("道路",       "≋", new[]
            {
                new SettingsGroupVm("人行横道",     "RoadCrosswalk"),
                new SettingsGroupVm("市政道路 P0", "RoadMunicipal"),
            }));

            Categories.Add(new SettingsCategoryVm("标高",       "⬍", new[]
            {
                new SettingsGroupVm("符号样式", "ElevationSymbol"),
                new SettingsGroupVm("文字样式", "ElevationText"),
            }));

            Categories.Add(new SettingsCategoryVm("尺寸",       "↔", new[]
            {
                new SettingsGroupVm("尺寸参数", "DimParams"),
            }));

            Categories.Add(new SettingsCategoryVm("地脚螺栓",   "◉", new[]
            {
                new SettingsGroupVm("螺栓参数", "AnchorBoltParams"),
            }));

            Categories.Add(new SettingsCategoryVm("设备基础",   "▤", new[]
            {
                new SettingsGroupVm("基础参数", "EquipFoundationParams"),
            }));
        }

        private void OnApplyCurrent()
        {
            StatusMessage = "TODO: 置为当前 (ApplyStyle)";
        }

        private void OnSaveUserSettings()
        {
            StatusMessage = "TODO: 保存用户设置 (hy-settings.json)";
        }

        private void OnRestoreAutoSaved()
        {
            StatusMessage = "TODO: 恢复至自动保存的设置";
        }

        private void OnLoadDefaults()
        {
            StatusMessage = "TODO: 加载初始设置 (重置默认值)";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>设置面板一级目录。</summary>
    public class SettingsCategoryVm
    {
        public string Name { get; }
        public string Icon { get; }
        public ObservableCollection<SettingsGroupVm> Groups { get; } = new ObservableCollection<SettingsGroupVm>();

        public SettingsCategoryVm(string name, string icon, System.Collections.Generic.IEnumerable<SettingsGroupVm> groups)
        {
            Name = name;
            Icon = icon;
            if (groups != null)
                foreach (var g in groups) Groups.Add(g);
        }

        public override string ToString() => Name ?? "(未命名)";
    }

    /// <summary>
    /// 设置面板二级分组（一个 Expander 对应一条）。
    /// GroupKey 供 View 侧 DataTemplateSelector/ContentTemplate 选择具体控件骨架；
    /// 未配模板的 GroupKey 由 View 渲染为"待迁移"占位。
    /// </summary>
    public class SettingsGroupVm : INotifyPropertyChanged
    {
        public string Header { get; }
        public string GroupKey { get; }

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded == value) return; _isExpanded = value; OnPropertyChanged(); }
        }

        public SettingsGroupVm(string header, string groupKey)
        {
            Header = header;
            GroupKey = groupKey;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
