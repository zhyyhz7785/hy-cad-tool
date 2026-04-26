using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HyCADTool.Presentation.Views.Preferences
{
    /// <summary>
    /// 设备基础 设置骨架：EquipFoundationParams
    /// </summary>
    public partial class EquipFoundationSettingsView : UserControl
    {
        public static readonly DependencyProperty SectionProperty = DependencyProperty.Register(
            nameof(Section), typeof(string), typeof(EquipFoundationSettingsView),
            new PropertyMetadata(null, OnSectionChanged));

        public string Section
        {
            get => (string)GetValue(SectionProperty);
            set => SetValue(SectionProperty, value);
        }

        public EquipFoundationSettingsView()
        {
            InitializeComponent();
            Loaded += (_, __) => ApplySection();
            Dispatcher.BeginInvoke(new Action(ApplySection), DispatcherPriority.Loaded);
        }

        private static void OnSectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EquipFoundationSettingsView v) v.ApplySection();
        }

        private void ApplySection()
        {
            var key = string.IsNullOrWhiteSpace(Section) ? "EquipFoundationParams" : Section;
            if (Resources["Section_" + key] is DataTemplate t)
            {
                Host.ContentTemplate = t;
                Host.Content = new object();
            }
        }
    }
}
