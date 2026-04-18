using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Refactored.Infrastructure.Configuration;

namespace HyCADTool.Refactored.Presentation.ViewModels
{
    /// <summary>
    /// HyB 面板「设置」伪分类使用的总 ViewModel（对应 Blender Preferences）。
    /// 
    /// 阶段 C 改造：本类不再持有自己的参数存储，而是作为壳子持有真实 ViewModel：
    ///   Settings / BaseReinVm / PileVm / ClusterVm
    /// 子 View 通过 DataContext 继承沿逻辑树访问 {Binding Settings.XXX} / {Binding BaseReinVm.XXX}。
    /// 持久化复用原各 VM 内部机制（SettingsPanelViewModel.SaveSettings 等）。
    /// </summary>
    public class HySettingsViewModel : INotifyPropertyChanged
    {
        // ================================================================
        //  1. 持有的真实 ViewModel（壳子模式）
        // ================================================================

        /// <summary>
        /// 样式/钢筋/道路/标高/尺寸通用参数（核心持久化）
        /// 取当前活动文档的 VM；空文档时退化为默认构造实例。
        /// </summary>
        public SettingsPanelViewModel Settings => SettingsPanelViewModel.Current
                                                   ?? _settingsFallback
                                                   ?? (_settingsFallback = new SettingsPanelViewModel());
        private SettingsPanelViewModel _settingsFallback;

        /// <summary>
        /// 底板配筋 ViewModel（DI 注册 InstancePerDependency，此处单例缓存）
        /// </summary>
        public BaseReinPanelViewModel BaseReinVm => _baseReinVm
                                                   ?? (_baseReinVm = ServiceLocator.TryResolve<BaseReinPanelViewModel>());
        private BaseReinPanelViewModel _baseReinVm;

        /// <summary>
        /// 桩基 ViewModel（其自身有 Current 多文档机制）
        /// </summary>
        public PilePanelViewModel PileVm => PilePanelViewModel.Current
                                            ?? _pileVmFallback
                                            ?? (_pileVmFallback = new PilePanelViewModel());
        private PilePanelViewModel _pileVmFallback;

        /// <summary>
        /// 聚类 ViewModel（DI 注册 InstancePerDependency，此处单例缓存）
        /// </summary>
        public ClusterPanelViewModel ClusterVm => _clusterVm
                                                  ?? (_clusterVm = ServiceLocator.TryResolve<ClusterPanelViewModel>()
                                                                   ?? new ClusterPanelViewModel());
        private ClusterPanelViewModel _clusterVm;

        // ================================================================
        //  2. 一级目录 / 二级分组（不变）
        // ================================================================

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

        // ================================================================
        //  3. 状态栏 & 自动保存（阶段 D 再双向绑定到 Settings.AutoSaveEnabled）
        // ================================================================

        private bool _autoSaveEnabled = true;
        public bool AutoSaveEnabled
        {
            get => Settings?.AutoSaveEnabled ?? _autoSaveEnabled;
            set
            {
                _autoSaveEnabled = value;
                if (Settings != null) Settings.AutoSaveEnabled = value;
                OnPropertyChanged();
            }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        // ================================================================
        //  4. 底栏命令（阶段 D 接真逻辑）
        // ================================================================

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

            // 订阅 Settings 变更：StatusMessage 同步（首次访问会惰性创建）
            var settings = Settings;
            if (settings != null)
            {
                settings.PropertyChanged += OnSettingsPropertyChanged;
            }
        }

        private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsPanelViewModel.StatusMessage) && sender is SettingsPanelViewModel s)
            {
                StatusMessage = s.StatusMessage;
            }
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

        // ================================================================
        //  5. 底栏命令实现（阶段 D 接入）
        // ================================================================

        private void OnApplyCurrent()
        {
            var s = Settings;
            if (s == null) { StatusMessage = "无可用设置实例"; return; }
            // ApplyStyleCommand 内部调用 SaveAsDefault → SaveSettings + EnsureStylesApplied
            s.ApplyStyleCommand?.Execute(null);
            StatusMessage = s.StatusMessage;
        }

        private void OnSaveUserSettings()
        {
            var s = Settings;
            if (s == null) { StatusMessage = "无可用设置实例"; return; }
            s.SavePublic();
            StatusMessage = "用户设置已保存 → hy-settings.json";
        }

        private void OnRestoreAutoSaved()
        {
            var s = Settings;
            if (s == null) { StatusMessage = "无可用设置实例"; return; }
            s.ReloadFromDisk();
            StatusMessage = "已从磁盘重载设置";
        }

        private void OnLoadDefaults()
        {
            var s = Settings;
            if (s == null) { StatusMessage = "无可用设置实例"; return; }
            s.ResetCommand?.Execute(null);
            StatusMessage = s.StatusMessage;
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
