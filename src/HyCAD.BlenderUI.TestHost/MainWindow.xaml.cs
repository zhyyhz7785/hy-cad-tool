using System;
using System.Windows;
using System.Windows.Input;
using HyCAD.BlenderUI.Samples;

namespace HyCAD.BlenderUI.TestHost
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            List.ItemsSource = new[]
            {
                typeof(CrossSectionV2SamplePanel),
                typeof(CrossSectionV3SamplePanel),
                typeof(WidgetVisualGallery),
                typeof(WidgetGallerySample),
                typeof(AutoPropertySample),
                typeof(AreaSplitJoinSample),
                typeof(OperatorKeymapSample),
                typeof(SpaceTypeShowcaseSample),
                typeof(FullDesignerSample),
                typeof(SplitDesignerSample),
                typeof(ThreeLevelPropertyGroupSample),
            };
        }

        private void OnOpen(object sender, MouseButtonEventArgs e)
        {
            if (List.SelectedItem is not Type t) return;
            var win = (Window)Activator.CreateInstance(t);
            win.Owner = this;
            win.Show();
        }
    }
}
