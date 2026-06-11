using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HyCADTool.Shell.Views.Preferences
{
    /// <summary>
    /// 地脚螺栓 设置骨架：AnchorBoltParams（占位 + 未来扩展位）
    /// </summary>
    public partial class AnchorBoltSettingsView : UserControl
    {
        public static readonly DependencyProperty SectionProperty = DependencyProperty.Register(
            nameof(Section), typeof(string), typeof(AnchorBoltSettingsView),
            new PropertyMetadata(null, OnSectionChanged));

        public string Section
        {
            get => (string)GetValue(SectionProperty);
            set => SetValue(SectionProperty, value);
        }

        public AnchorBoltSettingsView()
        {
            InitializeComponent();
            Loaded += (_, __) => ApplySection();
            Dispatcher.BeginInvoke(new Action(ApplySection), DispatcherPriority.Loaded);
        }

        private static void OnSectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AnchorBoltSettingsView v) v.ApplySection();
        }

        private void ApplySection()
        {
            var key = string.IsNullOrWhiteSpace(Section) ? "AnchorBoltParams" : Section;
            if (Resources["Section_" + key] is DataTemplate t)
            {
                Host.ContentTemplate = t;
                Host.Content = new object();
            }
        }
    }
}
