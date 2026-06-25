using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using Autofac;
using HyCADTool.App.Bootstrap;
using HyCADTool.Shell.Commands;
using HyCADTool.Shell.Services;

namespace HyCADTool.Shell.ViewModels
{
    /// <summary>
    /// Blender 面板的总 ViewModel（唯一 PaletteSet，承载设置 / 过滤 / 命令分类 Tab）。
    /// 负责：
    /// - 从 <see cref="CommandCatalog.GroupByCategory"/> 拉分组数据，转成 <see cref="CategoryTabVm"/>。
    /// - 维护当前选中的分类（左侧 Tab）。
    /// - 维护搜索框文字，用关键词 + 简易拼音首字母过滤命令。
    /// </summary>
    public class HyBlenderPanelViewModel : INotifyPropertyChanged
    {
        private const int SearchDebounceMs = 160;

        /// <summary>命令编辑器左侧 Tab（仅 commands.json 业务分类，不含伪分类）。</summary>
        public ObservableCollection<CategoryTabVm> Tabs { get; } = new ObservableCollection<CategoryTabVm>();

        /// <summary>编辑器类型下拉菜单分栏。</summary>
        public ObservableCollection<EditorMenuSectionVm> EditorMenuSections { get; } = new ObservableCollection<EditorMenuSectionVm>();

        private string _selectedEditorKey = CommandsEditorKey;
        /// <summary>当前激活的编辑器 Key（commands / __preferences__ / …）。</summary>
        public string SelectedEditorKey
        {
            get => _selectedEditorKey;
            set => SelectEditor(value);
        }

