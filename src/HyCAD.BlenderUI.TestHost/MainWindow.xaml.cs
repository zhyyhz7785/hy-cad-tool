using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HyCAD.BlenderUI.Controls;
using HyCAD.BlenderUI.Preferences;
using HyCAD.BlenderUI.Samples;

namespace HyCAD.BlenderUI.TestHost
{
    public partial class MainWindow : Window
    {
        private bool _syncingPos;

        public MainWindow()
        {
            _syncingPos = true;
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
                typeof(WorkbenchSample),
                typeof(SplitDesignerSample),
                typeof(ThreeLevelPropertyGroupSample),
            };

            _syncingPos = true;
            ToolbarPosCombo.SelectedIndex =
                BlenderUiPreferences.Current.ToolbarPosition == ToolbarPosition.Side ? 1 : 0;
            _syncingPos = false;
            BlenderUiPreferences.Current.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(BlenderUiPreferences.ToolbarPosition)) return;
                _syncingPos = true;
                ToolbarPosCombo.SelectedIndex =
                    BlenderUiPreferences.Current.ToolbarPosition == ToolbarPosition.Side ? 1 : 0;
                _syncingPos = false;
            };
        }

        private void OnToolbarPosChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingPos) return;
            if (ToolbarPosCombo.SelectedItem is ComboBoxItem item &&
                item.Tag is string tag &&
                string.Equals(tag, "Side", StringComparison.OrdinalIgnoreCase))
            {
                BlenderUiPreferences.Current.ToolbarPosition = ToolbarPosition.Side;
            }
            else
            {
                BlenderUiPreferences.Current.ToolbarPosition = ToolbarPosition.Top;
            }
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
