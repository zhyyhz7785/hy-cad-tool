using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Presentation.Views.Preferences
{
    /// <summary>
    /// 样式类二级分组示范骨架。
    /// 通过 <see cref="Section"/> 依赖属性在多个模板之间切换（Text/Dim/MLeader/Table）。
    /// 
    /// DataContext 处理：本控件被实例化在 HyPreferencesView 的 ContentControl 模板里，
    /// 外层 DataContext 是 SettingsGroupVm，不是 HySettingsViewModel。
    /// 因此 Host.DataContext 需要主动向上查找到 HySettingsViewModel，
    /// 让 DataTemplate 内 {Binding Settings.XXX} 能解析到真正的参数 VM。
    /// </summary>
    public partial class StyleSettingsView : UserControl
    {
        public static readonly DependencyProperty SectionProperty = DependencyProperty.Register(
            nameof(Section), typeof(string), typeof(StyleSettingsView),
            new PropertyMetadata(null, OnSectionChanged));

        public string Section
        {
            get => (string)GetValue(SectionProperty);
            set => SetValue(SectionProperty, value);
        }

        public StyleSettingsView()
        {
            InitializeComponent();
            Loaded += (_, __) => ApplySection();
            DataContextChanged += (_, __) => ApplySection();
        }

        private static void OnSectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StyleSettingsView v) v.ApplySection();
        }

        private void ApplySection()
        {
            var key = string.IsNullOrWhiteSpace(Section) ? "Text" : Section;
            if (Resources["Section_" + key] is DataTemplate t)
            {
                Host.ContentTemplate = t;
                Host.DataContext = ResolveSettingsRootVm() ?? DataContext;
                Host.Content = Host.DataContext;
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
}
