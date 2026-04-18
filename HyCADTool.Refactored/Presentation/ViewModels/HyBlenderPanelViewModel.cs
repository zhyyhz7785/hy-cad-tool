using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using HyCADTool.ReCall;

namespace HyCADTool.Refactored.Presentation.ViewModels
{
    /// <summary>
    /// Blender 面板的总 ViewModel（独立面板，不再嵌入 HyToolPanel）。
    /// 负责：
    /// - 从 <see cref="CommandTable.GroupByCategory"/> 拉分组数据，转成 <see cref="CategoryTabVm"/>。
    /// - 维护当前选中的分类（左侧 Tab）。
    /// - 维护搜索框文字，用关键词 + 简易拼音首字母过滤命令。
    /// </summary>
    public class HyBlenderPanelViewModel : INotifyPropertyChanged
    {
        /// <summary>左侧所有分类 Tab。</summary>
        public ObservableCollection<CategoryTabVm> Tabs { get; } = new ObservableCollection<CategoryTabVm>();

        private CategoryTabVm _selectedTab;
        public CategoryTabVm SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (_selectedTab == value) return;
                _selectedTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPreferencesMode));
                RefreshFilter();
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
                RefreshFilter();
            }
        }

        public bool IsSearching => !string.IsNullOrWhiteSpace(_searchText);

        /// <summary>当前选中的是否为「设置」伪分类。</summary>
        public bool IsPreferencesMode => _selectedTab != null && _selectedTab.Key == PreferencesTabKey;

        /// <summary>设置面板的 ViewModel，首次切入「设置」时才创建。</summary>
        private HySettingsViewModel _preferencesVm;
        public HySettingsViewModel PreferencesVm
            => _preferencesVm ?? (_preferencesVm = new HySettingsViewModel());

        /// <summary>「设置」伪分类的稳定 Key。</summary>
        public const string PreferencesTabKey = "__preferences__";

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

        public HyBlenderPanelViewModel()
        {
            LoadFromCommandTable();
        }

        /// <summary>从 ReCall.CommandTable 重新拉分组（可供外部在 JSON 变化后触发刷新）。</summary>
        public void LoadFromCommandTable()
        {
            Tabs.Clear();
            try
            {
                #region agent log
                DebugLogger.Log("HyBlenderPanelViewModel.cs:LoadFromCommandTable:before_group", "before GroupByCategory", null, "H1");
                #endregion
                var groups = CommandTable.GroupByCategory();
                #region agent log
                DebugLogger.Log("HyBlenderPanelViewModel.cs:LoadFromCommandTable:after_group", "groups=" + (groups?.Count ?? -1), new { groupCount = groups?.Count ?? -1 }, "H1");
                #endregion
                Tabs.Add(new CategoryTabVm
                {
                    Key  = PreferencesTabKey,
                    Name = "设置",
                    Icon = "⚙",
                });

                int totalCommands = 0;
                foreach (var g in groups)
                {
                    var tab = new CategoryTabVm
                    {
                        Key  = g.Category,
                        Name = g.Category,
                        Icon = PickCategoryIcon(g.Category),
                    };
                    foreach (var it in g.Items)
                        tab.Items.Add(new CommandItemVm(it));
                    Tabs.Add(tab);
                    totalCommands += g.Items.Count;
                }

                StatusMessage = $"{groups.Count} 个分类，{totalCommands} 个命令";
            }
            catch (System.Exception ex)
            {
                StatusMessage = "读取 commands.json 失败：" + ex.Message;
            }

            SelectedTab = Tabs.FirstOrDefault();
        }

        private void RefreshFilter()
        {
            FilteredItems.Clear();
            GlobalSearchResults.Clear();

            if (_selectedTab == null) return;
            if (IsPreferencesMode) return; // 设置 Tab 不走命令过滤

            if (!IsSearching)
            {
                foreach (var it in _selectedTab.Items)
                    FilteredItems.Add(it);
                return;
            }

            var kw = _searchText.Trim().ToLowerInvariant();

            foreach (var it in _selectedTab.Items)
                if (MatchFuzzy(it, kw)) FilteredItems.Add(it);

            foreach (var tab in Tabs)
                foreach (var it in tab.Items)
                    if (MatchFuzzy(it, kw)) GlobalSearchResults.Add(it);
        }

        /// <summary>
        /// 模糊匹配：
        /// 1) 命令 key 包含 kw
        /// 2) DisplayName 包含 kw
        /// 3) DisplayName 拼音首字母拼接字符串包含 kw
        /// </summary>
        private static bool MatchFuzzy(CommandItemVm item, string kw)
        {
            if (string.IsNullOrEmpty(kw)) return true;
            if (item.MatchText.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            var pinyin = PinyinHelper.GetFirstLetters(item.DisplayName);
            return pinyin.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) >= 0;
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
                case "图框视口":   return "□";
                case "道路":       return "≋";
                case "块引线":     return "⎋";
                case "导出说明":   return "⇪";
                case "多段线垫层": return "▥";
                case "测试":       return "✎";
                default:           return "·";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Blender 面板左侧一个图标 Tab 的数据。</summary>
    public class CategoryTabVm
    {
        /// <summary>稳定 Key（用于区分伪分类如「设置」，普通分类默认 = Name）。</summary>
        public string Key { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public ObservableCollection<CommandItemVm> Items { get; } = new ObservableCollection<CommandItemVm>();

        public override string ToString() => Name ?? "(未命名)";
    }

    /// <summary>
    /// 极简拼音首字母映射：只覆盖当前 commands.json 里用到的中文字（常用工程术语）。
    /// 未命中的字用 '?' 占位，不会破坏匹配（命中 key 或 DisplayName 直接匹配的仍然生效）。
    /// 避免引入 NPinyin 第三方包。
    /// </summary>
    internal static class PinyinHelper
    {
        // 按需扩充：覆盖目录/按钮中文词。
        // 规则：一个汉字 → 一个大写拼音首字母。
        private static readonly Dictionary<char, char> Map = new Dictionary<char, char>
        {
            // 常用 / 钢筋 / 底板 / 桩 / 沉降 / 标高 / 尺寸 / 螺栓 / 设备 / 基础
            {'常','C'},{'用','Y'},{'钢','G'},{'筋','J'},{'底','D'},{'板','B'},{'配','P'},
            {'桩','Z'},{'基','J'},{'沉','C'},{'降','J'},{'标','B'},{'高','G'},
            {'尺','C'},{'寸','C'},{'标','B'},{'注','Z'},{'地','D'},{'脚','J'},{'螺','L'},{'栓','S'},
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
            {'保','B'},{'存','C'},{'载','Z'},{'加','J'},{'滤','L'},{'与','Y'},
            {'定','D'},{'查','C'},{'组','Z'},{'圆','Y'},{'心','X'},{'垫','D'},
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
                else
                {
                    sb.Append('?');
                }
            }
            return sb.ToString();
        }
    }
}
