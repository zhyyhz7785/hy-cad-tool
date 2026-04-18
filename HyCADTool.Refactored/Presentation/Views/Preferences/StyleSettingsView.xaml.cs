using System.Windows;
using System.Windows.Controls;

namespace HyCADTool.Refactored.Presentation.Views.Preferences
{
    /// <summary>
    /// 样式类二级分组示范骨架。
    /// 通过 <see cref="Section"/> 依赖属性在多个模板之间切换（Preview/Text/Dim/MLeader/Table）。
    /// 阶段 A：仅渲染控件骨架，字段 Text 为占位值，未接真实 Binding。
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
        }

        private static void OnSectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StyleSettingsView v) v.ApplySection();
        }

        private void ApplySection()
        {
            var key = string.IsNullOrWhiteSpace(Section) ? "Preview" : Section;
            if (Resources["Section_" + key] is DataTemplate t)
            {
                Host.ContentTemplate = t;
                Host.Content = new object();
            }
        }
    }
}
