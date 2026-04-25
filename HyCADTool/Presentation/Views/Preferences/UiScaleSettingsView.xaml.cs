using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HyCADTool.Presentation.ViewModels;

namespace HyCADTool.Presentation.Views.Preferences
{
    /// <summary>「界面 → 尺寸」：三类 Metric 比例预设与滑块；持久化走 <see cref="SettingsPanelViewModel"/>。</summary>
    public partial class UiScaleSettingsView : UserControl
    {
        public UiScaleSettingsView()
        {
            InitializeComponent();
            Loaded += (_, __) => RebindRootDataContext();
            DataContextChanged += (_, __) => RebindRootDataContext();
        }

        private void RebindRootDataContext()
        {
            var root = ResolveSettingsRootVm();
            if (root != null && !ReferenceEquals(DataContext, root))
                DataContext = root;
        }

        /// <summary>沿可视/逻辑树向上查找承载 HySettingsViewModel 的 DataContext。</summary>
        private HySettingsViewModel ResolveSettingsRootVm()
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

        private void OnPresetCompactClick(object sender, RoutedEventArgs e) => ApplyPreset(0.9, 0.85, 0.9);

        private void OnPresetStandardClick(object sender, RoutedEventArgs e) => ApplyPreset(1.0, 1.0, 1.0);

        private void OnPresetComfortClick(object sender, RoutedEventArgs e) => ApplyPreset(1.1, 1.15, 1.1);

        private void ApplyPreset(double font, double density, double input)
        {
            var root = ResolveSettingsRootVm();
            var s = root?.Settings;
            if (s == null) return;
            s.UiFontScale = font;
            s.UiDensityScale = density;
            s.UiInputWidthScale = input;
        }

        private void OnResetScalesClick(object sender, RoutedEventArgs e) => ApplyPreset(1.0, 1.0, 1.0);

        private void OnApplySaveClick(object sender, RoutedEventArgs e)
        {
            var root = ResolveSettingsRootVm();
            var s = root?.Settings;
            if (s == null)
            {
                MessageBox.Show("无可用设置实例，无法写入。", "HyCAD", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                s.SavePublic();
                MessageBox.Show(
                    "已写入 hy-settings.json。\n若界面未完全刷新，请关闭并重新打开 Hy 面板或相关窗口。",
                    "HyCAD",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("保存失败：" + ex.Message, "HyCAD", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
