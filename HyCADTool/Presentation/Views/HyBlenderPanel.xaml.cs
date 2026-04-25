using System;
using System.Windows.Controls;
using HyCADTool.Presentation.Input;
using HyCADTool.Presentation.ViewModels;

namespace HyCADTool.Presentation.Views
{
    /// <summary>
    /// Blender 风格命令面板（独立面板，通过 <c>HyB</c> 命令打开）。
    /// 左侧 <see cref="HyCAD.BlenderUI.Controls.IconTabBar"/> + 顶部
    /// <see cref="HyCAD.BlenderUI.Controls.Primitives.SearchBox"/> + 主区命令列表。
    /// Hy/HyB 均打开此面板：Hy → 跳「设置」Tab，HyB → 停在默认 Tab；过滤走独立「过滤」Tab。
    /// </summary>
    public partial class HyBlenderPanel : UserControl
    {
        private readonly HyBlenderPanelViewModel _vm;

        /// <summary>HyKeyMap attach 句柄；Unloaded 时释放避免泄漏。</summary>
        private IDisposable _keyMapDetach;

        /// <summary>无参构造（XAML 设计器 / Activator 用）。</summary>
        public HyBlenderPanel() : this(new HyBlenderPanelViewModel())
        {
        }

        /// <summary>生产构造：注入 Blender 面板 VM。</summary>
        public HyBlenderPanel(HyBlenderPanelViewModel vm)
        {
            InitializeComponent();
            _vm = vm ?? new HyBlenderPanelViewModel();
            DataContext = _vm;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_keyMapDetach != null) return;
            var bindings = new HyKeyMap.HyKeyMapBindings
            {
                FocusSearch = FocusSearchBox,
                ClearSearch = ClearSearchText,
                TogglePreferences = TogglePreferencesTab,
            };
            _keyMapDetach = HyKeyMap.Attach(this, bindings);
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _keyMapDetach?.Dispose();
            _keyMapDetach = null;
        }

        private void FocusSearchBox()
        {
            // 仅命令列表模式下才有 SearchBox；其他模式下聚焦无意义
            if (_vm == null || !_vm.IsCommandListMode) return;
            if (SearchBox == null) return;
            SearchBox.Focus();
        }

        private void ClearSearchText()
        {
            if (_vm == null) return;
            if (string.IsNullOrEmpty(_vm.SearchText)) return;
            _vm.SearchText = string.Empty;
        }

        private void TogglePreferencesTab()
        {
            if (_vm == null) return;
            if (_vm.IsPreferencesMode)
            {
                // 已在设置 → 退回第一个非伪分类 Tab
                for (int i = 0; i < _vm.Tabs.Count; i++)
                {
                    var t = _vm.Tabs[i];
                    if (t.Key == HyBlenderPanelViewModel.PreferencesTabKey) continue;
                    if (t.Key == HyBlenderPanelViewModel.FilterTabKey) continue;
                    _vm.SelectedTab = t;
                    return;
                }
            }
            else
            {
                _vm.SelectTab(HyBlenderPanelViewModel.PreferencesTabKey);
            }
        }
    }
}