        private CategoryTabVm _selectedTab;
        public CategoryTabVm SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (_selectedTab == value) return;
                _selectedTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedTabIndex));
                OnPropertyChanged(nameof(IsReinMode));
                OnPropertyChanged(nameof(IsG101Mode));
                OnPropertyChanged(nameof(IsG16Mode));
                OnPropertyChanged(nameof(IsStructure3DMode));
                OnPropertyChanged(nameof(IsPileMode));
                OnPropertyChanged(nameof(PilePanelHost));
                OnPropertyChanged(nameof(IsHyTableMode));
                OnPropertyChanged(nameof(HyTablePanelHost));
                OnPropertyChanged(nameof(IsFilterMode));
                OnPropertyChanged(nameof(FilterPanelHost));
                OnPropertyChanged(nameof(IsCommandListMode));
                OnPropertyChanged(nameof(ShowScalePanel));
                OnPropertyChanged(nameof(BreadcrumbText));
                RefreshFilterNow("tab-switch");
                if (value?.Key == G16TabKey)
                    HyCADTool.Features.G16.ViewModels.G16PanelViewModel.Current?.RefreshCatalogTree();
            }
        }

        /// <summary>
        /// 与 <see cref="SelectedTab"/> 双向同步的索引；供 <c>IconTabBar.SelectedIndex</c> 绑定。
        /// </summary>
        public int SelectedTabIndex
        {
            get => _selectedTab == null ? -1 : Tabs.IndexOf(_selectedTab);
            set
            {
                if (value < 0 || value >= Tabs.Count)
                {
                    SelectedTab = null;
                    return;
                }
                SelectedTab = Tabs[value];
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSearching));
                ScheduleFilterRefresh();
            }
        }

        public bool IsSearching => !string.IsNullOrWhiteSpace(_searchText);

        /// <summary>命令编辑器 Key。</summary>
        public const string CommandsEditorKey = "commands";

        /// <summary>当前是否为命令编辑器。</summary>
        public bool IsCommandEditorMode => _selectedEditorKey == CommandsEditorKey;

        /// <summary>命令编辑器左侧 IconTabBar 是否可见。</summary>
        public bool ShowCommandIconTabBar => IsCommandEditorMode;

        /// <summary>顶栏搜索框是否可见（设置 / 命令列表）。</summary>
        public bool IsHeaderSearchVisible => IsPreferencesMode || IsCommandEditorMode;

        /// <summary>当前选中的是否为「设置」编辑器。</summary>
        public bool IsPreferencesMode => _selectedEditorKey == PreferencesTabKey;

        /// <summary>当前选中的是否为「过滤」伪分类 Tab（位于命令编辑器内，常用之下、钢筋之上）。</summary>
        public bool IsFilterMode => IsCommandEditorMode && _selectedTab != null && _selectedTab.Key == FilterTabKey;

        /// <summary>当前选中的是否为「海绵城市」编辑器。</summary>
        public bool IsSpongeCityMode => _selectedEditorKey == SpongeCityTabKey;

        /// <summary>当前选中的是否为「钢筋」业务分类 Tab。</summary>
        public bool IsReinMode => IsCommandEditorMode && _selectedTab != null && _selectedTab.Key == ReinTabKey;

        /// <summary>当前选中的是否为「结构构件」伪分类 Tab（钢筋之后）。</summary>
        public bool IsG101Mode => IsCommandEditorMode && _selectedTab != null && _selectedTab.Key == G101TabKey;

        /// <summary>当前选中的是否为「16G101」伪分类 Tab（结构构件之后）。</summary>
        public bool IsG16Mode => IsCommandEditorMode && _selectedTab != null && _selectedTab.Key == G16TabKey;

        /// <summary>当前选中的是否为「3D结构」伪分类 Tab（16G101 之后）。</summary>
        public bool IsStructure3DMode => IsCommandEditorMode && _selectedTab != null && _selectedTab.Key == Structure3DTabKey;

        /// <summary>当前选中的是否为「桩基」业务分类 Tab。</summary>
        public bool IsPileMode => IsCommandEditorMode && _selectedTab != null && _selectedTab.Key == PileTabKey;

        /// <summary>当前选中的是否为「表格」业务分类 Tab（AC11 填值面板）。</summary>
        public bool IsHyTableMode => IsCommandEditorMode && _selectedTab != null && _selectedTab.Key == HyTableTabKey;

        /// <summary>当前选中的是否为「基础钢筋」编辑器。</summary>
        public bool IsBaseReinMode => _selectedEditorKey == BaseReinTabKey;

        /// <summary>当前选中的是否为「螺栓聚类与基础标注」编辑器。</summary>
        public bool IsClusterMode => _selectedEditorKey == ClusterTabKey;

        /// <summary>命令编辑器内：普通命令列表（非钢筋/结构构件/3D结构/桩基/过滤独立面板）。</summary>
        public bool IsCommandListMode => IsCommandEditorMode && !IsReinMode && !IsG101Mode && !IsG16Mode && !IsStructure3DMode && !IsPileMode && !IsHyTableMode && !IsFilterMode;

        /// <summary>出图比例区仅在「命令 · 常用」Tab 顶部显示。</summary>
        public bool ShowScalePanel => IsCommandListMode && _selectedTab != null && _selectedTab.Key == CommonTabKey;

        /// <summary>顶栏面包屑文本。</summary>
        public string BreadcrumbText
        {
            get
            {
                if (IsPreferencesMode)
                {
                    var cat = PreferencesVm?.SelectedCategory?.Name;
                    return string.IsNullOrEmpty(cat) ? "设置" : $"设置 · {cat}";
                }
                if (IsFilterMode) return "命令 · 过滤";
                if (IsSpongeCityMode) return "海绵城市";
                if (IsBaseReinMode) return "基础钢筋";
                if (IsClusterMode) return "螺栓聚类与基础标注";
                if (IsReinMode) return "命令 · 钢筋";
                if (IsG101Mode) return "命令 · 结构构件";
                if (IsG16Mode) return "命令 · 16G101";
                if (IsStructure3DMode) return "命令 · 3D结构";
                if (IsPileMode) return "命令 · 桩基";
                if (IsHyTableMode) return "命令 · 表格";
                if (_selectedTab != null) return $"命令 · {_selectedTab.Name}";
                return "命令";
            }
        }

        /// <summary>当前编辑器图标（下拉按钮显示）。</summary>
        public string CurrentEditorIcon => ResolveEditorIcon(_selectedEditorKey);

        public ICommand SelectEditorCommand { get; }

        /// <summary>惰性加载宿主：仅对应模式激活时非 null，供 ContentControl 延迟实例化子面板。</summary>
        public HyBlenderPanelViewModel PreferencesPanelHost => IsPreferencesMode ? this : null;
        public HyBlenderPanelViewModel FilterPanelHost => IsFilterMode ? this : null;
        public HyBlenderPanelViewModel SpongeCityPanelHost => IsSpongeCityMode ? this : null;
        public HyBlenderPanelViewModel PilePanelHost => IsPileMode ? this : null;
        public HyBlenderPanelViewModel HyTablePanelHost => IsHyTableMode ? this : null;
        public HyBlenderPanelViewModel BaseReinPanelHost => IsBaseReinMode ? this : null;
        public HyBlenderPanelViewModel ClusterPanelHost => IsClusterMode ? this : null;

        /// <summary>设置面板的 ViewModel，首次切入「设置」时才创建。</summary>
        private HySettingsViewModel _preferencesVm;
        public HySettingsViewModel PreferencesVm
        {
            get
            {
                if (_preferencesVm == null)
                {
                    _preferencesVm = new HySettingsViewModel();
                    _preferencesVm.PropertyChanged += OnPreferencesVmPropertyChanged;
                }
                return _preferencesVm;
            }
        }

        private void OnPreferencesVmPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HySettingsViewModel.SelectedCategory))
                OnPropertyChanged(nameof(BreadcrumbText));
        }

        /// <summary>过滤面板的 ViewModel，首次切入「过滤」时才创建。</summary>
        private FilterPanelViewModel _filterVm;
        public FilterPanelViewModel FilterVm
            => _filterVm ?? (_filterVm = new FilterPanelViewModel());

        /// <summary>海绵城市面板的 ViewModel，绑定当前文档的 VM。</summary>
        public HyCADTool.Features.SpongeCity.ViewModels.SpongeCityPanelViewModel SpongeCityVm
            => HyCADTool.Features.SpongeCity.ViewModels.SpongeCityPanelViewModel.Current;

        /// <summary>钢筋面板的设置 ViewModel，直接服务 gj / gb 等钢筋命令。</summary>
        public SettingsPanelViewModel ReinVm => SettingsPanelViewModel.Current;

        /// <summary>结构构件（22G101）面板 ViewModel。</summary>
        public HyCADTool.Features.G101.ViewModels.G101PanelViewModel G101Vm
            => HyCADTool.Features.G101.ViewModels.G101PanelViewModel.Current;

        /// <summary>16G101 参数化大样面板 ViewModel。</summary>
        public HyCADTool.Features.G16.ViewModels.G16PanelViewModel G16Vm
            => HyCADTool.Features.G16.ViewModels.G16PanelViewModel.Current;

        /// <summary>3D结构（HY3 工作流）面板 ViewModel。</summary>
        public HyCADTool.Features.Elevation.ViewModels.Structure3DPanelViewModel Structure3DVm
            => HyCADTool.Features.Elevation.ViewModels.Structure3DPanelViewModel.Current;

        /// <summary>桩基面板的 ViewModel（其自身有 Current 多文档机制），承载桩参数与桩基命令。</summary>
        public HyCADTool.Features.Pile.ViewModels.PilePanelViewModel PileVm
            => HyCADTool.Features.Pile.ViewModels.PilePanelViewModel.Current;

        /// <summary>基础钢筋面板的 ViewModel（需 DI 注入 IBaseReinforcementService）。</summary>
        private HyCADTool.Features.BaseRein.ViewModels.BaseReinPanelViewModel _baseReinVm;
        public HyCADTool.Features.BaseRein.ViewModels.BaseReinPanelViewModel BaseReinVm
            => _baseReinVm ?? (_baseReinVm = ResolveBaseReinVm());

        /// <summary>螺栓聚类与基础标注面板的 ViewModel。</summary>
        public ClusterPanelViewModel ClusterVm => ClusterPanelViewModel.Current;

        /// <summary>HyTable 填值面板 ViewModel（AC11）。</summary>
        private HyCADTool.Features.Tables.ViewModels.TablePanelViewModel _tablePanelVm;
        public HyCADTool.Features.Tables.ViewModels.TablePanelViewModel TablePanelVm
            => _tablePanelVm ?? (_tablePanelVm = ResolveTablePanelVm());

        /// <summary>「设置」伪分类的稳定 Key。</summary>
        public const string PreferencesTabKey = "__preferences__";

        /// <summary>「过滤」伪分类的稳定 Key。</summary>
        public const string FilterTabKey = "__filter__";

        /// <summary>「海绵城市」伪分类的稳定 Key。</summary>
        public const string SpongeCityTabKey = "__sponge__";

        /// <summary>「钢筋」业务分类的稳定 Key（来自 commands.json 的 category）。</summary>
        public const string ReinTabKey = "钢筋";

        /// <summary>「结构构件」伪分类的稳定 Key（22G101 参数化大样）。</summary>
        public const string G101TabKey = "__g101__";

        /// <summary>「16G101」伪分类的稳定 Key。</summary>
        public const string G16TabKey = "__g16__";

        /// <summary>「3D结构」伪分类的稳定 Key（HY3 三维基础模型工作流）。</summary>
        public const string Structure3DTabKey = "__structure3d__";

        /// <summary>「桩基」业务分类的稳定 Key（来自 commands.json 的 category）。</summary>
        public const string PileTabKey = "桩基";

        /// <summary>「表格」业务分类的稳定 Key（commands.json category）。</summary>
        public const string HyTableTabKey = "表格";

        /// <summary>「基础钢筋」伪分类的稳定 Key。</summary>
        public const string BaseReinTabKey = "__baserein__";

        /// <summary>「螺栓聚类与基础标注」伪分类的稳定 Key。</summary>
        public const string ClusterTabKey = "__cluster__";

        /// <summary>「常用」业务分类的稳定 Key（来自 commands.json 的 category）。</summary>
        public const string CommonTabKey = "常用";

        /// <summary>「hyob」业务分类的稳定 Key（从左侧 Tab 收入编辑器菜单「业务」区）。</summary>
        public const string HyobTabKey = "hyob";

        /// <summary>合并 Tab：沉降/标高/尺寸标注/地脚螺栓/设备基础/图框视口/块引线/导出说明/多段线垫层。</summary>
        public const string MergedDrawingToolsTabKey = "绘图工具";

        private static readonly string[] MergedPanelSectionOrder =
        {
            "沉降", "标高", "尺寸标注", "地脚螺栓", "设备基础", "图框视口", "块引线", "导出说明", "多段线垫层",
        };

        private static readonly HashSet<string> MergedSourceCategories = new HashSet<string>(StringComparer.Ordinal)
        {
            "沉降", "标高", "尺寸标注", "地脚螺栓", "设备基础", "图框视口", "块引线", "导出说明", "多段线垫层", "文字编辑",
        };

        /// <summary>过滤后的当前 Tab 命令（供 View 的 ListBox/ItemsControl 绑定）。</summary>
        public ObservableCollection<CommandItemVm> FilteredItems { get; } = new ObservableCollection<CommandItemVm>();

        /// <summary>全局搜索结果（跨所有分类扁平列出），仅 IsSearching==true 时有效。</summary>
        public ObservableCollection<CommandItemVm> GlobalSearchResults { get; } = new ObservableCollection<CommandItemVm>();

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        /// <summary>命令搜索扁平索引服务（启动时构建一次，后续 RefreshFilter 线性扫描）。</summary>
        private readonly CommandSearchService _searchService = new CommandSearchService();
        private readonly DispatcherTimer _searchDebounceTimer;

        public HyBlenderPanelViewModel()
        {
            // 经 SelectTab 路由：编辑器 Key 走 SelectEditor，业务分类 Key（如 hyob）走命令 Tab 选中。
            SelectEditorCommand = new RelayCommand<string>(SelectTab);
            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SearchDebounceMs)
            };
            _searchDebounceTimer.Tick += OnSearchDebounceTick;
            BuildEditorMenu();
            LoadFromCommandTable();
        }

        /// <summary>切换到指定编辑器（下拉菜单 / SelectTab 伪 Key 入口）。</summary>
        public void SelectEditor(string editorKey)
        {
            if (string.IsNullOrEmpty(editorKey)) return;
            if (_selectedEditorKey == editorKey) return;

            _selectedEditorKey = editorKey;
            OnPropertyChanged(nameof(SelectedEditorKey));
            NotifyEditorModeChanged();

            if (editorKey == CommandsEditorKey && _selectedTab == null && Tabs.Count > 0)
                SelectedTab = Tabs[0];

            RefreshFilterNow("editor-switch");
        }

        private void NotifyEditorModeChanged()
        {
            OnPropertyChanged(nameof(IsCommandEditorMode));
            OnPropertyChanged(nameof(ShowCommandIconTabBar));
            OnPropertyChanged(nameof(IsHeaderSearchVisible));
            OnPropertyChanged(nameof(IsPreferencesMode));
            OnPropertyChanged(nameof(IsFilterMode));
            OnPropertyChanged(nameof(IsSpongeCityMode));
            OnPropertyChanged(nameof(IsReinMode));
            OnPropertyChanged(nameof(IsG101Mode));
            OnPropertyChanged(nameof(IsG16Mode));
            OnPropertyChanged(nameof(IsStructure3DMode));
            OnPropertyChanged(nameof(IsPileMode));
            OnPropertyChanged(nameof(IsHyTableMode));
            OnPropertyChanged(nameof(IsBaseReinMode));
            OnPropertyChanged(nameof(IsClusterMode));
            OnPropertyChanged(nameof(IsCommandListMode));
            OnPropertyChanged(nameof(ShowScalePanel));
            OnPropertyChanged(nameof(BreadcrumbText));
            OnPropertyChanged(nameof(CurrentEditorIcon));
            OnPropertyChanged(nameof(PreferencesPanelHost));
            OnPropertyChanged(nameof(FilterPanelHost));
            OnPropertyChanged(nameof(SpongeCityPanelHost));
            OnPropertyChanged(nameof(PilePanelHost));
            OnPropertyChanged(nameof(HyTablePanelHost));
            OnPropertyChanged(nameof(BaseReinPanelHost));
            OnPropertyChanged(nameof(ClusterPanelHost));
        }

        private void BuildEditorMenu()
        {
            EditorMenuSections.Clear();

            var general = new EditorMenuSectionVm("常规");
            general.Items.Add(new EditorMenuItemVm(CommandsEditorKey, "命令", "★"));
            general.Items.Add(new EditorMenuItemVm(PreferencesTabKey, "设置", "⚙"));
            EditorMenuSections.Add(general);

            var business = new EditorMenuSectionVm("业务");
            business.Items.Add(new EditorMenuItemVm(SpongeCityTabKey, "海绵城市", "≈"));
            business.Items.Add(new EditorMenuItemVm(BaseReinTabKey, "基础钢筋", "▦"));
            business.Items.Add(new EditorMenuItemVm(ClusterTabKey, "螺栓聚类", "◉"));
            business.Items.Add(new EditorMenuItemVm(HyobTabKey, "hyob", "◷"));
            EditorMenuSections.Add(business);
        }

        private static string ResolveEditorIcon(string editorKey)
        {
            switch (editorKey)
            {
                case CommandsEditorKey: return "★";
                case PreferencesTabKey: return "⚙";
                case SpongeCityTabKey: return "≈";
                case BaseReinTabKey: return "▦";
                case ClusterTabKey: return "◉";
                default: return "·";
            }
        }

        private static bool IsEditorKey(string key)
        {
            return key == CommandsEditorKey
                || key == PreferencesTabKey
                || key == SpongeCityTabKey
                || key == BaseReinTabKey
                || key == ClusterTabKey;
        }

        /// <summary>
        /// 活动文档切换时由 <see cref="PanelManager"/> 调用，刷新各模式面板 VM 绑定。
        /// </summary>
        public void NotifyActiveDocumentChanged()
        {
            OnPropertyChanged(nameof(SpongeCityVm));
            OnPropertyChanged(nameof(ReinVm));
            OnPropertyChanged(nameof(G101Vm));
            OnPropertyChanged(nameof(G16Vm));
            OnPropertyChanged(nameof(Structure3DVm));
            OnPropertyChanged(nameof(PileVm));
            OnPropertyChanged(nameof(TablePanelVm));
            OnPropertyChanged(nameof(BaseReinVm));
            OnPropertyChanged(nameof(ClusterVm));
            _preferencesVm?.RefreshSettingsBindingsFromDocument();
        }

        /// <summary>
        /// 按 Key 选中 Tab（找不到时退为 FirstOrDefault，避免空视图）。
        /// 常见 Key：<see cref="PreferencesTabKey"/>、g.Category（如「常用」「钢筋」…）。
        /// </summary>
        public void SelectTab(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            if (IsEditorKey(key))
            {
                SelectEditor(key);
                return;
            }

            // hyob 收进编辑器菜单「业务」区，不在左侧 Tab 栏，但内容仍走命令列表渲染。
            if (string.Equals(key, HyobTabKey, StringComparison.OrdinalIgnoreCase) && _hyobTab != null)
            {
                SelectEditor(CommandsEditorKey);
                SelectedTab = _hyobTab;
                return;
            }

            var t = Tabs.FirstOrDefault(x => x.Key == key);
            if (t != null)
            {
                SelectEditor(CommandsEditorKey);
                SelectedTab = t;
            }
            else if (string.Equals(key, CommandsEditorKey, StringComparison.Ordinal))
            {
                SelectEditor(CommandsEditorKey);
            }
            else
            {
                SelectEditor(CommandsEditorKey);
                SelectedTab = Tabs.FirstOrDefault();
            }
        }

        /// <summary>hyob 分类 Tab：不进左侧 Tab 栏，由编辑器菜单「业务」区经 <see cref="SelectTab"/> 选中。</summary>
        private CategoryTabVm _hyobTab;

        /// <summary>从 CommandCatalog 重新拉分组（可供外部在 JSON 变化后触发刷新）。</summary>
        public void LoadFromCommandTable()
        {
            Tabs.Clear();
            _hyobTab = null;
            try
            {
                var groups = CommandCatalog.GroupByCategory();
                var mergedBuckets = new Dictionary<string, List<CommandItemVm>>(StringComparer.Ordinal);
                var normalTabs = new List<CategoryTabVm>();
                int totalCommands = 0;

                foreach (var g in groups)
                {
                    if (string.Equals(g.Category, "海绵命令", StringComparison.Ordinal))
                        continue;

                    if (MergedSourceCategories.Contains(g.Category))
                    {
                        var section = MapToMergedSection(g.Category);
                        if (!mergedBuckets.TryGetValue(section, out var list))
                        {
                            list = new List<CommandItemVm>();
                            mergedBuckets[section] = list;
                        }
                        foreach (var it in g.Items)
                            list.Add(new CommandItemVm(it));
                        totalCommands += g.Items.Count;
                        continue;
                    }

                    var tab = new CategoryTabVm
                    {
                        Key  = g.Category,
                        Name = g.Category,
                        Icon = PickCategoryIcon(g.Category),
                    };
                    foreach (var it in g.Items)
                    {
                        // HyB 即本面板自身的开关命令，不在面板内重复展示
                        if (string.Equals(it.Key, "HyB", StringComparison.OrdinalIgnoreCase))
                            continue;
                        tab.Items.Add(new CommandItemVm(it));
                    }
                    if (string.Equals(g.Category, "道路", StringComparison.Ordinal))
                        FillRoadPanelGroups(tab);

                    if (string.Equals(g.Category, HyobTabKey, StringComparison.OrdinalIgnoreCase))
                    {
                        _hyobTab = tab;   // 收进「业务」菜单，不占左侧 Tab 位
                    }
                    else
                    {
                        normalTabs.Add(tab);
                    }
                    totalCommands += tab.Items.Count;
                }

                // 「过滤」伪分类 Tab：常用之下、钢筋之上
                var filterTab = new CategoryTabVm
                {
                    Key  = FilterTabKey,
                    Name = "过滤",
                    Icon = "⧉",
                };
                int commonIdx = normalTabs.FindIndex(
                    t => string.Equals(t.Key, CommonTabKey, StringComparison.Ordinal));
                normalTabs.Insert(commonIdx >= 0 ? commonIdx + 1 : 0, filterTab);

                // 「结构构件」伪分类 Tab：钢筋之后
                var g101Tab = new CategoryTabVm
                {
                    Key  = G101TabKey,
                    Name = "结构构件",
                    Icon = "▣",
                };
                int reinIdx = normalTabs.FindIndex(
                    t => string.Equals(t.Key, ReinTabKey, StringComparison.Ordinal));
                normalTabs.Insert(reinIdx >= 0 ? reinIdx + 1 : normalTabs.Count, g101Tab);

                // 「3D结构」伪分类 Tab：结构构件之后
                var structure3DTab = new CategoryTabVm
                {
                    Key  = Structure3DTabKey,
                    Name = "3D结构",
                    Icon = "⬢",
                };
                var g16Tab = new CategoryTabVm
                {
                    Key  = G16TabKey,
                    Name = "16G101",
                    Icon = "▤",
                };
                int g101Idx = normalTabs.IndexOf(g101Tab);
                normalTabs.Insert(g101Idx >= 0 ? g101Idx + 1 : normalTabs.Count, g16Tab);

                int g16Idx = normalTabs.IndexOf(g16Tab);
                normalTabs.Insert(g16Idx >= 0 ? g16Idx + 1 : normalTabs.Count, structure3DTab);

                var mergedTab = mergedBuckets.Count > 0
                    ? CreateMergedDrawingToolsTab(mergedBuckets)
                    : null;

                foreach (var tab in normalTabs)
                {
                    Tabs.Add(tab);
                    if (mergedTab != null
                        && string.Equals(tab.Key, PileTabKey, StringComparison.Ordinal))
                    {
                        Tabs.Add(mergedTab);
                        mergedTab = null;
                    }
                }

                if (mergedTab != null)
                    Tabs.Add(mergedTab);

                StatusMessage = $"{Tabs.Count} 个分类，{totalCommands} 个命令";
            }
            catch (System.Exception ex)
            {
                StatusMessage = "读取 commands.json 失败：" + ex.Message;
            }

            // 每次 Tabs 重建后刷新扁平索引（hyob 虽不在 Tab 栏，仍要可搜索）
            _searchService.Rebuild(_hyobTab == null
                ? (IEnumerable<CategoryTabVm>)Tabs
                : Tabs.Concat(new[] { _hyobTab }));

            if (_selectedEditorKey == CommandsEditorKey)
                SelectedTab = Tabs.FirstOrDefault();
        }

        private static string MapToMergedSection(string category)
        {
            if (string.Equals(category, "文字编辑", StringComparison.Ordinal))
                return "导出说明";
            return category;
        }

        private static CategoryTabVm CreateMergedDrawingToolsTab(
            Dictionary<string, List<CommandItemVm>> buckets)
        {
            var tab = new CategoryTabVm
            {
                Key  = MergedDrawingToolsTabKey,
                Name = MergedDrawingToolsTabKey,
                Icon = PickCategoryIcon(MergedDrawingToolsTabKey),
            };

            var first = true;
            foreach (var name in MergedPanelSectionOrder)
            {
                if (!buckets.TryGetValue(name, out var list) || list.Count == 0) continue;
                list.Sort(CompareCommandItems);
                var section = new CommandSectionVm
                {
                    Header = name,
                    IsExpanded = first,
                };
                first = false;
                foreach (var vm in list)
                {
                    section.Items.Add(vm);
                    tab.Items.Add(vm);
                }
                tab.RoadPanelGroups.Add(section);
            }

            return tab;
        }

        private void OnSearchDebounceTick(object sender, EventArgs e)
        {
            _searchDebounceTimer.Stop();
            RefreshFilterNow("search-debounce");
        }

        private void ScheduleFilterRefresh()
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private void RefreshFilterNow(string reason)
        {
            var sw = Stopwatch.StartNew();
            FilteredItems.Clear();
            GlobalSearchResults.Clear();

            if (_selectedTab == null)
            {
                LogFilterPerf(reason, sw.ElapsedMilliseconds, 0, 0);
                return;
            }
            if (!IsCommandEditorMode)
            {
                LogFilterPerf(reason, sw.ElapsedMilliseconds, 0, 0);
                return;
            }
            if (IsReinMode || IsG101Mode || IsG16Mode || IsStructure3DMode || IsPileMode || IsHyTableMode || IsFilterMode)
            {
                LogFilterPerf(reason, sw.ElapsedMilliseconds, 0, 0);
                return;
            }

            if (!IsSearching)
            {
                foreach (var it in _selectedTab.Items)
                    FilteredItems.Add(it);
                LogFilterPerf(reason, sw.ElapsedMilliseconds, FilteredItems.Count, 0);
                return;
            }

            // 委托给扁平索引服务：当前 Tab 内过滤 + 全局搜索
            foreach (var it in _searchService.SearchInTab(_selectedTab, _searchText))
                FilteredItems.Add(it);

            foreach (var it in _searchService.SearchAll(_searchText))
                GlobalSearchResults.Add(it);

            LogFilterPerf(reason, sw.ElapsedMilliseconds, FilteredItems.Count, GlobalSearchResults.Count);
        }

        private static void LogFilterPerf(string reason, long elapsedMs, int tabCount, int globalCount)
        {
            // 仅写 Debug 输出：用于采集“搜索输入/切 Tab”基线，不阻塞命令行 UI。
            Debug.WriteLine(
                $"[HyPanel.Filter] reason={reason}, elapsedMs={elapsedMs}, tabItems={tabCount}, globalItems={globalCount}");
        }

        /// <summary>Hy 面板「道路」Tab 内五区顺序（与 commands.json roadPanelGroup 一致）。</summary>
        private static readonly string[] RoadPanelGroupOrder =
        {
            "工程", "路线", "纵断", "道路", "工具",
        };

        private static void FillRoadPanelGroups(CategoryTabVm tab)
        {
            tab.RoadPanelGroups.Clear();
            var buckets = new Dictionary<string, List<CommandItemVm>>(StringComparer.Ordinal);
            foreach (var vm in tab.Items)
            {
                var grp = vm.RoadPanelGroup;
                if (string.IsNullOrWhiteSpace(grp)) continue;
                if (!buckets.TryGetValue(grp, out var list))
                {
                    list = new List<CommandItemVm>();
                    buckets[grp] = list;
                }
                list.Add(vm);
            }

            var first = true;
            foreach (var name in RoadPanelGroupOrder)
            {
                if (!buckets.TryGetValue(name, out var list) || list.Count == 0) continue;
                list.Sort(CompareCommandItems);
                var section = new CommandSectionVm
                {
                    Header = name,
                    IsExpanded = first,
                };
                first = false;
                foreach (var vm in list)
                    section.Items.Add(vm);
                tab.RoadPanelGroups.Add(section);
            }
        }

        private static int CompareCommandItems(CommandItemVm a, CommandItemVm b)
        {
            int c = a.Order.CompareTo(b.Order);
            if (c != 0) return c;
            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
        }

        private static HyCADTool.Features.BaseRein.ViewModels.BaseReinPanelViewModel ResolveBaseReinVm()
        {
            try
            {
                var container = ServiceLocator.Container;
                if (container != null)
                    return container.Resolve<HyCADTool.Features.BaseRein.ViewModels.BaseReinPanelViewModel>();
            }
            catch
            {
                // 设计器或容器未初始化时忽略
            }
            return null;
        }

        private static HyCADTool.Features.Tables.ViewModels.TablePanelViewModel ResolveTablePanelVm()
        {
            try
            {
                var container = ServiceLocator.Container;
                if (container != null)
                    return container.Resolve<HyCADTool.Features.Tables.ViewModels.TablePanelViewModel>();
            }
            catch
            {
                // 设计器或容器未初始化时忽略
            }
            return new HyCADTool.Features.Tables.ViewModels.TablePanelViewModel();
        }

        private static string PickCategoryIcon(string category)
        {
            switch (category)
            {
                case "常用":       return "★";
                case "钢筋":       return "＃";
                case "底板配筋":   return "▦";
                case "桩基":       return "○";
                case "沉降":       return "↓";
                case "标高":       return "⬍";
                case "尺寸标注":   return "↔";
                case "地脚螺栓":   return "◉";
                case "设备基础":   return "▤";
                case "海绵命令":   return "❖";
                case "图框视口":   return "□";
                case "道路":       return "≋";
                case "块引线":     return "⎋";
                case "导出说明":   return "⇪";
                case "表格":       return "⊞";
                case "多段线垫层": return "▥";
                case "绘图工具":   return "⚒";
                case "测试":       return "✎";
                default:           return "·";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>编辑器类型下拉菜单分栏。</summary>
    public class EditorMenuSectionVm
    {
        public string Header { get; }
        public ObservableCollection<EditorMenuItemVm> Items { get; } = new ObservableCollection<EditorMenuItemVm>();

        public EditorMenuSectionVm(string header) => Header = header;
    }

    /// <summary>编辑器类型下拉菜单项。</summary>
    public class EditorMenuItemVm
    {
        public string Key { get; }
        public string Name { get; }
        public string Icon { get; }

        public EditorMenuItemVm(string key, string name, string icon)
        {
            Key = key;
            Name = name;
            Icon = icon;
        }
    }

    /// <summary>Blender 面板左侧一个图标 Tab 的数据。</summary>
    public class CategoryTabVm
    {
        /// <summary>稳定 Key（用于区分伪分类如「设置」，普通分类默认 = Name）。</summary>
        public string Key { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public ObservableCollection<CommandItemVm> Items { get; } = new ObservableCollection<CommandItemVm>();

        /// <summary>仅 category=道路 且 JSON 含 roadPanelGroup 时非空，供多 Expander 绑定。</summary>
        public ObservableCollection<CommandSectionVm> RoadPanelGroups { get; } = new ObservableCollection<CommandSectionVm>();

        public bool HasRoadPanelGroups => RoadPanelGroups.Count > 0;

        public override string ToString() => Name ?? "(未命名)";
    }

    /// <summary>
    /// 极简拼音首字母映射：只覆盖当前 commands.json 里用到的中文字（常用工程术语）。
    /// 未命中的字直接 <b>跳过</b>（不占位）：这样即便字典未收录个别字，
    /// 仍能用已收录字母的压缩形式命中（例："地脚螺栓"若"脚"未收 → "dls" 仍可匹配）。
    /// 避免引入 NPinyin 第三方包。
    /// </summary>
    public static class PinyinHelper
    {
        // 按需扩充：覆盖目录/按钮中文词。
        // 规则：一个汉字 → 一个大写拼音首字母。
        private static readonly Dictionary<char, char> Map = new Dictionary<char, char>
        {
            // 常用 / 钢筋 / 底板 / 桩 / 沉降 / 标高 / 尺寸 / 螺栓 / 设备 / 基础
            {'常','C'},{'用','Y'},{'钢','G'},{'筋','J'},{'底','D'},{'板','B'},{'配','P'},
            {'桩','Z'},{'基','J'},{'沉','C'},{'降','J'},{'标','B'},{'高','G'},
            {'尺','C'},{'寸','C'},{'注','Z'},{'地','D'},{'脚','J'},{'螺','L'},{'栓','S'},
            {'设','S'},{'备','B'},
            // 图框 / 视口 / 道路 / 块 / 引线 / 导出 / 说明 / 多段线 / 垫层 / 测试 / 杂项
            {'图','T'},{'框','K'},{'视','S'},{'口','K'},{'道','D'},{'路','L'},
            {'块','K'},{'引','Y'},{'线','X'},{'导','D'},{'出','C'},{'说','S'},{'明','M'},
            {'多','D'},{'段','D'},{'垫','D'},{'层','C'},{'测','C'},{'试','S'},{'杂','Z'},{'项','X'},
            // 动词 / 名词补充
            {'绘','H'},{'制','Z'},{'偏','P'},{'移','Y'},{'锚','M'},{'固','G'},{'加','J'},{'弯','W'},
            {'钩','G'},{'竖','S'},{'延','Y'},{'伸','S'},{'快','K'},{'速','S'},{'截','J'},{'断','D'},
            {'单','D'},{'六','L'},{'点','D'},{'选','X'},{'文','W'},{'字','Z'},{'更','G'},{'新','X'},
            {'旋','X'},{'转','Z'},{'符','F'},{'号','H'},{'拆','C'},{'分','F'},{'对','D'},{'齐','Q'},
            {'交','J'},{'顶','D'},{'构','G'},{'建','J'},{'初','C'},{'始','S'},{'化','H'},{'显','X'},{'示','S'},
            {'切','Q'},{'换','H'},{'表','B'},{'创','C'},{'矩','J'},{'形','X'},{'剖','P'},{'面','M'},
            {'最','Z'},{'小','X'},{'包','B'},{'围','W'},{'排','P'},{'列','L'},{'颜','Y'},{'色','S'},
            {'替','T'},{'直','Z'},{'生','S'},{'成','C'},{'编','B'},{'辑','J'},
            {'人','R'},{'行','X'},{'横','H'},{'方','F'},{'位','W'},{'样','Y'},{'式','S'},
            // 保存/加载/过滤/与（'加' 已在上方动词行收录，此处不重复）
            {'保','B'},{'存','C'},{'载','Z'},{'滤','L'},{'与','Y'},
            // 定查组圆心（'垫' 已在图框/多段线垫层行收录，此处不重复）
            {'定','D'},{'查','C'},{'组','Z'},{'圆','Y'},{'心','X'},
            // Hy 道路五区标题（'断' 已在动词行收录，此处不重复）
            {'工','G'},{'程','C'},{'纵','Z'},{'具','J'},
            // 数字/英文不需要
        };

        public static string GetFirstLetters(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (ch < 128)
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
                else if (Map.TryGetValue(ch, out var py))
                {
                    sb.Append(char.ToLowerInvariant(py));
                }
                // 未命中的字跳过，不阻断 FirstLetters 匹配
            }
            return sb.ToString();
        }
    }
}
