using System.Windows;
using System.Windows.Controls;

namespace HyCADTool.Presentation.Views.Preferences
{
    /// <summary>
    /// 道路 设置骨架：RoadCrosswalk（走人行横道，接 Settings.Road*）/ RoadMunicipal（P0 占位）
    /// </summary>
    public partial class RoadSettingsView : UserControl
    {
        public static readonly DependencyProperty SectionProperty = DependencyProperty.Register(
            nameof(Section), typeof(string), typeof(RoadSettingsView),
            new PropertyMetadata(null, OnSectionChanged));

        public string Section
        {
            get => (string)GetValue(SectionProperty);
            set => SetValue(SectionProperty, value);
        }

        public RoadSettingsView()
        {
            InitializeComponent();
            Loaded += (_, __) => ApplySection();
        }

        private static void OnSectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoadSettingsView v) v.ApplySection();
        }

        private void ApplySection()
        {
            var key = string.IsNullOrWhiteSpace(Section) ? "RoadCrosswalk" : Section;
            if (Resources["Section_" + key] is DataTemplate t)
            {
                Host.ContentTemplate = t;
                Host.Content = new object();
            }
        }
    }
}
