using System;
using System.Windows.Controls;
using HyCADTool.Shell.Input;
using HyCADTool.Shell.ViewModels;

namespace HyCADTool.Shell.Views
{
    /// <summary>
    /// Blender 风格命令面板（独立面板，通过 <c>HyB</c> 命令打开）。
    /// 左侧 <see cref="HyCAD.BlenderUI.Controls.IconTabBar"/> + 顶部
    /// <see cref="HyCAD.BlenderUI.Controls.Primitives.SearchBox"/> + 主区命令列表。
    /// Hy/HyB 均打开此面板：Hy → 跳「命令」Tab，HyB → toggle 保持当前 Tab；设置走 ShowSettingsPanel。
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
            if (_vm == null) return;
            if (_vm.IsPreferencesMode)
            {
                PrefsSearchBox?.Focus();
                return;
            }
            if (_vm.IsCommandEditorMode)
                SearchBox?.Focus();
        }

        private void ClearSearchText()
        {
            if (_vm == null) return;
            if (_vm.IsPreferencesMode)
            {
                if (!string.IsNullOrEmpty(_vm.PreferencesVm.SettingsSearchText))
                    _vm.PreferencesVm.SettingsSearchText = string.Empty;
                return;
            }
            if (!string.IsNullOrEmpty(_vm.SearchText))
                _vm.SearchText = string.Empty;
        }

        private void TogglePreferencesTab()
        {
            if (_vm == null) return;
            if (_vm.IsPreferencesMode)
                _vm.SelectEditor(HyBlenderPanelViewModel.CommandsEditorKey);
            else
                _vm.SelectEditor(HyBlenderPanelViewModel.PreferencesTabKey);
        }

        private void OnEditorTypeButtonClick(object sender, System.Windows.RoutedEventArgs e)
        {
            if (EditorMenuPopup != null)
                EditorMenuPopup.IsOpen = !EditorMenuPopup.IsOpen;
        }

        private void OnEditorMenuItemClick(object sender, System.Windows.RoutedEventArgs e)
        {
            if (EditorMenuPopup != null)
                EditorMenuPopup.IsOpen = false;
        }
    }
}
