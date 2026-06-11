using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HyCADTool.Shell.ViewModels;

namespace HyCADTool.Shell.Views.Preferences
{
    /// <summary>
    /// 「界面 → 主题」二级分组视图。
    ///
    /// 与其他 Preferences 子视图一致：被实例化在 HyPreferencesView 的 ContentControl 模板里，
    /// 外层 DataContext 是 SettingsGroupVm，需要主动向上找 HySettingsViewModel 才能让
    /// {Binding Settings.Theme} 解析到真正的 SettingsPanelViewModel。
    ///
    /// 历史教训（已修复 / 不要回退）：
    /// - 不能仅靠 XAML {Binding Settings.Theme} —— 进入子视图时 DataContext 是 SettingsGroupVm，
    ///   PathError sticky 导致首次 SelectionChanged 写不回 ViewModel；
    /// - 必须在 Loaded 时主动 RebindRootDataContext，并且在 SelectionChanged 中 code-behind 直接
    ///   把 lb.SelectedValue 推回 SettingsPanelViewModel.Current.Theme，绕开 sticky binding。
    /// </summary>
    public partial class ThemeSettingsView : UserControl
    {
        public ThemeSettingsView()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                RebindRootDataContext();
                HookListBoxIfReady();
            };
            DataContextChanged += (_, __) => RebindRootDataContext();
        }

        private bool _hooked;

        private void HookListBoxIfReady()
        {
            if (_hooked) return;
            var lb = this.FindName("ThemeList") as ListBox;
            if (lb == null) return;
            _hooked = true;

            try
            {
                var current = SettingsPanelViewModel.Current;
                if (current != null && !string.IsNullOrEmpty(current.Theme))
                {
                    lb.SelectedValue = current.Theme;
                }
            }
            catch { /* 若 Current 尚未初始化则跳过，等下次 Loaded 重试 */ }

            lb.SelectionChanged += (s, e) =>
            {
                try
                {
                    var pushedKey = (lb.SelectedValue as string) ?? string.Empty;
                    if (string.IsNullOrEmpty(pushedKey)) return;

                    var current = SettingsPanelViewModel.Current;
                    if (current != null && !string.Equals(current.Theme, pushedKey, System.StringComparison.OrdinalIgnoreCase))
                    {
                        current.Theme = pushedKey;
                    }
                }
                catch { /* 单次切换失败不影响后续，下次点击会再次推送 */ }
            };
        }

        private void RebindRootDataContext()
        {
            var root = ResolveSettingsRootVm();
            if (root != null && !ReferenceEquals(DataContext, root))
            {
                DataContext = root;
            }
        }

        /// <summary>沿可视/逻辑树向上查找承载 HySettingsViewModel 的 DataContext。</summary>
        private object ResolveSettingsRootVm()
        {
            DependencyObject node = this;
            while (node != null)
            {
                if (node is FrameworkElement fe && fe.DataContext is HySettingsViewModel root)
                    return root;
                node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);
            }
            return null;
        }
    }

    /// <summary>
    /// 主题预览卡片数据。在 ThemeSettingsView.xaml 内 inline 实例化为 4 项，
    /// 作为 ListBox.ItemsSource。Key 必须与 BlenderThemeManager.BlenderThemeName 枚举名一致。
    /// 4 个 Brush 对应 ItemTemplate 上的 4 个色块预览（见 XAML 注释）。
    /// </summary>
    public class ThemePresetVm
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Subtitle { get; set; }
        public Brush C1 { get; set; }
        public Brush C2 { get; set; }
        public Brush C3 { get; set; }
        public Brush C4 { get; set; }
    }
}
