using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HyCADTool.Presentation.Views.Preferences
{
    /// <summary>
    /// 底板 设置骨架：BasePlateRein / BasePlateDraw / BasePlateAdvanced
    /// 绑定路径走 HySettingsViewModel.BaseReinVm.*（沿逻辑树继承 DataContext）
    /// </summary>
    public partial class BasePlateSettingsView : UserControl
    {
        public static readonly DependencyProperty SectionProperty = DependencyProperty.Register(
            nameof(Section), typeof(string), typeof(BasePlateSettingsView),
            new PropertyMetadata(null, OnSectionChanged));

        public string Section
        {
            get => (string)GetValue(SectionProperty);
            set => SetValue(SectionProperty, value);
        }

        public BasePlateSettingsView()
        {
            InitializeComponent();
            Loaded += (_, __) => ApplySection();
            Dispatcher.BeginInvoke(new Action(ApplySection), DispatcherPriority.Loaded);
        }

        private static void OnSectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BasePlateSettingsView v) v.ApplySection();
        }

        private void ApplySection()
        {
            var key = string.IsNullOrWhiteSpace(Section) ? "BasePlateRein" : Section;
            if (Resources["Section_" + key] is DataTemplate t)
            {
                Host.ContentTemplate = t;
                Host.Content = new object();
            }
        }
    }
}
