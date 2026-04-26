using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HyCADTool.Presentation.Views.Preferences
{
    /// <summary>
    /// 桩基 设置骨架：PileParams / PileMargin
    /// 绑定路径走 HySettingsViewModel.PileVm.*
    /// </summary>
    public partial class PileSettingsView : UserControl
    {
        public static readonly DependencyProperty SectionProperty = DependencyProperty.Register(
            nameof(Section), typeof(string), typeof(PileSettingsView),
            new PropertyMetadata(null, OnSectionChanged));

        public string Section
        {
            get => (string)GetValue(SectionProperty);
            set => SetValue(SectionProperty, value);
        }

        public PileSettingsView()
        {
            InitializeComponent();
            Loaded += (_, __) => ApplySection();
            Dispatcher.BeginInvoke(new Action(ApplySection), DispatcherPriority.Loaded);
        }

        private static void OnSectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PileSettingsView v) v.ApplySection();
        }

        private void ApplySection()
        {
            var key = string.IsNullOrWhiteSpace(Section) ? "PileParams" : Section;
            if (Resources["Section_" + key] is DataTemplate t)
            {
                Host.ContentTemplate = t;
                Host.Content = new object();
            }
        }
    }
}
