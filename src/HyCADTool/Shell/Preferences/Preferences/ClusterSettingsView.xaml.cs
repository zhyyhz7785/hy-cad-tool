using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HyCADTool.Presentation.Views.Preferences
{
    /// <summary>
    /// 聚类 设置骨架：ClusterParams / ClusterDrawSwitch
    /// 绑定路径走 HySettingsViewModel.ClusterVm.*
    /// </summary>
    public partial class ClusterSettingsView : UserControl
    {
        public static readonly DependencyProperty SectionProperty = DependencyProperty.Register(
            nameof(Section), typeof(string), typeof(ClusterSettingsView),
            new PropertyMetadata(null, OnSectionChanged));

        public string Section
        {
            get => (string)GetValue(SectionProperty);
            set => SetValue(SectionProperty, value);
        }

        public ClusterSettingsView()
        {
            InitializeComponent();
            Loaded += (_, __) => ApplySection();
            Dispatcher.BeginInvoke(new Action(ApplySection), DispatcherPriority.Loaded);
        }

        private static void OnSectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ClusterSettingsView v) v.ApplySection();
        }

        private void ApplySection()
        {
            var key = string.IsNullOrWhiteSpace(Section) ? "ClusterParams" : Section;
            if (Resources["Section_" + key] is DataTemplate t)
            {
                Host.ContentTemplate = t;
                Host.Content = new object();
            }
        }
    }
}
