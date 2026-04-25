using System.Windows;
using System.Windows.Controls;

namespace HyCADTool.Presentation.Views.Preferences
{
    /// <summary>
    /// 钢筋二级分组示范骨架。
    /// 通过 <see cref="Section"/> 切换：Rein（钢筋参数）/ ReinDim（尺寸参数）。
    /// </summary>
    public partial class ReinSettingsView : UserControl
    {
        public static readonly DependencyProperty SectionProperty = DependencyProperty.Register(
            nameof(Section), typeof(string), typeof(ReinSettingsView),
            new PropertyMetadata(null, OnSectionChanged));

        public string Section
        {
            get => (string)GetValue(SectionProperty);
            set => SetValue(SectionProperty, value);
        }

        public ReinSettingsView()
        {
            InitializeComponent();
            Loaded += (_, __) => ApplySection();
        }

        private static void OnSectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ReinSettingsView v) v.ApplySection();
        }

        private void ApplySection()
        {
            var key = string.IsNullOrWhiteSpace(Section) ? "Rein" : Section;
            if (Resources["Section_" + key] is DataTemplate t)
            {
                Host.ContentTemplate = t;
                Host.Content = new object();
            }
        }
    }
}
