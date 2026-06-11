using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Shell.ViewModels
{
    /// <summary>
    /// HyB 面板「设置」伪分类使用的总 ViewModel（对应 Blender Preferences）。
    /// 
    /// 壳子持有 <see cref="SettingsPanelViewModel"/>；业务参数在各自业务面板编辑（钢筋 gj / 聚类 / 底板配筋等）。
    /// 持久化复用 SettingsPanelViewModel.SaveSettings 等。
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
        /// 曾订阅 StatusMessage 的 Settings 实例；随 <see cref="Settings"/> 指向的文档 VM 切换而迁移。
        /// </summary>
        private SettingsPanelViewModel _settingsStatusSubscription;

        // ================================================================
        //  2. 一级目录 / 二级分组
        // ================================================================

        public ObservableCollection<SettingsCategoryVm> Categories { get; } = new ObservableCollection<SettingsCategoryVm>();

        /// <summary>所有分类的分组扁平合并：设置面板「三合一」单滚动列表的数据源。</summary>
        public ObservableCollection<SettingsGroupVm> AllGroups { get; } = new ObservableCollection<SettingsGroupVm>();

        private SettingsCategoryVm _selectedCategory;
        public SettingsCategoryVm SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory == value) return;
                _selectedCategory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedCategoryIndex));
            }
        }

        /// <summary>供 IconTabBar.SelectedIndex 双向绑定。</summary>
        public int SelectedCategoryIndex
        {
            get => _selectedCategory == null ? -1 : Categories.IndexOf(_selectedCategory);
            set
            {
                if (value < 0 || value >= Categories.Count) return;
                SelectedCategory = Categories[value];
            }
        }

        private const int SettingsSearchDebounceMs = 160;
        private readonly DispatcherTimer _settingsSearchDebounceTimer;
        private string _settingsSearchText = string.Empty;

        /// <summary>设置面板顶栏搜索（匹配分类名 / 分组名）。</summary>
        public string SettingsSearchText
        {
            get => _settingsSearchText;
            set
            {
                if (_settingsSearchText == value) return;
                _settingsSearchText = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSettingsSearching));
                ScheduleSettingsFilterRefresh();
            }
        }

        public bool IsSettingsSearching => !string.IsNullOrWhiteSpace(_settingsSearchText);

        /// <summary>搜索命中：扁平「分类 · 分组」列表。</summary>
        public ObservableCollection<SettingsSearchMatchVm> FilteredGroups { get; } = new ObservableCollection<SettingsSearchMatchVm>();

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

            _settingsSearchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SettingsSearchDebounceMs)
            };
            _settingsSearchDebounceTimer.Tick += (_, __) =>
            {
                _settingsSearchDebounceTimer.Stop();
                RefreshSettingsFilter();
            };

            BuildSkeleton();
            SelectedCategory = Categories.Count > 0 ? Categories[0] : null;

            RefreshSettingsBindingsFromDocument();

            try
            {
                var dm = AcApp.DocumentManager;
                dm.DocumentActivated -= OnDocumentManagerSurfaceChanged;
                dm.DocumentToBeDestroyed -= OnDocumentManagerSurfaceChanged;
                dm.DocumentActivated += OnDocumentManagerSurfaceChanged;
                dm.DocumentToBeDestroyed += OnDocumentManagerSurfaceChanged;
            }
            catch
            {
                // 非 AutoCAD 宿主 / 设计器
            }
        }

        /// <summary>
        /// <see cref="Settings"/> 由 Current 与 fallback 合成，引用随文档切换而变。
        /// 若不单独 <c>OnPropertyChanged(nameof(Settings))</c>，WPF 仍把副比例等子绑定挂在旧实例上，
        /// 会出现「勾选启用副比例后输入框仍灰、无法改数值」等现象。
        /// </summary>
        private void OnDocumentManagerSurfaceChanged(object sender, Autodesk.AutoCAD.ApplicationServices.DocumentCollectionEventArgs e)
            => RefreshSettingsBindingsFromDocument();

        /// <summary>活动文档切换时刷新设置绑定（供 HyBlenderPanelViewModel 显式通知）。</summary>
        public void RefreshSettingsBindingsFromDocument()
        {
            OnPropertyChanged(nameof(Settings));
            AttachSettingsStatusSubscription();
        }

        private void AttachSettingsStatusSubscription()
        {
            var s = Settings;
            if (ReferenceEquals(s, _settingsStatusSubscription)) return;

            if (_settingsStatusSubscription != null)
                _settingsStatusSubscription.PropertyChanged -= OnSettingsPropertyChanged;

            _settingsStatusSubscription = s;

            if (s != null)
                s.PropertyChanged += OnSettingsPropertyChanged;
        }

        private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsPanelViewModel.StatusMessage) && sender is SettingsPanelViewModel s)
            {
                StatusMessage = s.StatusMessage;
            }
        }

        private void ScheduleSettingsFilterRefresh()
        {
            _settingsSearchDebounceTimer.Stop();
            _settingsSearchDebounceTimer.Start();
        }

        private void RefreshSettingsFilter()
        {
            FilteredGroups.Clear();
            if (!IsSettingsSearching) return;

            var q = _settingsSearchText.Trim().ToLowerInvariant();
            var qPinyin = PinyinHelper.GetFirstLetters(_settingsSearchText);

            foreach (var cat in Categories)
            {
                foreach (var grp in cat.Groups)
                {
                    if (MatchesSettingsQuery(cat.Name, grp.Header, q, qPinyin))
                        FilteredGroups.Add(new SettingsSearchMatchVm(cat, grp));
                }
            }
        }

        private static bool MatchesSettingsQuery(string category, string header, string q, string qPinyin)
        {
            var cat = (category ?? string.Empty).ToLowerInvariant();
            var hdr = (header ?? string.Empty).ToLowerInvariant();
            var combined = cat + " " + hdr;
            if (combined.Contains(q)) return true;
            var letters = PinyinHelper.GetFirstLetters(combined);
            return !string.IsNullOrEmpty(qPinyin) && letters.Contains(qPinyin);
        }

        private void BuildSkeleton()
        {
            Categories.Add(new SettingsCategoryVm("界面", "◐", new[]
            {
                new SettingsGroupVm("主题", "Theme"),
                new SettingsGroupVm("尺寸", "UiScale"),
            }));

            Categories.Add(new SettingsCategoryVm("设置", "⚙", new[]
            {
                new SettingsGroupVm("图层",     "LayerCatalog"),
                new SettingsGroupVm("文字样式", "TextStyle"),
                new SettingsGroupVm("标注样式", "DimStyle"),
                new SettingsGroupVm("引线样式", "MLeaderStyle"),
                new SettingsGroupVm("表格样式", "TableStyle"),
            }));

            Categories.Add(new SettingsCategoryVm("设备基础", "▤", new[]
            {
                new SettingsGroupVm("基础参数", "EquipFoundationParams"),
            }));

            AllGroups.Clear();
            foreach (var cat in Categories)
                foreach (var grp in cat.Groups)
                    AllGroups.Add(grp);
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

            // 显式保存一份到用户文档（默认目录 = 我的文档），与自动保存的 %APPDATA% 存储互不影响。
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "hy-settings.json",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = "HyCAD 设置 (*.json)|*.json|所有文件 (*.*)|*.*",
                DefaultExt = ".json",
                AddExtension = true,
                Title = "保存用户设置（默认放在用户文档）"
            };
            if (dlg.ShowDialog() != true) { StatusMessage = "已取消保存"; return; }
            try
            {
                s.SaveSettingsToFile(dlg.FileName);
                StatusMessage = $"已保存到 {dlg.FileName}";
            }
            catch (System.Exception ex) { StatusMessage = $"保存失败: {ex.Message}"; }
        }

        private void OnRestoreAutoSaved()
        {
            var s = Settings;
            if (s == null) { StatusMessage = "无可用设置实例"; return; }

            // 调取用户设置：默认从用户文档目录选取 json 恢复。
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = "HyCAD 设置 (*.json)|*.json|所有文件 (*.*)|*.*",
                CheckFileExists = true,
                Title = "调取用户设置"
            };
            if (dlg.ShowDialog() != true) { StatusMessage = "已取消调取"; return; }
            try
            {
                s.LoadSettingsFromFile(dlg.FileName);
                StatusMessage = $"已从 {dlg.FileName} 调取";
            }
            catch (System.Exception ex) { StatusMessage = $"加载失败: {ex.Message}"; }
        }

        private void OnLoadDefaults()
        {
            var s = Settings;
            if (s == null) { StatusMessage = "无可用设置实例"; return; }
            // 1) Reset 改 VM + 写 JSON；2) 立即 Apply 让 AutoCAD 样式回到默认
            s.ResetCommand?.Execute(null);
            s.ApplyStyleCommand?.Execute(null);
            StatusMessage = s.StatusMessage;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>设置搜索扁平命中项。</summary>
    public class SettingsSearchMatchVm
    {
        public SettingsCategoryVm Category { get; }
        public SettingsGroupVm Group { get; }
        public string DisplayLabel => $"{Category.Name} · {Group.Header}";

        public SettingsSearchMatchVm(SettingsCategoryVm category, SettingsGroupVm group)
        {
            Category = category;
            Group = group;
        }
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
